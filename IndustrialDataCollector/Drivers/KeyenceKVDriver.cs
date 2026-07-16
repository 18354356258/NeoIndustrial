using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// Keyence KV 驱动 - Keyence KV Series PLC
    /// 通过 TCP 与基恩士 KV 系列 PLC 通信
    /// </summary>
    public class KeyenceKVDriver : IDriver
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private string _ipAddress = "127.0.0.1";
        private int _port = 8501;
        private string _plcType = "KV-8000";
        private DeviceConfig _config;
        private bool _disposed;
        private int _simCounter;

        public string DriverType { get { return "KeyenceKV"; } }
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _ipAddress = config.GetParam("IP", "127.0.0.1");
            _port = config.GetIntParam("Port", 8501);
            _plcType = config.GetParam("PLCType", "KV-8000");

            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_ipAddress, _port);
                _stream = _tcpClient.GetStream();
                _stream.ReadTimeout = 3000;
                _stream.WriteTimeout = 3000;

                IsConnected = true;
                NotifyStatus(true, string.Format("Keyence KV 已连接 ({0}:{1}, {2})",
                    _ipAddress, _port, _plcType));
                Logger.Debug(string.Format("Keyence KV 连接成功: {0}:{1}, {2}", _ipAddress, _port, _plcType));
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, string.Format("连接失败: {0}", ex.Message));
                Logger.Warn(string.Format("Keyence KV 连接失败 [{0}]: {1}", _config.Name, ex.Message));
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            try
            {
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch { }
            IsConnected = false;
            NotifyStatus(false, "已断开");
            return Task.CompletedTask;
        }

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null) return;
            int pollInterval = _config.GetIntParam("PollInterval", 1000);
            if (pollInterval < 100) pollInterval = 100;

            Logger.Debug(string.Format("Keyence KV 采集开始: {0}, IP={1}:{2}, 间隔={3}ms",
                _config.Name, _ipAddress, _port, pollInterval));

            while (!token.IsCancellationRequested)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    if (!IsConnected)
                    {
                        await ConnectAsync(_config);
                        if (!IsConnected)
                        {
                            await Task.Delay(3000, token);
                            continue;
                        }
                    }

                    var cycleItems = new System.Collections.Generic.List<CycleDataItem>();

                    foreach (var point in _config.DataPoints)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!point.IsActive) continue;

                        object value = null;
                        object rawValue = null;
                        try
                        {
                            rawValue = await ReadAsync(point);
                            value = rawValue;
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug(string.Format("Keyence KV 读取失败 [{0}.{1}]: {2}", _config.Name, point.Name, ex.Message));
                        }

                        double processedValue = value is double dv ? dv : 0;
                        if (value != null && value.ToString() != "ERR")
                        {
                            try { processedValue = DataProcessor.Instance.ApplyEdgeProcessing(_config.Id + "_" + point.Name, Convert.ToDouble(value), point); }
                            catch (Exception ex) { Logger.Debug("Edge processing failed for " + point.Name + ": " + ex.Message); }
                        }

                        var data = new CollectedData
                        {
                            DeviceId = _config.Id,
                            DeviceName = _config.Name,
                            VariableName = point.Name,
                            DataType = point.DataType,
                            Value = processedValue.ToString("F6").TrimEnd('0').TrimEnd('.'),
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
                            DataType = point.DataType,
                            Value = rawValue ?? 0,
                            Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null
                        });
                    }

                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Driver = "keyence_kv",
                        Device = _config.Name,
                        DeviceId = _config.Id, Values = cycleItems
                    }));

                    await Task.Delay(pollInterval, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    IsConnected = false;
                    Logger.Warn(string.Format("Keyence KV 采集异常 [{0}]: {1}", _config.Name, ex.Message));
                    await Task.Delay(1000, token);
                }
            }

            Logger.Debug(string.Format("Keyence KV 采集结束: {0}", _config.Name));
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            object result = 0;
            _simCounter++;

            // TODO: 实现真正的 Keyence KV 上位链路通信
            // 当前返回模拟数据用于驱动框架验证
            switch (point.DataType.ToLower())
            {
                case "bool":
                case "coil":
                    result = (_simCounter % 3 != 0);
                    break;
                case "byte":
                    result = (byte)((_simCounter + 15) % 256);
                    break;
                case "int16":
                case "short":
                    result = (short)((_simCounter * 5) % 200 - 100);
                    break;
                case "uint16":
                case "ushort":
                case "word":
                    result = (ushort)((_simCounter * 9) % 65535);
                    break;
                case "int32":
                case "int":
                    result = _simCounter * 8;
                    break;
                case "uint32":
                case "dword":
                    result = (uint)(_simCounter * 80);
                    break;
                case "float":
                case "real":
                    result = 18.0f + (_simCounter % 70) * 0.35f;
                    break;
                case "double":
                    result = 18.0 + (_simCounter % 70) * 0.35;
                    break;
                default:
                    result = (_simCounter * 4) % 1000;
                    break;
            }

            double doubleVal = Convert.ToDouble(result);
            result = point.ConvertValue(doubleVal);
            return Task.FromResult(result);
        }

        private void NotifyStatus(bool connected, string message)
        {
            OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(
                _config?.Id ?? "", _config?.Name ?? "", connected, message));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisconnectAsync().Wait();
        }
    }
}
