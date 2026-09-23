using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using Newtonsoft.Json.Linq;

namespace IndustrialDataCollection.Services
{
    // ════════════════════════════════════════════════════════════════════
    //  新工具 1: 查询语义节点（统一替代原 workshops/lines/equipments/tags）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticListNodesTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            try
            {
                string parentId = args["parent_id"]?.Value<string>() ?? "";
                string kind = args["kind"]?.Value<string>() ?? "";
                string keyword = args["keyword"]?.Value<string>() ?? "";
                string status = args["status"]?.Value<string>() ?? "";

                List<SemanticNode> nodes;

                if (!string.IsNullOrEmpty(keyword))
                {
                    nodes = SemanticService.Instance.SearchNodes(keyword, string.IsNullOrEmpty(kind) ? null : kind);
                }
                else if (!string.IsNullOrEmpty(parentId))
                {
                    nodes = SemanticService.Instance.GetChildren(parentId, string.IsNullOrEmpty(kind) ? null : kind);
                }
                else if (string.IsNullOrEmpty(parentId) && string.IsNullOrEmpty(kind))
                {
                    nodes = SemanticService.Instance.GetRootNodes();
                }
                else
                {
                    nodes = SemanticService.Instance.SearchNodes("", string.IsNullOrEmpty(kind) ? null : kind);
                }

                if (!string.IsNullOrEmpty(status))
                    nodes = nodes.Where(n => n.Status == status).ToList();

                return JToken.FromObject(new { total = nodes.Count, items = nodes.Select(n => new { id = n.Id, parent_id = n.ParentId, name = n.Name, code = n.Code, kind = n.Kind, kind_display = NodeKind.GetDisplayName(n.Kind), status = n.Status, status_display = NodeStatus.GetDisplayName(n.Status), source_type = n.SourceType, source_id = n.SourceId, description = n.Description, properties = n.Properties, sort_order = n.SortOrder, created_at = n.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), updated_at = n.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss") }) });
            }
            catch (Exception ex)
            {
                return JToken.FromObject(new { error = "查询节点失败: " + ex.Message });
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 2: 查询变量关系
    // ════════════════════════════════════════════════════════════════════

    public class SemanticListVariableRelationsTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            try
            {
                string variableNodeId = args["variable_node_id"]?.Value<string>() ?? "";
                string relationType = args["relation_type"]?.Value<string>() ?? "";

                List<SemanticVariableRelation> relations;
                if (string.IsNullOrEmpty(variableNodeId))
                    relations = SemanticService.Instance.GetAllVariableRelations();
                else if (!string.IsNullOrEmpty(relationType))
                    relations = SemanticService.Instance.GetVariableRelationsByType(variableNodeId, relationType);
                else
                    relations = SemanticService.Instance.GetVariableRelations(variableNodeId);

                return JToken.FromObject(new { total = relations.Count, items = relations.Select(r => new { id = r.Id, variable_node_id = r.VariableNodeId, relation_type = r.RelationType, target_type = r.TargetType, target_datasource_id = r.TargetDatasourceId, target_table_name = r.TargetTableName, target_field_name = r.TargetFieldName, constant_value = r.ConstantValue, expression = r.Expression, unit = r.Unit, description = r.Description, condition_variable_ids = r.ConditionVariableIds }) });
            }
            catch (Exception ex)
            {
                return JToken.FromObject(new { error = "查询变量关系失败: " + ex.Message });
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 3: 查询节点关系（替代原 semantic_list_relations）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticListNodeRelationsTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            try
            {
                string nodeId = args["node_id"]?.Value<string>() ?? "";

                List<SemanticEquipmentRelation> relations;
                if (string.IsNullOrEmpty(nodeId))
                    relations = SemanticService.Instance.GetAllNodeRelations();
                else
                    relations = SemanticService.Instance.GetNodeRelations(nodeId);

                return JToken.FromObject(new { total = relations.Count, items = relations.Select(r => new { id = r.Id, source_node_id = r.SourceNodeId, target_node_id = r.TargetNodeId, relation_type = r.RelationType, description = r.Description }) });
            }
            catch (Exception ex)
            {
                return JToken.FromObject(new { error = "查询节点关系失败: " + ex.Message });
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 4: 查询事件（替代原 semantic_list_events）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticListEventsV2Tool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string nodeId = args["node_id"]?.Value<string>() ?? "";
            string eventType = args["event_type"]?.Value<string>() ?? "";
            int limit = 500;
            if (args["limit"] != null)
                int.TryParse(args["limit"].Value<string>(), out limit);

            DateTime? from = null;
            DateTime? to = null;
            if (args["from"] != null)
            {
                DateTime f;
                if (DateTime.TryParse(args["from"].Value<string>(), out f)) from = f;
            }
            if (args["to"] != null)
            {
                DateTime t;
                if (DateTime.TryParse(args["to"].Value<string>(), out t)) to = t;
            }

            var events = SemanticService.Instance.GetEvents(
                string.IsNullOrEmpty(nodeId) ? null : nodeId,
                from, to,
                string.IsNullOrEmpty(eventType) ? null : eventType,
                limit);

            return JToken.FromObject(new
            {
                total = events.Count,
                items = events.Select(ev => new
                {
                    id = ev.Id,
                    node_id = ev.NodeId,
                    variable_relation_id = ev.VariableRelationId,
                    event_type = ev.EventType,
                    processing_method = ev.ProcessingMethod,
                    processing_config = ev.ProcessingConfig,
                    occurred_at = ev.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    ended_at = ev.EndedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                    description = ev.Description
                })
            });
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 5: 获取节点路径
    // ════════════════════════════════════════════════════════════════════

    public class SemanticGetNodePathTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string nodeId = args["node_id"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(nodeId))
                return JToken.FromObject(new { error = "node_id 为必填参数" });

            var path = SemanticService.Instance.GetNodePath(nodeId);
            return JToken.FromObject(new
            {
                node_id = nodeId,
                path = path.Select(n => new
                {
                    id = n.Id,
                    name = n.Name,
                    kind = n.Kind,
                    kind_display = NodeKind.GetDisplayName(n.Kind)
                }),
                path_string = string.Join(" / ", path.Select(n => n.Name))
            });
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 6: 查询设备变量（复用 semantic_list_nodes + kind=Variable）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticListDeviceVariablesTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string equipmentNodeId = args["equipment_node_id"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(equipmentNodeId))
                return JToken.FromObject(new { error = "equipment_node_id 为必填参数" });

            var variables = SemanticService.Instance.GetChildren(equipmentNodeId, NodeKind.Variable);

            var items = new List<object>();
            foreach (var v in variables)
            {
                double? realtimeValue = null;
                string sourceId = v.SourceId ?? "";
                int pipeIdx = sourceId.IndexOf('|');
                if (pipeIdx > 0)
                {
                    string dcId = sourceId.Substring(0, pipeIdx);
                    string dpName = sourceId.Substring(pipeIdx + 1);
                    var latest = DataProcessor.Instance.GetLatest(dcId, dpName);
                    if (latest != null && double.TryParse(latest.Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double val))
                        realtimeValue = val;
                }

                items.Add(new
                {
                    id = v.Id,
                    name = v.Name,
                    code = v.Code,
                    variable_role = v.GetProperty("VariableRole", ""),
                    unit = v.GetProperty("Unit", ""),
                    data_type = v.GetProperty("DataType", ""),
                    realtime_value = realtimeValue
                });
            }

            return JToken.FromObject(new { total = items.Count, items });
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  旧工具类（向后兼容，委托给新 API）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticListWorkshopsTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            try
            {
                var nodes = SemanticService.Instance.SearchNodes("", NodeKind.Workshop);
                return JToken.FromObject(new { total = nodes.Count, items = nodes.Select(w => new { id = w.Id, name = w.Name, code = w.Code, description = w.Description }) });
            }
            catch (Exception ex)
            {
                return JToken.FromObject(new { error = "查询车间失败: " + ex.Message });
            }
        }
    }

    public class SemanticListLinesTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            try
            {
                string workshopId = args["workshop_id"]?.Value<string>() ?? "";
                List<SemanticNode> lines;
            if (string.IsNullOrEmpty(workshopId))
                lines = SemanticService.Instance.SearchNodes("", NodeKind.ProductionLine);
            else
                lines = SemanticService.Instance.GetChildren(workshopId, NodeKind.ProductionLine);
            return JToken.FromObject(new
            {
                total = lines.Count,
                items = lines.Select(l => new
                {
                    id = l.Id,
                    name = l.Name,
                    code = l.Code,
                    workshop_id = l.ParentId,
                    description = l.Description
                })
            });
            }
            catch (Exception ex)
            {
                return JToken.FromObject(new { error = "查询产线失败: " + ex.Message });
            }
        }
    }

    public class SemanticListEquipmentsTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            try
            {
                string workshopId = args["workshop_id"]?.Value<string>() ?? "";
                string lineId = args["production_line_id"]?.Value<string>() ?? "";
                List<SemanticNode> equipments;
            if (!string.IsNullOrEmpty(lineId))
                equipments = SemanticService.Instance.GetChildren(lineId, NodeKind.Equipment);
            else if (!string.IsNullOrEmpty(workshopId))
                equipments = SemanticService.Instance.GetDescendants(workshopId, false)
                    .Where(n => n.Kind == NodeKind.Equipment).ToList();
            else
                equipments = SemanticService.Instance.SearchNodes("", NodeKind.Equipment);
            var items = equipments.Select(e => new
            {
                id = e.Id,
                name = e.Name,
                code = e.Code,
                equipment_type = e.GetProperty("EquipmentType", ""),
                workshop_id = e.GetProperty("legacyWorkshopId", ""),
                production_line_id = e.GetProperty("legacyLineId", ""),
                description = e.Description
            }).ToList();

            return new JObject
            {
                ["total"] = equipments.Count,
                ["items"] = JArray.FromObject(items)
            };
            }
            catch (Exception ex)
            {
                return JToken.FromObject(new { error = "查询设备失败: " + ex.Message });
            }
        }
    }

