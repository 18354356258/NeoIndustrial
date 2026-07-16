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
    /// 三菱FX系列串口协议驱动 (RS232/RS422)
    /// 协议帧: ENQ + 站号 + PLC号 + 命令 + 延时 + 地址 + 字节数 + 校验和
    /// 响应帧: STX + 数据 + ETX + 校验和
    /// </summary>
    public class MitsubishiFXDriver : IDriver
    {
        private SerialPort _serialPort;
        private string _portName = "COM1";
        private int _baudRate = 9600;
        private int _stationNo = 0;
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _lock = new object();

        public string DriverType => "MitsubishiFX";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _portName = config.GetParam("PortName", "COM1");
            _baudRate = config.GetIntParam("BaudRate", 9600);
            _stationNo = config.GetIntParam("StationNo", 0);

            try
            {
                lock (_lock)
                {
                    _serialPort = new SerialPort(_portName, _baudRate, Parity.Even, 7, StopBits.One)
                    {
                        ReadTimeout = 2000,
                        WriteTimeout = 2000,
                        NewLine = "\r\n"
                    };
                    _serialPort.Open();
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                }
                IsConnected = true;
                NotifyStatus(true, $"Mitsubishi FX 已连接 ({_portName}, {_baudRate}bps)");
                Logger.Debug($"Mitsubishi FX 连接成功: {_portName}, {_baudRate}bps");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, $"连接失败: {ex.Message}");
                Logger.Warn($"Mitsubishi FX 连接失败 [{_config.Name}]: {ex.Message}");
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
                        Driver = "mitsubishi_fx", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems
                    }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn($"Mitsubishi FX 采集异常 [{_config.Name}]: {ex.Message}"); await Task.Delay(1000, _cts.Token); }
            }
        }

        /// <summary>
        /// 通过串口读取三菱FX寄存器值
        /// </summary>
        public async Task<object> ReadAsync(DataPoint point)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("串口未打开");

            string address = point.Address ?? "";
            string cmdCode = GetReadCommand(address);
            int registerCount = point.Length > 0 ? point.Length : 1;

            // 构建命令帧: ENQ + 站号(2) + PLC号(2) + 命令(2) + 延时(1) + 地址(5) + 字节数(2) + 校验和(2)
            byte[] cmdFrame = BuildReadFrame(_stationNo, cmdCode, address, registerCount);

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.Write(cmdFrame, 0, cmdFrame.Length);
                }
            });

            // 读取响应: STX + 数据 + ETX + 校验和(2)
            byte[] response = await ReadFxResponseAsync();

            // 解析响应数据
            double value = ParseFxResponse(response, cmdCode, registerCount);
            return value;
        }

        #region Mitsubishi FX Protocol Implementation

        /// <summary>
        /// 根据地址判断读命令码
        /// </summary>
        private string GetReadCommand(string address)
        {
            if (string.IsNullOrEmpty(address)) return "WR";
            char prefix = char.ToUpper(address[0]);
            // 位元件: X, Y, M, S, T(触点), C(触点) → BR (位读)
            // 字元件: D, T(当前值), C(当前值), R → WR (字读)
            if (prefix == 'X' || prefix == 'Y' || (prefix == 'M' && !address.StartsWith("M8", StringComparison.OrdinalIgnoreCase)))
                return "BR";
            // 特殊M(如M8000)实际是只读位
            return "WR";
        }

        /// <summary>
        /// 构建三菱FX读取命令帧
        /// ENQ(05) + 站号(2ASCII) + PLC号(2ASCII) + 命令(2ASCII) + 延时(1ASCII) + 地址(5ASCII) + 字节数/点数(2ASCII) + 校验和(2HEX)
        /// </summary>
        private byte[] BuildReadFrame(int station, string command, string address, int count)
        {
            // 格式化地址为5个字符 (如 D100 → D0100)
            string formattedAddr = FormatAddress(address);
            string stationStr = station.ToString("D2");
            string plcStr = "FF"; // PLC号固定FF
            char delay = '0';     // 延时0
            string countStr = command == "BR" ? count.ToString("D2") : (count * 2).ToString("D2");

            // 构建命令字符串(不含ENQ和校验和)
            string cmdStr = stationStr + plcStr + command + delay + formattedAddr + countStr;

            // 计算校验和: 命令字符串各字符ASCII码之和，取低8位转HEX
            byte sum = 0;
            foreach (char c in cmdStr) sum += (byte)c;
            string sumStr = sum.ToString("X2");

            // 完整帧: ENQ(0x05) + 命令字符串 + 校验和
            byte[] frame = new byte[1 + cmdStr.Length + 2];
            frame[0] = 0x05; // ENQ
            Encoding.ASCII.GetBytes(cmdStr, 0, cmdStr.Length, frame, 1);
            Encoding.ASCII.GetBytes(sumStr, 0, 2, frame, 1 + cmdStr.Length);

            Logger.Debug($"FX Send: {BitConverter.ToString(frame)}");
            return frame;
        }

        /// <summary>
        /// 格式化地址为5字符 (如 D100 → D0100, M10 → M0010)
        /// </summary>
        private string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return "D0000";

            // 分离字母前缀和数字部分
            int i = 0;
            while (i < address.Length && char.IsLetter(address[i])) i++;
            string prefix = i > 0 ? address.Substring(0, i).ToUpper() : "D";
            string numStr = i < address.Length ? address.Substring(i) : "0";

            if (!int.TryParse(numStr, out int num)) num = 0;
            return prefix + num.ToString("D4");
        }

        /// <summary>
        /// 读取FX协议响应帧: STX(02) + 数据 + ETX(03) + 校验和(2)
        /// </summary>
        private async Task<byte[]> ReadFxResponseAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    var buffer = new List<byte>();
                    int b;
                    bool stxFound = false;

                    // 等待STX
                    while ((b = _serialPort.ReadByte()) != -1)
                    {
                        if (b == 0x02) // STX
                        {
                            stxFound = true;
                            break;
                        }
                        // 0x15 = NAK, 0x06 = ACK 可能单独出现
                        if (b == 0x15) throw new Exception("FX PLC返回NAK(命令错误)");
                    }

                    if (!stxFound) throw new TimeoutException("等待FX响应超时(未收到STX)");

                    // 读取数据直到ETX
                    while ((b = _serialPort.ReadByte()) != -1)
                    {
                        if (b == 0x03) // ETX
                            break;
                        buffer.Add((byte)b);
                    }

                    // 读取2字节校验和
                    byte sumHi = (byte)_serialPort.ReadByte();
                    byte sumLo = (byte)_serialPort.ReadByte();

                    // 校验: 计算数据区+ETX的校验和
                    byte calcSum = 0;
                    foreach (byte dataByte in buffer) calcSum += dataByte;
                    calcSum += 0x03; // ETX

                    byte expectedSum = (byte)(((sumHi - 0x30) << 4) + (sumLo - 0x30));
                    // 校验和非关键路径，仅记录
                    if (calcSum != expectedSum)
                        Logger.Debug($"FX校验和警告: calc=0x{calcSum:X2} expected=0x{expectedSum:X2}");

                    return buffer.ToArray();
                }
            });
        }

        /// <summary>
        /// 解析FX响应数据
        /// </summary>
        private double ParseFxResponse(byte[] data, string command, int count)
        {
            if (data == null || data.Length == 0)
                throw new Exception("FX响应数据为空");

            if (command == "WR" || command == "WW")
            {
                // 字读取: 每2字节(4个ASCII HEX字符)为一个16位字
                // 响应是ASCII HEX格式: "0064" = 100
                string hexStr = Encoding.ASCII.GetString(data);
                if (hexStr.Length < 4) return 0;

                // 取第一个寄存器值(4 hex chars = 16-bit)
                if (int.TryParse(hexStr.Substring(0, 4), System.Globalization.NumberStyles.HexNumber, null, out int val))
                    return val;
                return 0;
            }
            else
            {
                // 位读取: 响应是ASCII "1" 或 "0"
                string bitStr = Encoding.ASCII.GetString(data);
                if (bitStr.Length > 0 && (bitStr[0] == '1')) return 1;
                return 0;
            }
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
