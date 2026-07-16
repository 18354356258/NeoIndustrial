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
    /// Sparkplug B 驱动 - 通过 MQTT 传输订阅 Sparkplug B 格式的工业物联网消息
    /// 解析 Protobuf/JSON 编码的 Sparkplug B 消息
    /// </summary>
    public class SparkplugBDriver : IDriver
    {
        private IMqttClient _mqttClient;
        private string _brokerHost = "127.0.0.1";
        private int _brokerPort = 1883;
        private string _groupId = "SparkplugB";
        private string _nodeId = "Node1";
        private DeviceConfig _config;
        private bool _disposed;
        private bool _isDisconnecting;
        private bool _started;

        public string DriverType => "SparkplugB";
        public bool IsConnected => _mqttClient?.IsConnected ?? false;

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _brokerHost = config.GetParam("BrokerHost", "127.0.0.1");
            _brokerPort = config.GetIntParam("BrokerPort", 1883);
            _groupId = config.GetParam("GroupID", "SparkplugB");
            _nodeId = config.GetParam("NodeID", "Node1");

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
                    Logger.Warn(string.Format("Sparkplug B 断开 [{0}]: {1}", _config?.Name, e.Reason));
                    NotifyStatus(false, "Sparkplug B 已断开");
                    await Task.Delay(5000);
                    if (!_isDisconnecting && !_disposed)
                        await ConnectAsync(_config);
                };

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_brokerHost, _brokerPort)
                    .WithClientId(string.Format("SparkplugB_{0}", Guid.NewGuid().ToString("N")))
                    .WithCleanSession()
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60));

                var result = await _mqttClient.ConnectAsync(optionsBuilder.Build());

                if (result.ResultCode == MqttClientConnectResultCode.Success)
                {
                    // Sparkplug B 主题: spBv1.0/{GroupID}/NDATA/{NodeID}
                    string topicFilter = string.Format("spBv1.0/{0}/#", _groupId);
                    var subscribeOptions = new MqttFactory().CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(topicFilter)
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                        .Build();

                    await _mqttClient.SubscribeAsync(subscribeOptions);

                    Logger.Info(string.Format("Sparkplug B 已连接: {0}:{1}, Group={2}, Node={3}", _brokerHost, _brokerPort, _groupId, _nodeId));
                    NotifyStatus(true, string.Format("Sparkplug B 订阅中 ({0}/{1})", _groupId, _nodeId));
                    return true;
                }
                else
                {
                    Logger.Warn(string.Format("Sparkplug B 连接失败: {0}", result.ReasonString));
                    NotifyStatus(false, string.Format("连接失败: {0}", result.ReasonString));
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("Sparkplug B 连接异常 [{0}]: {1}", _config?.Name, ex.Message));
                NotifyStatus(false, string.Format("连接异常: {0}", ex.Message));
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
            Logger.Info(string.Format("Sparkplug B 驱动已启动: {0}, Group={1}, Node={2}", _config?.Name, _groupId, _nodeId));
            return Task.CompletedTask;
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            return Task.FromResult<object>(0);
        }

        private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            if (!_started || _isDisconnecting) return Task.CompletedTask;
            try
            {
                string topic = e.ApplicationMessage.Topic;
                var seg = e.ApplicationMessage.PayloadSegment;
                string payload = Encoding.UTF8.GetString(seg.Array, seg.Offset, seg.Count);

                Logger.Debug(string.Format("Sparkplug B 收到: {0}", topic));

                var cycleItems = new System.Collections.Generic.List<CycleDataItem>();

                if (_config == null || _config.DataPoints.Count == 0)
                {
                    var autoData = new CollectedData
                    {
                        DeviceId = _config?.Id ?? "",
                        DeviceName = _config?.Name ?? "SparkplugB",
                        SourceDriverType = "SparkplugB",
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
                    JToken json = null;
                    try
                    {
                        json = JToken.Parse(payload);
                    }
                    catch { }

                    foreach (var point in _config.DataPoints)
                    {
                        if (!point.IsActive) continue;

                        string value = null;

                        if (json != null)
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

                        if (value == null)
                            value = payload;

                        var data = new CollectedData
                        {
                            DeviceId = _config.Id,
                            DeviceName = _config.Name,
                            SourceDriverType = "SparkplugB",
                            VariableName = point.Name,
                            DataType = point.DataType,
                            Value = value,
                            Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null,
                            Timestamp = DateTime.Now
                        };
                        OnDataReceived?.Invoke(this, new CollectedDataEventArgs(data));

                        cycleItems.Add(new CycleDataItem
                        {
                        VariableId = point.VariableId, 
                            Id = string.Format("{0}|{1}", _config.Name, point.Name),
                            DataType = point.DataType,
                            Value = value,
                            Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null
                        });
                    }
                }

                if (cycleItems.Count > 0)
                {
                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Driver = "sparkplugb",
                        Device = _config?.Name ?? "SparkplugB",
                        DeviceId = _config.Id, Values = cycleItems
                    }));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("Sparkplug B 消息处理异常: {0}", ex.Message));
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
