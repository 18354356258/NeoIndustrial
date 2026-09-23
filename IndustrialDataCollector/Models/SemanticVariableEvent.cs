using System;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 统一事件模型 — 兼容设备事件和变量事件
    /// 替代原 SemanticEquipmentEvent，支持事件类型、处理方式等完整配置
    /// </summary>
    public class SemanticVariableEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("node_id")]
        public string NodeId { get; set; } = "";

        [JsonProperty("variable_relation_id")]
        public string VariableRelationId { get; set; } = "";

        [JsonProperty("event_type")]
        public string EventType { get; set; } = "";

        [JsonProperty("processing_method")]
        public string ProcessingMethod { get; set; } = "仅记录";

        [JsonProperty("processing_config")]
        public string ProcessingConfig { get; set; } = "";

        [JsonProperty("occurred_at")]
        public DateTime OccurredAt { get; set; } = DateTime.Now;

        [JsonProperty("ended_at")]
        public DateTime? EndedAt { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        /// <summary>
        /// 深拷贝
        /// </summary>
        public SemanticVariableEvent Clone()
        {
            // JSON 深拷贝 — 永不漏字段（规则 50）
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<SemanticVariableEvent>(json);
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1} @ {2:yyyy-MM-dd HH:mm:ss}",
                EventType, NodeId, OccurredAt);
        }
    }

    /// <summary>
    /// 事件类型枚举常量 — 通用事件 + 变量专用事件
    /// </summary>
    public static class SemanticEventType
    {
        // ── 通用事件 ──
        public const string Start = "启动";
        public const string Stop = "停止";
        public const string Maintenance = "维护保养";
        public const string ParameterChange = "参数变更";
        public const string CommunicationLost = "通讯中断";
        public const string CommunicationRestored = "通讯恢复";
        public const string Fault = "故障";

        // ── 变量专用事件 ──
        public const string OverUpperLimit = "超上限";
        public const string OverLowerLimit = "超下限";
        public const string OverHighHigh = "超高高限(HH)";
        public const string OverLowLow = "超低低限(LL)";
        public const string DeviationFromTarget = "偏离目标值";
        public const string QualityAbnormal = "质量异常";
        public const string CollectionAbnormal = "采集异常";
        public const string DataLoss = "数据丢失";
        public const string FrozenChange = "冻结变化";
        public const string CalculationFailed = "计算失败";
        public const string ManualConfirm = "人工确认";
        public const string ManualDispose = "人工处置";

        /// <summary>
        /// 通用事件列表
        /// </summary>
        public static string[] GeneralEvents = new string[]
        {
            Start, Stop, Maintenance, ParameterChange,
            CommunicationLost, CommunicationRestored, Fault
        };

        /// <summary>
        /// 变量专用事件列表
        /// </summary>
        public static string[] VariableEvents = new string[]
        {
            OverUpperLimit, OverLowerLimit, OverHighHigh, OverLowLow,
            DeviationFromTarget, QualityAbnormal, CollectionAbnormal,
            DataLoss, FrozenChange, CalculationFailed,
            ManualConfirm, ManualDispose
        };

        /// <summary>
        /// 所有事件类型
        /// </summary>
        public static string[] AllTypes
        {
            get
            {
                var all = new string[GeneralEvents.Length + VariableEvents.Length];
                GeneralEvents.CopyTo(all, 0);
                VariableEvents.CopyTo(all, GeneralEvents.Length);
                return all;
            }
        }
    }

    /// <summary>
    /// 事件处理方式枚举常量
    /// </summary>
    public static class EventProcessingMethod
    {
        public const string LogOnly = "仅记录";
        public const string Alarm = "报警";
        public const string MessageNotify = "消息通知";
        public const string InSiteMessage = "站内消息";
        public const string Email = "邮件";
        public const string SMS = "短信";
        public const string Webhook = "Webhook";
        public const string CallAPI = "调用API";
        public const string TriggerWorkflow = "触发工作流";
        public const string GenerateWorkOrder = "生成工单";
        public const string TriggerMcpTask = "触发MCP任务";
        public const string TriggerAIAnalysis = "触发AI分析";

        public static string[] AllMethods = new string[]
        {
            LogOnly, Alarm, MessageNotify, InSiteMessage, Email,
            SMS, Webhook, CallAPI, TriggerWorkflow, GenerateWorkOrder,
            TriggerMcpTask, TriggerAIAnalysis
        };
    }
}
