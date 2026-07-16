using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IndustrialDataCollection.Services
{
    // ================================================================
    //  MCP 工具接口与元数据 — 新增工具只需实现 IMcpTool + [McpTool] 特性
    // ================================================================

    /// <summary>
    /// 标记一个类为 MCP 工具，自动注册到 McpService 工具表
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class McpToolAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public McpToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// 标记工具方法参数
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class McpParamAttribute : Attribute
    {
        public string Description { get; }
        public bool Required { get; set; } = false;
        public McpParamAttribute(string description) { Description = description; }
    }

    /// <summary>
    /// MCP 工具接口 — 所有工具实现此接口
    /// </summary>
    public interface IMcpTool
    {
        /// <summary>执行工具，返回 JSON 可序列化的结果</summary>
        Task<object> ExecuteAsync(JObject arguments);
    }

    /// <summary>
    /// MCP JSON-RPC 2.0 消息类型
    /// </summary>
    internal class McpRequest
    {
        public string jsonrpc { get; set; }
        public string method { get; set; }
        public JToken @params { get; set; }
        public object id { get; set; }
    }

    internal class McpResponse
    {
        public string jsonrpc { get; set; } = "2.0";
        public object id { get; set; }
        public JToken result { get; set; }
        public JToken error { get; set; }
    }

    internal class McpToolDefinition
    {
        public string name { get; set; }
        public string description { get; set; }
        public McpInputSchema inputSchema { get; set; }
    }

    internal class McpInputSchema
    {
        public string type { get; set; } = "object";
        public Dictionary<string, McpPropertyDef> properties { get; set; }
        public List<string> required { get; set; }
    }

    internal class McpPropertyDef
    {
        public string type { get; set; }
        public string description { get; set; }
    }

    // ================================================================
    //  MCP 工具注册表 — 管理所有工具的实现与元数据
    // ================================================================

    internal class McpToolRegistry
    {
        /// <summary>全局单例引用（McpService 构造时设置）</summary>
        public static McpToolRegistry Instance { get; internal set; }

        private readonly Dictionary<string, IMcpTool> _tools = new Dictionary<string, IMcpTool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, McpToolDefinition> _definitions = new Dictionary<string, McpToolDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 注册一个工具
        /// </summary>
        public void Register(string name, string description, IMcpTool instance,
            Dictionary<string, (string type, string description, bool required)> parameters = null)
        {
            _tools[name] = instance;
            var def = new McpToolDefinition
            {
                name = name,
                description = description,
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertyDef>(),
                    required = new List<string>()
                }
            };
            if (parameters != null)
            {
                foreach (var kv in parameters.OrderBy(x => x.Key))
                {
                    def.inputSchema.properties[kv.Key] = new McpPropertyDef
                    {
                        type = kv.Value.type,
                        description = kv.Value.description
                    };
                    if (kv.Value.required)
                        def.inputSchema.required.Add(kv.Key);
                }
            }
            _definitions[name] = def;
        }

        /// <summary>
        /// 获取所有工具定义（tools/list 响应）
        /// </summary>
        public List<McpToolDefinition> GetDefinitions()
        {
            return _definitions.Values.OrderBy(d => d.name).ToList();
        }

        /// <summary>
        /// 执行指定工具
        /// </summary>
        public async Task<object> ExecuteAsync(string name, JObject arguments)
        {
            IMcpTool tool;
            if (!_tools.TryGetValue(name, out tool))
                throw new KeyNotFoundException("Tool not found: " + name);
            return await tool.ExecuteAsync(arguments ?? new JObject());
        }

        /// <summary>
        /// 注销一个工具
        /// </summary>
        public void Unregister(string name)
        {
            _tools.Remove(name);
            _definitions.Remove(name);
        }
    }

    // ================================================================
    //  MCP 服务主体 — 基于 HttpListener 的 Streamable HTTP 传输
    // ================================================================

    /// <summary>
    /// MCP (Model Context Protocol) 服务 — 为 AI Agent 提供工具调用接口
    /// 基于 JSON-RPC 2.0 / Streamable HTTP，与 REST API 平行运行
    /// </summary>
    public class McpService
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private volatile bool _isRunning;
        private readonly McpToolRegistry _registry = new McpToolRegistry();

        // === 配置 ===
        public int Port { get; set; } = 5101;
        public bool TokenAuthEnabled { get; set; } = true;
        public string McpToken { get; set; } = "admin123";
        public bool IsRunning { get { return _isRunning; } }

        /// <summary>当前活跃的 MCP 服务实例</summary>
        public static McpService ActiveInstance { get; set; }

        /// <summary>服务名称，用于 MCP initialize 响应</summary>
        public string ServerName { get; set; } = "MatriX Industrial Data Platform";

        /// <summary>服务版本</summary>
        public string ServerVersion { get; set; } = "1.6.0";

        // === 事件 ===
        public event Action<string> OnLog;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            DateFormatString = "yyyy-MM-dd HH:mm:ss.fff",
            StringEscapeHandling = StringEscapeHandling.Default
        };

        // ======================== 生命周期 ========================

        /// <summary>初始化并注册所有内置工具</summary>
        public McpService()
        {
            McpToolRegistry.Instance = _registry;
            RegisterBuiltinTools();
        }

        /// <summary>供外部服务调用的工具执行入口（如事件处理引擎）</summary>
        public async Task<object> ExecuteToolAsync(string toolName, JObject arguments)
        {
            return await _registry.ExecuteAsync(toolName, arguments);
        }

        public void Start()
        {
            if (_isRunning) return;
            _cts = new CancellationTokenSource();

            // 端口自动抢占：从配置端口开始，失败则递增尝试（最多10次）
            int startPort = Port;
            int maxTries = 10;
            string lastError = "";

            for (int tryPort = startPort; tryPort < startPort + maxTries; tryPort++)
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(string.Format("http://+:{0}/", tryPort));
                try
                {
                    _listener.Start();
                    _isRunning = true;
                    Port = tryPort; // 更新为实际生效端口
                    // 重建 MCP 数据源工具
                    McpDataSourceRegistry.Rebuild();
                    string msg = string.Format("MCP 服务已启动 — http://+:{0}/mcp (全网卡)", Port);
                    if (tryPort != startPort)
                        msg += string.Format(" [端口 {0} 被占用，自动切换]", startPort);
                    Log(msg);
                    Task.Run(() => ListenLoop(_cts.Token));
                    return;
                }
                catch (HttpListenerException ex)
                {
                    _listener.Close();
                    _listener = null;
                    if (ex.Message.Contains("Access") || ex.Message.Contains("拒绝"))
                    {
                        lastError = "权限不足：请以管理员身份运行程序，或执行 netsh http add urlacl url=http://+:" + tryPort + "/ user=Everyone";
                        break; // 权限问题不重试
                    }
                    lastError = ex.Message;
                    // 端口冲突，尝试下一个
                }
            }

            throw new Exception(string.Format("端口 {0}-{1} 均无法监听: {2}", startPort, startPort + maxTries - 1, lastError));
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
            Log("MCP 服务已停止");
        }

        // ======================== HTTP 监听循环 ========================

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    if (ct.IsCancellationRequested) break;
                    var t = Task.Run(() => HandleRequestSafe(context), ct);
                }
                catch (HttpListenerException) { break; }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void HandleRequestSafe(HttpListenerContext context)
        {
            try
            {
                HandleRequest(context);
            }
            catch (Exception ex)
            {
                try
                {
                    byte[] err = Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(new { jsonrpc = "2.0", id = (object)null, error = new { code = -32603, message = "Internal error: " + ex.Message } }));
                    var resp = context.Response;
                    resp.StatusCode = 500;
                    resp.ContentType = "application/json; charset=utf-8";
                    resp.ContentLength64 = err.Length;
                    resp.OutputStream.Write(err, 0, err.Length);
                    resp.Close();
                }
                catch { }
            }
        }

        // ======================== 路由 ========================

        private void HandleRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            // CORS
            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, Mcp-Session-Id");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 200;
                resp.Close();
                return;
            }

            // 只处理 /mcp 端点（Streamable HTTP 单端点模式）
            string path = req.Url.AbsolutePath.ToLowerInvariant();
            if (path != "/mcp" && path != "/mcp/")
            {
                WriteJsonRpcError(resp, null, -32601, "Method not found: " + path);
                return;
            }

            // Token 认证
            if (TokenAuthEnabled && !string.IsNullOrEmpty(McpToken))
            {
                string token = req.QueryString["token"];
                if (string.IsNullOrEmpty(token))
                {
                    string auth = req.Headers["Authorization"];
                    if (!string.IsNullOrEmpty(auth))
                        token = auth.Replace("Bearer ", "").Trim();
                }
                if (token != McpToken)
                {
                    resp.StatusCode = 401;
                    byte[] body = Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(new { jsonrpc = "2.0", id = (object)null, error = new { code = -32001, message = "Unauthorized: 请在 ?token= 参数或 Authorization: Bearer 头中提供有效 Token" } }));
                    resp.ContentType = "application/json; charset=utf-8";
                    resp.ContentLength64 = body.Length;
                    resp.OutputStream.Write(body, 0, body.Length);
                    resp.Close();
                    return;
                }
            }

            if (req.HttpMethod == "POST")
                HandlePost(req, resp);
            else if (req.HttpMethod == "GET")
                HandleGet(resp);
            else if (req.HttpMethod == "DELETE")
                HandleDelete(resp);
            else
            {
                WriteJsonRpcError(resp, null, -32600, "Method not allowed: " + req.HttpMethod);
                return;
            }

            Log(string.Format("MCP {0} {1} → {2}", req.HttpMethod, req.Url.PathAndQuery, resp.StatusCode));
        }

        // ======================== POST — JSON-RPC 请求处理 ========================

        private void HandlePost(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body;
            using (var reader = new System.IO.StreamReader(req.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                WriteJsonRpcError(resp, null, -32700, "Parse error: empty body");
                return;
            }

            // 可能是单个请求或批量请求（数组）
            JToken root;
            try
            {
                root = JToken.Parse(body);
            }
            catch
            {
                WriteJsonRpcError(resp, null, -32700, "Parse error: invalid JSON");
                return;
            }

            if (root.Type == JTokenType.Array)
            {
                // JSON-RPC 批量请求
                var results = new JArray();
                foreach (var item in root as JArray)
                {
                    try
                    {
                        var reqObj = item.ToObject<McpRequest>();
                        results.Add(ProcessRequest(reqObj));
                    }
                    catch (Exception ex)
                    {
                        results.Add(JToken.FromObject(new McpResponse
                        {
                            id = null,
                            error = JToken.FromObject(new { code = -32603, message = ex.Message })
                        }));
                    }
                }
                // 批量响应中如果全部是通知（无id），返回空
                if (results.All(r => r["id"] == null || r["id"].Type == JTokenType.Null))
                {
                    resp.StatusCode = 202;
                    resp.Close();
                    return;
                }
                WriteJson(resp, 200, results);
            }
            else
            {
                // 单个 JSON-RPC 请求
                var request = root.ToObject<McpRequest>();
                var response = ProcessRequest(request);
                // 通知（无id）不需要响应
                if (request.id == null)
                {
                    resp.StatusCode = 202;
                    resp.Close();
                    return;
                }
                WriteJson(resp, 200, response);
            }
        }

        private McpResponse ProcessRequest(McpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.jsonrpc) || request.jsonrpc != "2.0")
            {
                return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32600, message = "Invalid Request: jsonrpc must be \"2.0\"" }) };
            }

            try
            {
                switch (request.method)
                {
                    case "initialize":
                        return HandleInitialize(request);
                    case "tools/list":
                        return HandleToolsList(request);
                    case "tools/call":
                        return HandleToolsCallSync(request);
                    case "notifications/initialized":
                        return new McpResponse { id = request.id, result = JToken.FromObject(new { }) };
                    case "ping":
                        return new McpResponse { id = request.id, result = JToken.FromObject(new { }) };
                    default:
                        return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32601, message = "Method not found: " + request.method }) };
                }
            }
            catch (KeyNotFoundException ex)
            {
                return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32602, message = ex.Message }) };
            }
            catch (Exception ex)
            {
                Logger.Error("MCP 请求处理失败: " + ex);
                return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32603, message = "Internal error: " + ex.Message }) };
            }
        }

        // ======================== MCP 标准方法实现 ========================

        private McpResponse HandleInitialize(McpRequest request)
        {
            var caps = new JObject
            {
                ["tools"] = new JObject
                {
                    ["listChanged"] = false
                }
            };
            return new McpResponse
            {
                id = request.id,
                result = JToken.FromObject(new
                {
                    protocolVersion = "2025-03-26",
                    capabilities = caps,
                    serverInfo = new
                    {
                        name = ServerName,
                        version = ServerVersion
                    }
                })
            };
        }

        private McpResponse HandleToolsList(McpRequest request)
        {
            var tools = _registry.GetDefinitions();
            return new McpResponse
            {
                id = request.id,
                result = JToken.FromObject(new { tools = tools })
            };
        }

        private McpResponse HandleToolsCallSync(McpRequest request)
        {
            var args = request.@params as JObject;
            if (args == null)
                return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32602, message = "Missing params" }) };

            string toolName = args["name"]?.ToString();
            if (string.IsNullOrEmpty(toolName))
                return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32602, message = "Missing tool name" }) };

            JObject arguments = args["arguments"] as JObject ?? new JObject();

            try
            {
                var result = _registry.ExecuteAsync(toolName, arguments).GetAwaiter().GetResult();
                var content = new JArray();
                content.Add(new JObject
                {
                    ["type"] = "text",
                    ["text"] = result is string ? (string)result : JsonConvert.SerializeObject(result, JsonSettings)
                });
                return new McpResponse
                {
                    id = request.id,
                    result = JToken.FromObject(new { content = content })
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    id = request.id,
                    result = JToken.FromObject(new
                    {
                        content = new JArray { new JObject { ["type"] = "text", ["text"] = "Error: " + ex.Message } },
                        isError = true
                    })
                };
            }
        }

                // HandleToolsCallSync handles tools/call synchronously

        // ======================== GET / DELETE ========================

        private void HandleGet(HttpListenerResponse resp)
        {
            // GET — 返回 SSE 流（Streamable HTTP 规范），简单实现返回服务信息后关闭
            WriteJson(resp, 200, new
            {
                jsonrpc = "2.0",
                result = new
                {
                    protocolVersion = "2025-03-26",
                    serverInfo = new { name = ServerName, version = ServerVersion },
                    endpoint = string.Format("/mcp (POST — JSON-RPC 2.0)", Port)
                }
            });
        }

        private void HandleDelete(HttpListenerResponse resp)
        {
            // DELETE — 会话结束（Streamable HTTP 规范）
            WriteJson(resp, 200, new { jsonrpc = "2.0", result = new { message = "Session closed" } });
        }

        // ======================== 批量包装：异步 → 同步适配 ========================

        private McpResponse ProcessRequestSync(McpRequest request)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    return await ProcessRequestAsync(request);
                }
                catch (Exception ex)
                {
                    return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32603, message = ex.Message }) };
                }
            });
            return task.GetAwaiter().GetResult();
        }

        private async Task<McpResponse> ProcessRequestAsync(McpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.jsonrpc) || request.jsonrpc != "2.0")
            {
                return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32600, message = "Invalid Request: jsonrpc must be \"2.0\"" }) };
            }

            try
            {
                switch (request.method)
                {
                    case "initialize":
                        return HandleInitialize(request);
                    case "tools/list":
                        return HandleToolsList(request);
                    case "tools/call":
                        return HandleToolsCallSync(request);
                    case "notifications/initialized":
                        return new McpResponse { id = request.id, result = JToken.FromObject(new { }) };
                    case "ping":
                        return new McpResponse { id = request.id, result = JToken.FromObject(new { }) };
                    default:
                        return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32601, message = "Method not found: " + request.method }) };
                }
            }
            catch (KeyNotFoundException ex)
            {
                return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32602, message = ex.Message }) };
            }
            catch (Exception ex)
            {
                Logger.Error("MCP 请求处理失败: " + ex);
                return new McpResponse { id = request.id, error = JToken.FromObject(new { code = -32603, message = "Internal error: " + ex.Message }) };
            }
        }

                // End of duplicate async handlers cleared

        // ======================== 内置工具注册 ========================

        private void RegisterBuiltinTools()
        {
            // 工具 1: 查询实时数据
            _registry.Register("query_realtime_data",
                "查询指定设备的所有变量实时数据。返回变量名、值、单位、时间戳、数据类型。device_id 优先。",
                new QueryRealtimeTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["device_id"] = ("string", "设备GUID，首选。与 device_name 二选一", false),
                    ["device_name"] = ("string", "设备名称，备选", false)
                });

            // 工具 2: 设备列表
            _registry.Register("list_devices",
                "列出所有已配置的设备及其采集状态。返回设备名、驱动类型、是否正在采集。",
                new ListDevicesTool());

            // 工具 3: 设备状态
            _registry.Register("get_device_status",
                "获取单个设备的详细状态：连接状态、变量数、最后采集时间、运行错误信息。device_id 优先。",
                new GetDeviceStatusTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["device_id"] = ("string", "设备GUID，首选。与 device_name 二选一", false),
                    ["device_name"] = ("string", "设备名称，备选", false)
                });

            // 工具 4: 设备配置
            _registry.Register("get_device_config",
                "获取设备的完整配置信息：变量点列表、采样间隔、驱动参数。device_id 优先。",
                new GetDeviceConfigTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["device_id"] = ("string", "设备GUID，首选。与 device_name 二选一", false),
                    ["device_name"] = ("string", "设备名称，备选", false)
                });

            // 工具 5: 历史数据查询
            _registry.Register("query_history_data",
                "从数据库查询指定设备/变量的历史数据。支持时间范围过滤。",
                new QueryHistoryTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["device_name"] = ("string", "设备名称，可选。不填则查询全部设备", false),
                    ["variable_name"] = ("string", "变量名，可选。不填则查询设备的全部变量", false),
                    ["start_time"] = ("string", "起始时间，可选。格式 yyyy-MM-dd HH:mm:ss", false),
                    ["end_time"] = ("string", "结束时间，可选。格式 yyyy-MM-dd HH:mm:ss", false),
                    ["limit"] = ("integer", "返回条数上限，可选。默认 100", false)
                });

            // 工具 6: 数据库状态
            _registry.Register("get_database_status",
                "查询数据库写入服务的状态：各数据库连接是否正常、已启用哪些数据库。",
                new GetDatabaseStatusTool());

            // 工具 7: 修复数据库连接
            _registry.Register("repair_database",
                "尝试修复所有断开的数据库连接（重连+健康检查）。返回修复后各数据库的连接状态。适用于：数据库重启、网络闪断后恢复。",
                new RepairDatabaseTool());

            // ── 设备 CRUD 工具（Phase 1: AI 可配置设备） ──

            _registry.Register("add_device",
                "创建新采集设备。支持全部40种驱动协议。name 必填，driver 默认为 Simulator。",
                new AddDeviceTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["name"] = ("string", "设备名称，必填", true),
                    ["driver"] = ("string", "驱动类型，默认Simulator", false),
                    ["ip"] = ("string", "设备IP地址", false),
                    ["port"] = ("integer", "端口号", false),
                    ["group"] = ("string", "分组路径", false),
                    ["scan_interval_ms"] = ("integer", "扫描间隔毫秒", false)
                });

            _registry.Register("update_device",
                "修改设备配置（名称、IP、端口、分组、扫描间隔）。device_id 优先。",
                new UpdateDeviceTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["device_id"] = ("string", "设备GUID，首选", false),
                    ["device_name"] = ("string", "设备名称，备选", false),
                    ["name"] = ("string", "新名称", false),
                    ["ip"] = ("string", "新IP", false),
                    ["port"] = ("integer", "新端口", false),
                    ["group"] = ("string", "新分组", false),
                    ["scan_interval_ms"] = ("integer", "新扫描间隔", false)
                });

            _registry.Register("start_device",
                "启动指定设备的采集任务。device_id 优先。设备必须有变量点才能启动。",
                new StartDeviceTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["device_id"] = ("string", "设备GUID，首选", false),
                    ["device_name"] = ("string", "设备名称，备选", false)
                });

            _registry.Register("stop_device",
                "停止指定设备的采集任务。不影响其他设备。",
                new StopDeviceTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["device_id"] = ("string", "设备GUID，首选", false),
                    ["device_name"] = ("string", "设备名称，备选", false)
                });

            _registry.Register("add_variables",
                "给指定设备批量添加变量点。points 为数组，每项含 name/address/data_type/unit 等字段。device_id 优先。",
                new AddVariablesTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["device_id"] = ("string", "设备GUID，首选", false),
                    ["device_name"] = ("string", "设备名称，备选", false),
                    ["points"] = ("string", "变量点数组JSON", true)
                });

            _registry.Register("update_variables",
                "修改设备变量点参数（名称、单位、报警阈值等）。通过 point_id 或 name 定位。device_id 优先。",
                new UpdateVariablesTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["device_id"] = ("string", "设备GUID，首选", false),
                    ["device_name"] = ("string", "设备名称，备选", false),
                    ["points"] = ("string", "变量点更新数组JSON", true)
                });

            _registry.Register("reload_config",
                "热重载设备配置，使 add_device/add_variables/update_device 等变更立即生效。无需重启应用。",
                new ReloadConfigTool());

            // ======================== 数据源分析工具 ========================

            _registry.Register("datasource_list_all",
                "列出所有已配置的数据源及其状态（类型、连接状态、表数量）。不传参数。",
                new DataSourceListAllTool());

            _registry.Register("datasource_table_info",
                "获取指定数据源中某个表的统计信息：行数、列数、列名和类型、最新时间戳、用途分类。适合快速了解一张表里有什么数据。",
                new DataSourceTableInfoTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["source_id"] = ("string", "数据源ID（可从 datasource_list_all 获取）", true),
                    ["table_name"] = ("string", "要查询的表名", true)
                });

            _registry.Register("datasource_latest_data",
                "查询某个表的最新N条记录，快速预览数据内容。不传 limit 默认10条。",
                new DataSourceLatestDataTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["source_id"] = ("string", "数据源ID", true),
                    ["table_name"] = ("string", "表名", true),
                    ["limit"] = ("number", "返回条数，默认10，最大200", false)
                });

            _registry.Register("datasource_query_timerange",
                "按时间范围查询某个表的数据。需要表中有时间戳列（ts/timestamp/time）。支持过滤条件。",
                new DataSourceQueryTimerangeTool(),
                new Dictionary<string, (string, string, bool)>
                {
                    ["source_id"] = ("string", "数据源ID", true),
                    ["table_name"] = ("string", "表名", true),
                    ["time_column"] = ("string", "时间列名，默认 ts", false),
                    ["time_start"] = ("string", "开始时间，格式 yyyy-MM-dd HH:mm:ss", true),
                    ["time_end"] = ("string", "结束时间，格式 yyyy-MM-dd HH:mm:ss", true),
                    ["columns"] = ("string", "要查询的列，逗号分隔，默认 *", false),
                    ["limit"] = ("number", "最大返回行数，默认100", false)
                });

            // v2.5.1: 平台自描述工具（社区版：overview/drivers/tools）
            var introService = new McpPlatformIntroService();
            _registry.Register("introduce_platform",
                "介绍工业数采平台的核心能力、40种驱动、MCP工具分类和使用方法。AI Agent连接后建议优先调用此工具了解平台全貌。支持 topic 参数：overview(概览)/drivers(驱动)/tools(工具)/best_practices(最佳实践)",
                new IntroducePlatformTool(introService),
                new Dictionary<string, (string, string, bool)>
                {
                    ["topic"] = ("string", "主题：overview(默认)/drivers/tools/best_practices", false)
                });
        }

        // ======================== 工具方法 ========================

        private void WriteJson(HttpListenerResponse resp, int status, object data)
        {
            resp.StatusCode = status;
            resp.ContentType = "application/json; charset=utf-8";
            string json = JsonConvert.SerializeObject(data, JsonSettings);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }

        private void WriteJsonRpcError(HttpListenerResponse resp, object id, int code, string message)
        {
            WriteJson(resp, 400, new McpResponse
            {
                id = id,
                error = JToken.FromObject(new { code = code, message = message })
            });
        }

        private void Log(string msg)
        {
            var handler = OnLog;
            if (handler != null) handler(msg);
        }

        // ======================== 工具辅助方法 ========================

        /// <summary>
        /// 解析设备参数：优先 device_id（ASCII 安全），降级 device_name
        /// </summary>
        public static DeviceConfig ResolveDevice(JObject args)
        {
            var configs = ConfigService.Instance.GetAllDevices();

            string deviceId = args["device_id"]?.ToString();
            if (!string.IsNullOrEmpty(deviceId))
                return configs.FirstOrDefault(c => c.Id == deviceId);

            string deviceName = args["device_name"]?.ToString();
            if (!string.IsNullOrEmpty(deviceName))
                return configs.FirstOrDefault(c =>
                    string.Equals(c.Name, deviceName, StringComparison.OrdinalIgnoreCase));

            return null;
        }
    }

    // ================================================================
    //  内置工具实现
    // ================================================================

    /// <summary>查询实时数据</summary>
    internal class QueryRealtimeTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            var config = McpService.ResolveDevice(args);
            if (config == null)
                return Task.FromResult<object>(new { error = "未找到设备。请提供 device_id 或 device_name" });

            string deviceName = config.Name;
            var data = RestApiService.Instance.GetLatestData(deviceName);

            var variables = data.GroupBy(d => d.VariableName).Select(g =>
            {
                var last = g.Last();
                return new
                {
                    variable = g.Key,
                    data_type = last.DataType,
                    value = last.Value,
                    unit = last.Unit,
                    tag = last.Tag,
                    tag_cn = last.TagCn,
                    timestamp = last.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
            }).ToList();

            return Task.FromResult<object>(new
            {
                device = deviceName,
                variable_count = variables.Count,
                variables = variables
            });
        }
    }

    /// <summary>设备列表</summary>
    internal class ListDevicesTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            var service = DataCollectionService.Instance;
            var configs = ConfigService.Instance.GetAllDevices();
            var runningIds = new HashSet<string>(service.GetRunningDeviceIds());

            var devices = configs.Select(c => new
            {
                name = c.Name,
                driver = service.GetDeviceDriverType(c.Id) ?? c.DriverType,
                is_running = runningIds.Contains(c.Id),
                variable_count = c.DataPoints?.Count ?? 0,
                group = c.Group ?? "",
                tag_path = c.TagPath ?? "",
                tag_path_cn = c.TagPathCn ?? ""
            }).OrderBy(d => d.group).ThenBy(d => d.name).ToList();

            return Task.FromResult<object>(new
            {
                total = devices.Count,
                running_count = devices.Count(d => d.is_running),
                devices = devices
            });
        }
    }

    /// <summary>设备状态</summary>
    internal class GetDeviceStatusTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            var config = McpService.ResolveDevice(args);
            if (config == null)
                return Task.FromResult<object>(new { error = "未找到设备。请提供 device_id 或 device_name" });

            var service = DataCollectionService.Instance;
            var runningIds = new HashSet<string>(service.GetRunningDeviceIds());
            bool isRunning = runningIds.Contains(config.Id);
            string driverType = service.GetDeviceDriverType(config.Id) ?? config.DriverType;

            return Task.FromResult<object>(new
            {
                name = config.Name,
                device_id = config.Id,
                driver = driverType,
                is_running = isRunning,
                variable_count = config.DataPoints?.Count ?? 0,
                latest_data = isRunning ? "运行中" : "已停止"
            });
        }
    }

    /// <summary>设备配置</summary>
    internal class GetDeviceConfigTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            var config = McpService.ResolveDevice(args);
            if (config == null)
                return Task.FromResult<object>(new { error = "未找到设备。请提供 device_id 或 device_name" });

            var points = (config.DataPoints ?? new List<DataPoint>())
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    name = p.Name,
                    address = p.Address,
                    data_type = p.DataType,
                    unit = p.Unit,
                    scale_factor = p.ScaleFactor,
                    offset = p.Offset,
                    tag = p.Tag,
                    tag_cn = p.TagCn,
                    alarm_enabled = p.AlarmEnabled,
                    alarm_h = p.AlarmH,
                    alarm_l = p.AlarmL
                }).ToList();

            return Task.FromResult<object>(new
            {
                name = config.Name,
                driver = config.DriverType,
                group = config.Group ?? "",
                active_variable_count = points.Count,
                variables = points
            });
        }
    }

    /// <summary>历史数据查询</summary>
    internal class QueryHistoryTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            string deviceName = args["device_name"]?.ToString();
            string variableName = args["variable_name"]?.ToString();
            string startTime = args["start_time"]?.ToString();
            string endTime = args["end_time"]?.ToString();
            int limit = 100;
            if (args["limit"] != null) int.TryParse(args["limit"].ToString(), out limit);
            if (limit > 1000) limit = 1000;
            if (limit < 1) limit = 100;

            try
            {
                var results = await DatabaseWriteService.Instance.QueryHistoryAsync(
                    deviceName, variableName, startTime, endTime, limit);

                return new
                {
                    device = deviceName ?? "(全部)",
                    variable = variableName ?? "(全部)",
                    start_time = startTime ?? "(不限)",
                    end_time = endTime ?? "(不限)",
                    count = results.Count,
                    records = results.Select(r => new
                    {
                        device = r.device,
                        variable = r.variable,
                        value = r.value,
                        unit = r.unit,
                        tag = r.tag,
                        tag_cn = r.tag_cn,
                        timestamp = r.timestamp
                    })
                };
            }
            catch (Exception ex)
            {
                return new { error = "查询历史数据失败: " + ex.Message, detail = ex.ToString() };
            }
        }
    }

    /// <summary>数据库状态</summary>
    internal class GetDatabaseStatusTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            var dbService = DatabaseWriteService.Instance;
            var statuses = dbService.GetConnectionStatuses();

            return Task.FromResult<object>(new
            {
                any_enabled = dbService.IsAnyEnabled,
                databases = statuses.Select(kv => new
                {
                    type = kv.Key,
                    enabled = kv.Value.enabled,
                    connected = kv.Value.connected,
                    device_count = kv.Value.deviceCount
                }).ToList()
            });
        }
    }

    /// <summary>修复数据库连接</summary>
    internal class RepairDatabaseTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            var dbService = DatabaseWriteService.Instance;

            // 调用健康检查触发重连
            bool healthy = dbService.EnsureConnectionsHealthy();

            // 返回修复后的状态
            var statuses = dbService.GetConnectionStatuses();
            var repaired = statuses.Where(kv => kv.Value.enabled && kv.Value.connected).ToList();
            var stillBroken = statuses.Where(kv => kv.Value.enabled && !kv.Value.connected).ToList();

            return Task.FromResult<object>(new
            {
                repair_triggered = true,
                healthy = healthy,
                repaired_count = repaired.Count,
                still_broken_count = stillBroken.Count,
                repaired = repaired.Select(kv => kv.Key).ToList(),
                still_broken = stillBroken.Select(kv => kv.Key).ToList(),
                databases = statuses.Select(kv => new
                {
                    type = kv.Key,
                    enabled = kv.Value.enabled,
                    connected = kv.Value.connected,
                    device_count = kv.Value.deviceCount
                }).ToList()
            });
        }
    }

    /// <summary>v2.5.1: 平台自描述工具</summary>
    internal class IntroducePlatformTool : IMcpTool
    {
        private readonly McpPlatformIntroService _introService;
        public IntroducePlatformTool(McpPlatformIntroService introService) { _introService = introService; }

        public async Task<object> ExecuteAsync(Newtonsoft.Json.Linq.JObject args)
        {
            string topic = args["topic"]?.ToString() ?? "overview";
            try
            {
                var result = _introService.IntroducePlatform(topic);
                return new { platform_intro = result };
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }
    }
}
