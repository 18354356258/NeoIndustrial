using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 设备配置模型
    /// </summary>
    public class DeviceConfig
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [Obsolete("v2.0: 统一使用中文语义路径 tag_cn，英文标签已废弃。保留字段仅用于 JSON 反序列化兼容。")]
        [JsonProperty("nameEn")]
        public string NameEn { get; set; } = "";

        [JsonProperty("driverType")]
        public string DriverType { get; set; } = "ModbusTcp";

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("group")]
        public string Group { get; set; } = "";

        [JsonProperty("tunnelId")]
        public string TunnelId { get; set; } = "";

        [JsonProperty("tagPath")] public string TagPath { get; set; } = "";
        [JsonProperty("tagPathCn")] public string TagPathCn { get; set; } = "";

        /// <summary>v2.0 MQTT 发布模式: Resolved(默认,规范化 tag_id) / Original(保留原始 DataPoint 字段)</summary>
        [JsonProperty("mqttPublishMode")] public string MqttPublishMode { get; set; } = "Resolved";

        [JsonProperty("connectionParams")]
        public Dictionary<string, string> ConnectionParams { get; set; } = new Dictionary<string, string>();

        [JsonProperty("dataPoints")]
        public List<DataPoint> DataPoints { get; set; } = new List<DataPoint>();

        /// <summary>
        /// 获取连接参数，不存在时返回默认值
        /// </summary>
        public string GetParam(string key, string defaultValue)
        {
            string val;
            if (ConnectionParams.TryGetValue(key, out val) && !string.IsNullOrEmpty(val))
                return val;
            return defaultValue;
        }

        /// <summary>
        /// 获取整数参数
        /// </summary>
        public int GetIntParam(string key, int defaultValue)
        {
            int val;
            if (int.TryParse(GetParam(key, defaultValue.ToString()), out val))
                return val;
            return defaultValue;
        }

        /// <summary>
        /// 深拷贝
        /// </summary>
        public DeviceConfig Clone()
        {
            // JSON 深拷贝 — 永不漏字段（规则 50）
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<DeviceConfig>(json);
        }

        public override string ToString()
        {
            return $"{Name} [{DriverType}]";
        }
    }
}
