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
    /// DALI 照明控制协议驱动 (通过DALI-USB网关)
    /// DALI命令: 16位前向帧(地址字节+数据字节)
    /// 响应: 8位返回帧
    /// 短地址: 0-63, 组地址: 0-15, 广播: 254/255
    /// </summary>
    public class DALIDriver : IDriver
    {
        private SerialPort _serialPort;
        private string _portName = "COM1";
        private int _baudRate = 9600;
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _lock = new object();

        public string DriverType => "DALI";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _portName = config.GetParam("PortName", "COM1");
            _baudRate = config.GetIntParam("BaudRate", 9600);

            try
            {
                lock (_lock)
                {
                    _serialPort = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
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
                NotifyStatus(true, $"DALI 已连接 ({_portName}, {_baudRate}bps)");
                Logger.Debug($"DALI 连接成功: {_portName}, {_baudRate}bps");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, $"连接失败: {ex.Message}");
                Logger.Warn($"DALI 连接失败 [{_config.Name}]: {ex.Message}");
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
                        Driver = "dali", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems
                    }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn($"DALI 采集异常 [{_config.Name}]: {ex.Message}"); await Task.Delay(1000, _cts.Token); }
            }
        }

        /// <summary>
        /// 通过串口(DALI-USB网关)发送DALI命令读取数据
        /// </summary>
        public async Task<object> ReadAsync(DataPoint point)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("串口未打开");

            string address = point.Address ?? "0";
            byte shortAddr = ParseAddress(address);

            // 构建DALI查询命令: 查询状态(QUERY STATUS)
            // 标准指令 144: QUERY STATUS (地址YAAAAA + 10010000)
            byte[] forwardFrame = BuildQueryCommand(shortAddr);

            byte[] response = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.Write(forwardFrame, 0, forwardFrame.Length);
                    Logger.Debug($"DALI Query -> addr:{shortAddr}: {BitConverter.ToString(forwardFrame)}");
                }
                return ReadBackwardFrame();
            });

            return ParseResponse(response);
        }

        #region DALI Protocol Implementation

        /// <summary>
        /// 解析DALI地址: "0"-"63" → 短地址, "G0"-"G15" → 组地址, "BC" → 广播
        /// </summary>
        private byte ParseAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return 0;

            address = address.Trim().ToUpper();

            if (address == "BC" || address == "BROADCAST") return 0xFE;
            if (address == "BCU" || address == "UNASSIGNED") return 0xFF;

            // 组地址: G0-G15
            if (address.StartsWith("G"))
            {
                if (byte.TryParse(address.Substring(1), out byte gAddr) && gAddr <= 15)
                    return (byte)(0x80 | (gAddr << 1)); // 组地址: 1AAA AAA0
                throw new NotSupportedException($"DALI无效组地址: {address}");
            }

            // 短地址: 0-63
            if (byte.TryParse(address, out byte sAddr) && sAddr <= 63)
                return (byte)(sAddr << 1); // 短地址: AAA AAA0

            throw new NotSupportedException($"DALI无效地址: {address} (支持0-63/G0-G15/BC)");
        }

        /// <summary>
        /// 构建DALI前向帧 (2字节: 地址字节 + 命令字节)
        /// 查询状态: 标准指令 144 = 0x90 (QUERY STATUS)
        /// 前向帧: YAAAAAAS | XXXXXXXX (Y=0/1, A=地址, S=0, X=命令)
        /// </summary>
        private byte[] BuildQueryCommand(byte address)
        {
            // 地址字节格式: YAAAAAAS
            // Y=0(地址为短地址) AAA AAA=地址6bit, S=0(直接弧光功率)
            byte addrByte = (byte)(address | 0x00); // S=0, Y already set by ParseAddress

            // QUERY STATUS: 命令码 0x90 (144)
            byte cmdByte = 0x90;

            return new byte[] { addrByte, cmdByte };
        }

        /// <summary>
        /// 读取DALI返回帧 (1字节)
        /// 网关通过串口返回响应字节
        /// </summary>
        private byte[] ReadBackwardFrame()
        {
            try
            {
                int b = _serialPort.ReadByte();
                if (b < 0) throw new TimeoutException("等待DALI返回帧超时");

                byte response = (byte)b;
                Logger.Debug($"DALI Backward <- : 0x{response:X2}");

                return new byte[] { response };
            }
            catch (TimeoutException)
            {
                // DALI无响应可能意味着设备不存在或离线
                Logger.Debug("DALI无响应(设备可能离线)");
                return new byte[] { 0x00 };
            }
        }

        /// <summary>
        /// 解析DALI返回帧
        /// QUERY STATUS 返回: 8位状态字
        /// Bit0: 灯故障, Bit1: 灯电弧功率开启, Bit2: 查询结果=限制错误
        /// Bit3: 复位状态, Bit4: 丢失短地址, Bit5: 电源周期检测
        /// Bit6: 控制装置错误, Bit7: 硬件错误/未连接
        /// </summary>
        private double ParseResponse(byte[] response)
        {
            if (response == null || response.Length == 0)
                return 0;

            byte status = response[0];

            // 返回状态字作为值
            // status=0x00: OK(灯正常)
            // status=0xFF: 无响应/离线
            if (status == 0xFF) return 0;

            // 提取关键状态位
            bool lampFailure = (status & 0x01) != 0;
            bool lampOn = (status & 0x02) != 0;
            bool limitError = (status & 0x04) != 0;
            bool resetState = (status & 0x08) != 0;
            bool missingShortAddr = (status & 0x10) != 0;
            bool powerCycleSeen = (status & 0x20) != 0;

            // 返回简化的状态值:
            // 灯亮+无故障 = 1, 灯灭+无故障 = 0, 有故障 = -1
            if (lampFailure || limitError) return -1;
            if (lampOn) return 1;

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
