using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// MQTT 配置模型
    /// </summary>
    public class MqttConfig
    {
        [JsonProperty("brokerHost")]
        public string BrokerHost { get; set; } = "localhost";

        [JsonProperty("brokerPort")]
        public int BrokerPort { get; set; } = 1883;

        [JsonProperty("clientId")]
        public string ClientId { get; set; } = "";

        [JsonProperty("username")]
        public string Username { get; set; } = "";

        [JsonProperty("password")]
        public string Password { get; set; } = "";

        [JsonProperty("topicPrefix")]
        public string TopicPrefix { get; set; } = "industrial/data";

        [JsonProperty("qos")]
        public int Qos { get; set; } = 1;

        [JsonProperty("autoReconnect")]
        public bool AutoReconnect { get; set; } = true;

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = false;
    }
}
