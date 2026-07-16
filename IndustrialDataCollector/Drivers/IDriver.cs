using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// 采集数据事件参数
    /// </summary>
    public class CollectedDataEventArgs : EventArgs
    {
        public CollectedData Data { get; }
        public CollectedDataEventArgs(CollectedData data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// 驱动状态事件参数
    /// </summary>
    public class DriverStatusEventArgs : EventArgs
    {
        public string DeviceId { get; }
        public string DeviceName { get; }
        public bool IsConnected { get; }
        public string Message { get; }
        public DriverStatusEventArgs(string deviceId, string deviceName, bool isConnected, string message)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            IsConnected = isConnected;
            Message = message;
        }
    }

    /// <summary>
    /// 采集周期批量数据 - 一个采集周期内所有变量的归并消息
    /// </summary>
    public class CycleDataItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("dt")]
        public string DataType { get; set; }

        [JsonProperty("v")]
        public object Value { get; set; }

        [JsonProperty("u")]
        public string Unit { get; set; }

        [JsonProperty("tag")] public string Tag { get; set; } = "";
        [JsonProperty("tag_cn")] public string TagCn { get; set; } = "";

        /// <summary>v2.0: Tag 统一身份系统中的永久变量 ID</summary>
        [JsonProperty("variableId")] public string VariableId { get; set; } = "";
    }

    /// <summary>
    /// 采集周期批量数据载体
    /// </summary>
    /// <summary>
    /// 历史数据记录（MCP 查询用）
    /// </summary>
    public class HistoryRecord
    {
        public string device { get; set; }
        public string variable { get; set; }
        public string value { get; set; }
        public string unit { get; set; }
        public string timestamp { get; set; }
        public string tag { get; set; }
        public string tag_cn { get; set; }
    }

    public class CycleDataBatch
    {
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("driver")]
        public string Driver { get; set; }

        [JsonProperty("device")]
        public string Device { get; set; }

        [JsonProperty("device_id")]
        public string DeviceId { get; set; } = "";

        [JsonProperty("values")]
        public List<CycleDataItem> Values { get; set; }
    }

    public class CycleDataEventArgs : EventArgs
    {
        public CycleDataBatch Batch { get; }
        public CycleDataEventArgs(CycleDataBatch batch) { Batch = batch; }
    }

    /// <summary>
    /// 设备驱动接口（所有 PLC 驱动必须实现）
    /// </summary>
    public interface IDriver : IDisposable
    {
        /// <summary>驱动类型名称</summary>
        string DriverType { get; }

        /// <summary>是否已连接</summary>
        bool IsConnected { get; }

        /// <summary>收到采集数据事件（逐变量，用于实时监控表格）</summary>
        event EventHandler<CollectedDataEventArgs> OnDataReceived;

        /// <summary>采集周期完成事件（批量，用于 MQTT 归并发布）</summary>
        event EventHandler<CycleDataEventArgs> OnCycleCompleted;

        /// <summary>驱动状态变化事件</summary>
        event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        /// <summary>连接到设备</summary>
        Task<bool> ConnectAsync(DeviceConfig config);

        /// <summary>断开连接</summary>
        Task DisconnectAsync();

        /// <summary>开始周期性采集</summary>
        Task StartCollectAsync(CancellationToken token);

        /// <summary>读取单个数据点</summary>
        Task<object> ReadAsync(DataPoint point);
    }
}
