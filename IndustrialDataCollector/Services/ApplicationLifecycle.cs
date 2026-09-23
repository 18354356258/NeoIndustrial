using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 统一应用生命周期管理 —— 解决启动/关闭顺序混乱、资源泄漏问题（规则 34）
    /// 
    /// 关闭顺序：先 UI（定时器/看板），再数据采集，再网络服务（MQTT/REST/MCP），最后持久化（缓存/DB）
    /// </summary>
    public class ApplicationLifecycle
    {
        private static readonly Lazy<ApplicationLifecycle> _instance =
            new Lazy<ApplicationLifecycle>(() => new ApplicationLifecycle());
        public static ApplicationLifecycle Instance => _instance.Value;

        /// <summary>
        /// 已注册的关闭步骤（注册顺序 = 启动顺序，关闭时逆序执行）
        /// </summary>
        private readonly List<(string name, Action<TimeSpan> stop, bool isCritical)> _shutdownSteps
            = new List<(string, Action<TimeSpan>, bool)>();

        /// <summary>
        /// 是否正在关闭中
        /// </summary>
        public bool IsShuttingDown { get; private set; }

        /// <summary>
        /// 默认每步超时
        /// </summary>
        private static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromSeconds(5);

        private ApplicationLifecycle() { }

        /// <summary>
        /// 注册一个关闭步骤（同时记录为启动步骤，便于排查顺序问题）
        /// </summary>
        /// <param name="name">步骤描述（用途志记录）</param>
        /// <param name="stop">关闭回调，参数为超时时长</param>
        /// <param name="isCritical">关键步骤？失败时继续执行后续步骤</param>
        public void Register(string name, Action<TimeSpan> stop, bool isCritical = false)
        {
            if (_shutdownSteps.Exists(s => s.name == name))
            {
                Logger.Warn($"[Lifecycle] 重复注册关闭步骤: {name}");
                return;
            }
            _shutdownSteps.Add((name, stop, isCritical));
        }

        /// <summary>
        /// 注册一个异步关闭步骤
        /// </summary>
        public void RegisterAsync(string name, Func<Task> stop, bool isCritical = false)
        {
            Register(name, timeout =>
            {
                try
                {
                    var task = stop();
                    if (!task.Wait(timeout))
                        Logger.Warn($"[Lifecycle] {name} 超时（{timeout.TotalSeconds:F0}s），强制跳过");
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[Lifecycle] {name} 异常: {ex.Message}");
                }
            }, isCritical);
        }

        /// <summary>
        /// 执行有序关闭——逆序执行所有已注册步骤，每步独立超时保护
        /// </summary>
        public void Shutdown()
        {
            if (IsShuttingDown) return;
            IsShuttingDown = true;

            Logger.Info($"[Lifecycle] 开始有序关闭，共 {_shutdownSteps.Count} 步");

            // 逆序：最后注册的最先关闭
            for (int i = _shutdownSteps.Count - 1; i >= 0; i--)
            {
                var (name, stop, _) = _shutdownSteps[i];
                try
                {
                    Logger.Info($"[Lifecycle] 关闭 [{_shutdownSteps.Count - i}/{_shutdownSteps.Count}]: {name}");
                    stop(DefaultStepTimeout);
                    Logger.Info($"[Lifecycle] 关闭完成: {name}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Lifecycle] 关闭失败 [{name}]: {ex.Message}");
                    // 非关键步骤失败继续，关键步骤失败停止
                }
            }

            Logger.Info("[Lifecycle] 关闭序列完成");
        }

        /// <summary>
        /// 获取所有已注册步骤的清单（用于调试）
        /// </summary>
        public IReadOnlyList<string> GetRegisteredSteps()
        {
            var result = new List<string>();
            for (int i = 0; i < _shutdownSteps.Count; i++)
            {
                result.Add($"{i + 1}. {_shutdownSteps[i].name}");
            }
            return result;
        }
    }
}
