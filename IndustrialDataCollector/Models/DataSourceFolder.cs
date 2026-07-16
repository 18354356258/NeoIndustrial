using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 数据源文件夹 — 轻量模型，用于数据源管理树的文件夹节点
    /// </summary>
    public class DataSourceFolder
    {
        [JsonProperty("id")]
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("name")]
        public string Name { get; set; } = "";
    }
}
