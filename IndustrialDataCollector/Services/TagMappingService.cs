using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// v2.0 Tag ↔ Variable 双向映射 + TagCn 实时派生
    /// TagCn 不持久化，从语义树路径实时计算，确保永远与语义树一致。
    /// </summary>
    public class TagMappingService
    {
        private static readonly Lazy<TagMappingService> _instance =
            new Lazy<TagMappingService>(() => new TagMappingService());
        public static TagMappingService Instance => _instance.Value;

        // TagId → Tag
        private readonly ConcurrentDictionary<string, Tag> _tagById = new ConcurrentDictionary<string, Tag>();
        // VariableId → TagId
        private readonly ConcurrentDictionary<string, string> _variableToTagId = new ConcurrentDictionary<string, string>();
        // 锁用于批量更新
        private readonly object _lock = new object();

        private TagMappingService() { }

        /// <summary>注册 Tag（启动迁移或新增变量时调用）</summary>
        public void Register(Tag tag)
        {
            if (string.IsNullOrEmpty(tag.TagId) || string.IsNullOrEmpty(tag.VariableId))
                return;

            lock (_lock)
            {
                _tagById[tag.TagId] = tag;
                _variableToTagId[tag.VariableId] = tag.TagId;
            }
        }

        /// <summary>批量注册（启动迁移时调用）</summary>
        public void RegisterBatch(IEnumerable<Tag> tags)
        {
            lock (_lock)
            {
                foreach (var tag in tags)
                {
                    if (string.IsNullOrEmpty(tag.TagId) || string.IsNullOrEmpty(tag.VariableId))
                        continue;
                    _tagById[tag.TagId] = tag;
                    _variableToTagId[tag.VariableId] = tag.TagId;
                }
            }
        }

        /// <summary>通过 VariableId 查找 TagId</summary>
        public string GetTagId(string variableId)
        {
            if (string.IsNullOrEmpty(variableId)) return null;
            _variableToTagId.TryGetValue(variableId, out var tagId);
            return tagId;
        }

        /// <summary>通过 TagId 查找 Tag</summary>
        public Tag GetTag(string tagId)
        {
            if (string.IsNullOrEmpty(tagId)) return null;
            _tagById.TryGetValue(tagId, out var tag);
            return tag;
        }

        /// <summary>通过 VariableId 查找 Tag</summary>
        public Tag GetTagByVariable(string variableId)
        {
            var tagId = GetTagId(variableId);
            return tagId != null ? GetTag(tagId) : null;
        }

        /// <summary>
        /// 实时派生 TagCn = 语义路径 + "/" + 变量名
        /// 不持久化，永远跟语义树一致
        /// </summary>
        public string GetTagCn(string variableId)
        {
            var tag = GetTagByVariable(variableId);
            if (tag == null) return "";

            return DeriveTagCn(tag.DeviceId, tag.VariableName);
        }

        /// <summary>通过 TagId 派生 TagCn</summary>
        public string GetTagCnByTagId(string tagId)
        {
            var tag = GetTag(tagId);
            if (tag == null) return "";

            return DeriveTagCn(tag.DeviceId, tag.VariableName);
        }

        /// <summary>语义树变化后更新：用新路径重算所有 Tag 的 TagCn（内存缓存标记，实际不存）</summary>
        public void OnSemanticTreeChanged(string deviceId)
        {
            // TagCn 是实时派生的，语义树变了下次 GetTagCn 自动拿到新值
            // 这里只做日志记录
            var affected = _tagById.Values.Count(t => t.DeviceId == deviceId && t.IsActive);
            if (affected > 0)
                Logger.Debug($"语义树变化: 设备 {deviceId} 的 {affected} 个 Tag 将在下次查询时自动更新 tag_cn");
        }

        /// <summary>软删除某变量对应的 Tag</summary>
        public void SoftDelete(string variableId)
        {
            var tag = GetTagByVariable(variableId);
            if (tag != null)
                tag.IsActive = false;
        }

        /// <summary>软删除某设备下所有 Tag</summary>
        public void SoftDeleteByDevice(string deviceId)
        {
            foreach (var tag in _tagById.Values.Where(t => t.DeviceId == deviceId))
                tag.IsActive = false;
        }

        /// <summary>获取活跃 Tag 总数（用于监控）</summary>
        public int ActiveCount => _tagById.Values.Count(t => t.IsActive);

        /// <summary>获取所有 Tag（含非活跃）</summary>
        public List<Tag> GetAllTags() => _tagById.Values.ToList();

        /// <summary>获取所有活跃 Tag</summary>
        public List<Tag> GetActiveTags() => _tagById.Values.Where(t => t.IsActive).ToList();

        /// <summary>获取某设备下所有活跃 Tag</summary>
        public List<Tag> GetTagsByDevice(string deviceId)
        {
            return _tagById.Values.Where(t => t.DeviceId == deviceId && t.IsActive).ToList();
        }

        /// <summary>清空映射（用于 reload_config）</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _tagById.Clear();
                _variableToTagId.Clear();
            }
        }

        // ── TagCn 派生逻辑 ──

        private string DeriveTagCn(string deviceId, string variableName)
        {
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(variableName))
                return variableName ?? "";

            try
            {
                // 从语义服务获取设备完整路径
                var devicePath = SemanticService.Instance?.GetFullPath(deviceId);
                if (!string.IsNullOrEmpty(devicePath))
                    return devicePath + "/" + variableName;

                // 语义服务不可用时，从 ConfigService 获取设备名兜底
                var devices = ConfigService.Instance?.LoadDevices();
                var device = devices?.Find(d => d.Id == deviceId);
                var deviceName = device?.Name ?? deviceId;
                return deviceName + "/" + variableName;
            }
            catch
            {
                return variableName;
            }
        }
    }
}
