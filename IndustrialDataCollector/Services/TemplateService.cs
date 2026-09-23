using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 配置模板引擎 — 模板 CRUD、模板生成（从设备提取）、模板应用（到设备）、模板覆盖
    /// 持久化: {exe目录}/templates/*.json
    /// </summary>
    public class TemplateService
    {
        private static readonly Lazy<TemplateService> _instance =
            new Lazy<TemplateService>(() => new TemplateService());
        public static TemplateService Instance => _instance.Value;

        private readonly string _templateDir;
        private readonly ConfigService _configService;
        private readonly SemanticService _semanticService;

        private TemplateService()
        {
            _templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
            if (!Directory.Exists(_templateDir))
            {
                Directory.CreateDirectory(_templateDir);
                Logger.Info("已创建模板目录: " + _templateDir);
            }
            _configService = ConfigService.Instance;
            _semanticService = SemanticService.Instance;
        }

        // ════════════════════════════════════════════════════════════════
        //  模板 CRUD
        // ════════════════════════════════════════════════════════════════

        /// <summary>扫描 templates/*.json 并返回所有模板</summary>
        public List<DeviceTemplate> ListAll()
        {
            var templates = new List<DeviceTemplate>();
            try
            {
                if (!Directory.Exists(_templateDir))
                    return templates;

                foreach (var file in Directory.GetFiles(_templateDir, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file, Encoding.UTF8);
                        var template = JsonConvert.DeserializeObject<DeviceTemplate>(json);
                        if (template != null)
                            templates.Add(template);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(string.Format("加载模板文件失败 [{0}]: {1}",
                            Path.GetFileName(file), ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("扫描模板目录失败: " + ex.Message);
            }
            return templates.OrderBy(t => t.Category).ThenBy(t => t.TemplateName).ToList();
        }

        /// <summary>加载单个模板</summary>
        public DeviceTemplate Load(string templateId)
        {
            try
            {
                string filePath = GetTemplatePath(templateId);
                if (!File.Exists(filePath))
                {
                    Logger.Error("模板文件不存在: " + templateId);
                    return null;
                }
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonConvert.DeserializeObject<DeviceTemplate>(json);
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("加载模板失败 [{0}]: {1}", templateId, ex.Message));
                return null;
            }
        }

        /// <summary>保存模板到 {templateId}.json</summary>
        public void Save(DeviceTemplate template)
        {
            if (template == null)
                throw new ArgumentNullException("template");

            try
            {
                if (string.IsNullOrEmpty(template.TemplateId))
                    template.TemplateId = "tpl_" + Guid.NewGuid().ToString("N").Substring(0, 12);

                if (template.CreatedAt == default(DateTime))
                    template.CreatedAt = DateTime.Now;
                template.UpdatedAt = DateTime.Now;

                string filePath = GetTemplatePath(template.TemplateId);
                var json = JsonConvert.SerializeObject(template, Formatting.Indented);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                Logger.Info(string.Format("模板已保存: {0} [{1}]", template.TemplateName, template.TemplateId));
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("保存模板失败 [{0}]: {1}", template.TemplateName, ex.Message));
                throw;
            }
        }

        /// <summary>删除模板文件，返回 false 表示文件不存在</summary>
        public bool Delete(string templateId)
        {
            try
            {
                string filePath = GetTemplatePath(templateId);
                if (!File.Exists(filePath))
                    return false;
                File.Delete(filePath);
                Logger.Info("模板已删除: " + templateId);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("删除模板失败 [{0}]: {1}", templateId, ex.Message));
                return false;
            }
        }

        /// <summary>从所有模板中提取去重分类路径</summary>
        public List<string> ListCategories()
        {
            var categories = new HashSet<string>();
            foreach (var tpl in ListAll())
            {
                if (!string.IsNullOrEmpty(tpl.Category))
                    categories.Add(tpl.Category);
            }
            return categories.OrderBy(c => c).ToList();
        }

        // ════════════════════════════════════════════════════════════════
        //  模板生成 — 从设备提取配置生成模板
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 从指定设备提取所有可复用配置，生成模板
        /// </summary>
        public DeviceTemplate GenerateFromDevice(string sourceDeviceId, TemplateGenOptions options)
        {
            if (string.IsNullOrEmpty(sourceDeviceId))
                throw new ArgumentNullException("sourceDeviceId");
            if (options == null)
                options = new TemplateGenOptions();

            var devices = _configService.LoadDevices();
            var sourceDevice = devices.FirstOrDefault(d => d.Id == sourceDeviceId);
            if (sourceDevice == null)
            {
                Logger.Error("源设备不存在: " + sourceDeviceId);
                return null;
            }

            var template = new DeviceTemplate
            {
                TemplateId = "tpl_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                TemplateName = !string.IsNullOrEmpty(options.TemplateName)
                    ? options.TemplateName : sourceDevice.Name + "_模板",
                Category = !string.IsNullOrEmpty(options.Category)
                    ? options.Category : sourceDevice.DriverType,
                Description = !string.IsNullOrEmpty(options.Description)
                    ? options.Description : "从设备 " + sourceDevice.Name + " 生成的配置模板",
                Version = "1.0",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                CreatedFromDevice = sourceDevice.Name,
                CreatedFromDriver = sourceDevice.DriverType
            };

            // 1. 提取变量列表 → VariablePatterns
            if (sourceDevice.DataPoints != null && sourceDevice.DataPoints.Count > 0)
            {
                foreach (var dp in sourceDevice.DataPoints)
                {
                    var pattern = new VariablePattern
                    {
                        TemplateVarName = dp.Name,
                        MatchRule = options.MatchMode,
                        MatchPattern = options.MatchMode == "正则"
                            ? BuildRegexPattern(dp.Name)
                            : dp.Name,
                        DataType = dp.DataType,
                        Unit = dp.Unit,
                        TagCn = dp.TagCn
                    };
                    template.VariablePatterns.Add(pattern);
                }
                template.VariableCount = template.VariablePatterns.Count;
            }

            // 2. 提取语义关系
            if (options.IncludeSemanticRelations)
            {
                ExtractSemanticRelations(template, sourceDeviceId);
            }

            // 3. Fabric 配置（当前架构中 Fabric 请求为即席提交，无持久化配置，留空）
            if (options.IncludeFabricConfigs)
            {
                Logger.Debug("Fabric 配置提取: 无持久化 Fabric 配置，模板中留空");
            }

            // 4. 提取事件规则（从语义层历史事件中提取唯一事件类型）
            if (options.IncludeEventRules)
            {
                ExtractEventRules(template, sourceDeviceId);
            }

            // 5. 提取清洗策略
            if (options.IncludeCleaningStrategies && sourceDevice.DataPoints != null)
            {
                foreach (var dp in sourceDevice.DataPoints)
                {
                    var strategies = ExtractCleaningStrategies(dp);
                    template.CleaningStrategies.AddRange(strategies);
                }
            }

            // 6. 持久化
            Save(template);

            Logger.Info(string.Format("模板生成完成: {0}，变量 {1}，关系 {2}，事件 {3}，清洗 {4}",
                template.TemplateName, template.VariableCount,
                template.SemanticRelations.Count, template.EventRules.Count,
                template.CleaningStrategies.Count));

            return template;
        }

        // ════════════════════════════════════════════════════════════════
        //  模板应用 — 将模板应用到目标设备
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 将模板应用到指定设备，匹配变量并写入语义关系、事件规则、清洗策略
        /// </summary>
        public TemplateApplyResult ApplyToDevice(string templateId, string deviceId)
        {
            var result = new TemplateApplyResult();

            try
            {
                var template = Load(templateId);
                if (template == null)
                {
                    result.Success = false;
                    Logger.Error("模板不存在: " + templateId);
                    return result;
                }

                var devices = _configService.LoadDevices();
                var targetDevice = devices.FirstOrDefault(d => d.Id == deviceId);
                if (targetDevice == null)
                {
                    result.Success = false;
                    Logger.Error("目标设备不存在: " + deviceId);
                    return result;
                }

                result.TemplateName = template.TemplateName;
                result.DeviceName = targetDevice.Name;
                result.TotalPatterns = template.VariablePatterns.Count;

                // 变量匹配
                var matchedVars = new Dictionary<string, string>(); // templateVarName → actualVarName
                foreach (var pattern in template.VariablePatterns)
                {
                    var matchResult = MatchVariable(pattern, targetDevice.DataPoints ?? new List<DataPoint>());
                    result.VariableMatches.Add(matchResult);

                    if (matchResult.Success)
                    {
                        matchedVars[pattern.TemplateVarName] = matchResult.ActualVarName;
                        result.MatchedCount++;
                    }
                    else
                    {
                        result.SkippedCount++;
                    }
                }

                bool anyMatch = matchedVars.Count > 0;

                // 应用语义关系
                if (template.InheritanceConfig.InheritSemanticRelations && anyMatch)
                {
                    result.AppliedRelations = ApplySemanticRelations(template, matchedVars, deviceId, result.AppliedItems);
                }

                // 应用 Fabric 配置
                if (template.InheritanceConfig.InheritFabricConfigs && anyMatch)
                {
                    result.AppliedFabric = ApplyFabricConfigs(template, matchedVars, result.AppliedItems);
                }

                // 应用事件规则
                if (template.InheritanceConfig.InheritEventRules && anyMatch)
                {
                    result.AppliedEvents = ApplyEventRules(template, matchedVars, deviceId, result.AppliedItems);
                }

                // 应用清洗策略
                if (template.InheritanceConfig.InheritCleaningStrategies && anyMatch)
                {
                    result.AppliedCleaning = ApplyCleaningStrategiesToDevice(template, matchedVars, targetDevice, result.AppliedItems);
                }

                // 写入设备配置
                if (result.AppliedCleaning > 0)
                {
                    _configService.SaveDevices(devices);
                }

                result.Success = true;

                // 写入应用日志
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir,
                    string.Format("TemplateApply_{0:yyyyMMdd_HHmmss}.log", DateTime.Now));
                WriteApplyLog(result, logPath);
                result.LogPath = logPath;
            }
            catch (Exception ex)
            {
                result.Success = false;
                Logger.Error("模板应用失败: " + ex.Message);
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════
        //  模板覆盖 — 从设备重新生成模板内容，保留元信息，递增版本
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 用设备最新配置覆盖模板内容，保留 TemplateId/名称/分类，递增版本号
        /// </summary>
        public DeviceTemplate OverwriteFromDevice(string templateId, string sourceDeviceId)
        {
            var oldTemplate = Load(templateId);
            if (oldTemplate == null)
            {
                Logger.Error("模板不存在: " + templateId);
                return null;
            }

            // 递增版本号
            string oldVersion = oldTemplate.Version ?? "1.0";
            double verNum;
            if (double.TryParse(oldVersion, out verNum))
                oldVersion = (verNum + 0.1).ToString("F1");
            else
                oldVersion = "1.1";

            var options = new TemplateGenOptions
            {
                TemplateName = oldTemplate.TemplateName,
                Category = oldTemplate.Category,
                Description = oldTemplate.Description,
                MatchMode = "精确"
            };

            // 重新提取设备配置（但不自动保存为新的模板 ID）
            var devices = _configService.LoadDevices();
            var sourceDevice = devices.FirstOrDefault(d => d.Id == sourceDeviceId);
            if (sourceDevice == null)
            {
                Logger.Error("源设备不存在: " + sourceDeviceId);
                return null;
            }

            var newTemplate = new DeviceTemplate
            {
                TemplateId = oldTemplate.TemplateId,
                TemplateName = oldTemplate.TemplateName,
                Category = oldTemplate.Category,
                Description = oldTemplate.Description ?? "",
                Version = oldVersion,
                CreatedAt = oldTemplate.CreatedAt,
                UpdatedAt = DateTime.Now,
                CreatedFromDevice = sourceDevice.Name,
                CreatedFromDriver = sourceDevice.DriverType
            };

            // 重新提取各部分
            if (sourceDevice.DataPoints != null && sourceDevice.DataPoints.Count > 0)
            {
                foreach (var dp in sourceDevice.DataPoints)
                {
                    newTemplate.VariablePatterns.Add(new VariablePattern
                    {
                        TemplateVarName = dp.Name,
                        MatchRule = options.MatchMode,
                        MatchPattern = dp.Name,
                        DataType = dp.DataType,
                        Unit = dp.Unit,
                        TagCn = dp.TagCn
                    });
                }
                newTemplate.VariableCount = newTemplate.VariablePatterns.Count;
            }

            if (options.IncludeSemanticRelations)
                ExtractSemanticRelations(newTemplate, sourceDeviceId);

            if (options.IncludeEventRules)
                ExtractEventRules(newTemplate, sourceDeviceId);

            if (options.IncludeCleaningStrategies && sourceDevice.DataPoints != null)
            {
                foreach (var dp in sourceDevice.DataPoints)
                {
                    newTemplate.CleaningStrategies.AddRange(ExtractCleaningStrategies(dp));
                }
            }

            Save(newTemplate);
            Logger.Info(string.Format("模板已覆盖: {0} v{1}", newTemplate.TemplateName, newTemplate.Version));

            return newTemplate;
        }

        // ════════════════════════════════════════════════════════════════
        //  提取辅助方法
        // ════════════════════════════════════════════════════════════════

        private void ExtractSemanticRelations(DeviceTemplate template, string sourceDeviceId)
        {
            try
            {
                var deviceNode = _semanticService.GetNodeBySource("device", sourceDeviceId);
                if (deviceNode == null) return;

                var varNodes = _semanticService.GetChildren(deviceNode.Id, "variable");
                if (varNodes == null || varNodes.Count == 0) return;

                // 建立 nodeId → varName 映射（从 SourceId 解析）
                var nodeIdToVarName = new Dictionary<string, string>();
                foreach (var vn in varNodes)
                {
                    string varName = ExtractVarNameFromSourceId(vn.SourceId, sourceDeviceId);
                    if (!string.IsNullOrEmpty(varName))
                        nodeIdToVarName[vn.Id] = varName;
                    else
                        nodeIdToVarName[vn.Id] = vn.Name;
                }

                foreach (var vn in varNodes)
                {
                    string sourceVarName = nodeIdToVarName.ContainsKey(vn.Id)
                        ? nodeIdToVarName[vn.Id] : vn.Name;

                    var relations = _semanticService.GetVariableRelations(vn.Id);
                    if (relations == null) continue;

                    foreach (var rel in relations)
                    {
                        string targetVarName = "";
                        if (rel.TargetType == "variable" && !string.IsNullOrEmpty(rel.TargetVariableNodeId))
                        {
                            targetVarName = nodeIdToVarName.ContainsKey(rel.TargetVariableNodeId)
                                ? nodeIdToVarName[rel.TargetVariableNodeId]
                                : rel.TargetVariableNodeId;
                        }
                        else if (rel.TargetType == "datasource_field")
                        {
                            targetVarName = string.Format("{0}.{1}",
                                rel.TargetTableName ?? "", rel.TargetFieldName ?? "");
                        }
                        else
                        {
                            targetVarName = !string.IsNullOrEmpty(rel.Expression)
                                ? rel.Expression
                                : (rel.ConstantValue ?? "");
                        }

                        template.SemanticRelations.Add(new TemplateSemanticRelation
                        {
                            SourceVar = sourceVarName,
                            TargetVar = targetVarName,
                            RelationType = rel.RelationType,
                            TargetType = rel.TargetType,
                            Description = rel.Description ?? ""
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("提取语义关系时出错: " + ex.Message);
            }
        }

        private void ExtractEventRules(DeviceTemplate template, string sourceDeviceId)
        {
            try
            {
                var deviceNode = _semanticService.GetNodeBySource("device", sourceDeviceId);
                if (deviceNode == null) return;

                var events = _semanticService.GetEvents(deviceNode.Id, null, null, null, 200);
                if (events == null || events.Count == 0) return;

                // 按事件类型 + 处理方式去重
                var seen = new HashSet<string>();
                foreach (var evt in events)
                {
                    if (string.IsNullOrEmpty(evt.EventType)) continue;

                    string key = evt.EventType + "|" + (evt.ProcessingMethod ?? "");
                    if (seen.Contains(key))
                        continue;
                    seen.Add(key);

                    // 截取描述
                    string desc = evt.Description ?? "";
                    if (desc.Length > 30)
                        desc = desc.Substring(0, 30) + "...";

                    template.EventRules.Add(new TemplateEventRule
                    {
                        RuleName = evt.EventType + (string.IsNullOrEmpty(desc) ? "" : " - " + desc),
                        Condition = new TemplateRuleCondition
                        {
                            Type = "StatusChange",
                            TargetVar = "",
                            Trigger = evt.EventType
                        },
                        Actions = new List<string> { evt.ProcessingMethod ?? "仅记录" },
                        ProcessingMethod = evt.ProcessingMethod ?? "仅记录"
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("提取事件规则时出错: " + ex.Message);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  应用辅助方法
        // ════════════════════════════════════════════════════════════════

        private int ApplySemanticRelations(DeviceTemplate template, Dictionary<string, string> matchedVars,
            string deviceId, List<AppliedItem> items)
        {
            int count = 0;
            try
            {
                var deviceNode = _semanticService.GetNodeBySource("device", deviceId);
                if (deviceNode == null)
                {
                    Logger.Debug("目标设备无语义节点，跳过关系应用: " + deviceId);
                    return 0;
                }

                var varNodes = _semanticService.GetChildren(deviceNode.Id, "variable");
                if (varNodes == null || varNodes.Count == 0) return 0;

                // 建立 actualVarName → nodeId 映射
                var nameToNodeId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var vn in varNodes)
                {
                    if (matchedVars.ContainsValue(vn.Name))
                        nameToNodeId[vn.Name] = vn.Id;
                }

                foreach (var rel in template.SemanticRelations)
                {
                    // 双向匹配：SourceVar 和 TargetVar 都在 matchedVars 中
                    bool sourceOk = matchedVars.ContainsValue(rel.SourceVar);
                    bool targetOk = matchedVars.ContainsValue(rel.TargetVar)
                        || rel.TargetType != "variable"; // 非变量目标不需要匹配

                    if (!sourceOk || !targetOk)
                    {
                        if (template.InheritanceConfig.SkipMissingVariables)
                        {
                            items.Add(new AppliedItem
                            {
                                ItemType = "relation",
                                Description = string.Format("{0} → {1} (跳过:变量未匹配)",
                                    rel.SourceVar, rel.TargetVar),
                                Success = false
                            });
                            continue;
                        }
                    }

                    string sourceNodeId;
                    string targetNodeId = "";
                    if (!nameToNodeId.TryGetValue(rel.SourceVar, out sourceNodeId))
                        continue;

                    if (rel.TargetType == "variable")
                    {
                        if (!nameToNodeId.TryGetValue(rel.TargetVar, out targetNodeId))
                            continue;
                    }

                    var newRel = new SemanticVariableRelation
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                        VariableNodeId = sourceNodeId,
                        RelationType = rel.RelationType,
                        TargetType = rel.TargetType ?? "datasource_field",
                        Description = rel.Description ?? "",
                        TargetVariableNodeId = targetNodeId
                    };

                    _semanticService.SaveVariableRelation(newRel);
                    count++;
                    items.Add(new AppliedItem
                    {
                        ItemType = "relation",
                        Description = string.Format("{0} → {1} [{2}]",
                            rel.SourceVar, rel.TargetVar, rel.RelationType),
                        Success = true
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("应用语义关系失败: " + ex.Message);
            }
            return count;
        }

        private int ApplyFabricConfigs(DeviceTemplate template, Dictionary<string, string> matchedVars,
            List<AppliedItem> items)
        {
            int count = 0;
            foreach (var fc in template.FabricConfigs)
            {
                if (!fc.Enabled) continue;

                bool hasVar = matchedVars.ContainsValue(fc.TargetVar)
                    || matchedVars.ContainsKey(fc.TargetVar);
                if (!hasVar && template.InheritanceConfig.SkipMissingVariables)
                {
                    items.Add(new AppliedItem
                    {
                        ItemType = "fabric",
                        Description = string.Format("{0} → {1} (跳过:变量未匹配)", fc.Operator, fc.TargetVar),
                        Success = false
                    });
                    continue;
                }

                count++;
                items.Add(new AppliedItem
                {
                    ItemType = "fabric",
                    Description = string.Format("{0} 算子 → {1}", fc.Operator, fc.TargetVar),
                    Success = true
                });
            }
            return count;
        }

        private int ApplyEventRules(DeviceTemplate template, Dictionary<string, string> matchedVars,
            string deviceId, List<AppliedItem> items)
        {
            int count = 0;
            try
            {
                var deviceNode = _semanticService.GetNodeBySource("device", deviceId);
                if (deviceNode == null) return 0;

                foreach (var rule in template.EventRules)
                {
                    if (rule.Condition != null && !string.IsNullOrEmpty(rule.Condition.TargetVar))
                    {
                        bool varMatched = matchedVars.ContainsValue(rule.Condition.TargetVar)
                            || matchedVars.ContainsKey(rule.Condition.TargetVar);
                        if (!varMatched && template.InheritanceConfig.SkipMissingVariables)
                        {
                            items.Add(new AppliedItem
                            {
                                ItemType = "event",
                                Description = string.Format("{0} (跳过:变量未匹配)", rule.RuleName),
                                Success = false
                            });
                            continue;
                        }
                    }

                    var evt = new SemanticVariableEvent
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                        NodeId = deviceNode.Id,
                        EventType = rule.Condition != null ? (rule.Condition.Trigger ?? rule.RuleName) : rule.RuleName,
                        ProcessingMethod = rule.ProcessingMethod ?? "仅记录",
                        Description = string.Format("模板应用: {0}", rule.RuleName),
                        OccurredAt = DateTime.Now
                    };
                    _semanticService.SaveEvent(evt);
                    count++;
                    items.Add(new AppliedItem
                    {
                        ItemType = "event",
                        Description = string.Format("{0} [{1}]", rule.RuleName, evt.ProcessingMethod),
                        Success = true
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("应用事件规则失败: " + ex.Message);
            }
            return count;
        }

        private int ApplyCleaningStrategiesToDevice(DeviceTemplate template, Dictionary<string, string> matchedVars,
            DeviceConfig targetDevice, List<AppliedItem> items)
        {
            int count = 0; 
            if (targetDevice.DataPoints == null) return 0;

            foreach (var strategy in template.CleaningStrategies)
            {
                if (!strategy.Enabled) continue;

                // 用 matchedVars 反查实际变量名
                string actualVarName = matchedVars.ContainsKey(strategy.TargetVar)
                    ? matchedVars[strategy.TargetVar]
                    : (matchedVars.ContainsValue(strategy.TargetVar) ? strategy.TargetVar : null);

                var dp = actualVarName != null
                    ? targetDevice.DataPoints.FirstOrDefault(p =>
                        string.Equals(p.Name, actualVarName, StringComparison.OrdinalIgnoreCase))
                    : null;

                if (dp == null)
                {
                    if (template.InheritanceConfig.SkipMissingVariables)
                    {
                        items.Add(new AppliedItem
                        {
                            ItemType = "cleaning",
                            Description = string.Format("{0} → {1} (跳过:变量未匹配)", strategy.TargetVar, strategy.Strategy),
                            Success = false
                        });
                    }
                    continue;
                }

                ApplyCleaningToDataPoint(dp, strategy);
                count++;
                items.Add(new AppliedItem
                {
                    ItemType = "cleaning",
                    Description = string.Format("{0} ← {1}", dp.Name, strategy.Strategy),
                    Success = true
                });
            }
            return count;
        }

        private void ApplyCleaningToDataPoint(DataPoint dp, TemplateCleaningStrategy strategy)
        {
            switch (strategy.Strategy)
            {
                case "死区过滤":
                    dp.DeadBandEnabled = strategy.Enabled;
                    if (strategy.Params != null && strategy.Params.ContainsKey("dead_band"))
                        dp.DeadBand = Convert.ToDouble(strategy.Params["dead_band"]);
                    break;
                case "限幅过滤":
                    dp.ClipEnabled = strategy.Enabled;
                    if (strategy.Params != null)
                    {
                        if (strategy.Params.ContainsKey("min"))
                            dp.ClipMin = Convert.ToDouble(strategy.Params["min"]);
                        if (strategy.Params.ContainsKey("max"))
                            dp.ClipMax = Convert.ToDouble(strategy.Params["max"]);
                    }
                    break;
                case "离群值检测":
                    dp.OutlierEnabled = strategy.Enabled;
                    if (strategy.Params != null && strategy.Params.ContainsKey("sigma"))
                        dp.SigmaThreshold = Convert.ToDouble(strategy.Params["sigma"]);
                    break;
                case "空值过滤":
                    dp.NanFilterEnabled = strategy.Enabled;
                    if (strategy.Params != null)
                    {
                        if (strategy.Params.ContainsKey("filter_nan"))
                            dp.NanFilterNaN = Convert.ToBoolean(strategy.Params["filter_nan"]);
                        if (strategy.Params.ContainsKey("filter_inf"))
                            dp.NanFilterInf = Convert.ToBoolean(strategy.Params["filter_inf"]);
                        if (strategy.Params.ContainsKey("filter_negative"))
                            dp.NanFilterNegative = Convert.ToBoolean(strategy.Params["filter_negative"]);
                        if (strategy.Params.ContainsKey("replacement"))
                            dp.NanFilterReplacement = Convert.ToDouble(strategy.Params["replacement"]);
                    }
                    break;
                case "冻结检测":
                    dp.FreezeEnabled = strategy.Enabled;
                    if (strategy.Params != null && strategy.Params.ContainsKey("window"))
                        dp.FreezeWindow = Convert.ToInt32(strategy.Params["window"]);
                    break;
                case "尖峰抑制":
                    dp.SpikeEnabled = strategy.Enabled;
                    if (strategy.Params != null)
                    {
                        if (strategy.Params.ContainsKey("window"))
                            dp.SpikeWindow = Convert.ToInt32(strategy.Params["window"]);
                        if (strategy.Params.ContainsKey("threshold"))
                            dp.SpikeThreshold = Convert.ToDouble(strategy.Params["threshold"]);
                    }
                    break;
                case "变化率限制":
                    dp.RocLimitEnabled = strategy.Enabled;
                    if (strategy.Params != null && strategy.Params.ContainsKey("max_rate"))
                        dp.RocLimitMax = Convert.ToDouble(strategy.Params["max_rate"]);
                    break;
                case "IQR检测":
                    dp.IqrEnabled = strategy.Enabled;
                    if (strategy.Params != null && strategy.Params.ContainsKey("multiplier"))
                        dp.IqrMultiplier = Convert.ToDouble(strategy.Params["multiplier"]);
                    break;
                case "量程合理性":
                    dp.RangeEnabled = strategy.Enabled;
                    if (strategy.Params != null)
                    {
                        if (strategy.Params.ContainsKey("min"))
                            dp.RangeMin = Convert.ToDouble(strategy.Params["min"]);
                        if (strategy.Params.ContainsKey("max"))
                            dp.RangeMax = Convert.ToDouble(strategy.Params["max"]);
                    }
                    break;
            }

            // 自动设置 CleanEnabled 汇总标记
            dp.CleanEnabled = dp.DeadBandEnabled || dp.ClipEnabled || dp.OutlierEnabled
                || dp.NanFilterEnabled || dp.FreezeEnabled || dp.SpikeEnabled
                || dp.RocLimitEnabled || dp.IqrEnabled || dp.RangeEnabled;
        }

        // ════════════════════════════════════════════════════════════════
        //  清洗策略提取辅助方法
        // ════════════════════════════════════════════════════════════════

        private List<TemplateCleaningStrategy> ExtractCleaningStrategies(DataPoint dp)
        {
            var list = new List<TemplateCleaningStrategy>();

            if (dp.DeadBandEnabled)
            {
                list.Add(new TemplateCleaningStrategy
                {
                    TargetVar = dp.Name,
                    Strategy = "死区过滤",
                    Params = new Dictionary<string, object> { { "dead_band", dp.DeadBand } },
                    Enabled = true
                });
            }
            if (dp.ClipEnabled)
            {
                list.Add(new TemplateCleaningStrategy
                {
                    TargetVar = dp.Name,
                    Strategy = "限幅过滤",
                    Params = new Dictionary<string, object> { { "min", dp.ClipMin }, { "max", dp.ClipMax } },
                    Enabled = true
                });
            }
            if (dp.OutlierEnabled)
            {
                list.Add(new TemplateCleaningStrategy
                {
                    TargetVar = dp.Name,
                    Strategy = "离群值检测",
                    Params = new Dictionary<string, object> { { "sigma", dp.SigmaThreshold } },
                    Enabled = true
                });
            }
            if (dp.NanFilterEnabled)
            {
                list.Add(new TemplateCleaningStrategy
                {
                    TargetVar = dp.Name,
                    Strategy = "空值过滤",
                    Params = new Dictionary<string, object>
                    {
                        { "filter_nan", dp.NanFilterNaN },
                        { "filter_inf", dp.NanFilterInf },
                        { "filter_negative", dp.NanFilterNegative },
                        { "replacement", dp.NanFilterReplacement }
                    },
                    Enabled = true
                });
            }
            if (dp.FreezeEnabled)
            {
                list.Add(new TemplateCleaningStrategy
                {
                    TargetVar = dp.Name,
                    Strategy = "冻结检测",
                    Params = new Dictionary<string, object> { { "window", dp.FreezeWindow } },
                    Enabled = true
                });
            }
            if (dp.SpikeEnabled)
            {
                list.Add(new TemplateCleaningStrategy
                {
                    TargetVar = dp.Name,
                    Strategy = "尖峰抑制",
                    Params = new Dictionary<string, object>
                    {
                        { "window", dp.SpikeWindow },
                        { "threshold", dp.SpikeThreshold }
                    },
                    Enabled = true
                });
            }
            if (dp.RocLimitEnabled)
            {
                list.Add(new TemplateCleaningStrategy
                {
                    TargetVar = dp.Name,
                    Strategy = "变化率限制",
                    Params = new Dictionary<string, object> { { "max_rate", dp.RocLimitMax } },
                    Enabled = true
                });
            }
            if (dp.IqrEnabled)
            {
                list.Add(new TemplateCleaningStrategy
                {
                    TargetVar = dp.Name,
                    Strategy = "IQR检测",
                    Params = new Dictionary<string, object> { { "multiplier", dp.IqrMultiplier } },
                    Enabled = true
                });
            }
            if (dp.RangeEnabled)
            {
                list.Add(new TemplateCleaningStrategy
                {
                    TargetVar = dp.Name,
                    Strategy = "量程合理性",
                    Params = new Dictionary<string, object> { { "min", dp.RangeMin }, { "max", dp.RangeMax } },
                    Enabled = true
                });
            }

            return list;
        }

        // ════════════════════════════════════════════════════════════════
        //  通用辅助方法
        // ════════════════════════════════════════════════════════════════

        /// <summary>根据 MatchRule 执行变量匹配</summary>
        private VariableMatchResult MatchVariable(VariablePattern pattern, List<DataPoint> devicePoints)
        {
            var result = new VariableMatchResult
            {
                TemplateVar = pattern.TemplateVarName,
                MatchMethod = pattern.MatchRule
            };

            foreach (var dp in devicePoints)
            {
                bool matched = false;
                switch (pattern.MatchRule)
                {
                    case "精确":
                        matched = string.Equals(dp.Name, pattern.MatchPattern, StringComparison.OrdinalIgnoreCase);
                        break;
                    case "包含":
                        matched = dp.Name.IndexOf(pattern.MatchPattern, StringComparison.OrdinalIgnoreCase) >= 0
                               || pattern.MatchPattern.IndexOf(dp.Name, StringComparison.OrdinalIgnoreCase) >= 0;
                        break;
                    case "正则":
                        try
                        {
                            matched = Regex.IsMatch(dp.Name, pattern.MatchPattern, RegexOptions.IgnoreCase);
                        }
                        catch
                        {
                            matched = false;
                        }
                        break;
                }

                if (matched)
                {
                    result.Success = true;
                    result.ActualVarName = dp.Name;
                    return result;
                }
            }

            result.Success = false;
            result.SkipReason = "未找到匹配变量";
            return result;
        }

        /// <summary>从变量名构造正则匹配模式（数字→\d+，特殊字符转义）</summary>
        private string BuildRegexPattern(string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return variableName;

            var sb = new StringBuilder();
            sb.Append("^");
            bool inDigits = false;
            foreach (char c in variableName)
            {
                if (char.IsDigit(c))
                {
                    if (!inDigits)
                    {
                        sb.Append(@"\d+");
                        inDigits = true;
                    }
                }
                else
                {
                    inDigits = false;
                    if ("\\^$.|?*+()[{".IndexOf(c) >= 0)
                        sb.Append('\\');
                    sb.Append(c);
                }
            }
            sb.Append("$");
            return sb.ToString();
        }

        /// <summary>从条件中提取变量名列表（供后续扩展使用）</summary>
        private List<string> ExtractVariablesFromCondition(TemplateRuleCondition condition)
        {
            var vars = new List<string>();
            if (condition == null) return vars;

            if (!string.IsNullOrEmpty(condition.TargetVar))
                vars.Add(condition.TargetVar);
            if (!string.IsNullOrEmpty(condition.Trigger))
                vars.Add(condition.Trigger);

            return vars.Distinct().ToList();
        }

        /// <summary>写模板应用日志</summary>
        private void WriteApplyLog(TemplateApplyResult result, string logPath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== 模板应用日志 ===");
                sb.AppendLine(string.Format("时间: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
                sb.AppendLine(string.Format("模板: {0}", result.TemplateName));
                sb.AppendLine(string.Format("设备: {0}", result.DeviceName));
                sb.AppendLine(string.Format("结果: {0}", result.Success ? "成功" : "失败"));
                sb.AppendLine(string.Format("变量匹配: {0}/{1} 成功, {2} 跳过",
                    result.MatchedCount, result.TotalPatterns, result.SkippedCount));
                sb.AppendLine();

                sb.AppendLine("--- 变量匹配详情 ---");
                foreach (var m in result.VariableMatches)
                {
                    if (m.Success)
                        sb.AppendLine(string.Format("  [{0}] ✓ 匹配 → {1} (方法: {2})",
                            m.TemplateVar, m.ActualVarName, m.MatchMethod));
                    else
                        sb.AppendLine(string.Format("  [{0}] ✗ 未匹配: {1}",
                            m.TemplateVar, m.SkipReason));
                }

                sb.AppendLine();
                sb.AppendLine("--- 应用汇总 ---");
                sb.AppendLine(string.Format("  语义关系: {0}", result.AppliedRelations));
                sb.AppendLine(string.Format("  Fabric配置: {0}", result.AppliedFabric));
                sb.AppendLine(string.Format("  事件规则: {0}", result.AppliedEvents));
                sb.AppendLine(string.Format("  清洗策略: {0}", result.AppliedCleaning));

                if (result.AppliedItems.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("--- 应用明细 ---");
                    foreach (var item in result.AppliedItems)
                    {
                        sb.AppendLine(string.Format("  [{0}] {1} - {2}",
                            item.ItemType, item.Description,
                            item.Success ? "成功" : "失败"));
                    }
                }

                File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
                Logger.Info("应用日志已写入: " + logPath);
            }
            catch (Exception ex)
            {
                Logger.Error("写入应用日志失败: " + ex.Message);
            }
        }

        /// <summary>从 SourceId 中提取变量名（格式: {devId}|{dpName}）</summary>
        private string ExtractVarNameFromSourceId(string sourceId, string deviceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return "";
            string prefix = deviceId + "|";
            if (sourceId.StartsWith(prefix))
                return sourceId.Substring(prefix.Length);
            return "";
        }

        private string GetTemplatePath(string templateId)
        {
            return Path.Combine(_templateDir, templateId + ".json");
        }
    }
}
