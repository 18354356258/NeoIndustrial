using System;
using System.Collections.Generic;
using System.Linq;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 设备克隆服务 — 深拷贝设备配置并复制语义关系，支持快速创建同类型设备
    /// </summary>
    public class DeviceCloneService
    {
        private static readonly Lazy<DeviceCloneService> _instance =
            new Lazy<DeviceCloneService>(() => new DeviceCloneService());
        public static DeviceCloneService Instance => _instance.Value;

        private readonly ConfigService _configService;
        private readonly SemanticService _semanticService;

        private DeviceCloneService()
        {
            _configService = ConfigService.Instance;
            _semanticService = SemanticService.Instance;
        }

        /// <summary>
        /// 克隆设备：深拷贝配置 + 重新生成ID + 复制语义关系
        /// </summary>
        public CloneResult CloneDevice(CloneOptions options)
        {
            if (options == null)
                return new CloneResult { Success = false, Error = "克隆选项为空" };

            var result = new CloneResult();

            try
            {
                // 1. 校验
                string error;
                if (!ValidateCloneOptions(options, out error))
                {
                    result.Success = false;
                    result.Error = error;
                    result.SourceDevice = options.SourceDeviceId;
                    Logger.Error("设备克隆验证失败: " + error);
                    return result;
                }

                // 2. 加载所有设备，找到源设备
                var devices = _configService.LoadDevices();
                var sourceDevice = devices.FirstOrDefault(d => d.Id == options.SourceDeviceId);
                if (sourceDevice == null)
                {
                    result.Success = false;
                    result.Error = "源设备不存在";
                    result.SourceDevice = options.SourceDeviceId;
                    return result;
                }

                string oldDeviceId = sourceDevice.Id;

                // 3. JSON 深拷贝
                var clonedDevice = sourceDevice.Clone();

                // 4. 生成新的 DeviceId
                string newDeviceId = Guid.NewGuid().ToString();
                clonedDevice.Id = newDeviceId;

                // 5. 更新设备名称和分组
                clonedDevice.Name = options.NewDeviceName;
                if (!string.IsNullOrEmpty(options.NewGroupPath))
                {
                    clonedDevice.TagPathCn = options.NewGroupPath;
                    clonedDevice.Group = options.NewGroupPath;
                }

                // 6. 遍历 DataPoints，生成新的 VariableId，建立映射
                var oldVarIdToNewVarId = new Dictionary<string, string>();
                if (options.CloneVariables && clonedDevice.DataPoints != null)
                {
                    foreach (var dp in clonedDevice.DataPoints)
                    {
                        string oldVarId = dp.VariableId;
                        string newVarId = TagGenerationService.GenerateVariableId();
                        dp.VariableId = newVarId;
                        dp.Id = Guid.NewGuid().ToString(); // 同时更新 DataPoint.Id

                        if (!string.IsNullOrEmpty(oldVarId))
                            oldVarIdToNewVarId[oldVarId] = newVarId;

                        // 自动生成中文标签
                        if (options.AutoGenerateTags && string.IsNullOrEmpty(dp.TagCn))
                        {
                            dp.TagCn = dp.Name;
                        }
                    }
                    result.ClonedVariables = clonedDevice.DataPoints.Count;
                }

                // 如果不克隆变量，清空 DataPoints（保留连接参数裸设备）
                if (!options.CloneVariables)
                {
                    clonedDevice.DataPoints = new List<DataPoint>();
                }

                // 不清洗策略 — 清空所有清洗字段
                if (!options.CloneCleaningStrategies && clonedDevice.DataPoints != null)
                {
                    foreach (var dp in clonedDevice.DataPoints)
                    {
                        ResetCleaningFields(dp);
                    }
                }

                // 不克隆驱动参数 — 清空连接参数
                if (!options.CloneDriverParams)
                {
                    clonedDevice.ConnectionParams = new Dictionary<string, string>();
                }

                // 7. 添加到设备列表并保存
                devices.Add(clonedDevice);
                _configService.SaveDevices(devices); // 保存会触发 SemanticService.SyncFromDeviceConfigs

                // 8. 克隆语义关系
                int clonedRelations = 0;
                if (options.CloneSemanticRelations)
                {
                    clonedRelations = CloneSemanticRelations(oldDeviceId, newDeviceId);
                }
                result.ClonedRelations = clonedRelations;

                // 9. 设置结果
                result.Success = true;
                result.SourceDevice = sourceDevice.Name;
                result.NewDeviceName = options.NewDeviceName;
                result.NewDeviceId = newDeviceId;
                result.ClonedVariables = result.ClonedVariables; // already set

                Logger.Info(string.Format("设备克隆成功: {0} → {1} [{2}]，变量 {3}，关系 {4}",
                    sourceDevice.Name, options.NewDeviceName, newDeviceId,
                    result.ClonedVariables, clonedRelations));
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = "克隆异常: " + ex.Message;
                Logger.Error("设备克隆失败: " + ex.Message);
            }

            return result;
        }

        /// <summary>
        /// 验证克隆选项：源设备存在 + 新名称不重复
        /// </summary>
        private bool ValidateCloneOptions(CloneOptions options, out string error)
        {
            error = "";

            if (string.IsNullOrEmpty(options.SourceDeviceId))
            {
                error = "源设备ID为空";
                return false;
            }

            if (string.IsNullOrEmpty(options.NewDeviceName))
            {
                error = "新设备名称为空";
                return false;
            }

            try
            {
                var devices = _configService.LoadDevices();

                // 检查源设备存在
                var sourceDevice = devices.FirstOrDefault(d => d.Id == options.SourceDeviceId);
                if (sourceDevice == null)
                {
                    error = string.Format("源设备不存在: {0}", options.SourceDeviceId);
                    return false;
                }

                // 检查新名称不重复
                bool nameExists = devices.Any(d =>
                    d.Id != options.SourceDeviceId
                    && string.Equals(d.Name, options.NewDeviceName, StringComparison.OrdinalIgnoreCase));
                if (nameExists)
                {
                    error = string.Format("设备名称已存在: {0}", options.NewDeviceName);
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = "验证失败: " + ex.Message;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 克隆语义关系：从旧设备复制所有变量关系到新设备
        /// </summary>
        private int CloneSemanticRelations(string oldDeviceId, string newDeviceId)
        {
            int count = 0;
            try
            {
                // 查找旧设备和新设备的语义节点
                var oldDeviceNode = _semanticService.GetNodeBySource("device", oldDeviceId);
                var newDeviceNode = _semanticService.GetNodeBySource("device", newDeviceId);

                if (oldDeviceNode == null || newDeviceNode == null)
                {
                    Logger.Debug(string.Format("语义节点未找到: old={0}, new={1}",
                        oldDeviceNode != null ? "found" : "null",
                        newDeviceNode != null ? "found" : "null"));
                    return 0;
                }

                // 获取旧设备和新设备的变量子节点
                var oldVarNodes = _semanticService.GetChildren(oldDeviceNode.Id, "variable");
                var newVarNodes = _semanticService.GetChildren(newDeviceNode.Id, "variable");

                if (oldVarNodes == null || oldVarNodes.Count == 0
                    || newVarNodes == null || newVarNodes.Count == 0)
                {
                    Logger.Debug("变量节点为空，跳过关系克隆");
                    return 0;
                }

                // 建立变量名 → 新节点ID 映射
                var nameToNewNodeId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var newNode in newVarNodes)
                {
                    if (!string.IsNullOrEmpty(newNode.Name))
                        nameToNewNodeId[newNode.Name] = newNode.Id;
                }

                // 建立旧节点ID → 变量名映射
                var oldNodeIdToName = new Dictionary<string, string>();
                foreach (var oldNode in oldVarNodes)
                {
                    if (!string.IsNullOrEmpty(oldNode.Name))
                        oldNodeIdToName[oldNode.Id] = oldNode.Name;
                }
                // 同时收录新节点自身（用于变量→变量关系的 target 映射）
                var newNodeIdToName = new Dictionary<string, string>();
                foreach (var newNode in newVarNodes)
                {
                    if (!string.IsNullOrEmpty(newNode.Name))
                        newNodeIdToName[newNode.Id] = newNode.Name;
                }

                // 遍历旧节点的所有变量关系
                foreach (var oldVarNode in oldVarNodes)
                {
                    string varName = oldNodeIdToName.ContainsKey(oldVarNode.Id)
                        ? oldNodeIdToName[oldVarNode.Id] : oldVarNode.Name;

                    var relations = _semanticService.GetVariableRelations(oldVarNode.Id);
                    if (relations == null || relations.Count == 0) continue;

                    foreach (var rel in relations)
                    {
                        // 找到对应的新源节点ID
                        string newSourceNodeId;
                        if (!nameToNewNodeId.TryGetValue(varName, out newSourceNodeId))
                            continue;

                        // 处理目标：如果是变量→变量关系，需要将旧 target ID 映射为新 target ID
                        string newTargetVariableNodeId = "";
                        if (rel.TargetType == "variable" && !string.IsNullOrEmpty(rel.TargetVariableNodeId))
                        {
                            // 通过旧 target 节点名找到新 target 节点ID
                            string targetVarName;
                            if (oldNodeIdToName.TryGetValue(rel.TargetVariableNodeId, out targetVarName)
                                || newNodeIdToName.TryGetValue(rel.TargetVariableNodeId, out targetVarName))
                            {
                                nameToNewNodeId.TryGetValue(targetVarName, out newTargetVariableNodeId);
                            }
                        }

                        // 创建新关系
                        var newRel = new SemanticVariableRelation
                        {
                            Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                            VariableNodeId = newSourceNodeId,
                            RelationType = rel.RelationType,
                            TargetType = rel.TargetType,
                            TargetDatasourceId = rel.TargetDatasourceId,
                            TargetTableName = rel.TargetTableName,
                            TargetFieldName = rel.TargetFieldName,
                            TargetVariableNodeId = newTargetVariableNodeId,
                            ConstantValue = rel.ConstantValue,
                            Expression = rel.Expression,
                            Unit = rel.Unit,
                            Description = rel.Description
                        };

                        if (rel.ConditionVariableIds != null && rel.ConditionVariableIds.Count > 0)
                        {
                            newRel.ConditionVariableIds = new List<string>(rel.ConditionVariableIds);
                        }

                        _semanticService.SaveVariableRelation(newRel);
                        count++;
                    }
                }

                Logger.Info(string.Format("语义关系克隆完成: {0} 条", count));
            }
            catch (Exception ex)
            {
                Logger.Error("克隆语义关系失败: " + ex.Message);
            }

            return count;
        }

        /// <summary>
        /// 重置 DataPoint 的所有清洗字段
        /// </summary>
        private void ResetCleaningFields(DataPoint dp)
        {
            dp.CleanEnabled = false;
            dp.DeadBandEnabled = false;
            dp.DeadBand = 0.0;
            dp.ClipEnabled = false;
            dp.ClipMin = 0.0;
            dp.ClipMax = 100.0;
            dp.OutlierEnabled = false;
            dp.SigmaThreshold = 3.0;
            dp.NanFilterEnabled = false;
            dp.NanFilterNaN = true;
            dp.NanFilterInf = true;
            dp.NanFilterNegative = false;
            dp.NanFilterReplacement = 0.0;
            dp.FreezeEnabled = false;
            dp.FreezeWindow = 10;
            dp.SpikeEnabled = false;
            dp.SpikeWindow = 5;
            dp.SpikeThreshold = 3.0;
            dp.RocLimitEnabled = false;
            dp.RocLimitMax = 1.0;
            dp.IqrEnabled = false;
            dp.IqrMultiplier = 1.5;
            dp.RangeEnabled = false;
            dp.RangeMin = 0.0;
            dp.RangeMax = 100.0;
        }
    }
}
