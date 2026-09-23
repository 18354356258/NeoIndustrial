using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 设备配置模板 — 可复用的设备配置模式，支持变量匹配、语义关系、Fabric算子、事件规则、清洗策略的模板化
    /// </summary>
    public class DeviceTemplate
    {
        [JsonProperty("template_id")]
        public string TemplateId { get; set; }

        [JsonProperty("template_name")]
        public string TemplateName { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0";

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("created_from_device")]
        public string CreatedFromDevice { get; set; }

        [JsonProperty("created_from_driver")]
        public string CreatedFromDriver { get; set; }

        [JsonProperty("variable_count")]
        public int VariableCount { get; set; }

        [JsonProperty("variable_patterns")]
        public List<VariablePattern> VariablePatterns { get; set; } = new List<VariablePattern>();

        [JsonProperty("semantic_relations")]
        public List<TemplateSemanticRelation> SemanticRelations { get; set; } = new List<TemplateSemanticRelation>();

        [JsonProperty("fabric_configs")]
        public List<TemplateFabricConfig> FabricConfigs { get; set; } = new List<TemplateFabricConfig>();

        [JsonProperty("event_rules")]
        public List<TemplateEventRule> EventRules { get; set; } = new List<TemplateEventRule>();

        [JsonProperty("cleaning_strategies")]
        public List<TemplateCleaningStrategy> CleaningStrategies { get; set; } = new List<TemplateCleaningStrategy>();

        [JsonProperty("inheritance_config")]
        public InheritanceConfig InheritanceConfig { get; set; } = new InheritanceConfig();
    }

    /// <summary>
    /// 变量匹配模式 — 定义模板变量如何匹配到实际设备变量
    /// </summary>
    public class VariablePattern
    {
        [JsonProperty("template_var_name")]
        public string TemplateVarName { get; set; }

        /// <summary>匹配规则: "精确" / "正则" / "包含"</summary>
        [JsonProperty("match_rule")]
        public string MatchRule { get; set; } = "精确";

        [JsonProperty("match_pattern")]
        public string MatchPattern { get; set; }

        [JsonProperty("data_type")]
        public string DataType { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; }

        [JsonProperty("tag_cn")]
        public string TagCn { get; set; }
    }

    /// <summary>
    /// 模板中的语义关系 — 描述变量间的关联
    /// </summary>
    public class TemplateSemanticRelation
    {
        [JsonProperty("source_var")]
        public string SourceVar { get; set; }

        [JsonProperty("target_var")]
        public string TargetVar { get; set; }

        [JsonProperty("relation_type")]
        public string RelationType { get; set; }

        [JsonProperty("target_type")]
        public string TargetType { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }

    /// <summary>
    /// 模板中的Fabric算子配置 — 预配置的分析/报警算子
    /// </summary>
    public class TemplateFabricConfig
    {
        [JsonProperty("operator")]
        public string Operator { get; set; }

        [JsonProperty("target_var")]
        public string TargetVar { get; set; }

        [JsonProperty("params")]
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// 模板中的事件规则 — 预定义的事件触发条件和动作
    /// </summary>
    public class TemplateEventRule
    {
        [JsonProperty("rule_name")]
        public string RuleName { get; set; }

        [JsonProperty("condition")]
        public TemplateRuleCondition Condition { get; set; }

        [JsonProperty("actions")]
        public List<string> Actions { get; set; } = new List<string>();

        [JsonProperty("processing_method")]
        public string ProcessingMethod { get; set; } = "仅记录";
    }

    /// <summary>
    /// 模板规则条件
    /// </summary>
    public class TemplateRuleCondition
    {
        /// <summary>条件类型: "FabricResult" / "Threshold" / "StatusChange"</summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("operator")]
        public string Operator { get; set; }

        [JsonProperty("target_var")]
        public string TargetVar { get; set; }

        [JsonProperty("trigger")]
        public string Trigger { get; set; }
    }

    /// <summary>
    /// 模板中的清洗策略 — 预配置的数据清洗规则
    /// </summary>
    public class TemplateCleaningStrategy
    {
        [JsonProperty("target_var")]
        public string TargetVar { get; set; }

        [JsonProperty("strategy")]
        public string Strategy { get; set; }

        [JsonProperty("params")]
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// 继承配置 — 控制模板应用到设备时的继承行为
    /// </summary>
    public class InheritanceConfig
    {
        [JsonProperty("inherit_semantic_relations")]
        public bool InheritSemanticRelations { get; set; } = true;

        [JsonProperty("inherit_fabric_configs")]
        public bool InheritFabricConfigs { get; set; } = true;

        [JsonProperty("inherit_event_rules")]
        public bool InheritEventRules { get; set; } = true;

        [JsonProperty("inherit_cleaning_strategies")]
        public bool InheritCleaningStrategies { get; set; } = true;

        [JsonProperty("skip_missing_variables")]
        public bool SkipMissingVariables { get; set; } = true;
    }

    // ════════════════════════════════════════════════════════════════
    //  应用结果模型（运行时，不需要 JSON 持久化）
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 单个变量的匹配结果
    /// </summary>
    public class VariableMatchResult
    {
        public string TemplateVar { get; set; }
        public bool Success { get; set; }
        public string ActualVarName { get; set; }
        public string MatchMethod { get; set; }
        public string SkipReason { get; set; }
    }

    /// <summary>
    /// 模板应用项 — 记录每个关系/Fabric/事件/清洗的应用状态
    /// </summary>
    public class AppliedItem
    {
        public string ItemType { get; set; } // "relation"/"fabric"/"event"/"cleaning"
        public string Description { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// 模板应用到设备的完整结果
    /// </summary>
    public class TemplateApplyResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("template_name")]
        public string TemplateName { get; set; }

        [JsonProperty("device_name")]
        public string DeviceName { get; set; }

        [JsonProperty("total_patterns")]
        public int TotalPatterns { get; set; }

        [JsonProperty("matched_count")]
        public int MatchedCount { get; set; }

        [JsonProperty("skipped_count")]
        public int SkippedCount { get; set; }

        [JsonProperty("variable_matches")]
        public List<VariableMatchResult> VariableMatches { get; set; } = new List<VariableMatchResult>();

        [JsonProperty("applied_relations")]
        public int AppliedRelations { get; set; }

        [JsonProperty("applied_fabric")]
        public int AppliedFabric { get; set; }

        [JsonProperty("applied_events")]
        public int AppliedEvents { get; set; }

        [JsonProperty("applied_cleaning")]
        public int AppliedCleaning { get; set; }

        [JsonProperty("applied_items")]
        public List<AppliedItem> AppliedItems { get; set; } = new List<AppliedItem>();

        [JsonProperty("log_path")]
        public string LogPath { get; set; }
    }

    // ════════════════════════════════════════════════════════════════
    //  模板生成选项（运行时）
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 从设备生成模板的选项
    /// </summary>
    public class TemplateGenOptions
    {
        public string TemplateName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }

        /// <summary>变量匹配模式: "精确" / "正则" / "包含"</summary>
        public string MatchMode { get; set; } = "精确";

        public bool IncludeSemanticRelations { get; set; } = true;
        public bool IncludeFabricConfigs { get; set; } = true;
        public bool IncludeEventRules { get; set; } = true;
        public bool IncludeCleaningStrategies { get; set; } = true;
    }

    // ════════════════════════════════════════════════════════════════
    //  克隆选项和结果
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 设备克隆选项
    /// </summary>
    public class CloneOptions
    {
        public string SourceDeviceId { get; set; }
        public string NewDeviceName { get; set; }
        public string NewGroupPath { get; set; }
        public bool CloneDriverParams { get; set; } = true;
        public bool CloneVariables { get; set; } = true;
        public bool CloneCleaningStrategies { get; set; } = true;
        public bool CloneFabricConfigs { get; set; } = true;
        public bool CloneEventRules { get; set; } = true;
        public bool CloneSemanticRelations { get; set; } = true;
        public bool AutoGenerateTags { get; set; } = true;
        public bool AutoOpenConfig { get; set; } = true;
        public bool AutoStartCollect { get; set; } = false;
    }

    /// <summary>
    /// 设备克隆结果
    /// </summary>
    public class CloneResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("source_device")]
        public string SourceDevice { get; set; }

        [JsonProperty("new_device_name")]
        public string NewDeviceName { get; set; }

        [JsonProperty("new_device_id")]
        public string NewDeviceId { get; set; }

        [JsonProperty("cloned_variables")]
        public int ClonedVariables { get; set; }

        [JsonProperty("cloned_relations")]
        public int ClonedRelations { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }
    }
}
