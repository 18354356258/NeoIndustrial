using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 数据处理服务 - 缓存最新值和历史数据
    /// </summary>
    public class DataProcessor
    {
        private static readonly Lazy<DataProcessor> _instance =
            new Lazy<DataProcessor>(() => new DataProcessor());
        public static DataProcessor Instance
        {
            get { return _instance.Value; }
        }

        // 最新值缓存 key = "deviceId_variableName"
        private readonly ConcurrentDictionary<string, CollectedData> _latestCache =
            new ConcurrentDictionary<string, CollectedData>();

        // 历史数据缓存
        private readonly ConcurrentDictionary<string, Queue<CollectedData>> _historyCache =
            new ConcurrentDictionary<string, Queue<CollectedData>>();
        private const int MAX_HISTORY_PER_VARIABLE = 1000;

        public event EventHandler<CollectedData> DataProcessed;

        private DataProcessor() { }

        /// <summary>
        /// 处理并缓存数据
        /// </summary>
        public CollectedData Process(CollectedData data)
        {
            if (data == null) return null;

            string cacheKey = data.DeviceId + "_" + data.VariableName;

            // 更新最新值
            _latestCache[cacheKey] = data;

            // 更新历史
            Queue<CollectedData> history = _historyCache.GetOrAdd(cacheKey,
                (key) => new Queue<CollectedData>());
            lock (history)
            {
                history.Enqueue(data);
                while (history.Count > MAX_HISTORY_PER_VARIABLE)
                    history.Dequeue();
            }

            if (DataProcessed != null)
                DataProcessed(this, data);
            return data;
        }

        /// <summary>
        /// 获取最新值
        /// </summary>
        public CollectedData GetLatest(string deviceId, string variableName)
        {
            string cacheKey = deviceId + "_" + variableName;
            CollectedData result;
            _latestCache.TryGetValue(cacheKey, out result);
            return result;
        }

        /// <summary>
        /// 获取设备的所有最新值
        /// </summary>
        public List<CollectedData> GetLatestByDevice(string deviceId)
        {
            return _latestCache.Values
                .Where(d => d.DeviceId == deviceId)
                .OrderBy(d => d.VariableName)
                .ToList();
        }

        /// <summary>
        /// 获取所有最新值
        /// </summary>
        public List<CollectedData> GetAllLatest()
        {
            return _latestCache.Values
                .OrderBy(d => d.DeviceName)
                .ThenBy(d => d.VariableName)
                .ToList();
        }

        /// <summary>
        /// 获取历史数据
        /// </summary>
        public List<CollectedData> GetHistory(string deviceId, string variableName, int count = 100)
        {
            string cacheKey = deviceId + "_" + variableName;
            Queue<CollectedData> queue;
            if (_historyCache.TryGetValue(cacheKey, out queue))
            {
                lock (queue)
                {
                    return queue.Reverse().Take(count).ToList();
                }
            }
            return new List<CollectedData>();
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void ClearAll()
        {
            _latestCache.Clear();
            _historyCache.Clear();
        }

        /// <summary>
        /// 清空指定设备的缓存
        /// </summary>
        public void ClearDevice(string deviceId)
        {
            List<string> keys = new List<string>();
            foreach (var kv in _latestCache)
            {
                if (kv.Key.StartsWith(deviceId + "_"))
                    keys.Add(kv.Key);
            }
            foreach (var k in keys)
            {
                CollectedData removedData;
                _latestCache.TryRemove(k, out removedData);
                Queue<CollectedData> removedQueue;
                _historyCache.TryRemove(k, out removedQueue);
            }
        }

        /// <summary>
        /// 获取缓存统计
        /// </summary>
        public int LatestCount
        {
            get { return _latestCache.Count; }
        }

        public int GetHistoryCount()
        {
            int total = 0;
            foreach (var kv in _historyCache)
            {
                lock (kv.Value)
                {
                    total += kv.Value.Count;
                }
            }
            return total;
        }

        // ═══════ 边缘计算管线 ═══════

        // 设备配置缓存（按 pointId 索引 DataPoint）
        private readonly ConcurrentDictionary<string, DataPoint> _pointCache =
            new ConcurrentDictionary<string, DataPoint>();

        // 设备 ID → 设备名映射（供报警快照使用）
        private readonly ConcurrentDictionary<string, string> _deviceNames =
            new ConcurrentDictionary<string, string>();

        /// <summary>注册设备的所有变量点到缓存中（采集启动时调用）</summary>
        public void RegisterDevicePoints(DeviceConfig config)
        {
            if (config?.DataPoints == null) return;
            _deviceNames[config.Id] = config.Name;
            foreach (var point in config.DataPoints)
                _pointCache[MakeKey(config.Id, point.Name)] = point;
        }

        /// <summary>清除设备的所有变量点缓存（采集停止时调用）</summary>
        public void UnregisterDevicePoints(string deviceId)
        {
            var prefix = deviceId + "_";
            var keys = _pointCache.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var k in keys) _pointCache.TryRemove(k, out _);
            _deviceNames.TryRemove(deviceId, out _);
            // 同时清理报警状态
            foreach (var k in _alarmCounters.Keys.Where(k => k.StartsWith(prefix)).ToList())
                _alarmCounters.TryRemove(k, out _);
            foreach (var k in _alarmFired.Keys.Where(k => k.StartsWith(prefix)).ToList())
                _alarmFired.TryRemove(k, out _);
            foreach (var k in _freezeCounters.Keys.Where(k => k.StartsWith(prefix)).ToList())
                _freezeCounters.TryRemove(k, out _);
            foreach (var k in _rangeFired.Keys.Where(k => k.StartsWith(prefix)).ToList())
                _rangeFired.TryRemove(k, out _);
        }

        /// <summary>从缓存获取变量点配置</summary>
        public DataPoint GetPoint(string pointId)
        {
            _pointCache.TryGetValue(pointId, out var point);
            return point;
        }

        private static string MakeKey(string deviceId, string variableName)
        {
            return deviceId + "_" + variableName;
        }

        private readonly ConcurrentDictionary<string, List<double>> _filterBuffers =
            new ConcurrentDictionary<string, List<double>>();
        private readonly ConcurrentDictionary<string, double> _lastValues =
            new ConcurrentDictionary<string, double>();
        private readonly ConcurrentDictionary<string, int> _alarmCounters =
            new ConcurrentDictionary<string, int>();
        /// <summary>已触发的报警，key=pointId，null=未在报警状态</summary>
        private readonly ConcurrentDictionary<string, string> _alarmFired =
            new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, int> _freezeCounters =
            new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, bool> _rangeFired =
            new ConcurrentDictionary<string, bool>();

        /// <summary>
        /// 对采集值应用完整的边缘计算管线
        /// </summary>
        public double ApplyEdgeProcessing(string pointId, double rawValue, DataPoint point)
        {
            if (point == null) return rawValue;
            double value = rawValue;

            // 0. 倍率+偏移（已在调用前完成，此为安全网）
            value = point.ConvertValue(value);

            // 0.5. 信号变换（在滤波之前）
            if (point.SquareRootEnabled && value >= 0)
                value = Math.Sqrt(value);
            if (point.AbsValueEnabled)
                value = Math.Abs(value);
            if (point.RateOfChangeEnabled)
            {
                double lastVal;
                if (_lastValues.TryGetValue(pointId, out lastVal))
                    value = value - lastVal;
            }

            // 1. 公式计算
            if (point.CalculationEnabled && !string.IsNullOrWhiteSpace(point.CalculationExpression))
            {
                try { value = EvaluateExpression(point.CalculationExpression, value); }
                catch { /* 表达式错误时保持原值 */ }
            }

            // 2. 滤波
            if (point.FilterEnabled)
                value = ApplyFilter(pointId, value, point);

            // 3. 修约
            if (point.RoundingEnabled)
                value = ApplyRounding(value, point);

            // 4. 数据清洗
            if (point.CleanEnabled)
                value = ApplyCleaning(pointId, value, point);

            // 5. 报警检测
            if (point.AlarmEnabled)
                CheckAlarms(pointId, value, point);

            _lastValues[pointId] = value;

            // 有任何边缘处理时写一条 Info 日志，方便验证管线生效
            if (Math.Abs(value - rawValue) > 0.0001 || point.RoundingEnabled || point.FilterEnabled || point.CleanEnabled || point.CalculationEnabled || point.SquareRootEnabled || point.AbsValueEnabled || point.RateOfChangeEnabled)
                Logger.Info(string.Format("[Edge] {0}: raw={1:F4} → processed={2:F4}", point.Name, rawValue, value));

            return value;
        }

        private double ApplyFilter(string pointId, double value, DataPoint point)
        {
            var buffer = _filterBuffers.GetOrAdd(pointId, k => new List<double>());
            lock (buffer)
            {
                buffer.Add(value);
                while (buffer.Count > Math.Max(point.FilterWindow, 2))
                    buffer.RemoveAt(0);

                if (buffer.Count < 2) return value;

                switch (point.FilterMode)
                {
                    case 0: // 滑动平均
                        return buffer.Skip(buffer.Count - point.FilterWindow).Average();
                    case 1: // 中值滤波
                        var window = buffer.Skip(buffer.Count - point.FilterWindow).OrderBy(v => v).ToList();
                        int mid = window.Count / 2;
                        return window.Count % 2 == 0 ? (window[mid - 1] + window[mid]) / 2.0 : window[mid];
                    case 2: // 指数平滑
                        double lastFiltered = _lastValues.TryGetValue(pointId, out double lv) ? lv : buffer[buffer.Count - 2];
                        return point.FilterAlpha * value + (1 - point.FilterAlpha) * lastFiltered;
                    default:
                        return value;
                }
            }
        }

        private static double ApplyRounding(double value, DataPoint point)
        {
            double multiplier = Math.Pow(10, point.RoundingDecimals);
            switch (point.RoundingMode)
            {
                case 0: return Math.Round(value, point.RoundingDecimals);
                case 1: return Math.Floor(value * multiplier) / multiplier;
                case 2: return Math.Ceiling(value * multiplier) / multiplier;
                case 3: return Math.Truncate(value * multiplier) / multiplier;
                default: return value;
            }
        }

        private double ApplyCleaning(string pointId, double value, DataPoint point)
        {
            // ── 0. 空值过滤（最先执行）──
            if (point.NanFilterEnabled)
            {
                bool isInvalid = false;
                if (point.NanFilterNaN && double.IsNaN(value)) isInvalid = true;
                if (point.NanFilterInf && double.IsInfinity(value)) isInvalid = true;
                if (point.NanFilterNegative && value < 0) isInvalid = true;
                if (isInvalid)
                {
                    double replacement = _lastValues.TryGetValue(pointId, out double lv) ? lv : point.NanFilterReplacement;
                    Logger.Debug(string.Format("[Clean] NaN/Inf/Neg filtered: {0} value={1} → {2}", point.Name, value, replacement));
                    return replacement;
                }
            }

            // ── 1. 死区抑制 ──
            if (point.DeadBandEnabled)
            {
                double lastVal;
                if (_lastValues.TryGetValue(pointId, out lastVal) && Math.Abs(value - lastVal) < point.DeadBand)
                    return lastVal;
            }

            // ── 2. 尖峰/跳变抑制（在限幅之前，因为尖峰可能在正常范围内）──
            if (point.SpikeEnabled)
            {
                var spikeBuf = _filterBuffers.GetOrAdd(pointId + "_spike", k => new List<double>());
                lock (spikeBuf)
                {
                    if (spikeBuf.Count >= point.SpikeWindow)
                    {
                        double median = spikeBuf.OrderBy(v => v).ElementAt(spikeBuf.Count / 2);
                        double mad = spikeBuf.Select(v => Math.Abs(v - median)).OrderBy(v => v).ElementAt(spikeBuf.Count / 2);
                        if (mad > 0 && Math.Abs(value - median) > point.SpikeThreshold * mad)
                        {
                            Logger.Info(string.Format("[Clean] Spike detected: {0} value={1:F3} replaced with median={2:F3}", point.Name, value, median));
                            value = median;
                        }
                    }
                    spikeBuf.Add(value);
                    while (spikeBuf.Count > point.SpikeWindow * 2) spikeBuf.RemoveAt(0);
                }
            }

            // ── 3. 变化率限制 ──
            if (point.RocLimitEnabled)
            {
                double lastVal;
                if (_lastValues.TryGetValue(pointId, out lastVal))
                {
                    double delta = value - lastVal;
                    if (Math.Abs(delta) > point.RocLimitMax)
                    {
                        double clamped = lastVal + Math.Sign(delta) * point.RocLimitMax;
                        Logger.Debug(string.Format("[Clean] ROC limit: {0} {1:F3}→{2:F3} (Δ={3:F3}>{4:F3})", point.Name, lastVal, clamped, delta, point.RocLimitMax));
                        value = clamped;
                    }
                }
            }

            // ── 4. 限幅 ──
            if (point.ClipEnabled)
            {
                if (value < point.ClipMin) value = point.ClipMin;
                if (value > point.ClipMax) value = point.ClipMax;
            }

            // ── 5. 异常值剔除（3σ）──
            if (point.OutlierEnabled)
            {
                var buffer = _filterBuffers.GetOrAdd(pointId, k => new List<double>());
                lock (buffer)
                {
                    if (buffer.Count >= 5)
                    {
                        double mean = buffer.Average();
                        double std = Math.Sqrt(buffer.Average(v => Math.Pow(v - mean, 2)));
                        if (std > 0 && Math.Abs(value - mean) > point.SigmaThreshold * std)
                        {
                            Logger.Debug(string.Format("[Edge] Outlier detected: {0} value={1:F3}, mean={2:F3}, sigma={3:F3}", point.Name, value, mean, std));
                            return _lastValues.TryGetValue(pointId, out double lastVal) ? lastVal : mean;
                        }
                    }
                }
            }

            // ── 6. 冻结值检测（仅记录，不修改值）──
            if (point.FreezeEnabled)
            {
                double lastVal;
                int freezeCount = _freezeCounters.GetOrAdd(pointId, 0);
                if (_lastValues.TryGetValue(pointId, out lastVal) && Math.Abs(value - lastVal) < 1e-9)
                {
                    freezeCount++;
                    _freezeCounters[pointId] = freezeCount;
                    if (freezeCount == point.FreezeWindow)
                        Logger.Info(string.Format("[Clean] Freeze detected: {0} unchanged for {1} cycles", point.Name, freezeCount));
                }
                else
                {
                    if (freezeCount >= point.FreezeWindow)
                        Logger.Info(string.Format("[Clean] Freeze recovered: {0}", point.Name));
                    _freezeCounters[pointId] = 0;
                }
            }

            // ── 7. IQR 四分位距检测（仅记录，不修改值）──
            if (point.IqrEnabled)
            {
                var iqrBuf = _filterBuffers.GetOrAdd(pointId + "_iqr", k => new List<double>());
                lock (iqrBuf)
                {
                    if (iqrBuf.Count >= 10)
                    {
                        var sorted = iqrBuf.OrderBy(v => v).ToList();
                        double q1 = sorted[sorted.Count / 4];
                        double q3 = sorted[sorted.Count * 3 / 4];
                        double iqr = q3 - q1;
                        double lower = q1 - point.IqrMultiplier * iqr;
                        double upper = q3 + point.IqrMultiplier * iqr;
                        if (value < lower || value > upper)
                            Logger.Info(string.Format("[Clean] IQR outlier: {0} value={1:F3}, bounds=[{2:F3}, {3:F3}]", point.Name, value, lower, upper));
                    }
                    iqrBuf.Add(value);
                    while (iqrBuf.Count > 50) iqrBuf.RemoveAt(0);
                }
            }

            // ── 8. 量程合理性标记（仅记录，不修改值）──
            if (point.RangeEnabled)
            {
                if (value < point.RangeMin || value > point.RangeMax)
                {
                    bool alreadyReported = _rangeFired.ContainsKey(pointId);
                    if (!alreadyReported)
                    {
                        Logger.Info(string.Format("[Clean] Range violation: {0} value={1:F3}, range=[{2:F3}, {3:F3}]", point.Name, value, point.RangeMin, point.RangeMax));
                        _rangeFired[pointId] = true;
                    }
                }
                else
                {
                    _rangeFired.TryRemove(pointId, out _);
                }
            }

            return value;
        }

        private void CheckAlarms(string pointId, double value, DataPoint point)
        {
            string alarmLevel = null;

            if (point.AlarmHH_Enabled && value >= point.AlarmHH) alarmLevel = "HH";
            else if (point.AlarmH_Enabled && value >= point.AlarmH) alarmLevel = "H";
            else if (point.AlarmL_Enabled && value <= point.AlarmL) alarmLevel = "L";
            else if (point.AlarmLL_Enabled && value <= point.AlarmLL) alarmLevel = "LL";

            if (alarmLevel != null)
            {
                // 累计计数（同类报警重复触发时也累加，看板显示累加次数）
                _alarmCounters.AddOrUpdate(pointId, 1, (k, v) => v + 1);

                // 检查是否已在报警状态（同一级别不再重复触发）
                string currentAlarm;
                if (_alarmFired.TryGetValue(pointId, out currentAlarm) && currentAlarm == alarmLevel)
                    return; // 同一报警级别，避免日志洪水

                // 报警延迟：需要连续 N+1 个周期都在报警状态
                int counter;
                _alarmCounters.TryGetValue(pointId, out counter);
                if (counter >= point.AlarmDelay + 1)
                {
                    // 解析 deviceId 查设备名
                string alarmDeviceName = point.Name;
                try
                {
                    int sepIdx2 = pointId.IndexOf('_');
                    if (sepIdx2 > 0)
                    {
                        string alarmDevId = pointId.Substring(0, sepIdx2);
                        if (_deviceNames.TryGetValue(alarmDevId, out var dn) && !string.IsNullOrEmpty(dn))
                            alarmDeviceName = $"[{dn}] {point.Name}";
                    }
                }
                catch { }
                Logger.Info(string.Format("[ALARM {0}] {1} = {2:F3} {3}", alarmLevel, alarmDeviceName, value, point.Unit));
                    _alarmFired[pointId] = alarmLevel;

                    // 语义层事件: 报警 → 触发事件处理管线
                    try
                    {
                        int sepIdx = pointId.IndexOf('_');
                        if (sepIdx > 0)
                        {
                            string devId = pointId.Substring(0, sepIdx);
                            var node = SemanticService.Instance.GetNodeBySource("device", devId);
                            if (node != null)
                            {
                                var evt = new SemanticVariableEvent
                                {
                                    NodeId = node.Id,
                                    EventType = "报警",
                                    ProcessingMethod = "报警",
                                    Description = string.Format("变量 {0} 触发 {1} 报警: {2:F3} {3}", point.Name, alarmLevel, value, point.Unit)
                                };
                                SemanticService.Instance.SaveEvent(evt);
                                EventProcessingService.Instance.Process(evt);
                            }
                        }
                    }
                    catch { }
                }
            }
            else
            {
                // 恢复正常
                _alarmCounters[pointId] = 0;
                string old;
                if (_alarmFired.TryRemove(pointId, out old))
                {
                    // 解析 deviceId 查设备名
                string alarmOkDeviceName = point.Name;
                try
                {
                    int sepIdx3 = pointId.IndexOf('_');
                    if (sepIdx3 > 0)
                    {
                        string alarmOkDevId = pointId.Substring(0, sepIdx3);
                        if (_deviceNames.TryGetValue(alarmOkDevId, out var dn) && !string.IsNullOrEmpty(dn))
                            alarmOkDeviceName = $"[{dn}] {point.Name}";
                    }
                }
                catch { }
                Logger.Info(string.Format("[ALARM OK] {0} 报警恢复", alarmOkDeviceName));

                    // 语义层事件: 恢复
                    try
                    {
                        int sepIdx = pointId.IndexOf('_');
                        if (sepIdx > 0)
                        {
                            string devId = pointId.Substring(0, sepIdx);
                            var node = SemanticService.Instance.GetNodeBySource("device", devId);
                            if (node != null)
                            {
                                var evt = new SemanticVariableEvent
                                {
                                    NodeId = node.Id,
                                    EventType = "恢复",
                                    ProcessingMethod = "仅记录",
                                    Description = string.Format("变量 {0} 从 {1} 报警恢复", point.Name, old)
                                };
                                SemanticService.Instance.SaveEvent(evt);
                                EventProcessingService.Instance.Process(evt);
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 简单的数学表达式求值（支持 x, +, -, *, /, (, ), Math 函数）
        /// </summary>
        public static double EvaluateExpression(string expression, double xValue)
        {
            if (string.IsNullOrWhiteSpace(expression)) return xValue;

            string expr = expression.Replace(" ", "");
            // 替换 x 为实际值
            expr = expr.Replace("x", xValue.ToString("R"));

            // 使用 DataTable.Compute 做表达式求值
            try
            {
                var dt = new System.Data.DataTable();
                var result = dt.Compute(expr, null);
                return Convert.ToDouble(result);
            }
            catch
            {
                return xValue;
            }
        }

        /// <summary>
        /// 获取报警状态
        /// </summary>
        public string GetAlarmStatus(string pointId)
        {
            string level;
            if (_alarmFired.TryGetValue(pointId, out level) && level != null)
                return level;
            return "OK";
        }

        /// <summary>
        /// 获取当前所有活跃的变量报警快照，供看板等 UI 使用
        /// </summary>
        public List<AlarmSnapshot> GetActiveAlarmsSnapshot()
        {
            var result = new List<AlarmSnapshot>();
            foreach (var kv in _alarmFired)
            {
                string pointId = kv.Key;        // format: deviceId_variableName
                string level = kv.Value;
                _pointCache.TryGetValue(pointId, out var point);
                string varName = point?.Name ?? pointId;
                string unit = point?.Unit ?? "";
                int counter = 0;
                _alarmCounters.TryGetValue(pointId, out counter);
                // Extract device id from pointId (format: deviceId_variableName)
                int sep = pointId.IndexOf('_');
                string deviceId = sep > 0 ? pointId.Substring(0, sep) : pointId;
                // Look up device name from _deviceNames, fallback to deviceId
                string deviceName;
                if (!_deviceNames.TryGetValue(deviceId, out deviceName))
                    deviceName = deviceId;
                result.Add(new AlarmSnapshot
                {
                    DeviceId = deviceId,
                    Device = deviceName,
                    VariableName = varName,
                    Level = level,
                    Unit = unit,
                    Counter = Math.Max(1, counter),
                    PointId = pointId
                });
            }
            return result;
        }

        // ======================== 设备健康状态 ========================
        
        /// <summary>
        /// 单一真实来源：基于最后数据时间戳判定设备在线/离线。
        /// 上线 = 任意变量在 2×采集周期内收到过数据。
        /// </summary>
        public DeviceHealth GetDeviceHealth(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return DeviceHealth.Unknown;
            
            var vars = _latestCache.Values
                .Where(d => d.DeviceId == deviceId)
                .ToList();
            if (vars.Count == 0) return DeviceHealth.Unknown;

            var latestTs = vars.Max(v => v.Timestamp);
            // 默认 2 秒超时（覆盖绝大多数采集周期），最少 3 秒防瞬断假离线
            int timeoutMs = 3000;
            return (DateTime.Now - latestTs).TotalMilliseconds < timeoutMs
                ? DeviceHealth.Online
                : DeviceHealth.Offline;
        }

        /// <summary>
        /// 批量获取所有已注册设备的健康状态
        /// </summary>
        public Dictionary<string, DeviceHealth> GetAllDeviceHealth()
        {
            var result = new Dictionary<string, DeviceHealth>();
            var deviceIds = _latestCache.Values
                .Select(d => d.DeviceId)
                .Distinct()
                .ToList();
            foreach (var id in deviceIds)
                result[id] = GetDeviceHealth(id);
            // 也包含已注册但尚无数据的设备
            foreach (var id in _deviceNames.Keys)
                if (!result.ContainsKey(id))
                    result[id] = DeviceHealth.Unknown;
            return result;
        }
    }

    /// <summary>
    /// 设备健康状态 — 数据驱动的单一真实来源
    /// </summary>
    public enum DeviceHealth
    {
        Online,
        Offline,
        Unknown
    }

    /// <summary>
    /// 活跃报警快照，供 UI 层使用
    /// </summary>
    public class AlarmSnapshot
    {
        public string DeviceId { get; set; }
        public string Device { get; set; }
        public string VariableName { get; set; }
        public string Level { get; set; }
        public string Unit { get; set; }
        public int Counter { get; set; }
        public string PointId { get; set; }
    }
}
