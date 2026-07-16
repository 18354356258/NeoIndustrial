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
    /// 松下Mewtocol协议驱动 (RS232)
    /// 协议帧: % + 站号 + # + 命令 + 地址 + 校验(BCC/XOR)
    /// 响应帧: % + 站号 + $ + 数据 + 校验(BCC)
    /// </summary>
    public class PanasonicMewtocolDriver : IDriver
    {
        private SerialPort _serialPort;
        private string _portName = "COM1";
        private int _baudRate = 9600;
        private int _stationNo = 1;
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _lock = new object();

        public string DriverType => "PanasonicMewtocol";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _portName = config.GetParam("PortName", "COM1");
            _baudRate = config.GetIntParam("BaudRate", 9600);
            _stationNo = config.GetIntParam("StationNo", 1);

            try
            {
                lock (_lock)
                {
                    _serialPort = new SerialPort(_portName, _baudRate, Parity.Odd, 8, StopBits.One)
                    {
                        ReadTimeout = 2000,
                        WriteTimeout = 2000,
                        NewLine = "\r"
                    };
                    _serialPort.Open();
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                }
                IsConnected = true;
                NotifyStatus(true, $"Mewtocol 已连接 ({_portName}, {_baudRate}bps)");
                Logger.Debug($"Mewtocol 连接成功: {_portName}, {_baudRate}bps");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, $"连接失败: {ex.Message}");
                Logger.Warn($"Mewtocol 连接失败 [{_config.Name}]: {ex.Message}");
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
                        Driver = "mewtocol", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems
                    }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn($"Mewtocol 采集异常 [{_config.Name}]: {ex.Message}"); await Task.Delay(1000, _cts.Token); }
            }
        }

        /// <summary>
        /// 通过串口发送Mewtocol命令读取数据
        /// </summary>
        public async Task<object> ReadAsync(DataPoint point)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("串口未打开");

            string address = point.Address ?? "";
            string command = GetReadCommand(address);

            // 构建命令: %站号#命令地址校验\r
            string cmdStr = BuildCommand(_stationNo, command, address);

            byte[] response = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _serialPort.DiscardInBuffer();
                    byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdStr);
                    _serialPort.Write(cmdBytes, 0, cmdBytes.Length);
                    Logger.Debug($"Mewtocol Send: {cmdStr.TrimEnd('\r')}");
                }
                return ReadResponse();
            });

            return ParseResponse(response);
        }

        #region Mewtocol Protocol Implementation

        /// <summary>
        /// 根据地址类型判断读命令: DT/WR等数据寄存器用RD, R/X/Y等接点用RS
        /// </summary>
        private string GetReadCommand(string address)
        {
            if (string.IsNullOrEmpty(address)) return "RD";
            // 统一默认用RD读数据寄存器（覆盖DT、WR、WL等）
            return "RD";
        }

        /// <summary>
        /// 构建Mewtocol命令帧: %站号#命令地址BCC\r
        /// </summary>
        private string BuildCommand(int station, string command, string address)
        {
            // 格式化地址
            string addr = FormatAddress(address);
            string stationStr = station.ToString("D2");

            // 命令体 (不含%和BCC): 站号#命令地址
            string body = stationStr + "#" + command + addr;

            // 计算BCC (XOR校验): 从站号第一个字符到地址最后一个字符，异或
            byte bcc = 0;
            foreach (char c in body) bcc ^= (byte)c;

            string bccStr = bcc.ToString("X2");

            // 完整命令: % + 命令体 + BCC + \r
            string fullCmd = "%" + body + bccStr + "\r";
            return fullCmd;
        }

        /// <summary>
        /// 格式化地址 (如 DT100 → DT00100, R10 → R00010)
        /// 松下地址格式: 寄存器类型(2字符) + 编号(5字符，不足前补0)
        /// </summary>
        private string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return "DT00000";

            // 如果地址不带前缀，默认加DT
            int i = 0;
            while (i < address.Length && char.IsLetter(address[i])) i++;
            string prefix;
            string numStr;

            if (i == 0)
            {
                // 纯数字，默认DT
                prefix = "DT";
                numStr = address;
            }
            else
            {
                prefix = address.Substring(0, i).ToUpper();
                // 确保前缀2字符
                if (prefix.Length == 1) prefix = prefix + " ";
                numStr = address.Substring(i);
            }

            if (!int.TryParse(numStr, out int num)) num = 0;

            // 地址编号最多5位，数字部分前补0到5位
            return prefix + num.ToString("D5");
        }

        /// <summary>
        /// 读取Mewtocol响应: %站号$数据BCC\r
        /// </summary>
        private byte[] ReadResponse()
        {
            var buffer = new List<byte>();
            int b;
            bool headerFound = false;

            // 读取直到\r
            while ((b = _serialPort.ReadByte()) != -1)
            {
                if (b == 0x25) // %
                {
                    headerFound = true;
                    buffer.Clear();
                    buffer.Add((byte)b);
                    continue;
                }

                if (headerFound)
                {
                    buffer.Add((byte)b);
                    if (b == 0x0D) // \r - 帧结束
                    {
                        string response = Encoding.ASCII.GetString(buffer.ToArray());
                        Logger.Debug($"Mewtocol Recv: {response.TrimEnd('\r')}");

                        // 检查错误响应: ! 开头
                        if (buffer.Count > 1 && buffer[1] == 0x21) // '!'
                            throw new Exception($"Mewtocol错误响应: {response}");

                        // 验证BCC
                        if (buffer.Count > 5)
                        {
                            // 响应格式: %站号$数据BCC(2)\r
                            string respStr = Encoding.ASCII.GetString(buffer.ToArray());
                            int bccPos = respStr.Length - 3; // BCC在\r之前2位
                            if (bccPos > 3)
                            {
                                string dataPart = respStr.Substring(1, bccPos - 1); // 去掉%和BCC
                                byte calcBcc = 0;
                                foreach (char c in dataPart) calcBcc ^= (byte)c;

                                string recvBcc = respStr.Substring(bccPos, 2);
                                if (byte.TryParse(recvBcc, System.Globalization.NumberStyles.HexNumber, null, out byte expectedBcc))
                                {
                                    if (calcBcc != expectedBcc)
                                        Logger.Debug($"Mewtocol BCC校验警告: calc=0x{calcBcc:X2} expected=0x{expectedBcc:X2}");
                                }
                            }
                        }

                        return buffer.ToArray();
                    }
                }
            }

            throw new TimeoutException("等待Mewtocol响应超时");
        }

        /// <summary>
        /// 解析Mewtocol响应数据
        /// 正常响应: %01$RD64000D7200B9\r
        /// 数据部分: $之后的2字符状态码 + 数据(4字符HEX/16-bit)
        /// </summary>
        private double ParseResponse(byte[] response)
        {
            if (response == null || response.Length < 6)
                throw new Exception("Mewtocol响应数据不足");

            string respStr = Encoding.ASCII.GetString(response).TrimEnd('\r', '\n');

            // 解析: %站号$[状态码2][数据...]BCC
            int dataStart = respStr.IndexOf('$');
            if (dataStart < 0) throw new Exception("Mewtocol响应格式错误(缺少$)");

            // 状态码2字符
            string statusCode = respStr.Substring(dataStart + 1, 2);
            if (statusCode != "RD" && statusCode != "RS" && statusCode != "WD" && statusCode != "WS"
                && statusCode != "RC" && statusCode != "WC" && statusCode != "SD" && statusCode != "SC")
            {
                // 错误状态码: 如 "21"=设备号错误, "40"=BCC错误等
                throw new Exception($"Mewtocol PLC返回错误状态: {statusCode}");
            }

            // 数据在状态码之后，BCC之前
            string dataStr = respStr.Substring(dataStart + 3, respStr.Length - dataStart - 5); // 去掉status, BCC(2)

            // 数据格式: 4字符HEX = 16-bit值
            if (dataStr.Length >= 4)
            {
                string hexVal = dataStr.Substring(0, 4);
                if (int.TryParse(hexVal, System.Globalization.NumberStyles.HexNumber, null, out int val))
                    return val;
            }
            return 0;
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
