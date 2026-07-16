using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// MQTT 发布服务 - 单例，管理 MQTT 连接与数据发布
    /// </summary>
    public class MqttPublishService
    {
        private static readonly Lazy<MqttPublishService> _instance =
            new Lazy<MqttPublishService>(() => new MqttPublishService());
        public static MqttPublishService Instance
        {
            get { return _instance.Value; }
        }

        private IMqttClient _mqttClient;
        private MqttConfig _config;

        public bool IsConnected
        {
            get
            {
                if (_mqttClient == null) return false;
                return _mqttClient.IsConnected;
            }
        }

        public event EventHandler<bool> ConnectionStateChanged;

        private MqttPublishService() { }

        /// <summary>
        /// 连接到 MQTT Broker
        /// </summary>
        public async Task<bool> ConnectAsync(MqttConfig config)
        {
            try
            {
                _config = config;

                // 如果已有连接，先断开
                if (_mqttClient != null && _mqttClient.IsConnected)
                {
                    await _mqttClient.DisconnectAsync();
                }

                var factory = new MqttFactory();
                _mqttClient = factory.CreateMqttClient();

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(config.BrokerHost, config.BrokerPort)
                    .WithClientId(string.IsNullOrEmpty(config.ClientId)
                        ? Guid.NewGuid().ToString()
                        : config.ClientId)
                    .WithCleanSession()
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60));

                if (!string.IsNullOrEmpty(config.Username))
                {
                    optionsBuilder.WithCredentials(config.Username, config.Password);
                }

                var result = await _mqttClient.ConnectAsync(optionsBuilder.Build());

                bool success = result.ResultCode == MqttClientConnectResultCode.Success;

                if (success)
                {
                    // 注册断开事件
                    _mqttClient.DisconnectedAsync += async (e) =>
                    {
                        // Use Debug level for transient disconnects (auto-reconnect handles it)
                        Logger.Debug("MQTT 断开: " + e.Reason);
                        if (ConnectionStateChanged != null)
                            ConnectionStateChanged(this, false);

                        // 自动重连
                        if (config.AutoReconnect)
                        {
                            Logger.Debug("MQTT 自动重连中...");
                            await Task.Delay(5000);
                            await ConnectAsync(config);
                        }
                    };

                    Logger.Info("MQTT 连接成功: " + config.BrokerHost + ":" + config.BrokerPort);
                    if (ConnectionStateChanged != null)
                        ConnectionStateChanged(this, true);
                }
                else
                {
                    Logger.Warn("MQTT 连接失败: " + result.ReasonString);
                    if (ConnectionStateChanged != null)
                        ConnectionStateChanged(this, false);

                    // 初次连接失败也自动重试
                    if (_config != null && _config.AutoReconnect)
                    {
                        Logger.Info("MQTT 连接失败，5秒后自动重试...");
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(5000);
                            if (_mqttClient != null && !_mqttClient.IsConnected)
                                await ConnectAsync(_config);
                        });
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error("MQTT 连接异常: " + ex.Message);
                if (ConnectionStateChanged != null)
                    ConnectionStateChanged(this, false);

                // 连接异常也自动重试（如 EMQX 尚未启动）
                if (_config != null && _config.AutoReconnect)
                {
                    Logger.Info("MQTT 连接异常，5秒后自动重试...");
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(5000);
                        if (_mqttClient != null && !_mqttClient.IsConnected)
                            await ConnectAsync(_config);
                    });
                }

                return false;
            }
        }

        /// <summary>
        /// 断开 MQTT 连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                try
                {
                    await _mqttClient.DisconnectAsync();
                }
                catch { }
            }
            if (ConnectionStateChanged != null)
                ConnectionStateChanged(this, false);
        }

        /// <summary>
        /// 发布数据到指定主题
        /// </summary>
        public async Task PublishAsync(string topic, object data, int qos = 1)
        {
            if (!IsConnected) return;

            try
            {
                var jsonData = JsonConvert.SerializeObject(data);
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(Encoding.UTF8.GetBytes(jsonData))
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                    .WithRetainFlag(false)
                    .Build();

                await _mqttClient.PublishAsync(message);
            }
            catch (Exception ex)
            {
                Logger.Error("MQTT 发布失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 发布设备采集数据到 MQTT（单变量，保留兼容）
        /// </summary>
        public async Task PublishDeviceDataAsync(string deviceName, string topic,
            CollectedData data, int qos = 1)
        {
            var payload = new
            {
                device = deviceName,
                variable = data.VariableName,
                value = data.Value,
                unit = data.Unit,
                timestamp = data.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
            await PublishAsync(topic, payload, qos);
        }

        /// <summary>
        /// 批量发布一个采集周期的所有数据到单一 MQTT 消息
        /// </summary>
        public async Task PublishBatchAsync(string topicPrefix, Drivers.CycleDataBatch batch, int qos = 0)
        {
            if (!IsConnected || batch == null || batch.Values == null || batch.Values.Count == 0) return;

            try
            {
                // v2.0: MQTT 消息格式规范化 — 移除 tag，新增 tag_id
                var mapping = TagMappingService.Instance;
                var values = batch.Values.Select(item => new
                {
                    id = item.Id,
                    dt = item.DataType,
                    v = item.Value,
                    u = item.Unit,
                    tag_id = string.IsNullOrEmpty(item.VariableId) ? "" : mapping.GetTagId(item.VariableId) ?? "",
                    tag_cn = item.TagCn
                }).ToList();

                var payload = new
                {
                    timestamp = batch.Timestamp,
                    driver = batch.Driver,
                    device = batch.Device,
                    values
                };

                // 1. 批量话题：{prefix}/{device} — 包含全部变量
                string topic = string.Format("{0}/{1}", topicPrefix, batch.Device);
                var jsonData = JsonConvert.SerializeObject(payload);

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(Encoding.UTF8.GetBytes(jsonData))
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                    .WithRetainFlag(false)
                    .Build();

                await _mqttClient.PublishAsync(message);

                // 2. 逐变量子话题：{prefix}/{device}/{variableName} — 单变量 JSON
                // 订阅端可用 # 收全部，也可用 /变量名 精确订阅单个变量
                foreach (var item in batch.Values)
                {
                    // 从 id "设备名|变量名" 中提取变量名
                    string varName = item.Id;
                    int pipeIdx = item.Id.LastIndexOf('|');
                    if (pipeIdx >= 0 && pipeIdx < item.Id.Length - 1)
                        varName = item.Id.Substring(pipeIdx + 1);

                    // 变量名作为子话题最后一段
                    string subTopic = string.Format("{0}/{1}/{2}", topicPrefix, batch.Device, varName);

                    var singlePayload = new
                    {
                        timestamp = batch.Timestamp,
                        driver = batch.Driver,
                        device = batch.Device,
                        variable = varName,
                        value = item.Value,
                        unit = item.Unit,
                        data_type = item.DataType,
                        tag_id = string.IsNullOrEmpty(item.VariableId) ? "" : mapping.GetTagId(item.VariableId) ?? "",
                        tag_cn = item.TagCn
                    };

                    var subMsg = new MqttApplicationMessageBuilder()
                        .WithTopic(subTopic)
                        .WithPayload(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(singlePayload)))
                        .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                        .WithRetainFlag(false)
                        .Build();

                    await _mqttClient.PublishAsync(subMsg);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MQTT 批量发布失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 获取当前 MQTT 配置
        /// </summary>
        public MqttConfig GetConfig()
        {
            if (_config != null)
                return _config;
            return new MqttConfig();
        }

        /// <summary>
        /// v2.0 原始发布：传入 topic + payload 字符串，由出口层自行决定格式
        /// </summary>
        public async Task PublishRawAsync(string topic, string payload, int qos = 0)
        {
            if (!IsConnected || string.IsNullOrEmpty(topic)) return;
            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                    .WithRetainFlag(false)
                    .Build();
                await _mqttClient.PublishAsync(message);
            }
            catch (Exception ex)
            {
                Logger.Error("MQTT Raw 发布失败: " + ex.Message);
            }
        }
    }
}
