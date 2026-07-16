using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// REST API 服务 — 基于 HttpListener 的轻量 HTTP Server
    /// 暴露设备实时数据，支持 Token 认证 + Swagger 文档
    /// </summary>
    public class RestApiService
    {
        // 单例（供 MCP 等内部服务共享数据缓存）
        private static readonly Lazy<RestApiService> _instance =
            new Lazy<RestApiService>(() => new RestApiService());
        public static RestApiService Instance => _instance.Value;

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private volatile bool _isRunning;

        // 每设备保留最新 N 条数据
        private readonly ConcurrentDictionary<string, List<CollectedData>> _dataCache
            = new ConcurrentDictionary<string, List<CollectedData>>();
        private const int MAX_PER_DEVICE = 500;

        // === 配置 ===
        public int Port { get; set; } = 5000;
        public bool TokenAuthEnabled { get; set; } = true;
        public string ApiToken { get; set; } = "admin123";
        public bool SwaggerEnabled { get; set; } = true;
        public bool IsRunning { get { return _isRunning; } }

        // === 事件 ===
        public event Action<string> OnLog;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DateFormatString = "yyyy-MM-dd HH:mm:ss.fff",
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
        };

        // ======================== 生命周期 ========================

        public void Start()
        {
            if (_isRunning) return;
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add(string.Format("http://+:{0}/", Port));
            try
            {
                _listener.Start();
                _isRunning = true;
                Log("REST API 服务已启动 — http://+:" + Port + " (全网卡)");
                Task.Run(() => ListenLoop(_cts.Token));
            }
            catch (HttpListenerException ex)
            {
                _listener.Close();
                _listener = null;
                string msg = ex.Message.Contains("Access") || ex.Message.Contains("拒绝")
                    ? "权限不足：请以管理员身份运行程序，或执行 netsh http add urlacl url=http://+:" + Port + "/ user=Everyone"
                    : ex.Message;
                Log("启动失败: " + msg);
                throw new Exception("端口 " + Port + " 无法监听: " + msg);
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
            Log("REST API 服务已停止");
        }

        // ======================== 数据喂入 ========================

        public void FeedData(CollectedData data)
        {
            if (data == null || string.IsNullOrEmpty(data.DeviceName)) return;
            var list = _dataCache.GetOrAdd(data.DeviceName, _ => new List<CollectedData>());
            lock (list)
            {
                list.Add(data);
                while (list.Count > MAX_PER_DEVICE)
                    list.RemoveAt(0);
            }
        }

        public List<CollectedData> GetLatestData(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return new List<CollectedData>();
            List<CollectedData> list;
            if (_dataCache.TryGetValue(deviceName, out list))
            {
                lock (list) { return list.ToList(); }
            }
            return new List<CollectedData>();
        }

        public List<string> GetDeviceNames()
        {
            return _dataCache.Keys.OrderBy(k => k).ToList();
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
                    // 不 await — 并发处理
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
                    var resp = context.Response;
                    byte[] err = Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(new { error = "Internal Server Error", detail = ex.Message }));
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
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 200;
                return;  // finally 会 Close
            }

            // Token 认证 — 跳过 Swagger 页面
            string path = req.Url.AbsolutePath.ToLowerInvariant();
            if (TokenAuthEnabled && !string.IsNullOrEmpty(ApiToken)
                && !path.StartsWith("/swagger"))
            {
                string token = req.QueryString["token"];
                if (string.IsNullOrEmpty(token))
                {
                    string auth = req.Headers["Authorization"];
                    if (!string.IsNullOrEmpty(auth))
                        token = auth.Replace("Bearer ", "").Trim();
                }
                if (token != ApiToken)
                {
                    WriteJson(resp, 401, new { error = "Unauthorized", message = "请在 ?token= 参数或 Authorization: Bearer 头中提供有效 Token" });
                    return;
                }
            }

            try
            {
                if (path == "/swagger/index.html" || path == "/swagger" || path == "/swagger/")
                    ServeSwaggerUI(resp);
                else if (path == "/swagger/v1/swagger.json")
                    ServeSwaggerJson(resp);
                else if (path == "/api/devices")
                    HandleGetDevices(resp);
                else if (path == "/api/realtime")
                    HandleGetRealtime(req, resp);
                else if (path.StartsWith("/api/device/") && path.EndsWith("/realtime"))
                    HandleGetRealtimeByPath(path, resp);
                else if (path == "/" || path == "/api")
                    WriteJson(resp, 200, new { service = "MatriX Industrial Data Collector API", version = "1.0", swagger = "/swagger/index.html" });
                else
                    WriteJson(resp, 404, new { error = "Not Found", path = req.Url.AbsolutePath });
            }
            catch (Exception ex)
            {
                WriteJson(resp, 500, new { error = ex.Message });
            }

            // 日志
            Log(string.Format("{0} {1} → {2}", req.HttpMethod, req.Url.PathAndQuery, resp.StatusCode));
        }

        // ======================== 端点实现 ========================

        private void HandleGetDevices(HttpListenerResponse resp)
        {
            var devices = GetDeviceNames().Select(name => new
            {
                name = name,
                variableCount = GetLatestData(name)
                    .GroupBy(d => d.VariableName).Count()
            }).ToList();
            WriteJson(resp, 200, new { devices = devices, total = devices.Count });
        }

        private void HandleGetRealtime(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string device = req.QueryString["device"];
            if (string.IsNullOrEmpty(device))
            {
                WriteJson(resp, 400, new { error = "Missing 'device' parameter. Use ?device=DeviceName" });
                return;
            }
            var data = GetLatestData(device);
            if (data.Count == 0)
            {
                WriteJson(resp, 200, new { device = device, variables = new object[0], count = 0, message = "No data yet" });
                return;
            }
            // 返回每个变量的最新值
            var latest = data.GroupBy(d => d.VariableName).Select(g => new
            {
                variable = g.Key,
                dataType = g.Last().DataType,
                value = g.Last().Value,
                unit = g.Last().Unit,
                timestamp = g.Last().Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            }).ToList();
            WriteJson(resp, 200, new
            {
                device = device,
                variables = latest,
                count = latest.Count,
                updatedAt = data.Last().Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            });
        }

        private void HandleGetRealtimeByPath(string path, HttpListenerResponse resp)
        {
            // /api/device/{name}/realtime
            string prefix = "/api/device/";
            string suffix = "/realtime";
            string deviceName = path.Substring(prefix.Length, path.Length - prefix.Length - suffix.Length);
            deviceName = Uri.UnescapeDataString(deviceName);
            var data = GetLatestData(deviceName);
            if (data.Count == 0)
            {
                WriteJson(resp, 200, new { device = deviceName, variables = new object[0], count = 0 });
                return;
            }
            var latest = data.GroupBy(d => d.VariableName).Select(g => new
            {
                variable = g.Key,
                dataType = g.Last().DataType,
                value = g.Last().Value,
                unit = g.Last().Unit,
                timestamp = g.Last().Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            }).ToList();
            WriteJson(resp, 200, new
            {
                device = deviceName,
                variables = latest,
                count = latest.Count,
                updatedAt = data.Last().Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            });
        }

        // ======================== Swagger ========================

        private void ServeSwaggerUI(HttpListenerResponse resp)
        {
            if (!SwaggerEnabled)
            {
                WriteJson(resp, 404, new { error = "Swagger is disabled" });
                return;
            }
            resp.ContentType = "text/html; charset=utf-8";
            byte[] html = Encoding.UTF8.GetBytes(SwaggerHtml);
            resp.OutputStream.Write(html, 0, html.Length);
            resp.Close();
        }

        private void ServeSwaggerJson(HttpListenerResponse resp)
        {
            if (!SwaggerEnabled)
            {
                WriteJson(resp, 404, new { error = "Swagger is disabled" });
                return;
            }
            string host = string.Format("localhost:{0}", Port);
            string specJson = JsonConvert.SerializeObject(new
            {
                openapi = "3.0.0",
                info = new
                {
                    title = "MatriX 工业数采平台 API",
                    version = "1.0.0",
                    description = "实时工业设备数据查询接口。点击右上角 🔒 Authorize 按钮输入 Token（如 admin123），或通过 ?token=xxx 参数传递。"
                },
                servers = new[] { new { url = string.Format("http://{0}", host), description = "本地服务" } },
                components = new Dictionary<string, object>
                {
                    ["securitySchemes"] = new Dictionary<string, object>
                    {
                        ["ApiKeyQuery"] = new Dictionary<string, object>
                        {
                            ["type"] = "apiKey",
                            ["name"] = "token",
                            ["in"] = "query",
                            ["description"] = "在查询参数中传递 Token：?token=admin123"
                        },
                        ["ApiKeyHeader"] = new Dictionary<string, object>
                        {
                            ["type"] = "http",
                            ["scheme"] = "bearer",
                            ["bearerFormat"] = "plain",
                            ["description"] = "在请求头中传递 Token：Authorization: Bearer admin123"
                        }
                    }
                },
                // 默认安全：仅 /swagger 不认证，其余端点取二者之一
                security = new[]
                {
                    new Dictionary<string, object> { ["ApiKeyQuery"] = new object[0] },
                    new Dictionary<string, object> { ["ApiKeyHeader"] = new object[0] }
                },
                paths = new Dictionary<string, object>
                {
                    ["/api/devices"] = new
                    {
                        get = new
                        {
                            tags = new[] { "设备" },
                            summary = "获取所有设备列表",
                            responses = new Dictionary<string, object>
                            {
                                ["200"] = new { description = "成功", content = new { application = new { json = new { } } } }
                            }
                        }
                    },
                    ["/api/realtime"] = new
                    {
                        get = new
                        {
                            tags = new[] { "实时数据" },
                            summary = "查询设备实时数据（QueryString）",
                            parameters = new[] { new { name = "device", @in = "query", required = true, schema = new { type = "string" }, description = "设备名称" } },
                            responses = new Dictionary<string, object>
                            {
                                ["200"] = new { description = "成功" }
                            }
                        }
                    },
                    ["/api/device/{deviceName}/realtime"] = new
                    {
                        get = new
                        {
                            tags = new[] { "实时数据" },
                            summary = "查询设备实时数据（RESTful路径）",
                            parameters = new[] { new { name = "deviceName", @in = "path", required = true, schema = new { type = "string" }, description = "设备名称" } },
                            responses = new Dictionary<string, object>
                            {
                                ["200"] = new { description = "成功" }
                            }
                        }
                    }
                }
            }, JsonSettings);
            byte[] json = Encoding.UTF8.GetBytes(specJson);
            resp.ContentLength64 = json.Length;
            resp.OutputStream.Write(json, 0, json.Length);
            resp.Close();
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

        private void Log(string msg)
        {
            var handler = OnLog;
            if (handler != null) handler(msg);
        }

        #region Swagger HTML

        private static readonly string SwaggerHtml = @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>MatriX API Docs</title>
  <link rel=""stylesheet"" href=""https://unpkg.com/swagger-ui-dist@5/swagger-ui.css"">
  <style>
    * { margin:0; padding:0; box-sizing:border-box; }
    body { background:#f8f9fa; }
    .swagger-ui .topbar { background:#fff; border-bottom:1px solid #e0e0e0; padding:10px 0; }
    .swagger-ui .topbar .wrapper { display:flex; align-items:center; }
    .swagger-ui .topbar a { display:none; }
    .swagger-ui .info { margin:24px 0; }
    .swagger-ui .info .title { font-size:22px; color:#333; font-weight:600; }
    .swagger-ui .info .description p { color:#666; font-size:13px; }
    .swagger-ui .scheme-container { background:#fff; box-shadow:0 1px 3px rgba(0,0,0,.06); padding:12px 0; }
    .swagger-ui .opblock-tag { font-size:15px; color:#333; border:none; }
    .swagger-ui .opblock { border-radius:6px; box-shadow:0 1px 2px rgba(0,0,0,.04); margin-bottom:8px; }
    .swagger-ui .opblock .opblock-summary { padding:8px 16px; }
    .swagger-ui .btn { border-radius:4px; }
    .swagger-ui .opblock-body pre { background:#f5f5f5; border-radius:4px; font-size:12px; }
    .swagger-ui section.models { display:none; }
    .swagger-ui .markdown p, .swagger-ui .markdown li { font-size:13px; color:#555; }
    .swagger-ui .parameter__name { font-size:13px; }
    .swagger-ui table thead tr td, .swagger-ui table thead tr th { color:#555; font-size:12px; }
  </style>
</head>
<body>
  <div id=""swagger-ui""></div>
  <script src=""https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js""></script>
  <script>
    window.onload=function(){
      var ui = SwaggerUIBundle({
        url:'/swagger/v1/swagger.json',
        dom_id:'#swagger-ui',
        deepLinking:true,
        defaultModelsExpandDepth:-1,
        docExpansion:'list',
        filter:true,
        layout:'BaseLayout',
        onComplete: function() {
          var btn = document.querySelector('.auth-wrapper .authorize');
          if (btn) { btn.style.background='#0d6efd'; btn.style.color='#fff'; btn.style.fontWeight='bold'; }
        }
      });
    };
  </script>
</body>
</html>";

        #endregion
    }
}
