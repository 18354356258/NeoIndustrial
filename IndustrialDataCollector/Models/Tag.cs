using System;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// v2.0 Tag 统一身份实体
    /// Tag 是语义树（人类理解）和采集层（机器采集）之间的胶水层，
    /// 提供永久不变的身份标识 TagId，确保下游系统永不断联。
    /// TagCn 不持久化，由 TagMappingService 从语义树实时派生。
    /// </summary>
    public class Tag
    {
        /// <summary>永久唯一身份标识，格式 tag_{12位}，永不改变</summary>
        [JsonProperty("tagId")]
        public string TagId { get; set; } = "";

        /// <summary>关联的变量永久 ID（DataPoint.VariableId）</summary>
        [JsonProperty("variableId")]
        public string VariableId { get; set; } = "";

        /// <summary>关联的设备 ID（DeviceConfig.Id）</summary>
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; } = "";

        /// <summary>变量名称（用于 TagCn 派生）</summary>
        [JsonProperty("variableName")]
        public string VariableName { get; set; } = "";

        /// <summary>软删除标记</summary>
        [JsonProperty("isActive")]
        public bool IsActive { get; set; } = true;

        /// <summary>创建时间</summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>生成新的 TagId（tag_ + GUID 前 12 位）</summary>
        public static string GenerateId()
        {
            return "tag_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }
    }
}
