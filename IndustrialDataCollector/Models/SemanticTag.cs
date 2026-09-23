using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 语义标签模型 — 设备采集变量统一抽象为标签
    /// [已废弃] 请使用 SemanticNode（Kind="Variable"）替代
    /// </summary>
    [System.Obsolete("请使用 SemanticNode（Kind=\"Variable\"）替代")]
    public class SemanticTag
    {
        [JsonProperty("id")]
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("equipmentId")]
        public string EquipmentId { get; set; } = "";

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("code")]
        public string Code { get; set; } = "";

        [JsonProperty("variableRole")]
        public string VariableRole { get; set; } = "";

        [JsonProperty("unit")]
        public string Unit { get; set; } = "";

        [JsonProperty("dataType")]
        public string DataType { get; set; } = "";

        [JsonProperty("deviceConfigId")]
        public string DeviceConfigId { get; set; } = "";

        [JsonProperty("dataPointName")]
        public string DataPointName { get; set; } = "";

        public SemanticTag Clone()
        {
            // JSON 深拷贝 — 永不漏字段（规则 50）
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<SemanticTag>(json);
        }
    }
}
