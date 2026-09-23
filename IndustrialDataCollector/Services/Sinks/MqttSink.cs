using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Services.Sinks
{
    /// <summary>
    /// v2.0 MQTT 出口 —— 按 MqttPublishMode 生成不同格式的 MQTT 消息
    /// Original: 保留 DataPoint 原始字段
    /// Resolved: tag_id + tag_cn 替换 tag 字段
    /// </summary>
    public class MqttSink : ISink
    {
        private readonly MqttPublishService _mqtt;

        public MqttSink()
        {
            _mqtt = MqttPublishService.Instance;
        }

        private static string ExtractSimpleVarName(DataPacket item)
        {
            if (!string.IsNullOrEmpty(item.VariableName))
                return item.VariableName;
            if (!string.IsNullOrEmpty(item.TagCn))
            {
                int lastSlash = item.TagCn.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < item.TagCn.Length - 1)
                    return item.TagCn.Substring(lastSlash + 1);
                return item.TagCn;
            }
            return "value";
        }

        public string Name => "MQTT";

        public bool IsReady => _mqtt.IsConnected;

        public async Task<bool> WriteAsync(DataBatch batch, CancellationToken cancellationToken)
        {
            if (!IsReady || batch == null || batch.Values == null || batch.Values.Count == 0)
                return false;

            try
            {
                var config = _mqtt.GetConfig();
                string topic = string.Format("{0}/{1}", config.TopicPrefix, batch.Device);
                string publishMode = batch.MqttPublishMode ?? "Resolved";

                object payload;
                if (publishMode == "Original")
                {
                    // 原始格式：保留 DataPoint 原有字段
                    payload = new
                    {
                        timestamp = batch.Timestamp,
                        driver = batch.Driver,
                        device = batch.Device,
                        values = batch.Values.Select(v => new
                        {
                            id = string.Format("{0}|{1}", v.DeviceName, v.VariableName),
                            dt = v.DataType,
                            v = v.Value,
                            u = v.Unit,
                            tag_id = v.TagId,
                            tag_cn = v.TagCn
                        }).ToList()
                    };
                }
                else
                {
                    // Resolved 格式（默认）：规范化的 tag_id + tag_cn
                    payload = new
                    {
                        timestamp = batch.Timestamp,
                        driver = batch.Driver,
                        device = batch.Device,
                        values = batch.Values.Select(v => new
                        {
                            id = v.TagId,
                            v = v.Value,
                            u = v.Unit,
                            tag_cn = v.TagCn
                        }).ToList()
                    };
                }

                var jsonData = JsonConvert.SerializeObject(payload);
                await _mqtt.PublishRawAsync(topic, jsonData, config.Qos);

                // 规范化模式：额外发布逐变量子话题，支持订阅端 # 收全部 / /变量名 收单个
                if (publishMode != "Original")
                {
                    foreach (var item in batch.Values)
                    {
                        string varName = item.VariableName ?? ExtractSimpleVarName(item);
                        string subTopic = string.Format("{0}/{1}/{2}", config.TopicPrefix, batch.Device, varName);

                        var singlePayload = new
                        {
                            timestamp = batch.Timestamp,
                            driver = batch.Driver,
                            device = batch.Device,
                            variable = varName,
                            value = item.Value,
                            unit = item.Unit,
                            data_type = item.DataType,
                            tag_id = item.TagId,
                            tag_cn = item.TagCn
                        };

                        await _mqtt.PublishRawAsync(subTopic, JsonConvert.SerializeObject(singlePayload), config.Qos);
                    }
                }

                Logger.Debug(string.Format("MQTT sink published {0} values → {1}", batch.Values.Count, topic));
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("MQTT sink error: " + ex.Message);
                return false;
            }
        }
    }
}
