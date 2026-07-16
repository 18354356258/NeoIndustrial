using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Drivers;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 数据采集服务 - 管理所有设备驱动的采集生命周期
    /// </summary>
    public class DataCollectionService : IDisposable
    {
        private static readonly Lazy<DataCollectionService> _instance =
            new Lazy<DataCollectionService>(() => new DataCollectionService());
        public static DataCollectionService Instance
        {
            get { return _instance.Value; }
        }

        /// <summary>
        /// 驱动运行时条目（替代 C# 7 元组）
        /// </summary>
        private class DriverEntry
        {
            public IDriver Driver { get; set; }
            public CancellationTokenSource Cancellation { get; set; }
        }

        private readonly ConcurrentDictionary<string, DriverEntry> _runningDrivers =
            new ConcurrentDictionary<string, DriverEntry>();

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnDeviceStatusChanged;

        private DataCollectionService() { }

        /// <summary>
        /// 启动单个设备的采集
        /// </summary>
        public async Task StartDeviceAsync(DeviceConfig config)
        {
            if (config == null) return;

            // 如果已运行，先停止
            await StopDeviceAsync(config.Id);

            try
            {
                var driver = DriverManager.CreateDriver(config);
                var cts = new CancellationTokenSource();

                driver.OnDataReceived += (s, e) =>
                {
                    // 应用边缘计算管线
                    try
                    {
                        string pointId = e.Data.DeviceId + "_" + e.Data.VariableName;
                        var point = DataProcessor.Instance.GetPoint(pointId);
                        if (point != null && double.TryParse(e.Data.Value,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double rawVal))
                        {
                            double processed = DataProcessor.Instance.ApplyEdgeProcessing(pointId, rawVal, point);
                            e.Data.Value = processed.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)
                                .TrimEnd('0').TrimEnd('.');
                            if (string.IsNullOrEmpty(e.Data.Value)) e.Data.Value = "0";
                        }
                    }
                    catch (Exception ex) { Logger.Debug("Edge processing error: " + ex.Message); }
                    if (OnDataReceived != null) OnDataReceived(s, e);
                };
                driver.OnStatusChanged += (s, e) =>
                {
                    if (OnDeviceStatusChanged != null) OnDeviceStatusChanged(s, e);
                };
                driver.OnCycleCompleted += (s, e) =>
                {
                    if (OnCycleCompleted != null) OnCycleCompleted(s, e);
                };

                bool connected = await driver.ConnectAsync(config);
                if (connected)
                {
                    var entry = new DriverEntry();
                    entry.Driver = driver;
                    entry.Cancellation = cts;
                    _runningDrivers[config.Id] = entry;

                    // 注册变量点到边缘计算缓存
                    DataProcessor.Instance.RegisterDevicePoints(config);

                    // 后台运行采集循环
                    Task.Run(() => driver.StartCollectAsync(cts.Token));
                    Logger.Info("采集已启动: " + config.Name);

                    // 语义层 v2: 更新设备节点状态为 Online + 发事件
                    try
                    {
                        var node = SemanticService.Instance.GetNodeBySource("device", config.Id);
                        if (node != null)
                        {
                            SemanticService.Instance.UpdateNodeStatus(node.Id, NodeStatus.Online);
                            SemanticService.Instance.SaveEvent(new IndustrialDataCollection.Models.SemanticVariableEvent
                            {
                                NodeId = node.Id,
                                EventType = SemanticEventType.Start,
                                Description = string.Format("{0} 开始采集 (驱动: {1})", config.Name, config.DriverType)
                            });
                        }
                    }
                    catch { }
                }
                else
                {
                    driver.Dispose();
                    Logger.Warn("采集启动失败: " + config.Name + " - 连接失败");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("启动采集失败 [" + config.Name + "]: " + ex.Message);
            }
        }

        /// <summary>
        /// 停止单个设备的采集
        /// </summary>
        public async Task StopDeviceAsync(string deviceId)
        {
            DriverEntry entry;
            if (_runningDrivers.TryRemove(deviceId, out entry))
            {
                entry.Cancellation.Cancel();
                entry.Cancellation.Dispose();

                // 清除边缘计算缓存
                DataProcessor.Instance.UnregisterDevicePoints(deviceId);

                try
                {
                    await entry.Driver.DisconnectAsync();
                }
                catch { }

                entry.Driver.Dispose();

                Logger.Info("采集已停止: deviceId=" + deviceId);

                // 语义层 v2: 更新设备节点状态为 Stopped + 发事件
                try
                {
                    var node = SemanticService.Instance.GetNodeBySource("device", deviceId);
                    if (node != null)
                    {
                        SemanticService.Instance.UpdateNodeStatus(node.Id, NodeStatus.Stopped);
                        SemanticService.Instance.SaveEvent(new IndustrialDataCollection.Models.SemanticVariableEvent
                        {
                            NodeId = node.Id,
                            EventType = SemanticEventType.Stop,
                            Description = "采集已停止"
                        });
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 启动所有已启用设备的采集
        /// </summary>
        public async Task StartAllAsync(List<DeviceConfig> devices)
        {
            foreach (var device in devices)
            {
                if (device.Enabled)
                {
                    await StartDeviceAsync(device);
                }
            }
        }

        /// <summary>
        /// 停止所有设备的采集
        /// </summary>
        public async Task StopAllAsync()
        {
            List<string> keys = new List<string>(_runningDrivers.Keys);
            foreach (var key in keys)
            {
                await StopDeviceAsync(key);
            }
        }

        /// <summary>
        /// 获取指定设备是否在采集中
        /// </summary>
        public bool IsDeviceRunning(string deviceId)
        {
            return _runningDrivers.ContainsKey(deviceId);
        }

        /// <summary>
        /// 获取正在采集的设备数量
        /// </summary>
        public int RunningCount
        {
            get { return _runningDrivers.Count; }
        }

        /// <summary>
        /// 获取所有运行中的设备ID列表
        /// </summary>
        public List<string> GetRunningDeviceIds()
        {
            return new List<string>(_runningDrivers.Keys);
        }

        /// <summary>
        /// 获取运行中设备的驱动类型（用于MCP等外部查询）
        /// </summary>
        public string GetDeviceDriverType(string deviceId)
        {
            DriverEntry entry;
            if (_runningDrivers.TryGetValue(deviceId, out entry) && entry.Driver != null)
                return entry.Driver.DriverType;
            return null;
        }

        /// <summary>
        /// 停止所有设备并释放资源
        /// </summary>
        public async void Dispose()
        {
            await StopAllAsync();
        }
    }
}
