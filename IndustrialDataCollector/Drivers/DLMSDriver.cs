using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services; using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// DLMS/COSEM 智能电表协议驱动 (串口/光电)
    /// 简化实现: HDLC封装 + DLMS ReadRequest
    /// OBIS编码: 如 "1.1.0.0.0.255"(正向有功电能) "0.0.1.0.0.255"(电压)
    /// </summary>
    public class DLMSDriver : IDriver
    {
        private SerialPort _serialPort;
        private string _portName = "COM1";
        private int _baudRate = 9600;
        private int _clientAddress = 16;   // 客户端地址(一般为16)
        private int _serverAddress = 1;     // 电表逻辑地址
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _lock = new object();

        public string DriverType => "DLMS";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _portName = config.GetParam("PortName", "COM1");
            _baudRate = config.GetIntParam("BaudRate", 9600);
            _clientAddress = config.GetIntParam("ClientAddress", 16);
            _serverAddress = config.GetIntParam("ServerAddress", 1);

            try
            {
                lock (_lock)
                {
                    _serialPort = new SerialPort(_portName, _baudRate, Parity.Even, 8, StopBits.One)
                    {
                        ReadTimeout = 5000,
                        WriteTimeout = 3000,
                        NewLine = "\r\n"
                    };
                    _serialPort.Open();
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                }
                IsConnected = true;
                NotifyStatus(true, $"DLMS/COSEM 已连接 ({_portName}, {_baudRate}bps, 表地址:{_serverAddress})");
                Logger.Debug($"DLMS/COSEM 连接成功: {_portName}, {_baudRate}bps");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, $"连接失败: {ex.Message}");
                Logger.Warn($"DLMS/COSEM 连接失败 [{_config.Name}]: {ex.Message}");
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            lock (_lock)
            {
                try { _serialPort?.Close(); } catch { }
                try { _serialPort?.Dispose(); } catch { }
                _serialPort = null;
            }
            IsConnected = false;
            NotifyStatus(false, "已断开");
            return Task.CompletedTask;
        }

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null) return;
            int pollInterval = _config.GetIntParam("PollInterval", 1000);
            if (pollInterval < 100) pollInterval = 100;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    if (!IsConnected) { await ConnectAsync(_config); if (!IsConnected) { await Task.Delay(3000, _cts.Token); continue; } }
                    var cycleItems = new List<CycleDataItem>();
                    foreach (var point in _config.DataPoints)
                    {
                        _cts.Token.ThrowIfCancellationRequested();
                        if (!point.IsActive) continue;
                        object value = null;
                        try { value = await ReadAsync(point); } catch { }
                        double v = value is double dv ? dv : 0;
                        var data = new CollectedData
                        {
                            DeviceId = _config.Id, DeviceName = _config.Name, VariableName = point.Name,
                            DataType = point.DataType,
                            Value = v.ToString("F6").TrimEnd('0').TrimEnd('.'),
                            Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null,
                            Timestamp = DateTime.Now
                        };
                        OnDataReceived?.Invoke(this, new CollectedDataEventArgs(data));
                        cycleItems.Add(new CycleDataItem
                        {
                            VariableId = point.VariableId,
                            Id = string.Format("{0}|{1}", _config.Name, point.Name),
                            DataType = point.DataType, Value = v, Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null
                        });
                    }
                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Driver = "dlms", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems
                    }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn($"DLMS/COSEM 采集异常 [{_config.Name}]: {ex.Message}"); await Task.Delay(1000, _cts.Token); }
            }
        }

        /// <summary>
        /// 通过串口发送简化的DLMS/COSEM读取请求
        /// </summary>
        public async Task<object> ReadAsync(DataPoint point)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("串口未打开");

            string address = point.Address ?? "";

            // 解析OBIS编码 (如 "1.1.0.0.0.255" 或 "0.0.1.0.0.255")
            int classId = 1;
            string obis = "1.1.0.0.0.255";
            int attributeId = 2; // 默认读属性2(值)

            ParseObisAddress(address, ref classId, ref obis, ref attributeId);

            // 构建简化的DLMS ReadRequest帧
            byte[] request = BuildReadRequest(classId, obis, attributeId);

            byte[] response = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.Write(request, 0, request.Length);
                    Logger.Debug($"DLMS Send: {BitConverter.ToString(request)} -> class={classId} obis={obis} attr={attributeId}");
                }
                return ReadResponse();
            });

            return ParseResponse(response);
        }

        #region DLMS/COSEM Protocol Implementation (Simplified)

        /// <summary>
        /// 解析OBIS地址: "1.1.0.0.0.255" → classId=1, obis="1.1.0.0.0.255"
        /// 支持格式: "classId=3,obis=1.1.0.0.0.255,attr=2" 或直接 "1.1.0.0.0.255"
        /// </summary>
        private void ParseObisAddress(string address, ref int classId, ref string obis, ref int attributeId)
        {
            if (string.IsNullOrEmpty(address)) return;

            // 检查是否为复合格式
            if (address.Contains("="))
            {
                var parts = address.Split(',');
                foreach (var part in parts)
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2)
                    {
                        string k = kv[0].Trim().ToLower();
                        string v = kv[1].Trim();
                        if (k == "classid") int.TryParse(v, out classId);
                        else if (k == "obis") obis = v;
                        else if (k == "attr" || k == "attributeid") int.TryParse(v, out attributeId);
                    }
                }
            }
            else
            {
                // 直接OBIS编码
                obis = address;
            }
        }

        /// <summary>
        /// 构建简化的DLMS/COSEM ReadRequest
        /// 格式: HDLC帧头 + DLMS GetRequest
        /// </summary>
        private byte[] BuildReadRequest(int classId, string obis, int attributeId)
        {
            // 简化的DLMS请求帧:
            // HDLC: 0x7E + 帧格式(2) + 目标地址 + 源地址 + 控制(0x13=SNRM请求) + HCS + 0x7E
            // 实际发送: HDLC封装 + COSEM Get-Request

            // 简化实现: 发送ASCII查询格式(适用于一些简易DLMS网关)
            // 格式: /?{serverAddr}! 或 SNRM建立连接
            // 对于基本DLMS光电头: 先发送SNRM建立HDLC连接, 再发送GetRequest

            // 简化帧: COSEM Get-Request-Normal in HDLC
            byte[] hdlcFrame = new byte[]
            {
                0x7E,                   // HDLC Flag
                0xA0,                   // Frame Format (UI帧)
                (byte)(_serverAddress << 1 | 1),  // 目标地址(含LSB=1)
                (byte)(_clientAddress << 1),       // 源地址
                0x13,                   // Control (UI)
                0x00, 0x00,             // HCS占位
                // DLMS/COSEM payload (简化Get-Request)
                0xC0, 0x01,             // invoke-id + priority
                0xC1,                   // Get-Request
                (byte)classId,          // class-id
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // OBIS (待填充)
                (byte)attributeId,      // attribute-id
                0x7E                    // Frame end
            };

            // 填充OBIS编码
            var obisBytes = ParseObisBytes(obis);
            if (obisBytes.Length >= 6)
                Array.Copy(obisBytes, 0, hdlcFrame, 11, Math.Min(6, obisBytes.Length));

            // 计算FCS (简化CRC)
            ushort fcs = CalculateCrc16(hdlcFrame, 1, hdlcFrame.Length - 3);
            hdlcFrame[hdlcFrame.Length - 2] = (byte)(fcs & 0xFF);
            hdlcFrame[hdlcFrame.Length - 3] = (byte)(fcs >> 8);

            return hdlcFrame;
        }

        /// <summary>
        /// 解析OBIS编码字符串 → 6字节
        /// "1.1.0.0.0.255" → {0x01, 0x01, 0x00, 0x00, 0x00, 0xFF}
        /// </summary>
        private byte[] ParseObisBytes(string obis)
        {
            var result = new byte[6];
            var parts = obis.Split('.');
            for (int i = 0; i < Math.Min(parts.Length, 6); i++)
            {
                if (int.TryParse(parts[i], out int val))
                    result[i] = (byte)val;
            }
            return result;
        }

        /// <summary>
        /// 简化的CRC16校验 (用于HDLC FCS)
        /// </summary>
        private ushort CalculateCrc16(byte[] data, int start, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = start; i < start + length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (ushort)((crc >> 1) ^ 0x8408);
                    else
                        crc >>= 1;
                }
            }
            return (ushort)(crc ^ 0xFFFF);
        }

        /// <summary>
        /// 读取DLMS响应帧
        /// </summary>
        private byte[] ReadResponse()
        {
            var buffer = new List<byte>();
            int b;
            bool flagFound = false;
            int flagCount = 0;

            while ((b = _serialPort.ReadByte()) != -1)
            {
                if (b == 0x7E) // HDLC Flag
                {
                    flagCount++;
                    if (flagCount >= 2 && buffer.Count > 0)
                        break;  // 第二个flag → 帧结束
                    if (!flagFound) flagFound = true;
                    buffer.Add((byte)b);
                    continue;
                }

                if (b == 0x21)
                {
                    // 简化的ASCII响应(光电头模式): "021.5\n" 格式
                    var asciiBuffer = new List<byte> { (byte)b };
                    try
                    {
                        while ((b = _serialPort.ReadByte()) != -1)
                        {
                            asciiBuffer.Add((byte)b);
                            if (b == 0x0A) break;
                        }
                    }
                    catch { }
                    string asciiResp = Encoding.ASCII.GetString(asciiBuffer.ToArray()).Trim();
                    Logger.Debug($"DLMS Recv(ASCII): [{asciiResp}]");
                    return asciiBuffer.ToArray();
                }

                if (flagFound) buffer.Add((byte)b);
            }

            if (buffer.Count == 0) throw new TimeoutException("等待DLMS/COSEM响应超时");

            Logger.Debug($"DLMS Recv: {BitConverter.ToString(buffer.ToArray())}");
            return buffer.ToArray();
        }

        /// <summary>
        /// 解析DLMS响应数据
        /// </summary>
        private double ParseResponse(byte[] response)
        {
            if (response == null || response.Length == 0)
                throw new Exception("DLMS响应数据为空");

            // 尝试ASCII解析 (光电头模式响应: "021.5\n")
            string asciiStr = Encoding.ASCII.GetString(response).Trim('\r', '\n', '\0', ' ');
            if (!string.IsNullOrEmpty(asciiStr) && asciiStr.Length > 0)
            {
                // 检查错误
                if (asciiStr.StartsWith("ERR", StringComparison.OrdinalIgnoreCase)
                    || asciiStr.StartsWith("NAK", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"DLMS/COSEM错误响应: {asciiStr}");
                }

                // 尝试提取数字
                // 格式可能: "021.5" "234.8"
                if (double.TryParse(asciiStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double asciiVal))
                    return asciiVal;
            }

            // HDLC帧解析 (简化: 从第8字节开始提取数据)
            if (response.Length > 8)
            {
                // 检查是否包含COSEM Get-Response (0xC4)
                for (int i = 0; i < response.Length - 4; i++)
                {
                    if (response[i] == 0xC4) // Get-Response
                    {
                        // 简单提取后续的数值字节
                        if (i + 4 < response.Length)
                        {
                            // 尝试作为16-bit整数解析
                            int val = response[i + 3] << 8 | response[i + 4];
                            return val;
                        }
                    }
                }
            }

            throw new Exception("无法解析DLMS/COSEM响应数据");
        }

        #endregion

        private void NotifyStatus(bool connected, string message)
        {
            OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(_config?.Id ?? "", _config?.Name ?? "", connected, message));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisconnectAsync().Wait();
        }
    }
}
