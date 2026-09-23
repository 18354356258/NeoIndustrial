using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// MCP Fabric 声明式算子描述 — AI 用 JSON 描述分析意图
    /// </summary>
    public class FabricRequest
    {
        [JsonProperty("operator")]
        public string Operator { get; set; } = "";

        [JsonProperty("params")]
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();

        /// <summary>数据时间范围，默认 "1h"。实时快照用 "realtime"，历史窗口用 "5m" "1h" "24h" "7d"，精确范围用 "2026-06-30 08:00/2026-06-30 20:00"</summary>
        [JsonProperty("time_range")]
        public string TimeRange { get; set; } = "1h";

        /// <summary>输出配置。type=return(返回给AI)/file(保存到path)/mqtt(推送)/email(发送)</summary>
        [JsonProperty("output")]
        public FabricOutput Output { get; set; }
    }

    /// <summary>
    /// 算子输出配置 — 控制结果的去向
    /// </summary>
    public class FabricOutput
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "return";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("topic")]
        public string Topic { get; set; } = "";

        [JsonProperty("email")]
        public string Email { get; set; } = "";
    }

    /// <summary>
    /// 滑动窗口聚合参数
    /// </summary>
    public class WindowAggregateParams
    {
        [JsonProperty("device_id")]
        public string DeviceId { get; set; } = "";

        [JsonProperty("variable")]
        public string Variable { get; set; } = "";

        [JsonProperty("window_seconds")]
        public int WindowSeconds { get; set; } = 300;

        [JsonProperty("aggregations")]
        public List<string> Aggregations { get; set; } = new List<string> { "avg" };

        [JsonProperty("time_range")]
        public string TimeRange { get; set; } = "1h";
    }

    /// <summary>
    /// 趋势检测参数
    /// </summary>
    public class TrendDetectParams
    {
        [JsonProperty("device_id")]
        public string DeviceId { get; set; } = "";

        [JsonProperty("variable")]
        public string Variable { get; set; } = "";

        [JsonProperty("window_seconds")]
        public int WindowSeconds { get; set; } = 300;

        [JsonProperty("rise_rate_threshold")]
        public double? RiseRateThreshold { get; set; }

        [JsonProperty("fall_rate_threshold")]
        public double? FallRateThreshold { get; set; }

        [JsonProperty("time_range")]
        public string TimeRange { get; set; } = "1h";
    }

    /// <summary>
    /// 统计阈值报警参数
    /// </summary>
    public class ThresholdAlarmParams
    {
        [JsonProperty("device_id")]
        public string DeviceId { get; set; } = "";

        [JsonProperty("variable")]
        public string Variable { get; set; } = "";

        [JsonProperty("window_seconds")]
        public int WindowSeconds { get; set; } = 300;

        [JsonProperty("sigma")]
        public double Sigma { get; set; } = 3.0;

        [JsonProperty("time_range")]
        public string TimeRange { get; set; } = "1h";
    }

    /// <summary>
    /// 日报参数
    /// </summary>
    public class DailyReportParams
    {
        [JsonProperty("device_ids")]
        public List<string> DeviceIds { get; set; } = new List<string>();

        [JsonProperty("date")]
        public string Date { get; set; } = "";

        [JsonProperty("sections")]
        public List<string> Sections { get; set; } = new List<string> { "summary", "stats", "alarms" };

        [JsonProperty("time_range")]
        public string TimeRange { get; set; } = "24h";

        [JsonProperty("output")]
        public FabricOutput Output { get; set; }
    }

    /// <summary>
    /// Fabric 执行结果
    /// </summary>
    public class FabricResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("operator")]
        public string Operator { get; set; } = "";

        [JsonProperty("data")]
        public object Data { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("execution_ms")]
        public long ExecutionMs { get; set; }

        [JsonProperty("output_path")]
        public string OutputPath { get; set; }
    }

    /// <summary>
    /// 异常检测参数
    /// </summary>
    public class AnomalyDetectParams
    {
        [JsonProperty("device_name")]
        public string DeviceName { get; set; } = "";

        [JsonProperty("variable")]
        public string Variable { get; set; } = "";

        [JsonProperty("method")]
        public string Method { get; set; } = "all";

        [JsonProperty("threshold_sigma")]
        public double ThresholdSigma { get; set; } = 3.0;

        [JsonProperty("time_range")]
        public string TimeRange { get; set; } = "1h";
    }

    /// <summary>
    /// 相关性分析参数
    /// </summary>
    public class CorrelationParams
    {
        [JsonProperty("device_name")]
        public string DeviceName { get; set; } = "";

        [JsonProperty("variables")]
        public List<string> Variables { get; set; } = new List<string>();

        [JsonProperty("time_range")]
        public string TimeRange { get; set; } = "1h";
    }

    /// <summary>
    /// 根因定位参数
    /// </summary>
    public class RootCauseParams
    {
        [JsonProperty("device_name")]
        public string DeviceName { get; set; } = "";

        [JsonProperty("target_variable")]
        public string TargetVariable { get; set; } = "";

        [JsonProperty("candidate_variables")]
        public List<string> CandidateVariables { get; set; } = new List<string>();

        [JsonProperty("max_lag_seconds")]
        public int MaxLagSeconds { get; set; } = 60;

        [JsonProperty("time_range")]
        public string TimeRange { get; set; } = "1h";
    }

    /// <summary>
    /// 预测参数
    /// </summary>
    public class PredictParams
    {
        [JsonProperty("device_name")]
        public string DeviceName { get; set; } = "";

        [JsonProperty("variable")]
        public string Variable { get; set; } = "";

        [JsonProperty("predict_minutes")]
        public int PredictMinutes { get; set; } = 10;

        [JsonProperty("time_range")]
        public string TimeRange { get; set; } = "1h";
    }

    /// <summary>
    /// 已知算子元数据
    /// </summary>
    public class FabricOperatorMeta
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("category")]
        public string Category { get; set; } = "";

        [JsonProperty("params")]
        public List<FabricParamMeta> Params { get; set; } = new List<FabricParamMeta>();
    }

    public class FabricParamMeta
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("type")]
        public string Type { get; set; } = "string";

        [JsonProperty("required")]
        public bool Required { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = "";
    }
}
