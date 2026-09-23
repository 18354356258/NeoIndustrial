using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Drivers;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services.Sinks;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// v2.0 数据流引擎 —— 统一管线出口，将原始采集数据归一化后路由到可插拔 ISink
    /// 
    /// 管道链路: Driver → CycleDataBatch → [WAL] → DataStreamEngine → [MqttSink, DatabaseSink, ...]
    /// 
    /// 职责:
    /// 1. CycleDataBatch → DataBatch 归一化
    /// 2. 按设备 MqttPublishMode 决定输出格式
    /// 3. 并发路由到所有 ISink
    /// 4. 每个 sink 独立返回成功/失败
    /// </summary>
    public class DataStreamEngine
    {
        private static readonly Lazy<DataStreamEngine> _instance =
            new Lazy<DataStreamEngine>(() => new DataStreamEngine());

        public static DataStreamEngine Instance => _instance.Value;

        private readonly List<ISink> _sinks = new List<ISink>();
        private readonly ConfigService _configService = ConfigService.Instance;
        private readonly TagMappingService _tagMapping = TagMappingService.Instance;

        private DataStreamEngine()
        {
            // 默认注册 MQTT + DB 两个出口
            _sinks.Add(new MqttSink());
            _sinks.Add(new DatabaseSink());
        }

        /// <summary>注册自定义出口（MCP / 外部扩展）</summary>
        public void RegisterSink(ISink sink)
        {
            if (sink == null) return;
            lock (_sinks)
            {
                if (!_sinks.Any(s => s.Name == sink.Name))
                    _sinks.Add(sink);
            }
        }

        /// <summary>移除出口</summary>
        public void RemoveSink(string name)
        {
            lock (_sinks)
            {
                _sinks.RemoveAll(s => s.Name == name);
            }
        }

        /// <summary>获取已注册出口列表</summary>
        public IReadOnlyList<ISink> GetSinks()
        {
            lock (_sinks)
            {
                return _sinks.ToList();
            }
        }

        /// <summary>
        /// 核心路由方法：归一化 → 查 publish mode → 并发写所有出口 → 返回各出口成功/失败
        /// </summary>
        public async Task<Dictionary<string, bool>> RouteAsync(CycleDataBatch cycleBatch, CancellationToken ct = default)
        {
            var results = new Dictionary<string, bool>();
            if (cycleBatch == null || cycleBatch.Values == null || cycleBatch.Values.Count == 0)
                return results;

            // 1. CycleDataBatch → DataBatch 归一化
            var dataBatch = Normalize(cycleBatch);
            if (dataBatch == null) return results;

            // 2. 查询设备 MqttPublishMode
            var device = _configService.GetAllDevices()?
                .Find(d => d.Name == cycleBatch.Device || d.Id == dataBatch.DeviceId);
            if (device != null)
            {
                dataBatch.MqttPublishMode = device.MqttPublishMode ?? "Resolved";
            }

            // 3. 并发路由到所有出口
            ISink[] sinkArray;
            lock (_sinks) { sinkArray = _sinks.ToArray(); }

            var tasks = sinkArray.Select(async sink =>
            {
                bool ok = await sink.WriteAsync(dataBatch, ct);
                lock (results) { results[sink.Name] = ok; }
            });

            await Task.WhenAll(tasks);
            return results;
        }

        /// <summary>
        /// 归一化: CycleDataBatch (原始驱动字段) → DataBatch (tag_id + tag_cn 化)
        /// </summary>
        private DataBatch Normalize(CycleDataBatch batch)
        {
            if (batch == null || batch.Values == null) return null;

            var deviceConfig = _configService.GetAllDevices()?
                .Find(d => d.Name == batch.Device);
            string deviceId = deviceConfig?.Id ?? "";

            var packets = new List<DataPacket>();
            foreach (var item in batch.Values)
            {
                var packet = new DataPacket
                {
                    VariableId = item.VariableId ?? "",
                    TagId = string.IsNullOrEmpty(item.VariableId) ? "" : _tagMapping.GetTagId(item.VariableId) ?? "",
                    TagCn = item.TagCn ?? "",
                    DeviceName = batch.Device ?? "",
                    DeviceId = deviceId,
                    Driver = batch.Driver ?? "",
                    VariableName = ExtractVariableName(item.Id),
                    DataType = item.DataType ?? "",
                    Value = item.Value,
                    Unit = item.Unit ?? ""
                };
                packets.Add(packet);
            }

            return new DataBatch
            {
                Timestamp = batch.Timestamp,
                Driver = batch.Driver ?? "",
                Device = batch.Device ?? "",
                DeviceId = deviceId,
                Values = packets
            };
        }

        /// <summary>从 "设备名|变量名" 格式提取变量名</summary>
        private static string ExtractVariableName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            int idx = id.LastIndexOf('|');
            return idx >= 0 ? id.Substring(idx + 1) : id;
        }
    }
}
