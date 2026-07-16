using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Newtonsoft.Json.Linq;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// MQTT 订阅驱动 - 订阅 MQTT 主题，将收到的消息解析为采集数据
    /// </summary>
    public class MqttSubscribeDriver : IDriver
    {
        private IMqttClient _mqttClient;
        private string _brokerHost = "localhost";
        private int _brokerPort = 1883;
        private string _topicFilter = "#";
        private int _qos = 1;
        private string _username = "";
        private string _password = "";
        private DeviceConfig _config;
        private bool _disposed;
        private bool _isDisconnecting;
        private bool _started;

        public string DriverType => "MqttSubscribe";
        public bool IsConnected => _mqttClient?.IsConnected ?? false;

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _brokerHost = config.GetParam("BrokerHost", "localhost");
            _brokerPort = config.GetIntParam("BrokerPort", 1883);
            _topicFilter = config.GetParam("TopicFilter", "#");
            _qos = config.GetIntParam("Qos", 1);
            _username = config.GetParam("Username", "");
            _password = config.GetParam("Password", "");

            try
            {
                if (_mqttClient != null && _mqttClient.IsConnected)
                    await _mqttClient.DisconnectAsync();

                var factory = new MqttFactory();
                _mqttClient = factory.CreateMqttClient();

                _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
                _mqttClient.DisconnectedAsync += async (e) =>
                {
                    if (_isDisconnecting || _disposed) return;
                    Logger.Warn(string.Format("MQTT 订阅断开 [{0}]: {1}", _config?.Name, e.Reason));
                    NotifyStatus(false, "MQTT 已断开");
                    await Task.Delay(5000);
                    if (!_isDisconnecting && !_disposed)
                        await ConnectAsync(_config);
                };

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_brokerHost, _brokerPort)
                    .WithClientId($"IndSub_{Guid.NewGuid():N}")
                    .WithCleanSession()
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60));

                if (!string.IsNullOrEmpty(_username))
                    optionsBuilder.WithCredentials(_username, _password);

                var result = await _mqttClient.ConnectAsync(optionsBuilder.Build());

                if (result.ResultCode == MqttClientConnectResultCode.Success)
                {
                    var subscribeOptions = new MqttFactory().CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(_topicFilter)
                            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)_qos))
                        .Build();

                    await _mqttClient.SubscribeAsync(subscribeOptions);

                    Logger.Info($"MQTT 订阅已连接: {_brokerHost}:{_brokerPort}, 主题={_topicFilter}");
                    NotifyStatus(true, $"MQTT 订阅中 ({_topicFilter})");
                    return true;
                }
                else
                {
                    Logger.Warn($"MQTT 订阅连接失败: {result.ReasonString}");
                    NotifyStatus(false, $"连接失败: {result.ReasonString}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"MQTT 订阅连接异常 [{_config?.Name}]: {ex.Message}");
                NotifyStatus(false, $"连接异常: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            _isDisconnecting = true;
            _started = false;
            try
            {
                if (_mqttClient != null)
                {
                    _mqttClient.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
                    try
                    {
                        await _mqttClient.DisconnectAsync();
                    }
                    catch { }
                    _mqttClient.Dispose();
                    _mqttClient = null;
                }
            }
            catch { }
            NotifyStatus(false, "已断开");
        }

        public Task StartCollectAsync(CancellationToken token)
        {
            _started = true;
            _isDisconnecting = false;
            // MQTT 订阅驱动由消息事件驱动，不需要轮询循环
            // 保持连接，等待消息回调
            Logger.Info(string.Format("MQTT 订阅驱动已启动: {0}, 主题={1}", _config?.Name, _topicFilter));
            return Task.CompletedTask;
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            return Task.FromResult<object>(0);
        }

        /// <summary>
        /// MQTT 消息到达回调
        /// </summary>
        private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            if (!_started || _isDisconnecting) return Task.CompletedTask;
            try
            {
                string topic = e.ApplicationMessage.Topic;
                var seg = e.ApplicationMessage.PayloadSegment;
                string payload = Encoding.UTF8.GetString(seg.Array, seg.Offset, seg.Count);

                Logger.Debug($"MQTT 收到: {topic} => {payload}");

                var cycleItems = new System.Collections.Generic.List<CycleDataItem>();

                if (_config == null || _config.DataPoints.Count == 0)
                {
                    // 没有配置变量点，按主题名和 payload 自动生成
                    var autoData = new CollectedData
                    {
                        DeviceId = _config?.Id ?? "",
                        DeviceName = _config?.Name ?? "MQTT",
                        SourceDriverType = "MqttSubscribe",
                        VariableName = topic.Replace("/", "."),
                        Value = payload,
                        Unit = "",
                        Tag = "",
                        TagCn = "",
                        Timestamp = DateTime.Now
                    };
                    OnDataReceived?.Invoke(this, new CollectedDataEventArgs(autoData));
                    cycleItems.Add(new CycleDataItem
                    {
                        VariableId = _config.DataPoints.Find(p => p.Name == autoData.VariableName)?.VariableId ?? "",
                        Id = string.Format("{0}|{1}", autoData.DeviceName, autoData.VariableName),
                        DataType = "string",
                        Value = payload,
                        Unit = "",
                        Tag = "",
                        TagCn = ""
                    });
                }
                else
                {
                    // 尝试解析 JSON payload
                    JToken json = null;
                    try
                    {
                        json = JToken.Parse(payload);
                    }
                    catch { }

                    // v2.1: 检测内嵌的 CycleDataBatch 格式（含 values 数组）
                    JArray batchValues = null;
                    bool isSingleVarJson = false;
                    string singleVarValue = null;
                    string singleVarUnit = null;

                    if (json != null)
                    {
                        if (json["values"] is JArray valuesArr)
                        {
                            batchValues = valuesArr;
                        }
                        // 单变量格式：{"variable":"挤出量","value":"23.27","unit":"kg/h",...}
                        else if (json["variable"] != null && json["value"] != null)
                        {
                            isSingleVarJson = true;
                            singleVarValue = json["value"]?.ToString();
                            singleVarUnit = json["unit"]?.ToString() ?? "";
                        }
                    }

                    foreach (var point in _config.DataPoints)
                    {
                        if (!point.IsActive) continue;

                        string value = null;
                        string unit = null;
                        string tagId = null;
                        string tagCn = null;

                        // 单变量 JSON：直接取 value/unit 字段
                        if (isSingleVarJson)
                        {
                            value = singleVarValue;
                            unit = singleVarUnit;
                            tagId = json["tag_id"]?.ToString();
                            tagCn = json["tag_cn"]?.ToString();
                        }

                        // 优先从内嵌 batch values 中按 tag_cn / 变量名匹配
                        if (batchValues != null && batchValues.Count > 0)
                        {
                            JToken matched = null;
                            foreach (var item in batchValues)
                            {
                                // 按 tag_cn 精确匹配
                                string itemTagCn = item["tag_cn"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(point.TagCn) && itemTagCn == point.TagCn)
                                {
                                    matched = item;
                                    break;
                                }
                                // 按 tag_cn 最后一段匹配变量名（"一车间/28号挤压机/料筒温度" → "料筒温度"）
                                string itemTagCnLast = itemTagCn;
                                int lastSlash = itemTagCn.LastIndexOf('/');
                                if (lastSlash >= 0)
                                    itemTagCnLast = itemTagCn.Substring(lastSlash + 1);
                                if (!string.IsNullOrEmpty(itemTagCnLast)
                                    && string.Equals(itemTagCnLast, point.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    matched = item;
                                    break;
                                }
                                // 按 tag_cn 包含变量名匹配
                                if (!string.IsNullOrEmpty(point.Name)
                                    && itemTagCn.IndexOf(point.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    matched = item;
                                    break;
                                }
                                // 按 id 匹配（Resolved: "设备|变量名"，Original: tag_id）
                                string itemId = item["id"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(point.Name)
                                    && (itemId.EndsWith("|" + point.Name, StringComparison.OrdinalIgnoreCase)
                                        || itemId.IndexOf(point.Name, StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    matched = item;
                                    break;
                                }
                                // 按 tag_id 匹配
                                string itemTagId = item["tag_id"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(point.Tag) && itemTagId == point.Tag)
                                {
                                    matched = item;
                                    break;
                                }
                            }

                            if (matched != null)
                            {
                                value = matched["v"]?.ToString();
                                unit = matched["u"]?.ToString() ?? "";
                                tagId = matched["tag_id"]?.ToString();
                                tagCn = matched["tag_cn"]?.ToString();
                            }
                        }

                        // Fallback: 原有逐字段匹配逻辑（非 batch 或 batch 中未匹配到）
                        if (value == null && json != null)
                        {
                            JToken token = null;
                            if (!string.IsNullOrWhiteSpace(point.Address) && point.Address != "0")
                                token = json.SelectToken(point.Address);

                            if (token == null)
                                token = json.SelectToken(point.Name);

                            if (token == null)
                                token = FindToken(json, point.Name);

                            if (token != null)
                                value = token.ToString();
                        }

                        // 非 JSON payload，直接当值
                        if (value == null)
                            value = payload;

                        var data = new CollectedData
                        {
                            DeviceId = _config.Id,
                            DeviceName = _config.Name,
                            SourceDriverType = "MqttSubscribe",
                            VariableName = point.Name,
                            DataType = point.DataType,
                            Value = value,
                            Unit = !string.IsNullOrEmpty(unit) ? unit : point.Unit,
                            Tag = tagId ?? (point.OutputTag ? point.Tag : null),
                            TagCn = tagCn ?? (point.OutputTagCn ? point.TagCn : null),
                            Timestamp = DateTime.Now
                        };
                        OnDataReceived?.Invoke(this, new CollectedDataEventArgs(data));

                        cycleItems.Add(new CycleDataItem
                        {
                        VariableId = point.VariableId, 
                            Id = string.Format("{0}|{1}", _config.Name, point.Name),
                            DataType = point.DataType,
                            Value = value,
                            Unit = !string.IsNullOrEmpty(unit) ? unit : point.Unit,
                            Tag = tagId ?? (point.OutputTag ? point.Tag : null),
                            TagCn = tagCn ?? (point.OutputTagCn ? point.TagCn : null)
                        });
                    }
                }

                // 触发周期完成事件，驱动数据库写入
                if (cycleItems.Count > 0)
                {
                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Driver = "mqtt",
                        Device = _config?.Name ?? "MQTT",
                        DeviceId = _config.Id, Values = cycleItems
                    }));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"MQTT 消息处理异常: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        private JToken FindToken(JToken token, string name)
        {
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                        return prop.Value;
                    var found = FindToken(prop.Value, name);
                    if (found != null) return found;
                }
            }
            else if (token is JArray arr)
            {
                foreach (var item in arr)
                {
                    var found = FindToken(item, name);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private void NotifyStatus(bool connected, string message)
        {
            OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(
                _config?.Id ?? "", _config?.Name ?? "", connected, message));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisconnectAsync().Wait();
        }
    }
}
