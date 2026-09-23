using System;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// v2.0 Tag ID 生成服务
    /// 确保 DataPoint.VariableId 和 Tag.TagId 的格式一致性
    /// </summary>
    public static class TagGenerationService
    {
        /// <summary>为不带动 VariableId 的变量生成永久 ID</summary>
        public static string GenerateVariableId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        /// <summary>生成 TagId（tag_ + 12 位 hex）</summary>
        public static string GenerateTagId()
        {
            return Tag.GenerateId();
        }

        /// <summary>从 VariableId 派生 TagId（v2.0 不强制一一对应，TagId 独立生成）</summary>
        public static string GenerateTagIdForVariable(string variableId)
        {
            return Tag.GenerateId();
        }
    }
}
