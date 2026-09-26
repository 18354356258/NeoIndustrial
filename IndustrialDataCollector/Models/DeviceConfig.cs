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

            // v3.22.59：读取期兼容 —— ① 大小写不敏感（历史/工具写入 ip、port 等）；
            // ② 常见别名（scanIntervalMs/intervalMs → PollInterval 等），避免"参数配了不生效"。
            foreach (var kv in ConnectionParams)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(kv.Value))
                    return kv.Value;
            if (ParamAliases.TryGetValue(key, out var alts))
                foreach (var alt in alts)
                    foreach (var kv in ConnectionParams)
                        if (string.Equals(kv.Key, alt, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(kv.Value))
                            return kv.Value;
            return defaultValue;
        }

        /// <summary>v3.22.59：驱动参数别名（键名不同但语义相同的历史写法）</summary>
        private static readonly Dictionary<string, string[]> ParamAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["PollInterval"] = new[] { "scanIntervalMs", "scan_interval_ms", "intervalMs", "interval_ms", "Interval" },
            ["IP"] = new[] { "ipAddress", "IpAddress", "host", "Host" },
            ["Port"] = new[] { "portNumber" },
            ["ProgID"] = new[] { "prog_id", "progid", "server_name" },
            ["OpcNode"] = new[] { "opc_node", "node", "nodeName" },
            ["ReadSource"] = new[] { "read_source", "readSource" },
            ["UpdateRate"] = new[] { "update_rate", "updateRate", "UpdateMs" },
            ["PortName"] = new[] { "port_name", "comport", "ComPort" },
            ["BaudRate"] = new[] { "baud_rate", "baud" },
            ["DataBits"] = new[] { "data_bits", "databits" },
            ["StopBits"] = new[] { "stop_bits", "stopbits" },
            ["Parity"] = new[] { "parity" },
            ["BaseUrl"] = new[] { "base_url", "baseurl", "url", "URL", "http_url", "ip", "ipAddress" },
            ["URL"] = new[] { "url", "base_url", "baseurl", "ip", "ipAddress" },
            ["BrokerHost"] = new[] { "broker_host", "host", "Host" },
            ["BrokerPort"] = new[] { "broker_port" },
            ["TopicFilter"] = new[] { "topic_filter", "topic", "Topic" },
            ["Topic"] = new[] { "topic" },
            ["Username"] = new[] { "mqtt_user", "opcua_user", "user" },
            ["Password"] = new[] { "mqtt_pass", "opcua_pass", "pass" },
            ["Qos"] = new[] { "qos" },
            ["ClientAddress"] = new[] { "client_address", "clientaddress" },
            ["ServerAddress"] = new[] { "server_address", "serveraddress" },
            ["AmsNetId"] = new[] { "ams_net_id", "amsnetid" },
            ["AmsPort"] = new[] { "ams_port", "amsport" },
            ["AmsPortSource"] = new[] { "ams_port_source", "amsportsource" },
            ["NCU"] = new[] { "ncu" },
            ["StationNo"] = new[] { "station_no", "stationno", "station" },
            ["Station"] = new[] { "station" },
        };

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
