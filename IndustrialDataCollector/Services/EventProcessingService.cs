using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 事件处理引擎 — 根据事件配置的处理方式执行对应的处理管线
    /// 12 种处理方式: 仅记录 / 报警 / 消息通知 / 站内消息 / 邮件 / 短信 /
    ///              Webhook / 调用API / 触发工作流 / 生成工单 / 触发MCP任务 / 触发AI分析
    /// </summary>
    public class EventProcessingService
    {
        private static readonly Lazy<EventProcessingService> _instance =
            new Lazy<EventProcessingService>(() => new EventProcessingService());

        public static EventProcessingService Instance => _instance.Value;

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private readonly object _lock = new object();

        private EventProcessingService() { }

        /// <summary>
        /// 处理事件 — 根据 ProcessingMethod 路由到对应处理器
        /// </summary>
        public async Task ProcessAsync(SemanticVariableEvent evt)
        {
            if (evt == null) return;

            string method = evt.ProcessingMethod ?? "仅记录";
            string configJson = evt.ProcessingConfig ?? "{}";

            Logger.Info(string.Format("[EventProcessing] 事件触发: [{0}] {1} → {2}",
                evt.EventType, evt.Description?.Truncate(40), method));

            try
            {
                var config = ParseConfig(configJson);
                string nodeName = ResolveNodeName(evt.NodeId);
                var context = new EventContext { Event = evt, Config = config, NodeName = nodeName };

                switch (method)
                {
                    case "仅记录":         await HandleLogOnly(context); break;
                    case "报警":           await HandleAlarm(context); break;
                    case "消息通知":       await HandleMessageNotify(context); break;
                    case "站内消息":       await HandleInSiteMessage(context); break;
                    case "邮件":           await HandleEmail(context); break;
                    case "短信":           await HandleSMS(context); break;
                    case "Webhook":        await HandleWebhook(context); break;
                    case "调用API":        await HandleCallAPI(context); break;
                    case "触发工作流":     await HandleTriggerWorkflow(context); break;
                    case "生成工单":       await HandleGenerateWorkOrder(context); break;
                    case "触发MCP任务":    await HandleTriggerMcpTask(context); break;
                    case "触发AI分析":     await HandleTriggerAIAnalysis(context); break;
                    default:
                        Logger.Info(string.Format("[EventProcessing] 未知处理方式: {0}，降级为仅记录", method));
                        await HandleLogOnly(context);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] 处理失败 [{0}]: {1}", method, ex.Message));
            }
        }

        /// <summary>
        /// 同步快捷入口 — 用于 DataProcessor 等高频调用场景
        /// </summary>
        public void Process(SemanticVariableEvent evt)
        {
            Task.Run(() => ProcessAsync(evt)).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Logger.Error(string.Format("[EventProcessing] 异步处理异常: {0}", t.Exception?.InnerException?.Message));
            });
        }

        #region 配置解析与模板替换

        private Dictionary<string, object> ParseConfig(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return new Dictionary<string, object>();
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(json)
                    ?? new Dictionary<string, object>();
            }
            catch { return new Dictionary<string, object>(); }
        }

        private string GetConfigStr(Dictionary<string, object> cfg, string key, string def = "")
            => cfg.TryGetValue(key, out var v) ? v?.ToString() ?? def : def;

        private string[] GetConfigStrArray(Dictionary<string, object> cfg, string key)
        {
            try
            {
                if (cfg.TryGetValue(key, out var v) && v is JArray arr)
                    return arr.ToObject<string[]>() ?? new string[0];
            }
            catch { }
            return new string[0];
        }

        /// <summary>替换模板中的占位符 {FieldName}</summary>
        private string FillTemplate(string template, EventContext ctx)
        {
            if (string.IsNullOrEmpty(template)) return template;
            return template
                .Replace("{EventType}", ctx.Event.EventType ?? "")
                .Replace("{NodeName}", ctx.NodeName ?? "")
                .Replace("{NodeId}", ctx.Event.NodeId ?? "")
                .Replace("{Description}", ctx.Event.Description ?? "")
                .Replace("{OccurredAt}", ctx.Event.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("{ProcessingMethod}", ctx.Event.ProcessingMethod ?? "");
        }

        private string ResolveNodeName(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return "未知节点";
            try
            {
                var n = SemanticService.Instance.GetNode(nodeId);
                return n != null ? n.Name : nodeId;
            }
            catch { return nodeId; }
        }

        #endregion

        #region 处理管线

        /// <summary>1. 仅记录 — 记录到系统日志（默认行为）</summary>
        private Task HandleLogOnly(EventContext ctx)
        {
            Logger.Info(string.Format("[Event] 仅记录 | {0} | {1} | {2}",
                ctx.Event.EventType, ctx.NodeName, ctx.Event.Description));
            return Task.CompletedTask;
        }

        /// <summary>2. 报警 — 发布 MQTT 报警消息 + 日志标记</summary>
        private async Task HandleAlarm(EventContext ctx)
        {
            var alarmLevel = GetConfigStr(ctx.Config, "alarmLevel", "H");
            var alarmTarget = GetConfigStr(ctx.Config, "target", ctx.NodeName);

            Logger.Warn(string.Format("[ALARM] 报警触发 [{0}] {1}: {2}",
                alarmLevel, alarmTarget, ctx.Event.Description));

            // MQTT 报警消息 — 主通道
            try
            {
                var payload = new
                {
                    type = "alarm",
                    level = alarmLevel,
                    target = alarmTarget,
                    nodeId = ctx.Event.NodeId,
                    nodeName = ctx.NodeName,
                    eventType = ctx.Event.EventType,
                    description = ctx.Event.Description,
                    occurredAt = ctx.Event.OccurredAt.ToString("o"),
                    id = ctx.Event.Id
                };
                var json = JsonConvert.SerializeObject(payload);
                await MqttPublishService.Instance.PublishAsync("alarm/event", json);
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] MQTT 报警发布失败: {0}", ex.Message));
            }
        }

        /// <summary>3. 消息通知 — MQTT 通知消息，下游 IM 连接器可订阅</summary>
        private async Task HandleMessageNotify(EventContext ctx)
        {
            var notifyChannel = GetConfigStr(ctx.Config, "channel", "default");
            var notifyTitle = FillTemplate(GetConfigStr(ctx.Config, "title", "[通知] {EventType}"), ctx);
            var notifyBody = FillTemplate(GetConfigStr(ctx.Config, "body", "{Description}"), ctx);

            Logger.Info(string.Format("[Notify] {0} → {1}: {2}", notifyChannel, notifyTitle, notifyBody));

            try
            {
                var payload = new
                {
                    type = "notification",
                    channel = notifyChannel,
                    title = notifyTitle,
                    body = notifyBody,
                    nodeId = ctx.Event.NodeId,
                    nodeName = ctx.NodeName,
                    eventType = ctx.Event.EventType,
                    occurredAt = ctx.Event.OccurredAt.ToString("o")
                };
                var json = JsonConvert.SerializeObject(payload);
                await MqttPublishService.Instance.PublishAsync("notification/event", json);
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] 消息通知发布失败: {0}", ex.Message));
            }
        }

        /// <summary>4. 站内消息 — 持久化到本地事件表，可通过 REST API 查询</summary>
        private Task HandleInSiteMessage(EventContext ctx)
        {
            var msgTitle = FillTemplate(GetConfigStr(ctx.Config, "title", "{EventType}"), ctx);
            var msgBody = FillTemplate(GetConfigStr(ctx.Config, "body", "{Description}"), ctx);
            var msgLevel = GetConfigStr(ctx.Config, "level", "info");

            Logger.Info(string.Format("[InSiteMsg] [{0}] {1}: {2}", msgLevel, msgTitle, msgBody));

            // 站内消息存储在 in_site_messages 表
            try
            {
                var msg = new InSiteMessage
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 12),
                    Title = msgTitle,
                    Body = msgBody,
                    Level = msgLevel,
                    EventId = ctx.Event.Id,
                    NodeId = ctx.Event.NodeId,
                    EventType = ctx.Event.EventType,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                SemanticService.Instance.SaveInSiteMessage(msg);
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] 站内消息存储失败: {0}", ex.Message));
            }

            return Task.CompletedTask;
        }

        /// <summary>5. 邮件 — SMTP 发送（需在 ProcessingConfig 中配置 SMTP 参数）</summary>
        private async Task HandleEmail(EventContext ctx)
        {
            var to = GetConfigStr(ctx.Config, "to", "");
            var subject = FillTemplate(GetConfigStr(ctx.Config, "subject", "[{EventType}] {NodeName}"), ctx);
            var body = FillTemplate(GetConfigStr(ctx.Config, "body", "{Description}"), ctx);
            var smtpHost = GetConfigStr(ctx.Config, "smtpHost", "");
            var smtpPort = int.TryParse(GetConfigStr(ctx.Config, "smtpPort", "25"), out var p) ? p : 25;
            var smtpUser = GetConfigStr(ctx.Config, "smtpUser", "");
            var smtpPass = GetConfigStr(ctx.Config, "smtpPass", "");

            if (string.IsNullOrEmpty(to))
            {
                Logger.Warn("[EventProcessing] 邮件处理方式未配置收件人(to)，跳过发送");
                return;
            }

            Logger.Info(string.Format("[Email] 发送邮件 → {0}: {1}", to, subject));

            try
            {
                if (string.IsNullOrEmpty(smtpHost))
                {
                    // 无 SMTP 配置时，退化为 MQTT 邮件请求（由外部邮件服务处理）
                    var payload = new
                    {
                        type = "email_request",
                        to, subject, body,
                        eventId = ctx.Event.Id,
                        nodeName = ctx.NodeName
                    };
                    await MqttPublishService.Instance.PublishAsync("email/send",
                        JsonConvert.SerializeObject(payload));
                    Logger.Info("[Email] 已发布邮件请求到 MQTT email/send，等待外部邮件服务处理");
                }
                else
                {
                    // 有 SMTP 配置 → 直接发送
                    await SendEmailViaSmtp(smtpHost, smtpPort, smtpUser, smtpPass, to, subject, body);
                    Logger.Info(string.Format("[Email] 邮件发送成功 → {0}", to));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] 邮件发送失败: {0}", ex.Message));
            }
        }

        private async Task SendEmailViaSmtp(string host, int port, string user, string pass,
            string to, string subject, string body)
        {
            // System.Net.Mail 在 .NET Framework 4.8 中可用
            using (var mail = new System.Net.Mail.MailMessage())
            {
                mail.From = new System.Net.Mail.MailAddress(user);
                mail.To.Add(to);
                mail.Subject = subject;
                mail.Body = body;
                mail.BodyEncoding = Encoding.UTF8;
                mail.IsBodyHtml = false;

                using (var smtp = new System.Net.Mail.SmtpClient(host, port))
                {
                    smtp.EnableSsl = port == 587 || port == 465;
                    smtp.Credentials = new System.Net.NetworkCredential(user, pass);
                    await Task.Run(() => smtp.Send(mail));
                }
            }
        }

        /// <summary>6. 短信 — 通过 HTTP API 调用短信网关（阿里云 / 腾讯云 / 自定义）</summary>
        private async Task HandleSMS(EventContext ctx)
        {
            var phones = GetConfigStrArray(ctx.Config, "phones");
            var templateId = GetConfigStr(ctx.Config, "templateId", "");
            var apiUrl = GetConfigStr(ctx.Config, "apiUrl", "");
            var apiKey = GetConfigStr(ctx.Config, "apiKey", "");
            var content = FillTemplate(GetConfigStr(ctx.Config, "content", "{Description}"), ctx);

            if (phones.Length == 0)
            {
                Logger.Warn("[EventProcessing] 短信处理方式未配置手机号(phones)，跳过发送");
                return;
            }

            Logger.Info(string.Format("[SMS] 发送短信 → {0}: {1}",
                string.Join(",", phones), content.Truncate(30)));

            try
            {
                if (string.IsNullOrEmpty(apiUrl))
                {
                    // 无网关配置 → MQTT 短信请求
                    var payload = new
                    {
                        type = "sms_request",
                        phones,
                        templateId,
                        content,
                        eventId = ctx.Event.Id,
                        nodeName = ctx.NodeName
                    };
                    await MqttPublishService.Instance.PublishAsync("sms/send",
                        JsonConvert.SerializeObject(payload));
                    Logger.Info("[SMS] 已发布短信请求到 MQTT sms/send，等待外部短信网关处理");
                }
                else
                {
                    var smsBody = new
                    {
                        phones,
                        templateId,
                        content,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    var json = JsonConvert.SerializeObject(smsBody);
                    var req = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    if (!string.IsNullOrEmpty(apiKey))
                        req.Headers.Add("Authorization", string.Format("Bearer {0}", apiKey));

                    var resp = await _http.SendAsync(req);
                    var respBody = await resp.Content.ReadAsStringAsync();
                    Logger.Info(string.Format("[SMS] 短信网关响应 [{0}]: {1}", (int)resp.StatusCode,
                        respBody.Truncate(100)));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] 短信发送失败: {0}", ex.Message));
            }
        }

        /// <summary>7. Webhook — HTTP POST 到配置的 URL</summary>
        private async Task HandleWebhook(EventContext ctx)
        {
            var url = GetConfigStr(ctx.Config, "url", "");
            var method = GetConfigStr(ctx.Config, "httpMethod", "POST");
            var headersJson = GetConfigStr(ctx.Config, "headers", "{}");
            var bodyTemplate = GetConfigStr(ctx.Config, "body",
                "{\"eventType\":\"{EventType}\",\"nodeName\":\"{NodeName}\",\"description\":\"{Description}\"}");

            if (string.IsNullOrEmpty(url))
            {
                Logger.Warn("[EventProcessing] Webhook 未配置 URL，跳过");
                return;
            }

            var body = FillTemplate(bodyTemplate, ctx);
            Logger.Info(string.Format("[Webhook] {0} {1}", method, url));

            try
            {
                var req = new HttpRequestMessage(
                    method.Equals("POST", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Post :
                    method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Put :
                    method.Equals("DELETE", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Delete :
                    HttpMethod.Get,
                    url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                // 自定义 Headers
                try
                {
                    var headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(headersJson);
                    if (headers != null)
                        foreach (var h in headers)
                            req.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }
                catch { }

                var resp = await _http.SendAsync(req);
                var respBody = await resp.Content.ReadAsStringAsync();
                Logger.Info(string.Format("[Webhook] 响应 [{0}]: {1}", (int)resp.StatusCode,
                    respBody.Truncate(120)));
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] Webhook 调用失败: {0}", ex.Message));
            }
        }

        /// <summary>8. 调用API — 通用 HTTP API 调用（支持 GET/POST/PUT/DELETE + 自定义 Header + Body 模板）</summary>
        private async Task HandleCallAPI(EventContext ctx)
        {
            var url = GetConfigStr(ctx.Config, "url", "");
            var method = GetConfigStr(ctx.Config, "httpMethod", "POST");
            var headersJson = GetConfigStr(ctx.Config, "headers", "{}");
            var bodyTemplate = GetConfigStr(ctx.Config, "body",
                "{\"eventType\":\"{EventType}\",\"description\":\"{Description}\",\"nodeName\":\"{NodeName}\"}");
            var timeoutSec = int.TryParse(GetConfigStr(ctx.Config, "timeout", "15"), out var ts) ? ts : 15;

            if (string.IsNullOrEmpty(url))
            {
                Logger.Warn("[EventProcessing] 调用API 未配置 URL，跳过");
                return;
            }

            var body = FillTemplate(bodyTemplate, ctx);
            Logger.Info(string.Format("[CallAPI] {0} {1}", method, url));

            try
            {
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec)))
                {
                    var req = new HttpRequestMessage(new HttpMethod(method), url)
                    {
                        Content = !string.IsNullOrEmpty(body) && method != "GET"
                            ? new StringContent(body, Encoding.UTF8, "application/json")
                            : null
                    };

                    try
                    {
                        var headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(headersJson);
                        if (headers != null)
                            foreach (var h in headers)
                                req.Headers.TryAddWithoutValidation(h.Key, h.Value);
                    }
                    catch { }

                    var resp = await _http.SendAsync(req, cts.Token);
                    var respBody = await resp.Content.ReadAsStringAsync();
                    Logger.Info(string.Format("[CallAPI] 响应 [{0}]: {1}", (int)resp.StatusCode,
                        respBody.Truncate(200)));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] API 调用失败: {0}", ex.Message));
            }
        }

        /// <summary>9. 触发工作流 — 通过 Webhook 触发外部工作流引擎</summary>
        private async Task HandleTriggerWorkflow(EventContext ctx)
        {
            var workflowUrl = GetConfigStr(ctx.Config, "workflowUrl", "");
            var workflowId = GetConfigStr(ctx.Config, "workflowId", "");

            if (string.IsNullOrEmpty(workflowUrl))
            {
                Logger.Warn("[EventProcessing] 触发工作流未配置 URL，跳过");
                return;
            }

            var payload = new
            {
                workflowId,
                trigger = "event",
                eventType = ctx.Event.EventType,
                nodeId = ctx.Event.NodeId,
                nodeName = ctx.NodeName,
                description = ctx.Event.Description,
                occurredAt = ctx.Event.OccurredAt.ToString("o"),
                eventId = ctx.Event.Id
            };
            var json = JsonConvert.SerializeObject(payload);

            Logger.Info(string.Format("[Workflow] 触发工作流 {0}: {1}", workflowId, workflowUrl));

            try
            {
                var resp = await _http.PostAsync(workflowUrl,
                    new StringContent(json, Encoding.UTF8, "application/json"));
                Logger.Info(string.Format("[Workflow] 工作流响应 [{0}]", (int)resp.StatusCode));
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] 工作流触发失败: {0}", ex.Message));
            }
        }

        /// <summary>10. 生成工单 — 在本地数据库创建工单记录</summary>
        private async Task HandleGenerateWorkOrder(EventContext ctx)
        {
            var priority = GetConfigStr(ctx.Config, "priority", "normal");
            var assignee = GetConfigStr(ctx.Config, "assignee", "");
            var category = GetConfigStr(ctx.Config, "category", "设备维护");
            var title = FillTemplate(GetConfigStr(ctx.Config, "title", "[{EventType}] {NodeName}"), ctx);

            Logger.Info(string.Format("[WorkOrder] 生成工单 [{0}]: {1} → {2}", priority, title, assignee));

            try
            {
                var order = new WorkOrder
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 12),
                    Title = title,
                    Priority = priority,
                    Assignee = assignee,
                    Category = category,
                    EventId = ctx.Event.Id,
                    NodeId = ctx.Event.NodeId,
                    Description = ctx.Event.Description,
                    Status = "待处理",
                    CreatedAt = DateTime.Now
                };
                SemanticService.Instance.SaveWorkOrder(order);

                // MQTT 通知工单系统
                var payload = new
                {
                    type = "work_order_created",
                    orderId = order.Id,
                    title = order.Title,
                    priority = order.Priority,
                    assignee = order.Assignee,
                    status = order.Status
                };
                await MqttPublishService.Instance.PublishAsync("workorder/new",
                    JsonConvert.SerializeObject(payload));
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] 工单生成失败: {0}", ex.Message));
            }
        }

        /// <summary>11. 触发MCP任务 — 调用 MCP 协议执行指定工具</summary>
        private async Task HandleTriggerMcpTask(EventContext ctx)
        {
            var taskName = GetConfigStr(ctx.Config, "taskName", "");
            var taskInput = FillTemplate(GetConfigStr(ctx.Config, "taskInput",
                "{\"eventType\":\"{EventType}\",\"description\":\"{Description}\"}"), ctx);

            if (string.IsNullOrEmpty(taskName))
            {
                Logger.Warn("[EventProcessing] 触发MCP任务未配置 taskName，跳过");
                return;
            }

            Logger.Info(string.Format("[MCP] 触发任务: {0}", taskName));

            try
            {
                var mcpArgs = new JObject
                {
                    { "eventType", ctx.Event.EventType },
                    { "nodeId", ctx.Event.NodeId },
                    { "nodeName", ctx.NodeName },
                    { "description", ctx.Event.Description },
                    { "eventId", ctx.Event.Id }
                };

                // 解析 taskInput JSON 合并进去
                try
                {
                    var extra = JObject.Parse(string.IsNullOrEmpty(taskInput) ? "{}" : taskInput);
                    foreach (var kv in extra)
                        mcpArgs[kv.Key] = kv.Value;
                }
                catch { }

                var result = await McpService.ActiveInstance.ExecuteToolAsync(taskName, mcpArgs);
                Logger.Info(string.Format("[MCP] 任务 {0} 执行完成: {1}",
                    taskName, (result?.ToString() ?? "OK").Truncate(100)));
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] MCP任务执行失败: {0}", ex.Message));
            }
        }

        /// <summary>12. 触发AI分析 — 通过 MCP 调用 AI 进行智能分析</summary>
        private async Task HandleTriggerAIAnalysis(EventContext ctx)
        {
            var analysisType = GetConfigStr(ctx.Config, "analysisType", "general");
            var contextPrompt = FillTemplate(GetConfigStr(ctx.Config, "contextPrompt",
                "节点 {NodeName} 触发事件 [{EventType}]，描述: {Description}。请分析原因并给出建议。"), ctx);

            Logger.Info(string.Format("[AIAnalysis] 触发AI分析 [{0}]: {1}",
                analysisType, contextPrompt.Truncate(60)));

            try
            {
                // 组装 MCP AI 分析请求
                var mcpArgs = new JObject
                {
                    { "analysisType", analysisType },
                    { "eventType", ctx.Event.EventType },
                    { "nodeId", ctx.Event.NodeId },
                    { "nodeName", ctx.NodeName },
                    { "description", ctx.Event.Description },
                    { "contextPrompt", contextPrompt },
                    { "eventId", ctx.Event.Id }
                };

                var result = await McpService.ActiveInstance.ExecuteToolAsync("ai_analyze_event", mcpArgs);
                var resultStr = result?.ToString() ?? "OK";
                Logger.Info(string.Format("[AIAnalysis] AI分析结果: {0}",
                    resultStr.Truncate(200)));

                // 将分析结果写回事件描述
                if (!string.IsNullOrEmpty(resultStr))
                {
                    ctx.Event.Description = string.Format("{0} | AI分析: {1}",
                        ctx.Event.Description, resultStr.Truncate(200));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("[EventProcessing] AI分析失败: {0}", ex.Message));
            }
        }

        #endregion

        #region 辅助类型

        private class EventContext
        {
            public SemanticVariableEvent Event;
            public Dictionary<string, object> Config;
            public string NodeName;
        }

        #endregion
    }
}

namespace IndustrialDataCollection.Utils
{
    public static class StringExtensions
    {
        public static string Truncate(this string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
        }
    }
}
