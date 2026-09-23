using System;
using System.Collections.Generic;
using System.Linq;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// v2.0 Tag 迁移服务
    /// 系统启动时自动扫描所有 DeviceConfig.DataPoints，
    /// 为缺失 VariableId 的变量生成 ID，为缺失 Tag 的变量创建 Tag。
    /// </summary>
    public class TagMigrationService
    {
        /// <summary>
        /// 执行迁移，返回统计信息
        /// </summary>
        public static MigrationResult Migrate(List<DeviceConfig> devices)
        {
            var result = new MigrationResult();
            var mapping = TagMappingService.Instance;
            var existingTagIds = new HashSet<string>(mapping.GetAllTags().Select(t => t.TagId));

            foreach (var device in devices)
            {
                if (device.DataPoints == null || device.DataPoints.Count == 0)
                    continue;

                foreach (var point in device.DataPoints)
                {
                    // 1. 确保 VariableId 存在
                    if (string.IsNullOrEmpty(point.VariableId))
                    {
                        point.VariableId = TagGenerationService.GenerateVariableId();
                        result.VariableIdsGenerated++;
                    }

                    // 2. 检查是否已有 Tag
                    var existingTag = mapping.GetTagByVariable(point.VariableId);
                    if (existingTag != null)
                    {
                        result.TagsSkipped++;
                        continue;
                    }

                    // 3. 创建 Tag
                    var tag = new Tag
                    {
                        TagId = TagGenerationService.GenerateTagId(),
                        VariableId = point.VariableId,
                        DeviceId = device.Id,
                        VariableName = point.Name,
                        IsActive = point.IsActive,
                        CreatedAt = DateTime.Now
                    };

                    mapping.Register(tag);
                    result.TagsCreated++;

                    Logger.Debug($"Tag 迁移: {tag.TagId} ← {point.VariableId} ({device.Name}/{point.Name})");
                }
            }

            // 4. 清理孤立 Tag（Variable 已删除但 Tag 还在的）
            var activeVariableIds = new HashSet<string>(
                devices.SelectMany(d => d.DataPoints ?? Enumerable.Empty<DataPoint>())
                       .Select(p => p.VariableId)
            );

            foreach (var tag in mapping.GetAllTags())
            {
                if (!activeVariableIds.Contains(tag.VariableId) && tag.IsActive)
                {
                    mapping.SoftDelete(tag.VariableId);
                    result.OrphanTagsCleaned++;
                }
            }

            return result;
        }

        public class MigrationResult
        {
            public int VariableIdsGenerated { get; set; }
            public int TagsCreated { get; set; }
            public int TagsSkipped { get; set; }
            public int OrphanTagsCleaned { get; set; }

            public override string ToString()
            {
                return $"Tag 迁移完成: VariableId 生成 {VariableIdsGenerated} | " +
                       $"Tag 创建 {TagsCreated} | Tag 跳过 {TagsSkipped} | 孤立清理 {OrphanTagsCleaned}";
            }
        }
    }
}
