using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json.Linq;

namespace IndustrialDataCollection.Services
{
    // ================================================================
    //  语义层写工具 — AI 可配置变量关系 + 事件
    // ================================================================

    /// <summary>
    /// semantic_create_variable_relation — 创建变量间关系
    /// </summary>
    [McpTool("semantic_create_variable_relation",
        "创建两个变量之间的语义关系（上限/下限/SOP步骤/业务关联等17种类型）。AI可通过此工具建立设备间的数据约束与业务逻辑。")]
    internal class SemanticCreateVariableRelationTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            string sourceNodeId = args["source_node_id"]?.Value<string>() ?? "";
            string targetNodeId = args["target_node_id"]?.Value<string>() ?? "";
            string relationType = args["relation_type"]?.Value<string>() ?? "";
            string description = args["description"]?.Value<string>() ?? "";

            if (string.IsNullOrWhiteSpace(sourceNodeId))
                return Task.FromResult<object>(new { error = "参数 source_node_id（源变量节点ID）不能为空" });
            if (string.IsNullOrWhiteSpace(targetNodeId))
                return Task.FromResult<object>(new { error = "参数 target_node_id（目标变量节点ID）不能为空" });
            if (string.IsNullOrWhiteSpace(relationType))
                return Task.FromResult<object>(new { error = "参数 relation_type（关系类型）不能为空。可用类型: 上限/下限/目标值/标准值/SOP步骤/SIP要求/质量判定/报警阈值/补偿系数/计算公式/参考变量/业务关联/影响/约束/计算来源/关联设备/历史数据源" });

            // 校验关系类型
            var validTypes = new HashSet<string> {
                "上限","下限","目标值","标准值","SOP步骤","SIP要求","质量判定",
                "报警阈值","补偿系数","计算公式","参考变量","业务关联",
                "影响","约束","计算来源","关联设备","历史数据源"
            };
            if (!validTypes.Contains(relationType))
                return Task.FromResult<object>(new { error = $"不支持的关系类型: {relationType}。可用: {string.Join(",", validTypes)}" });

            var rel = new SemanticVariableRelation
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                VariableNodeId = sourceNodeId,
                TargetVariableNodeId = targetNodeId,
                TargetType = "variable",
                RelationType = relationType,
                Description = description
            };

            SemanticService.Instance.SaveVariableRelation(rel);
            Logger.Info($"[MCP SEMANTIC] AI 创建变量关系: {sourceNodeId} -[{relationType}]→ {targetNodeId}");

            return Task.FromResult<object>(new
            {
                success = true,
                relation_id = rel.Id,
                source_node_id = sourceNodeId,
                target_node_id = targetNodeId,
                relation_type = relationType,
                message = $"变量关系已创建: {relationType}"
            });
        }
    }

    /// <summary>
    /// semantic_create_event_config — 配置设备事件
    /// </summary>
    [McpTool("semantic_create_event_config",
        "为设备/变量配置事件规则。支持9种事件类型（启动/停止/报警/故障/恢复/参数修改/通讯中断/维护保养/AI分析）。AI可配置事件触发后的处理管线。")]
    internal class SemanticCreateEventConfigTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            string nodeId = args["node_id"]?.Value<string>() ?? "";
            string eventType = args["event_type"]?.Value<string>() ?? "";
            string description = args["description"]?.Value<string>() ?? "";
            string processingMethod = args["processing_method"]?.Value<string>() ?? "仅记录";

            if (string.IsNullOrWhiteSpace(nodeId))
                return Task.FromResult<object>(new { error = "参数 node_id（语义节点ID）不能为空" });
            if (string.IsNullOrWhiteSpace(eventType))
                return Task.FromResult<object>(new { error = "参数 event_type 不能为空" });

            var validTypes = new HashSet<string> {
                "启动","停止","报警","故障","恢复","参数修改","通讯中断","维护保养","AI分析"
            };
            if (!validTypes.Contains(eventType))
                return Task.FromResult<object>(new { error = $"不支持的事件类型: {eventType}。可用: {string.Join(",", validTypes)}" });

            var validMethods = new HashSet<string> {
                "仅记录","报警","消息通知","站内消息","邮件","短信","Webhook","调用API","触发工作流","生成工单","触发MCP任务","触发AI分析"
            };
            if (!validMethods.Contains(processingMethod))
                return Task.FromResult<object>(new { error = $"不支持的处理方式: {processingMethod}。可用: {string.Join(",", validMethods)}" });

            var evt = new SemanticVariableEvent
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                NodeId = nodeId,
                EventType = eventType,
                ProcessingMethod = processingMethod,
                Description = description,
                OccurredAt = DateTime.Now
            };

            SemanticService.Instance.SaveEvent(evt);
            Logger.Info($"[MCP SEMANTIC] AI 创建事件配置: node={nodeId}, type={eventType}, method={processingMethod}");

            return Task.FromResult<object>(new
            {
                success = true,
                event_id = evt.Id,
                node_id = nodeId,
                event_type = eventType,
                processing_method = processingMethod,
                message = $"事件配置已创建: {eventType} → {processingMethod}"
            });
        }
    }
}
