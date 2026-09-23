using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 语义设备模型 — 平台最核心对象，所有采集数据最终归属于设备
    /// [已废弃] 请使用 SemanticNode（Kind="Equipment"）替代
    /// </summary>
    [System.Obsolete("请使用 SemanticNode（Kind=\"Equipment\"）替代")]
    public class SemanticEquipment
    {
        [JsonProperty("id")]
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("code")]
        public string Code { get; set; } = "";

        [JsonProperty("equipmentType")]
        public string EquipmentType { get; set; } = "";

        [JsonProperty("workshopId")]
        public string WorkshopId { get; set; } = "";

        [JsonProperty("productionLineId")]
        public string ProductionLineId { get; set; } = "";

        [JsonProperty("deviceConfigId")]
        public string DeviceConfigId { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        public SemanticEquipment Clone()
        {
            // JSON 深拷贝 — 永不漏字段（规则 50）
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<SemanticEquipment>(json);
        }
    }
}
