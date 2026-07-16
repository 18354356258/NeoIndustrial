using System;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// 模拟器驱动 - 用于测试，生成模拟数据
    /// </summary>
    public class SimulatorDriver : IDriver
    {
        private DeviceConfig _config;
        private bool _disposed;
        private readonly Random _random = new Random();
        private long _sampleCount = 0;
        private double _baseTimestamp = 0;

        public string DriverType => "Simulator";
        public bool IsConnected => _config != null;

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _sampleCount = 0;
            _baseTimestamp = DateTime.Now.Ticks / 10000000.0;
            NotifyStatus(true, "模拟器已就绪");
            Logger.Info($"模拟器已启动: {config.Name}");
            return Task.FromResult(true);
        }

        public Task DisconnectAsync()
        {
            NotifyStatus(false, "模拟器已停止");
            Logger.Info($"模拟器已停止: {_config?.Name ?? "未知"}");
            _config = null;
            return Task.CompletedTask;
        }

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null) return;
            int pollInterval = _config.GetIntParam("PollInterval", 500);
            if (pollInterval < 100) pollInterval = 100;

            Logger.Debug($"模拟器开始采集: {_config.Name}, 间隔: {pollInterval}ms");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    var cycleItems = new System.Collections.Generic.List<CycleDataItem>();

                    foreach (var point in _config.DataPoints)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!point.IsActive) continue;

                        double simulatedValue = GenerateSimulatedValue(point);
                        var data = new CollectedData
                        {
                            DeviceId = _config.Id,
                            DeviceName = _config.Name,
                            VariableName = point.Name,
                            DataType = point.DataType,
                            Value = point.DataType.ToLower() == "int"
                                ? ((int)Math.Round(simulatedValue)).ToString()
                                : simulatedValue.ToString("F3"),
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
                            Value = simulatedValue,
                            Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null
                        });
                        _sampleCount++;
                    }

                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Driver = "simulator",
                        Device = _config.Name,
                        DeviceId = _config.Id, Values = cycleItems
                    }));

                    await Task.Delay(pollInterval, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.Warn($"模拟器采集异常: {ex.Message}");
                    await Task.Delay(1000, token);
                }
            }

            Logger.Debug($"模拟器采集结束: {_config.Name}");
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            double value = GenerateSimulatedValue(point);
            object result;
            switch (point.DataType.ToLower())
            {
                case "int":
                    result = (int)Math.Round(value);
                    break;
                case "bool":
                    result = value > 0.5;
                    break;
                case "float":
                    result = Math.Round(value, 3);
                    break;
                default:
                    result = Math.Round(value, 3);
                    break;
            }
            return Task.FromResult<object>(result);
        }

        /// <summary>
        /// 生成模拟数据，根据变量名自动推断波形
        /// </summary>
        private double GenerateSimulatedValue(DataPoint point)
        {
            double now = (DateTime.Now.Ticks / 10000000.0) - _baseTimestamp;
            string name = point.Name.ToLower();

            if (name.Contains("温度") || name.Contains("temp"))
                return 25.0 + 10.0 * Math.Sin(now * 0.1) + _random.NextDouble() * 2.0;

            if (name.Contains("压力") || name.Contains("press"))
                return 0.5 + 0.3 * Math.Sin(now * 0.05) + _random.NextDouble() * 0.1;

            if (name.Contains("流量") || name.Contains("flow"))
                return 100.0 + 30.0 * Math.Sin(now * 0.08 + 1.0) + _random.NextDouble() * 5.0;

            if (name.Contains("转速") || name.Contains("speed"))
                return 1500.0 + 200.0 * Math.Sin(now * 0.15) + _random.NextDouble() * 20.0;

            if (name.Contains("液位") || name.Contains("level"))
                return 50.0 + 20.0 * Math.Sin(now * 0.03) + _random.NextDouble() * 2.0;

            if (name.Contains("开关") || name.Contains("switch"))
                return Math.Sin(now * 0.5) > 0 ? 1.0 : 0.0;

            // 默认随机波形
            return 50.0 + 30.0 * Math.Sin(now * 0.07 + point.GetHashCode()) + _random.NextDouble() * 5.0;
        }

        public long SampleCount => _sampleCount;

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