    public class SemanticListTagsTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string equipmentId = args["equipment_id"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(equipmentId))
                return JToken.FromObject(new { error = "equipment_id 为必填参数" });

            var variables = SemanticService.Instance.GetChildren(equipmentId, NodeKind.Variable);
            var items = new List<object>();
            foreach (var v in variables)
            {
                double? realtimeValue = null;
                string sourceId = v.SourceId ?? "";
                int pipeIdx = sourceId.IndexOf('|');
                if (pipeIdx > 0)
                {
                    string dcId = sourceId.Substring(0, pipeIdx);
                    string dpName = sourceId.Substring(pipeIdx + 1);
                    var latest = DataProcessor.Instance.GetLatest(dcId, dpName);
                    if (latest != null && double.TryParse(latest.Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double val))
                        realtimeValue = val;
                }
                items.Add(new
                {
                    id = v.Id,
                    name = v.Name,
                    code = v.Code,
                    variable_role = v.GetProperty("VariableRole", ""),
                    unit = v.GetProperty("Unit", ""),
                    data_type = v.GetProperty("DataType", ""),
                    realtime_value = realtimeValue
                });
            }
            return JToken.FromObject(new { total = items.Count, items });
        }
    }

    public class SemanticListRelationsTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            try
            {
                string equipmentId = args["equipment_id"]?.Value<string>() ?? "";
                List<SemanticEquipmentRelation> relations;
                if (string.IsNullOrEmpty(equipmentId))
                    relations = SemanticService.Instance.GetAllNodeRelations();
                else
                    relations = SemanticService.Instance.GetNodeRelations(equipmentId);
                return JToken.FromObject(new { total = relations.Count, items = relations.Select(r => new { id = r.Id, source_equipment_id = r.SourceNodeId, target_equipment_id = r.TargetNodeId, relation_type = r.RelationType, description = r.Description }) });
            }
            catch (Exception ex)
            {
                return JToken.FromObject(new { error = "查询关系失败: " + ex.Message });
            }
        }
    }

    public class SemanticListEventsTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string equipmentId = args["equipment_id"]?.Value<string>() ?? "";
            int limit = 50;
            if (args["limit"] != null)
                int.TryParse(args["limit"].Value<string>(), out limit);
            var events = SemanticService.Instance.GetEvents(equipmentId, null, null, null, limit);
            return JToken.FromObject(new
            {
                total = events.Count,
                items = events.Select(ev => new
                {
                    id = ev.Id,
                    equipment_id = ev.NodeId,
                    event_type = ev.EventType,
                    occurred_at = ev.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    ended_at = ev.EndedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                    description = ev.Description
                })
            });
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 7: 获取完整语义树（AI空间感知）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticGetFullTreeTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            try
            {
                string rootKind = args["root_kind"]?.Value<string>() ?? "";
                int maxDepth = 6;
                if (args["max_depth"] != null)
                    int.TryParse(args["max_depth"].Value<string>(), out maxDepth);
                if (maxDepth > 10) maxDepth = 10;
                int maxNodes = 500;
                if (args["max_nodes"] != null)
                    int.TryParse(args["max_nodes"].Value<string>(), out maxNodes);

                // 一次查询加载全部节点，避免逐层递归 SQL
                var allNodes = SemanticService.Instance.GetAllNodes();
            var byParent = allNodes
                .Where(n => !string.IsNullOrEmpty(n.Id))
                .GroupBy(n => n.ParentId ?? "")
                .ToDictionary(g => g.Key, g => g.ToList());

            var roots = (string.IsNullOrEmpty(rootKind)
                ? allNodes.Where(n => string.IsNullOrEmpty(n.ParentId))
                : allNodes.Where(n => n.Kind == rootKind && string.IsNullOrEmpty(n.ParentId)))
                .ToList();

            int builtCount = 0;

            object BuildTree(SemanticNode n, int depth)
            {
                if (builtCount >= maxNodes) return null;
                builtCount++;

                List<SemanticNode> children = null;
                if (depth < maxDepth && byParent.TryGetValue(n.Id, out var rawChildren))
                {
                    children = rawChildren;
                }

                return new
                {
                    id = n.Id,
                    name = n.Name,
                    kind = n.Kind,
                    kind_display = NodeKind.GetDisplayName(n.Kind),
                    status = n.Status,
                    source_type = n.SourceType,
                    source_id = n.SourceId,
                    description = n.Description,
                    children = children?.Select(c => BuildTree(c, depth + 1)).Where(x => x != null),
                    has_children = children != null && children.Count > 0
                };
            }

            return new JObject
            {
                ["max_depth"] = maxDepth,
                ["max_nodes"] = maxNodes,
                ["total_roots"] = roots.Count,
                ["trees"] = JArray.FromObject(roots.Select(r => BuildTree(r, 0)).Where(x => x != null))
            };
            }
            catch (Exception ex)
            {
                return JToken.FromObject(new { error = "获取完整树失败: " + ex.Message });
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 8: 子树实时快照（AI监控大脑）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticGetRealtimeSnapshotTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string nodeId = args["node_id"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(nodeId))
                return JToken.FromObject(new { error = "node_id 为必填参数" });

            var node = SemanticService.Instance.GetNode(nodeId);
            if (node == null)
                return JToken.FromObject(new { error = "节点不存在" });

            var variableNodes = SemanticService.Instance.GetDescendants(nodeId)
                .Where(n => n.Kind == NodeKind.Variable).ToList();

            int online = 0, offline = 0, alarm = 0;
            var items = new List<object>();
            foreach (var v in variableNodes)
            {
                double? val = null;
                string valStr = null;
                DateTime? ts = null;
                string sourceId = v.SourceId ?? "";
                int pipeIdx = sourceId.IndexOf('|');
                if (pipeIdx > 0)
                {
                    string dcId = sourceId.Substring(0, pipeIdx);
                    string dpName = sourceId.Substring(pipeIdx + 1);
                    var latest = DataProcessor.Instance.GetLatest(dcId, dpName);
                    if (latest != null)
                    {
                        valStr = latest.Value;
                        double.TryParse(latest.Value,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double dv);
                        val = dv;
                        ts = latest.Timestamp;
                        online++;
                    }
                    else offline++;
                }

                items.Add(new
                {
                    id = v.Id,
                    name = v.Name,
                    code = v.Code,
                    unit = v.GetProperty("Unit", ""),
                    role = v.GetProperty("VariableRole", ""),
                    value = val,
                    value_str = valStr,
                    timestamp = ts?.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return JToken.FromObject(new
            {
                node = new { nodeId, node.Name, kind = node.Kind, kind_display = NodeKind.GetDisplayName(node.Kind) },
                variable_count = variableNodes.Count,
                online = online,
                offline = offline,
                alarm = alarm,
                items
            });
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 9: 数据流追溯（AI理解完整数据链路）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticGetDataFlowTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string nodeId = args["node_id"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(nodeId))
                return JToken.FromObject(new { error = "node_id 为必填参数" });

            var node = SemanticService.Instance.GetNode(nodeId);
            if (node == null)
                return JToken.FromObject(new { error = "节点不存在" });

            var flows = new List<object>();

            // 1. 设备数据流: Equipment → Variables → Relations
            if (node.Kind == NodeKind.Equipment)
            {
                var variables = SemanticService.Instance.GetChildren(nodeId, NodeKind.Variable);
                var conns = new List<object>();
                foreach (var v in variables)
                {
                    var rels = SemanticService.Instance.GetVariableRelations(v.Id);
                    foreach (var rel in rels)
                    {
                        conns.Add(new
                        {
                            variable = v.Name,
                            relation_type = rel.RelationType,
                            target = rel.TargetType == "datasource_field"
                                ? string.Format("{0}.{1}.{2}", rel.TargetDatasourceId, rel.TargetTableName, rel.TargetFieldName)
                                : rel.TargetType == "constant" ? "常量: " + rel.ConstantValue
                                : "表达式: " + rel.Expression
                        });
                    }
                }

                flows.Add(new
                {
                    type = "device_data_flow",
                    equipment = node.Name,
                    equipment_id = node.Id,
                    driver = node.GetProperty("DriverType", ""),
                    variable_count = variables.Count,
                    connections = conns
                });
            }

            // 2. 数据源流: Datasource → Tables → Fields → Variable Relations
            if (node.Kind == NodeKind.Datasource)
            {
                var tables = SemanticService.Instance.GetChildren(nodeId, NodeKind.DataTable);
                var tableInfos = new List<object>();
                foreach (var tbl in tables)
                {
                    var fields = SemanticService.Instance.GetChildren(tbl.Id, NodeKind.DataField);
                    tableInfos.Add(new
                    {
                        table = tbl.Name,
                        field_count = fields.Count,
                        fields = fields.Select(f => f.Name)
                    });
                }

                flows.Add(new
                {
                    type = "datasource_schema",
                    datasource = node.Name,
                    datasource_id = node.Id,
                    db_type = node.GetProperty("DbType", ""),
                    server = node.GetProperty("Server", ""),
                    database = node.GetProperty("Database", ""),
                    table_count = tables.Count,
                    tables = tableInfos
                });
            }

            // 3. Variable → 关联数据源和变量关系
            if (node.Kind == NodeKind.Variable)
            {
                var rels = SemanticService.Instance.GetVariableRelations(nodeId);
                flows.Add(new
                {
                    type = "variable_relations",
                    variable = node.Name,
                    variable_id = node.Id,
                    unit = node.GetProperty("Unit", ""),
                    role = node.GetProperty("VariableRole", ""),
                    relation_count = rels.Count,
                    relations = rels.Select(r => new
                    {
                        r.RelationType,
                        r.TargetType,
                        r.TargetDatasourceId,
                        r.TargetTableName,
                        r.TargetFieldName,
                        r.ConstantValue,
                        r.Expression,
                        r.Description
                    })
                });
            }

            return JToken.FromObject(new { node_id = nodeId, node_name = node.Name, flows });
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 10: 报警摘要（AI快速感知异常）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticGetAlarmSummaryTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string nodeId = args["node_id"]?.Value<string>() ?? "";
            int hours = 24;
            if (args["hours"] != null) int.TryParse(args["hours"].Value<string>(), out hours);
            if (hours > 720) hours = 720;

            DateTime from = DateTime.Now.AddHours(-hours);

            // 获取节点及其子树的所有变量
            var variables = string.IsNullOrEmpty(nodeId)
                ? SemanticService.Instance.SearchNodes("", NodeKind.Variable)
                : SemanticService.Instance.GetDescendants(nodeId).Where(n => n.Kind == NodeKind.Variable).ToList();

            // 查询每个变量的报警事件
            var alarmEvents = new List<SemanticVariableEvent>();
            foreach (var v in variables)
            {
                var evts = SemanticService.Instance.GetEvents(v.Id, from, null, null, 500);
                alarmEvents.AddRange(evts.Where(ev =>
                    ev.EventType == "alarm" || ev.EventType == "ALARM_HH" || ev.EventType == "ALARM_H" ||
                    ev.EventType == "ALARM_L" || ev.EventType == "ALARM_LL"));
            }

            // 按变量聚合
            var grouped = alarmEvents.GroupBy(e => e.NodeId).Select(g => new
            {
                node_id = g.Key,
                node_name = variables.FirstOrDefault(v => v.Id == g.Key)?.Name ?? "",
                total_alarms = g.Count(),
                latest = g.Max(e => e.OccurredAt).ToString("yyyy-MM-dd HH:mm:ss"),
                levels = g.GroupBy(e => e.EventType).ToDictionary(
                    eg => eg.Key,
                    eg => eg.Count()
                )
            }).OrderByDescending(g => g.total_alarms).Take(50);

            return JToken.FromObject(new
            {
                time_range = string.Format("最近 {0} 小时", hours),
                from = from.ToString("yyyy-MM-dd HH:mm:ss"),
                total_events = alarmEvents.Count,
                affected_variables = grouped.Count(),
                summary = grouped
            });
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 11: 智能关系推荐（AI辅助发现隐藏关联）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticSuggestRelationsTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string variableNodeId = args["variable_node_id"]?.Value<string>() ?? "";

            var variableNodes = string.IsNullOrEmpty(variableNodeId)
                ? SemanticService.Instance.SearchNodes("", NodeKind.Variable)
                : new List<SemanticNode> { SemanticService.Instance.GetNode(variableNodeId) };
            variableNodes = variableNodes.Where(v => v != null).ToList();

            var suggestions = new List<object>();
            foreach (var varNode in variableNodes.Take(50))
            {
                // 找命名相似的数据源字段
                var varName = varNode.Name.ToLowerInvariant();
                var allFields = SemanticService.Instance.SearchNodes("", NodeKind.DataField)
                    .Where(f => f.Name.ToLowerInvariant().Contains(varName) || varName.Contains(f.Name.ToLowerInvariant()))
                    .Take(5).ToList();

                foreach (var field in allFields)
                {
                    // 计算相似度
                    double sim = JaroWinklerSimilarity(varNode.Name, field.Name);
                    if (sim > 0.6)
                    {
                        var tableNode = SemanticService.Instance.GetNode(field.ParentId);
                        var dsNode = tableNode != null ? SemanticService.Instance.GetNode(tableNode.ParentId) : null;
                        suggestions.Add(new
                        {
                            variable_id = varNode.Id,
                            variable_name = varNode.Name,
                            suggested_field = field.Name,
                            field_node_id = field.Id,
                            table = tableNode?.Name ?? "",
                            datasource = dsNode?.Name ?? "",
                            similarity = Math.Round(sim, 3),
                            suggestion_type = "命名相似"
                        });
                    }
                }
            }

            return JToken.FromObject(new
            {
                total_suggestions = suggestions.Count,
                items = suggestions.Cast<dynamic>().OrderByDescending(s => s.similarity).Take(20).Cast<object>()
            });
        }

        private double JaroWinklerSimilarity(string s1, string s2)
        {
            if (s1 == s2) return 1.0;
            s1 = s1.ToLowerInvariant(); s2 = s2.ToLowerInvariant();
            int len1 = s1.Length, len2 = s2.Length;
            if (len1 == 0 || len2 == 0) return 0;

            int matchDistance = Math.Max(len1, len2) / 2 - 1;
            if (matchDistance < 0) matchDistance = 0;

            bool[] match1 = new bool[len1], match2 = new bool[len2];
            int matches = 0, transpositions = 0;

            for (int i = 0; i < len1; i++)
            {
                int start = Math.Max(0, i - matchDistance);
                int end = Math.Min(i + matchDistance + 1, len2);
                for (int j = start; j < end; j++)
                {
                    if (match2[j] || s1[i] != s2[j]) continue;
                    match1[i] = match2[j] = true;
                    matches++;
                    break;
                }
            }
            if (matches == 0) return 0;

            int k = 0;
            for (int i = 0; i < len1; i++)
            {
                if (!match1[i]) continue;
                while (!match2[k]) k++;
                if (s1[i] != s2[k]) transpositions++;
                k++;
            }

            double jaro = (matches / (double)len1 + matches / (double)len2 + (matches - transpositions / 2.0) / (double)matches) / 3.0;
            int prefix = 0;
            for (int i = 0; i < Math.Min(4, Math.Min(len1, len2)); i++)
            {
                if (s1[i] == s2[i]) prefix++; else break;
            }
            return jaro + 0.1 * prefix * (1 - jaro);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 12: 执行数据源查询（AI直接查历史数据）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticExecuteQueryTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            string datasourceId = args["datasource_id"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(datasourceId))
                return JToken.FromObject(new { error = "datasource_id 为必填参数" });

            string sql = args["sql"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(sql))
                return JToken.FromObject(new { error = "sql 为必填参数" });

            int maxRows = 200;
            if (args["max_rows"] != null)
                int.TryParse(args["max_rows"].Value<string>(), out maxRows);
            if (maxRows > 1000) maxRows = 1000;

            try
            {
                var result = await DataSourceService.Instance.RunQueryAsync(datasourceId, sql);
                return JToken.FromObject(new
                {
                    datasource_id = datasourceId,
                    sql = sql,
                    columns = result.columns,
                    row_count = result.rows.Count,
                    truncated = result.truncated,
                    elapsed_ms = result.elapsedMs,
                    rows = result.rows.Select(r => r.Select(v => v?.ToString() ?? "null"))
                });
            }
            catch (Exception ex)
            {
                return JToken.FromObject(new { error = "查询失败: " + ex.Message });
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  新工具 13: 批量更新节点（AI驱动的标签维护）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticBatchUpdateNodesTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            var updates = args["updates"] as JArray;
            if (updates == null || updates.Count == 0)
                return JToken.FromObject(new { error = "updates 数组为必填参数" });
            if (updates.Count > 100)
                return JToken.FromObject(new { error = "单次最多 100 条更新" });

            var results = new List<object>();
            int success = 0, failed = 0;
            foreach (var token in updates)
            {
                var item = token as JObject;
                if (item == null) continue;
                string nodeId = item["node_id"]?.Value<string>() ?? "";
                if (string.IsNullOrEmpty(nodeId))
                {
                    results.Add(new { node_id = "", status = "failed", reason = "node_id 缺失" });
                    failed++;
                    continue;
                }

                var node = SemanticService.Instance.GetNode(nodeId);
                if (node == null)
                {
                    results.Add(new { node_id = nodeId, status = "failed", reason = "节点不存在" });
                    failed++;
                    continue;
                }

                bool changed = false;
                foreach (var prop in item.Properties())
                {
                    switch (prop.Name)
                    {
                        case "name":
                            string nv = prop.Value.Value<string>();
                            if (!string.IsNullOrEmpty(nv) && nv != node.Name) { node.Name = nv; changed = true; }
                            break;
                        case "description":
                            string dv = prop.Value.Value<string>();
                            if (dv != null && dv != node.Description) { node.Description = dv; changed = true; }
                            break;
                        case "status":
                            string sv = prop.Value.Value<string>();
                            if (!string.IsNullOrEmpty(sv) && sv != node.Status) { node.Status = sv; changed = true; }
                            break;
                        default:
                            // 写入自定义 property
                            if (prop.Name.StartsWith("prop_"))
                            {
                                string pk = prop.Name.Substring(5);
                                string pv = prop.Value.Value<string>();
                                if (pv != null) { node.SetProperty(pk, pv); changed = true; }
                            }
                            break;
                    }
                }

                if (changed)
                {
                    node.UpdatedAt = DateTime.Now;
                    SemanticService.Instance.SaveNode(node);
                    success++;
                    results.Add(new { node_id = nodeId, status = "updated" });
                }
                else
                {
                    results.Add(new { node_id = nodeId, status = "unchanged" });
                }
            }

            return JToken.FromObject(new { total = updates.Count, success, failed, results });
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  工具 26: 变量影响分析（语义图谱）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticImpactGraphTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string variableNodeId = args["variable_node_id"]?.Value<string>() ?? "";
            int maxDepth = args["max_depth"] != null ? args["max_depth"].Value<int>() : 5;

            if (string.IsNullOrEmpty(variableNodeId))
                return JToken.FromObject(new { error = "请提供 variable_node_id" });

            var graph = SemanticService.Instance.GetImpactGraph(variableNodeId, maxDepth);
            var node = SemanticService.Instance.GetNode(variableNodeId);
            var nameMap = new Dictionary<string, string>();
            nameMap[variableNodeId] = node != null ? node.Name : variableNodeId;

            // 收集所有涉及的变量ID
            var allIds = new HashSet<string>();
            foreach (var p in graph.Upstream) allIds.Add(p.VariableNodeId);
            foreach (var p in graph.Downstream) allIds.Add(p.VariableNodeId);
            var names = SemanticService.Instance.GetVariableNames(allIds);
            foreach (var kv in names) nameMap[kv.Key] = kv.Value;

            // 构建关系链（带名称）
            var downstreamChains = new List<object>();
            var upstreamChains = new List<object>();

            foreach (var p in graph.Downstream.OrderBy(p => p.Depth))
            {
                downstreamChains.Add(new
                {
                    variable_id = p.VariableNodeId,
                    variable_name = nameMap.ContainsKey(p.VariableNodeId) ? nameMap[p.VariableNodeId] : p.VariableNodeId,
                    depth = p.Depth,
                    relation_type = p.RelationType,
                    relation_description = p.RelationDescription,
                    from_node_id = p.ViaSourceId,
                    from_node_name = nameMap.ContainsKey(p.ViaSourceId) ? nameMap[p.ViaSourceId] : p.ViaSourceId
                });
            }

            foreach (var p in graph.Upstream.OrderBy(p => p.Depth))
            {
                upstreamChains.Add(new
                {
                    variable_id = p.VariableNodeId,
                    variable_name = nameMap.ContainsKey(p.VariableNodeId) ? nameMap[p.VariableNodeId] : p.VariableNodeId,
                    depth = p.Depth,
                    relation_type = p.RelationType,
                    relation_description = p.RelationDescription,
                    from_node_id = p.ViaSourceId,
                    from_node_name = nameMap.ContainsKey(p.ViaSourceId) ? nameMap[p.ViaSourceId] : p.ViaSourceId
                });
            }

            return JToken.FromObject(new
            {
                root_variable_id = variableNodeId,
                root_variable_name = node != null ? node.Name : "未知",
                downstream_count = graph.Downstream.Count,
                upstream_count = graph.Upstream.Count,
                downstream = downstreamChains,
                upstream = upstreamChains,
                graph_summary = string.Format("{0} → 影响 {1} 个下游变量, ← 依赖 {2} 个上游变量",
                    node != null ? node.Name : variableNodeId,
                    graph.Downstream.Count, graph.Upstream.Count)
            });
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  工具 27: 变量上下游展开（精简版）
    // ════════════════════════════════════════════════════════════════════

    public class SemanticUpstreamTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string variableNodeId = args["variable_node_id"]?.Value<string>() ?? "";
            int maxDepth = args["max_depth"] != null ? args["max_depth"].Value<int>() : 5;

            if (string.IsNullOrEmpty(variableNodeId))
                return JToken.FromObject(new { error = "请提供 variable_node_id" });

            var graph = SemanticService.Instance.GetImpactGraph(variableNodeId, maxDepth);
            var allIds = new HashSet<string>();
            foreach (var p in graph.Upstream) allIds.Add(p.VariableNodeId);
            var names = SemanticService.Instance.GetVariableNames(allIds);

            var chains = graph.Upstream.OrderBy(p => p.Depth).Select(p => new
            {
                variable_id = p.VariableNodeId,
                variable_name = names.ContainsKey(p.VariableNodeId) ? names[p.VariableNodeId] : p.VariableNodeId,
                depth = p.Depth,
                relation_type = p.RelationType
            }).ToList();

            return JToken.FromObject(new { upstream_count = chains.Count, upstream = chains });
        }
    }

    public class SemanticDownstreamTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string variableNodeId = args["variable_node_id"]?.Value<string>() ?? "";
            int maxDepth = args["max_depth"] != null ? args["max_depth"].Value<int>() : 5;

            if (string.IsNullOrEmpty(variableNodeId))
                return JToken.FromObject(new { error = "请提供 variable_node_id" });

            var graph = SemanticService.Instance.GetImpactGraph(variableNodeId, maxDepth);
            var allIds = new HashSet<string>();
            foreach (var p in graph.Downstream) allIds.Add(p.VariableNodeId);
            var names = SemanticService.Instance.GetVariableNames(allIds);

            var chains = graph.Downstream.OrderBy(p => p.Depth).Select(p => new
            {
                variable_id = p.VariableNodeId,
                variable_name = names.ContainsKey(p.VariableNodeId) ? names[p.VariableNodeId] : p.VariableNodeId,
                depth = p.Depth,
                relation_type = p.RelationType
            }).ToList();

            return JToken.FromObject(new { downstream_count = chains.Count, downstream = chains });
        }
    }

    /// <summary>
    /// semantic_get_variable_history_source — 查询变量的历史数据存储位置
    /// 给定变量节点ID，返回该变量关联的「历史数据源」关系，即数据存储在哪个数据源/表/字段
    /// </summary>
    public class SemanticGetVariableHistorySourceTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            await Task.CompletedTask;
            string variableNodeId = args["variable_node_id"]?.Value<string>() ?? "";

            if (string.IsNullOrEmpty(variableNodeId))
                return JToken.FromObject(new { error = "请提供 variable_node_id" });

            // 查询该变量的所有关系，筛选 RelationType = "历史数据源"
            var allRels = SemanticService.Instance.GetVariableRelations(variableNodeId);
            var historyRels = allRels.Where(r => r.RelationType == "历史数据源").ToList();

            var results = new List<object>();
            foreach (var rel in historyRels)
            {
                string dsName = "";
                try
                {
                    var dsNode = SemanticService.Instance.GetNodeBySource("datasource", rel.TargetDatasourceId);
                    dsName = dsNode?.Name ?? rel.TargetDatasourceId;
                }
                catch { dsName = rel.TargetDatasourceId; }

                results.Add(new
                {
                    relation_id = rel.Id,
                    variable_node_id = rel.VariableNodeId,
                    datasource_id = rel.TargetDatasourceId,
                    datasource_name = dsName,
                    table_name = rel.TargetTableName,
                    field_names = (rel.TargetFieldName ?? "").Split(',').Select(f => f.Trim()).Where(f => !string.IsNullOrEmpty(f)).ToList(),
                    description = rel.Description ?? "",
                    // 给 AI Agent 的结构化提示
                    query_hint = string.Format("SELECT {0} FROM {1} WHERE variable = '{2}' AND timestamp >= ? AND timestamp <= ?",
                        string.Join(", ", (rel.TargetFieldName ?? "value").Split(',').Select(f => f.Trim()).Where(f => !string.IsNullOrEmpty(f))),
                        rel.TargetTableName,
                        GetVariableName(variableNodeId))
                });
            }

            return JToken.FromObject(new
            {
                variable_node_id = variableNodeId,
                variable_name = GetVariableName(variableNodeId),
                history_sources_count = results.Count,
                history_sources = results
            });
        }

        private string GetVariableName(string nodeId)
        {
            try
            {
                var node = SemanticService.Instance.GetNode(nodeId);
                return node?.Name ?? nodeId;
            }
            catch { return nodeId; }
        }
    }
}
