using System;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 采集到的数据点
    /// </summary>
    public class CollectedData
    {
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; } = "";

        [JsonProperty("deviceName")]
        public string DeviceName { get; set; } = "";

        [JsonProperty("variableName")]
        public string VariableName { get; set; } = "";

        [JsonProperty("dataType")]
        public string DataType { get; set; } = "";

        [JsonProperty("sourceDriverType")]
        public string SourceDriverType { get; set; } = "";

        [JsonProperty("value")]
        public string Value { get; set; } = "0";

        [JsonProperty("unit")]
        public string Unit { get; set; } = "";

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [JsonProperty("tag")]
        public string Tag { get; set; } = "";

        [JsonProperty("tag_cn")]
        public string TagCn { get; set; } = "";

        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {DeviceName}.{VariableName} = {Value} {Unit}";
        }
    }
}
