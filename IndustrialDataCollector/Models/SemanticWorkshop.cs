using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 车间模型 — 表示企业生产区域
    /// [已废弃] 请使用 SemanticNode（Kind="Workshop"）替代
    /// </summary>
    [System.Obsolete("请使用 SemanticNode（Kind=\"Workshop\"）替代")]
    public class SemanticWorkshop
    {
        [JsonProperty("id")]
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("code")]
        public string Code { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        public SemanticWorkshop Clone()
        {
            // JSON 深拷贝 — 永不漏字段（规则 50）
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<SemanticWorkshop>(json);
        }
    }
}
