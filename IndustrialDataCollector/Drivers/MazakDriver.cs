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
    /// Mazak CNC 串口协议驱动 (RS232)
    /// 使用简化的ASCII命令协议读取CNC变量
    /// 地址格式: "#100"→宏变量, "X100"→X轴位置, "S100"→主轴速度等
    /// </summary>
    public class MazakDriver : IDriver
    {
        private SerialPort _serialPort;
        private string _portName = "COM1";
        private int _baudRate = 9600;
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _lock = new object();

        public string DriverType => "Mazak";
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
                    _serialPort = new SerialPort(_portName, _baudRate, Parity.Even, 7, StopBits.One)
                    {
                        ReadTimeout = 3000,
                        WriteTimeout = 2000,
                        NewLine = "\r\n"
                    };
                    _serialPort.Open();
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                }
                IsConnected = true;
                NotifyStatus(true, $"Mazak 已连接 ({_portName}, {_baudRate}bps)");
                Logger.Debug($"Mazak 连接成功: {_portName}, {_baudRate}bps");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, $"连接失败: {ex.Message}");
                Logger.Warn($"Mazak 连接失败 [{_config.Name}]: {ex.Message}");
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
                        Driver = "mazak", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems
                    }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn($"Mazak 采集异常 [{_config.Name}]: {ex.Message}"); await Task.Delay(1000, _cts.Token); }
            }
        }

        /// <summary>
        /// 通过串口发送简化Mazak ASCII命令读取值
        /// </summary>
        public async Task<object> ReadAsync(DataPoint point)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("串口未打开");

            string address = point.Address ?? "";
            string cmd = BuildReadCommand(address);

            string response = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _serialPort.DiscardInBuffer();
                    byte[] cmdBytes = Encoding.ASCII.GetBytes(cmd);
                    _serialPort.Write(cmdBytes, 0, cmdBytes.Length);
                    Logger.Debug($"Mazak Send: {cmd.TrimEnd('\r', '\n')}");
                }
                return ReadResponse();
            });

            return ParseResponse(response);
        }

        #region Mazak Protocol Implementation

        /// <summary>
        /// 构建简化的Mazak ASCII读取命令
        /// 基本实现: READ {变量名}\r\n → 响应: {值}\r\n
        /// 复杂Mazatrol操作返回NotSupportedException
        /// </summary>
        private string BuildReadCommand(string address)
        {
            if (string.IsNullOrEmpty(address))
                return "READ #100\r\n";

            address = address.Trim();

            // 检查是否为复杂命令(程序上传/下载等)
            if (address.StartsWith("PROG", StringComparison.OrdinalIgnoreCase)
                || address.StartsWith("UPLOAD", StringComparison.OrdinalIgnoreCase)
                || address.StartsWith("DOWNLOAD", StringComparison.OrdinalIgnoreCase)
                || address.StartsWith("TOOL_DATA", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Mazak不支持的操作: {address}。仅支持变量读取(如 #100, X100, S100)");
            }

            return $"READ {address}\r\n";
        }

        /// <summary>
        /// 读取Mazak响应直到完整行结束
        /// </summary>
        private string ReadResponse()
        {
            var buffer = new List<byte>();
            int b;

            while ((b = _serialPort.ReadByte()) != -1)
            {
                buffer.Add((byte)b);
                if (b == 0x0A) // \n
                    break;
            }

            string response = Encoding.ASCII.GetString(buffer.ToArray())
                .TrimEnd('\r', '\n', ' ', '\t');
            Logger.Debug($"Mazak Recv: [{response}]");
            return response;
        }

        /// <summary>
        /// 解析Mazak响应值
        /// </summary>
        private double ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                throw new Exception("Mazak空响应");

            // 检查错误响应
            if (response.StartsWith("ERR", StringComparison.OrdinalIgnoreCase)
                || response.StartsWith("NG", StringComparison.OrdinalIgnoreCase)
                || response.StartsWith("NAK", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Mazak返回错误: {response}");
            }

            // 尝试解析为数字
            if (double.TryParse(response, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double val))
                return val;

            throw new Exception($"无法解析Mazak响应值: {response}");
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
