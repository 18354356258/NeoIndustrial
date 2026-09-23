using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 产线模型 — 表示工艺生产系统
    /// [已废弃] 请使用 SemanticNode（Kind="ProductionLine"）替代
    /// </summary>
    [System.Obsolete("请使用 SemanticNode（Kind=\"ProductionLine\"）替代")]
    public class SemanticProductionLine
    {
        [JsonProperty("id")]
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("workshopId")]
        public string WorkshopId { get; set; } = "";

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("code")]
        public string Code { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        public SemanticProductionLine Clone()
        {
            // JSON 深拷贝 — 永不漏字段（规则 50）
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<SemanticProductionLine>(json);
        }
    }
}
