using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// OPC DA 采集驱动（COM / 自动化接口）。
    ///
    /// v3.22.44 重写要点（修复「设备可连但变量采集值全是 0」）：
    ///   1) ConnectAsync 由「模拟连接」改为**真实 COM 连接**：解析 ProgID → 创建服务器对象 → Connect → 建组。
    ///      连不上就如实报错（含 HRESULT 与处置提示），不再无条件把 IsConnected 置 true 骗上层。
    ///   2) 服务器 / 组 / 项**连接一次后缓存复用**，不再每读一个点就重建整套 COM 对象（旧实现每点重建，慢且易失败）。
    ///   3) 读取失败**不再静默变成 0**：抛错 → 采集循环记账 → 写 WARN 日志 → 上报心跳离线并带失败原因；
    ///      读失败的点位本周期不产数据（宁可缺数，也不写假 0 污染库与看板）。
    ///   4) 启动自检：点位「地址」为空或仍是占位值 "0"/"1" 时给出明确告警——这是实测最常见的「全是 0」原因。
    ///   5) 类型转换失败给出人话原因（不支持的 CLR 类型 / 空值 VT_EMPTY / 字符串非数字），不再一律返回 0。
    ///   6) 质量码按 OPC 规范判定（Good 0xC0 / Uncertain 0x40 / Bad 0x00），Bad 视为无效并给出子状态解释。
    /// </summary>
    public class OPCDADriver : IDriver
    {
        private const int OpcDsCache = 1;          // OPC_DS_CACHE
        private const int OpcDsDevice = 2;         // OPC_DS_DEVICE
        private const int QualityGood = 0xC0;      // 质量 Good
        private const int QualityUncertain = 0x40; // 质量 Uncertain
        private const string DefaultProgId = "OPC.SimaticNET.1";
        private const string GroupName = "NeoIndGrp";
        private const int FailLogIntervalSec = 15; // 全失败告警节流（秒）

        // ── 配置 ──
        private DeviceConfig _config;
        private string _progId = DefaultProgId;
        private string _nodeName = "";
        private int _readSource = OpcDsCache;
        private int _updateRate = 500;
        private bool _simulate;

        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _comLock = new object();
        private DateTime _startTime = DateTime.Now;

        // ── COM 长连接（连接一次，反复读取）──
        private object _server;
        private object _groups;
        private object _group;
        private object _items;
        private readonly Dictionary<string, object> _itemCache = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _handleOf = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _nextHandle = 1;
        private string _connectPath = "";   // 实际生效的连接路径（服务器自身 ProgID，或回退到自动化包装器）

        // ── 运行态统计 ──
        private int _failStreak;            // 连续「全失败」周期数
        private int _comBroken;             // COM 层失效标志（1 = 需要重建连接）
        private DateTime _lastFailLog = DateTime.MinValue;
        private DateTime _lastQualityLog = DateTime.MinValue;

        public string DriverType => "OPCDA";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        // ══════════════════════════════ 连接 ══════════════════════════════

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            if (config != null) _config = config;
            if (_config == null)
            {
                IsConnected = false;
                return false;
            }

            _progId = (_config.GetParam("ProgID", DefaultProgId) ?? "").Trim();
            if (_progId.Length == 0) _progId = DefaultProgId;
            _nodeName = (_config.GetParam("OpcNode", "") ?? "").Trim();
            _readSource = string.Equals(_config.GetParam("ReadSource", "cache"), "device", StringComparison.OrdinalIgnoreCase)
                ? OpcDsDevice : OpcDsCache;
            _updateRate = _config.GetIntParam("UpdateRate", 500);
            if (_updateRate < 100) _updateRate = 100;
            _simulate = string.Equals(_config.GetParam("Simulate", "false"), "true", StringComparison.OrdinalIgnoreCase);

            if (_simulate)
            {
                IsConnected = true;
                NotifyStatus(true, "OPC DA 已连接（模拟数据模式）");
                Logger.Warn(string.Format("[OPC DA] 【模拟数据模式】{0}：采集值为程序生成、并非真实设备数据（参数 Simulate=true）。仅用于验证入库/展示链路，请勿用于生产。", _config.Name));
                return true;
            }

            try
            {
                await Task.Run(() => ConnectCore());

                IsConnected = true;
                NotifyStatus(true, string.Format("OPC DA 已连接 (ProgID={0}{1})", _progId,
                    _nodeName.Length == 0 ? "" : ", Node=" + _nodeName));
                Logger.Info(string.Format("[OPC DA] 连接成功: {0} → {1}（启用点位 {2} 个，更新周期 {3}ms）",
                    _config.Name, _connectPath, CountActivePoints(), _updateRate));
                LogStartupDiagnostics();
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                string reason = Describe(ex);
                NotifyStatus(false, "连接失败: " + Truncate(reason, 200));
                Logger.Warn(string.Format("[OPC DA] 连接失败 [{0}] ProgID={1}: {2}", _config.Name, _progId, reason));
                CleanupCom();
                return false;
            }
        }

        /// <summary>真实建立 COM 连接：解析 ProgID → 创建服务器 → Connect → 建组（全部走锁内，避免并发 COM 访问）</summary>
        private void ConnectCore()
        {
            lock (_comLock)
            {
                CleanupComCore();

                Type serverType = Type.GetTypeFromProgID(_progId);
                if (serverType == null)
                    throw new InvalidOperationException(string.Format(
                        "找不到 ProgID「{0}」：该 OPC 服务器未在本机注册。请确认采集机上已安装 OPC 服务器软件，且客户端位数与其一致（32 位 OPC 服务器需 32 位客户端）。",
                        _progId));

                object server = Activator.CreateInstance(serverType);
                string path = _progId;

                // 若服务器自身未暴露 OPC 自动化接口，回退到标准自动化包装器（OPCDAAuto.dll）
                if (!HasMember(server, "OPCGroups"))
                {
                    object wrapper = TryCreateWrapper();
                    if (wrapper != null)
                    {
                        try { Marshal.ReleaseComObject(server); } catch { }
                        server = wrapper;
                        path = "OPC.Automation.1 → " + _progId;
                    }
                }

                if (!HasMember(server, "OPCGroups"))
                    throw new InvalidOperationException(string.Format(
                        "对象「{0}」未提供 OPC DA 自动化接口（缺少 OPCGroups 成员）。请确认 ProgID 指向 OPC DA 服务器，或已安装 OPC 自动化包装器 OPCDAAuto.dll。",
                        _progId));

                // Connect
                try
                {
                    object[] connectArgs = _nodeName.Length == 0
                        ? new object[] { _progId }
                        : new object[] { _progId, _nodeName };
                    server.GetType().InvokeMember("Connect", BindingFlags.InvokeMethod, null, server, connectArgs);
                }
                catch (TargetInvocationException tie)
                {
                    Exception inner = tie.InnerException ?? tie;
                    try
                    {
                        // 兼容只接受 ProgID 单参的包装器
                        server.GetType().InvokeMember("Connect", BindingFlags.InvokeMethod, null, server, new object[] { _progId });
                    }
                    catch { throw inner; }
                }

                object groups = server.GetType().InvokeMember("OPCGroups", BindingFlags.GetProperty, null, server, null);
                if (groups == null)
                    throw new InvalidOperationException("OPC 服务器返回了空的 OPCGroups 集合，无法创建数据组。");

                object group = null;
                try
                {
                    try
                    {
                        // 清理同名残留组，避免重复连接时叠加
                        groups.GetType().InvokeMember("Remove", BindingFlags.InvokeMethod, null, groups, new object[] { GroupName });
                    }
                    catch { }
                    group = groups.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, groups, new object[] { GroupName });
                }
                catch (TargetInvocationException tie)
                {
                    throw (tie.InnerException ?? tie);
                }
                if (group == null)
                    throw new InvalidOperationException("OPC 服务器拒绝创建数据组（OPCGroups.Add 返回空）。");

                Type gt = group.GetType();
                TrySetProperty(gt, group, "UpdateRate", _updateRate);
                TrySetProperty(gt, group, "IsActive", true);
                TrySetProperty(gt, group, "IsSubscribed", false);
                TrySetProperty(gt, group, "Deadband", 0f);

                object items = gt.InvokeMember("OPCItems", BindingFlags.GetProperty, null, group, null);
                if (items == null)
                    throw new InvalidOperationException("OPC 数据组未提供 OPCItems 集合，无法添加点位。");

                _server = server;
                _groups = groups;
                _group = group;
                _items = items;
                _connectPath = path;
                _itemCache.Clear();
                _handleOf.Clear();
                _nextHandle = 1;
                Interlocked.Exchange(ref _comBroken, 0);
            }
        }

        /// <summary>尝试创建标准 OPC 自动化包装器（OPCDAAuto.dll）</summary>
        private static object TryCreateWrapper()
        {
            try
            {
                Type t = Type.GetTypeFromProgID("OPC.Automation.1");
                if (t == null) return null;
                object o = Activator.CreateInstance(t);
                return HasMember(o, "OPCGroups") ? o : null;
            }
            catch { return null; }
        }

        public Task DisconnectAsync()
        {
            CleanupCom();
            IsConnected = false;
            NotifyStatus(false, "已断开");
            return Task.CompletedTask;
        }

        private void CleanupCom()
        {
            lock (_comLock) { CleanupComCore(); }
        }

        private void CleanupComCore()
        {
            _itemCache.Clear();
            _handleOf.Clear();

            if (_group != null)
            {
                try
                {
                    _group.GetType().InvokeMember("Remove", BindingFlags.InvokeMethod, null, _group, new object[] { GroupName });
                }
                catch { }
            }
            if (_groups != null)
            {
                try
                {
                    _groups.GetType().InvokeMember("RemoveAll", BindingFlags.InvokeMethod, null, _groups, null);
                }
                catch { }
            }
            if (_server != null)
            {
                try
                {
                    _server.GetType().InvokeMember("Disconnect", BindingFlags.InvokeMethod, null, _server, null);
                }
                catch { }
            }

            _items = null;
            _group = null;
            _groups = null;
            Release(ref _server);
        }

        private static void Release(ref object com)
        {
            object o = com;
            com = null;
            if (o != null)
            {
                try { Marshal.ReleaseComObject(o); } catch { }
            }
        }

        // ══════════════════════════════ 采集 ══════════════════════════════

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null) return;

            int pollInterval = _config.GetIntParam("PollInterval", 1000);
            if (pollInterval < 100) pollInterval = 100;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _startTime = DateTime.Now;
            Logger.Info(string.Format("[OPC DA] 采集线程启动: {0}（ProgID={1}，读数源={2}，周期={3}ms）",
                _config.Name, _progId, _readSource == OpcDsDevice ? "device" : "cache", pollInterval));

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    if (!IsConnected)
                    {
                        await ConnectAsync(_config);
                        if (!IsConnected)
                        {
                            await Task.Delay(5000, _cts.Token);
                            continue;
                        }
                    }

                    var points = _config.DataPoints;
                    if (points == null || points.Count == 0)
                    {
                        await Task.Delay(Math.Max(pollInterval, 2000), _cts.Token);
                        continue;
                    }

                    var cycleItems = new List<CycleDataItem>();
                    int ok = 0, fail = 0;
                    string firstFail = null;

                    foreach (var point in points)
                    {
                        _cts.Token.ThrowIfCancellationRequested();
                        if (!point.IsActive) continue;

                        double v;
                        try
                        {
                            v = Convert.ToDouble(ReadSync(point), CultureInfo.InvariantCulture);
                        }
                        catch (Exception rex)
                        {
                            // v3.22.44：读失败不再伪造 0，本周期跳过该点位并记账
                            fail++;
                            if (firstFail == null) firstFail = rex.Message;
                            continue;
                        }

                        ok++;
                        var data = new CollectedData
                        {
                            DeviceId = _config.Id,
                            DeviceName = _config.Name,
                            VariableName = point.Name,
                            DataType = point.DataType,
                            SourceDriverType = "OPCDA",
                            Value = v.ToString("F6", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.'),
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
                            Value = v,
                            Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null
                        });
                    }

                    if (ok == 0 && fail > 0)
                    {
                        HandleAllPointsFailed(fail, firstFail);
                    }
                    else
                    {
                        if (fail > 0) HandlePartialFailure(ok, fail, firstFail);
                        else MarkHealthy();

                        OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch
                        {
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            Driver = "opcda",
                            Device = _config.Name,
                            DeviceId = _config.Id,
                            Values = cycleItems
                        }));
                    }

                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.Warn(string.Format("[OPC DA] 采集周期异常 [{0}]: {1}", _config.Name, Describe(ex)));
                    await Task.Delay(1000, _cts.Token);
                }
            }

            Logger.Info(string.Format("[OPC DA] 采集线程结束: {0}", _config.Name));
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            return Task.Run(() => ReadSync(point));
        }

        /// <summary>读取单个点位。失败一律抛异常（含原因与处置建议），绝不静默返回 0。</summary>
        private object ReadSync(DataPoint point)
        {
            if (_simulate) return SimulateValue(point);

            string itemId = (point.Address ?? "").Trim();
            if (itemId.Length == 0)
                throw new InvalidOperationException("点位地址（OPC ItemID）为空：请在点位的「点位地址」列填写 OPC 服务器中的真实 ItemID");

            lock (_comLock)
            {
                if (!IsConnected || _items == null || _group == null)
                    throw new InvalidOperationException("OPC DA 尚未连接");

                object item = GetOrAddItem(itemId);

                object[] readArgs = new object[] { _readSource, null, null, null };
                ParameterModifier[] mods = new ParameterModifier[]
                {
                    new ParameterModifier(4) { [1] = true, [2] = true, [3] = true }
                };

                try
                {
                    item.GetType().InvokeMember("Read", BindingFlags.InvokeMethod, null, item, readArgs, mods, null, null);
                }
                catch (TargetInvocationException tie)
                {
                    Exception inner = tie.InnerException ?? tie;
                    _itemCache.Remove(itemId);           // 句柄可能已失效，下个周期重建
                    _handleOf.Remove(itemId);
                    if (inner is COMException) MarkComBrokenIfFatal((COMException)inner);
                    throw new InvalidOperationException(string.Format("读取失败（ItemID=「{0}」）：{1}{2}",
                        itemId, inner.Message, MemberHint(inner, item)), inner);
                }
                catch (COMException com)
                {
                    _itemCache.Remove(itemId);
                    _handleOf.Remove(itemId);
                    MarkComBrokenIfFatal(com);
                    throw new InvalidOperationException(string.Format("读取失败（ItemID=「{0}」）：{1} [HRESULT=0x{2:X8}]",
                        itemId, com.Message, com.ErrorCode), com);
                }

                object raw = readArgs[1];
                int quality = QualityGood;
                if (readArgs[2] != null)
                {
                    try { quality = Convert.ToInt32(readArgs[2], CultureInfo.InvariantCulture); }
                    catch { quality = 0; }
                }

                if (IsBadQuality(quality))
                    throw new InvalidOperationException(string.Format(
                        "OPC 质量为「坏」（ItemID=「{0}」，quality=0x{1:X2}）{2}", itemId, quality, QualityHint(quality)));

                if (IsUncertainQuality(quality) && (DateTime.Now - _lastQualityLog).TotalSeconds > 10)
                {
                    _lastQualityLog = DateTime.Now;
                    Logger.Warn(string.Format("[OPC DA] 质量为「不确定」[{0}] ItemID={1} quality=0x{2:X2}（数值仍按有效处理）",
                        _config.Name, itemId, quality));
                }

                double v;
                string why;
                if (!TryConvertToDouble(raw, out v, out why))
                    throw new InvalidOperationException(string.Format(
                        "点位值不可用（ItemID=「{0}」，quality=0x{1:X2}）：{2}", itemId, quality, why));

                return v;
            }
        }

        /// <summary>取（或首次添加）点位项，并按 ItemID 缓存复用；AddItem 失败给出人话原因</summary>
        private object GetOrAddItem(string itemId)
        {
            object cached;
            if (_itemCache.TryGetValue(itemId, out cached) && cached != null) return cached;

            object item;
            try
            {
                int handle = _nextHandle++;
                item = _items.GetType().InvokeMember("AddItem", BindingFlags.InvokeMethod, null, _items,
                    new object[] { itemId, handle });
                _handleOf[itemId] = handle;
            }
            catch (TargetInvocationException tie)
            {
                Exception inner = tie.InnerException ?? tie;
                if (inner is COMException) MarkComBrokenIfFatal((COMException)inner);
                throw new InvalidOperationException(string.Format(
                    "OPC 服务器拒绝该点位地址（ItemID=「{0}」）：{1}{2}", itemId, inner.Message, ItemIdHint(itemId)), inner);
            }

            if (item == null)
                throw new InvalidOperationException(string.Format(
                    "OPC 服务器未能返回点位项对象（ItemID=「{0}」）{1}", itemId, ItemIdHint(itemId)));

            _itemCache[itemId] = item;
            return item;
        }

        // ══════════════════════════════ 诊断 ══════════════════════════════

        /// <summary>连接成功后的配置自检：没有启用点位 / 地址是空或占位值 —— 这两类问题会让采集「连上了却没有数据」</summary>
        private void LogStartupDiagnostics()
        {
            var points = _config.DataPoints ?? new List<DataPoint>();
            var actives = points.Where(p => p.IsActive).ToList();

            if (actives.Count == 0)
            {
                Logger.Warn(string.Format(
                    "[OPC DA] 【配置告警】{0}：没有任何启用中的点位 → 采集不会产生任何数据。请先在点位列表里添加变量并勾选启用。",
                    _config.Name));
                return;
            }

            int badCount = 0;
            var samples = new List<string>();
            foreach (var p in actives)
            {
                string a = (p.Address ?? "").Trim();
                if (a.Length == 0 || a == "0" || a == "1")
                {
                    badCount++;
                    if (samples.Count < 5) samples.Add(string.Format("{0}(地址=\"{1}\")", p.Name, a));
                }
            }

            if (badCount > 0)
            {
                Logger.Warn(string.Format(
                    "[OPC DA] 【配置告警】{0}：{1}/{2} 个启用点位的「点位地址」为空或仍是占位值 {3} —— 这些点位在 OPC 服务器上并不存在，采集必然失败（旧版本会把它静默写成 0）。请在点位列表里填写真实 ItemID，例如 S7:[S7 connection_1]DB100,REAL0。",
                    _config.Name, badCount, actives.Count, string.Join("、", samples.ToArray())));
            }
            else
            {
                Logger.Info(string.Format("[OPC DA] 点位地址自检通过：{0} 个启用点位均已填写 ItemID", actives.Count));
            }
        }

        /// <summary>整周期全部失败：写日志（节流）+ 上报离线并带原因 + 必要时重建 COM 连接</summary>
        private void HandleAllPointsFailed(int failCount, string reason)
        {
            _failStreak++;

            if ((DateTime.Now - _lastFailLog).TotalSeconds >= FailLogIntervalSec)
            {
                _lastFailLog = DateTime.Now;
                Logger.Warn(string.Format(
                    "[OPC DA] 【采集失败】{0}：本周期 {1} 个点位全部读取失败，未写入任何数据（不再伪造 0）。首个错误：{2}",
                    _config.Name, failCount, reason));
            }

            if (_failStreak == 1)
                NotifyStatus(false, "采集失败: " + Truncate(reason, 180));

            // COM 层已被判定失效（RPC 断开等）→ 断开重连，让下个周期走真实重连分支
            if (Interlocked.CompareExchange(ref _comBroken, 0, 0) == 1)
            {
                Logger.Warn(string.Format("[OPC DA] 检测到 COM 连接失效 [{0}]，正在重建 OPC 服务器连接…", _config.Name));
                CleanupCom();
                IsConnected = false;
            }
        }

        private void HandlePartialFailure(int ok, int fail, string reason)
        {
            if ((DateTime.Now - _lastFailLog).TotalSeconds >= FailLogIntervalSec)
            {
                _lastFailLog = DateTime.Now;
                Logger.Warn(string.Format(
                    "[OPC DA] 部分点位读取失败 [{0}]：成功 {1} 个 / 失败 {2} 个（失败点位本轮不产数据）。首个错误：{3}",
                    _config.Name, ok, fail, reason));
            }
        }

        private void MarkHealthy()
        {
            if (_failStreak > 0)
            {
                Logger.Info(string.Format("[OPC DA] 采集已恢复正常: {0}（此前连续 {1} 个周期全部失败）", _config.Name, _failStreak));
                NotifyStatus(true, "采集已恢复正常");
            }
            _failStreak = 0;
        }

        private void MarkComBrokenIfFatal(COMException com)
        {
            uint hr = unchecked((uint)com.ErrorCode);
            // RPC 服务器不可用 / 连接中断 / 被调用方断开 / 服务器进程已退出
            if (hr == 0x800706BA || hr == 0x800706BE || hr == 0x80010108 || hr == 0x800401FD || hr == 0x800706BF)
                Interlocked.Exchange(ref _comBroken, 1);
        }

        private int CountActivePoints()
        {
            var pts = _config == null ? null : _config.DataPoints;
            return pts == null ? 0 : pts.Count(p => p.IsActive);
        }

        // ══════════════════════════════ 取值转换 ══════════════════════════════

        private double SimulateValue(DataPoint point)
        {
            double t = (DateTime.Now - _startTime).TotalSeconds;
            int seed = (point.Name ?? "").Length + (point.VariableId ?? "").Length;
            return Math.Round(50.0 + 20.0 * Math.Sin(t / 10.0 + seed), 3);
        }

        /// <summary>把服务器返回的原生值转成 double。转不了就返回 false 并给出人话原因（不再兜底成 0）</summary>
        private static bool TryConvertToDouble(object raw, out double value, out string reason)
        {
            value = 0.0;
            reason = null;

            if (raw == null || raw == DBNull.Value)
            {
                reason = "服务器返回空值（VT_EMPTY），该点位当前没有可读数据";
                return false;
            }

            if (raw is double) { value = (double)raw; return true; }
            if (raw is float) { value = (float)raw; return true; }
            if (raw is int) { value = (int)raw; return true; }
            if (raw is short) { value = (short)raw; return true; }
            if (raw is long) { value = (long)raw; return true; }
            if (raw is sbyte) { value = (sbyte)raw; return true; }
            if (raw is byte) { value = (byte)raw; return true; }
            if (raw is ushort) { value = (ushort)raw; return true; }
            if (raw is uint) { value = (uint)raw; return true; }
            if (raw is ulong) { value = (ulong)raw; return true; }
            if (raw is decimal) { value = (double)(decimal)raw; return true; }
            if (raw is bool) { value = (bool)raw ? 1.0 : 0.0; return true; }
            if (raw is char) { value = (char)raw; return true; }

            string s = raw as string;
            if (s != null)
            {
                s = s.Trim();
                if (s.Length == 0)
                {
                    reason = "服务器返回空字符串";
                    return false;
                }
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
                string lower = s.ToLowerInvariant();
                if (lower == "true") { value = 1.0; return true; }
                if (lower == "false") { value = 0.0; return true; }
                reason = string.Format("服务器返回字符串「{0}」，无法转成数值；请把该点位的数据类型改成与服务器一致（数值量亦可直接改为 string 类型）", Truncate(s, 40));
                return false;
            }

            Array arr = raw as Array;
            if (arr != null)
            {
                if (arr.Length == 0)
                {
                    reason = "服务器返回空数组";
                    return false;
                }
                string inner = null;
                if (!TryConvertToDouble(arr.GetValue(0), out value, out inner))
                {
                    reason = "数组元素无法转为数值：" + inner;
                    return false;
                }
                if (arr.Length > 1) reason = string.Format("（数组共 {0} 个元素，已取第 1 个）", arr.Length);
                return true;
            }

            reason = string.Format("点位值类型「{0}」暂不支持转换；请确认点位数据类型与服务器一致（支持整型/浮点/布尔/字符串/数组）",
                raw.GetType().Name);
            return false;
        }

        // ══════════════════════════════ 质量码与错误解释 ══════════════════════════════

        private static bool IsBadQuality(int q) { return (q & 0xC0) == 0x00; }
        private static bool IsUncertainQuality(int q) { return (q & 0xC0) == QualityUncertain; }
        private static bool IsGoodQuality(int q) { return (q & 0xC0) == QualityGood; }

        private static string QualityHint(int q)
        {
            switch (q & 0x3C)
            {
                case 0x00: return "（服务器未返回有效数据：常见原因是 ItemID 不存在、OPC 服务器与 PLC 之间的链路未建立，或通道未启动）";
                case 0x04: return "（配置错误：ItemID 或服务器参数配置不合法）";
                case 0x08: return "（未连接：OPC 服务器没有连上底层设备/PLC）";
                case 0x0C: return "（设备故障）";
                case 0x10: return "（传感器故障）";
                case 0x14: return "（最后已知值：服务器暂时取不到新值）";
                case 0x18: return "（通讯故障）";
                case 0x1C: return "（设备已停用）";
                default: return "";
            }
        }

        private static string ItemIdHint(string itemId)
        {
            if (itemId == "0" || itemId == "1")
                return "。当前地址是占位默认值，请在设备的点位列表里把「点位地址」填成 OPC 服务器中的真实 ItemID（例如 S7:[S7 connection_1]DB100,REAL0）";
            return "。请用 OPC 客户端工具（如 Matrikon OPC Explorer、KEPServerEX Quick Client）核对这个 ItemID 是否存在、拼写是否一致";
        }

        /// <summary>反射调用失败时，如果怀疑是接口形状不匹配，提示可用的成员名，便于现场定位</summary>
        private static string MemberHint(Exception inner, object target)
        {
            if (inner == null || target == null) return "";
            string msg = inner.Message ?? "";
            if (msg.IndexOf("member", StringComparison.OrdinalIgnoreCase) < 0
                && msg.IndexOf("参数", StringComparison.Ordinal) < 0
                && msg.IndexOf("parameter", StringComparison.OrdinalIgnoreCase) < 0)
                return "";
            try
            {
                var names = target.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.MemberType == MemberTypes.Method || m.MemberType == MemberTypes.Property)
                    .Select(m => m.Name).Distinct().Take(25).ToArray();
                return "。该 COM 对象可用成员：" + string.Join(",", names);
            }
            catch { return ""; }
        }

        /// <summary>把反射/COM 异常翻译成人能直接照做的原因</summary>
        private static string Describe(Exception ex)
        {
            Exception e = ex;
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;

            string msg = e.Message ?? e.GetType().Name;

            COMException com = e as COMException;
            if (com != null)
            {
                uint hr = unchecked((uint)com.ErrorCode);
                if (hr == 0x800401F3) msg += "（ProgID 未注册：该 OPC 服务器未安装在本机，或客户端与其位数不一致——32 位 OPC 服务器需 32 位客户端）";
                else if (hr == 0x800706BA) msg += "（RPC 服务器不可用：DCOM 远程调用被拒，请检查节点名拼写、DCOM 配置与防火墙）";
                else if (hr == 0x80070005) msg += "（拒绝访问：DCOM 权限不足，请给运行账户授予 DCOM 启动/访问权限）";
                else if (hr == 0x80040154) msg += "（类未注册：OPC 服务器未正确安装或注册表被清理）";
                msg += string.Format(" [HRESULT=0x{0:X8}]", hr);
            }
            return msg;
        }

        // ══════════════════════════════ 小工具 ══════════════════════════════

        private static bool HasMember(object obj, string name)
        {
            if (obj == null) return false;
            try { return obj.GetType().GetMember(name).Length > 0; }
            catch { return false; }
        }

        private static void TrySetProperty(Type t, object target, string name, object value)
        {
            try { t.InvokeMember(name, BindingFlags.SetProperty, null, target, new object[] { value }); }
            catch { /* 部分服务器不支持该属性，忽略 */ }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        private void NotifyStatus(bool connected, string message)
        {
            OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(_config?.Id ?? "", _config?.Name ?? "", connected, message));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_cts != null) { _cts.Cancel(); _cts.Dispose(); _cts = null; }
            }
            catch { }
            try { DisconnectAsync().Wait(2000); } catch { }
        }
    }
}
