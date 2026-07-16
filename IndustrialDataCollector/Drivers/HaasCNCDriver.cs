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
    /// Haas CNC Q命令协议驱动 (RS232)
    /// 发送格式: Q{变量号} EOB\n  (如 "Q100 \n" 读取宏变量#100)
    /// 响应格式: " {变量值}\r\n"
    /// 变量范围: Q100-Q199(宏变量), Q600-Q699(系统变量)
    /// </summary>
    public class HaasCNCDriver : IDriver
    {
        private SerialPort _serialPort;
        private string _portName = "COM1";
        private int _baudRate = 9600;
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _lock = new object();

        public string DriverType => "HaasCNC";
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
                        ReadTimeout = 3000,
                        WriteTimeout = 2000,
                        NewLine = "\n"
                    };
                    _serialPort.Open();
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                }
                IsConnected = true;
                NotifyStatus(true, $"Haas CNC 已连接 ({_portName}, {_baudRate}bps)");
                Logger.Debug($"Haas CNC 连接成功: {_portName}, {_baudRate}bps");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, $"连接失败: {ex.Message}");
                Logger.Warn($"Haas CNC 连接失败 [{_config.Name}]: {ex.Message}");
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
                        Driver = "haas_cnc", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems
                    }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn($"Haas CNC 采集异常 [{_config.Name}]: {ex.Message}"); await Task.Delay(1000, _cts.Token); }
            }
        }

        /// <summary>
        /// 通过串口发送Haas Q命令读取宏变量值
        /// </summary>
        public async Task<object> ReadAsync(DataPoint point)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("串口未打开");

            string address = point.Address ?? "100";
            int varNum = ParseVariableNumber(address);

            // 构建Q命令: "Q{varNum} \n"
            string cmd = $"Q{varNum} \n";

            string response = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _serialPort.DiscardInBuffer();
                    byte[] cmdBytes = Encoding.ASCII.GetBytes(cmd);
                    _serialPort.Write(cmdBytes, 0, cmdBytes.Length);
                    Logger.Debug($"Haas Send: {cmd.TrimEnd('\n').Trim()}");
                }
                return ReadResponse();
            });

            // 解析响应值
            return ParseResponse(response);
        }

        #region Haas CNC Protocol Implementation

        /// <summary>
        /// 解析变量号: 地址如 "100" → 宏变量#100, "650" → 系统变量#650
        /// </summary>
        private int ParseVariableNumber(string address)
        {
            if (string.IsNullOrEmpty(address)) return 100;

            // 去掉可能的'#'前缀
            address = address.TrimStart('#');
            if (int.TryParse(address, out int num))
            {
                // 确保在有效范围内
                if (num >= 1 && num <= 999) return num;
                if (num >= 100 && num <= 199) return num;
            }
            return 100; // 默认宏变量100
        }

        /// <summary>
        /// 读取Haas CNC响应直到换行
        /// 响应格式: " {值}\r\n" 或 " {值}\n"
        /// 如果变量不存在返回 "UNDEFINED\r\n"
        /// </summary>
        private string ReadResponse()
        {
            var buffer = new List<byte>();
            int b;

            try
            {
                while ((b = _serialPort.ReadByte()) != -1)
                {
                    buffer.Add((byte)b);
                    if (b == 0x0A) // \n
                    {
                        string response = Encoding.ASCII.GetString(buffer.ToArray()).TrimEnd('\r', '\n', ' ');
                        Logger.Debug($"Haas Recv: [{response}]");
                        return response;
                    }
                }
            }
            catch (TimeoutException)
            {
                // 有些CNC不返回换行，返回缓冲区已有内容
                if (buffer.Count > 0)
                {
                    string response = Encoding.ASCII.GetString(buffer.ToArray()).TrimEnd('\r', '\n', ' ');
                    Logger.Debug($"Haas Recv(raw): [{response}]");
                    return response;
                }
                throw;
            }

            throw new TimeoutException("等待Haas CNC响应超时");
        }

        /// <summary>
        /// 解析Q命令响应
        /// 正常响应: " 123.456"
        /// 错误响应: "UNDEFINED"
        /// </summary>
        private double ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                throw new Exception("Haas CNC空响应");

            if (response.Contains("UNDEFINED") || response.Contains("ERROR"))
                throw new Exception($"Haas CNC返回未定义/错误: {response}");

            // 去掉前导下划线和空格
            response = response.Replace("_", "").Trim();

            if (double.TryParse(response, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double val))
                return val;

            throw new Exception($"无法解析Haas CNC响应值: {response}");
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
