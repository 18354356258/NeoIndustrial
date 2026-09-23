using Newtonsoft.Json;
using System;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 事件模型 — 记录设备行为，统一设备运行事件
    /// </summary>
    public class SemanticEquipmentEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("equipmentId")]
        public string EquipmentId { get; set; } = "";

        [JsonProperty("eventType")]
        public string EventType { get; set; } = "";

        [JsonProperty("occurredAt")]
        public DateTime OccurredAt { get; set; } = DateTime.Now;

        [JsonProperty("endedAt")]
        public DateTime? EndedAt { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        public SemanticEquipmentEvent Clone()
        {
            // JSON 深拷贝 — 永不漏字段（规则 50）
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<SemanticEquipmentEvent>(json);
        }
    }
}
