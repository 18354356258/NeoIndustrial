using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 节点关系模型 — 建立节点之间的业务关联
    /// 兼容旧版 EquipmentId 字段，新增 NodeId 字段为推荐用法
    /// </summary>
    public class SemanticEquipmentRelation
    {
        [JsonProperty("id")]
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        // ── 新增：节点级关系（推荐） ──

        [JsonProperty("source_node_id")]
        public string SourceNodeId { get; set; } = "";

        [JsonProperty("target_node_id")]
        public string TargetNodeId { get; set; } = "";

        // ── 保留：设备级关系（向后兼容） ──

        [JsonProperty("source_equipment_id")]
        public string SourceEquipmentId
        {
            get { return SourceNodeId; }
            set { SourceNodeId = value; }
        }

        [JsonProperty("target_equipment_id")]
        public string TargetEquipmentId
        {
            get { return TargetNodeId; }
            set { TargetNodeId = value; }
        }

        [JsonProperty("relation_type")]
        public string RelationType { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        public SemanticEquipmentRelation Clone()
        {
            // JSON 深拷贝 — 永不漏字段（规则 50）
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<SemanticEquipmentRelation>(json);
        }
    }
}
