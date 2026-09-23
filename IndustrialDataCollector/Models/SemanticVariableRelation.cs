using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 变量关系模型 — 关联变量到数据源表字段/常量/表达式，支持复合查询条件
    /// </summary>
    public class SemanticVariableRelation
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("variable_node_id")]
        public string VariableNodeId { get; set; } = "";

        [JsonProperty("relation_type")]
        public string RelationType { get; set; } = "";

        [JsonProperty("target_type")]
        public string TargetType { get; set; } = "datasource_field";

        [JsonProperty("target_datasource_id")]
        public string TargetDatasourceId { get; set; } = "";

        [JsonProperty("target_table_name")]
        public string TargetTableName { get; set; } = "";

        [JsonProperty("target_field_name")]
        public string TargetFieldName { get; set; } = "";

        /// <summary>目标变量节点ID（TargetType=variable 时使用）</summary>
        [JsonProperty("target_variable_node_id")]
        public string TargetVariableNodeId { get; set; } = "";

        [JsonProperty("constant_value")]
        public string ConstantValue { get; set; } = "";

        [JsonProperty("expression")]
        public string Expression { get; set; } = "";

        [JsonProperty("unit")]
        public string Unit { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("condition_variable_ids")]
        public string ConditionVariableIdsJson { get; set; } = "[]";

        /// <summary>
        /// 查询条件变量ID列表
        /// </summary>
        [JsonIgnore]
        public List<string> ConditionVariableIds
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(ConditionVariableIdsJson)) return new List<string>();
                    return JsonConvert.DeserializeObject<List<string>>(ConditionVariableIdsJson)
                        ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                ConditionVariableIdsJson = (value != null && value.Count > 0)
                    ? JsonConvert.SerializeObject(value)
                    : "[]";
            }
        }

        /// <summary>
        /// 深拷贝
        /// </summary>
        public SemanticVariableRelation Clone()
        {
            return new SemanticVariableRelation
            {
                Id = this.Id,
                VariableNodeId = this.VariableNodeId,
                RelationType = this.RelationType,
                TargetType = this.TargetType,
                TargetDatasourceId = this.TargetDatasourceId,
                TargetTableName = this.TargetTableName,
                TargetFieldName = this.TargetFieldName,
                TargetVariableNodeId = this.TargetVariableNodeId,
                ConstantValue = this.ConstantValue,
                Expression = this.Expression,
                Unit = this.Unit,
                Description = this.Description,
                ConditionVariableIdsJson = this.ConditionVariableIdsJson
            };
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1} -> {2}", RelationType, VariableNodeId,
                TargetType == VariableTargetType.DatasourceField ? TargetFieldName :
                TargetType == VariableTargetType.Constant ? ConstantValue :
                TargetType == VariableTargetType.Variable ? ("变量:" + TargetVariableNodeId) : Expression);
        }
    }

    /// <summary>
    /// 变量关系类型枚举常量
    /// </summary>
    public static class VariableRelationType
    {
        public const string UpperLimit = "上限";
        public const string LowerLimit = "下限";
        public const string TargetValue = "目标值";
        public const string StandardValue = "标准值";
        public const string SOPStep = "SOP步骤";
        public const string SIPRequirement = "SIP要求";
        public const string QualityJudgment = "质量判定";
        public const string AlarmThreshold = "报警阈值";
        public const string CompensationFactor = "补偿系数";
        public const string CalculationFormula = "计算公式";
        public const string ReferenceVariable = "参考变量";
        public const string BusinessAssociation = "业务关联";

        // --- 语义图谱关系（变量→变量）---
        public const string Affects = "影响";
        public const string ConstrainedBy = "被约束";
        public const string DerivedFrom = "计算来源";
        public const string RelatedEquipment = "关联设备";

        // --- 历史数据追溯 ---
        public const string HistoricalDataSource = "历史数据源";

        public static string[] AllTypes = new string[]
        {
            UpperLimit, LowerLimit, TargetValue, StandardValue,
            SOPStep, SIPRequirement, QualityJudgment, AlarmThreshold,
            CompensationFactor, CalculationFormula, ReferenceVariable, BusinessAssociation,
            Affects, ConstrainedBy, DerivedFrom, RelatedEquipment,
            HistoricalDataSource
        };

        public static string[] GraphTypes = new string[]
        {
            Affects, ConstrainedBy, DerivedFrom, RelatedEquipment
        };
    }

    /// <summary>
    /// 目标类型枚举常量
    /// </summary>
    public static class VariableTargetType
    {
        public const string DatasourceField = "datasource_field";
        public const string Constant = "constant";
        public const string Expression = "expression";
        /// <summary>变量 → 变量关系（语义图谱核心）</summary>
        public const string Variable = "variable";
    }

    /// <summary>
    /// 影响图路径节点
    /// </summary>
    public class ImpactPath
    {
        public string VariableNodeId { get; set; } = "";
        public int Depth { get; set; }
        public string RelationType { get; set; } = "";
        public string RelationDescription { get; set; } = "";
        /// <summary>经过哪个源节点到达此节点</summary>
        public string ViaSourceId { get; set; } = "";
    }

    /// <summary>
    /// 影响图分析结果
    /// </summary>
    public class ImpactGraphResult
    {
        public string RootVariableId { get; set; } = "";
        public List<ImpactPath> Downstream { get; set; } = new List<ImpactPath>();
        public List<ImpactPath> Upstream { get; set; } = new List<ImpactPath>();
    }
}
