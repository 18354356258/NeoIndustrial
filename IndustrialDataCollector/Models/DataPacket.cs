using System.Collections.Generic;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// v2.0 归一化数据点 —— 管线中传递的单个变量值
    /// </summary>
    public class DataPacket
    {
        /// <summary>变量永久身份（GUID）</summary>
        public string VariableId { get; set; } = "";

        /// <summary>Tag 系统分配的全局唯一 tag_id</summary>
        public string TagId { get; set; } = "";

        /// <summary>中文语义路径（设备全路径 + 变量名，实时派生）</summary>
        public string TagCn { get; set; } = "";

        /// <summary>设备名称</summary>
        public string DeviceName { get; set; } = "";

        /// <summary>设备 GUID</summary>
        public string DeviceId { get; set; } = "";

        /// <summary>驱动标识</summary>
        public string Driver { get; set; } = "";

        /// <summary>变量名（短名）</summary>
        public string VariableName { get; set; } = "";

        /// <summary>数据类型</summary>
        public string DataType { get; set; } = "";

        /// <summary>当前值</summary>
        public object Value { get; set; }

        /// <summary>单位</summary>
        public string Unit { get; set; } = "";
    }

    /// <summary>
    /// v2.0 归一化数据批次 —— 一设备一轮采集的标准化输出
    /// </summary>
    public class DataBatch
    {
        /// <summary>采集时间戳（Unix 毫秒）</summary>
        public long Timestamp { get; set; }

        /// <summary>驱动标识</summary>
        public string Driver { get; set; } = "";

        /// <summary>设备名称</summary>
        public string Device { get; set; } = "";

        /// <summary>设备 GUID</summary>
        public string DeviceId { get; set; } = "";

        /// <summary>MqttPublishMode 发布模式: Original / Resolved</summary>
        public string MqttPublishMode { get; set; } = "Resolved";

        /// <summary>归一化变量值列表</summary>
        public List<DataPacket> Values { get; set; } = new List<DataPacket>();
    }
}
