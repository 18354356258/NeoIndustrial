using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json.Linq;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// MTConnect 驱动 - 通过 HTTP GET 周期性请求 MTConnect Agent 的 REST API
    /// 返回 XML/JSON 格式的机床数据
    /// </summary>
    public class MTConnectDriver : IDriver
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private string _baseUrl = "http://127.0.0.1:5000";
        private int _interval = 1000;
        private DeviceConfig _config;
        private bool _disposed;
        private bool _isConnected;

        public string DriverType => "MTConnect";
        public bool IsConnected => _isConnected;

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _baseUrl = config.GetParam("URL", "http://127.0.0.1:5000");
            _interval = config.GetIntParam("Interval", 1000);

            if (string.IsNullOrWhiteSpace(_baseUrl))
            {
                NotifyStatus(false, "URL 为空");
                return false;
            }

            try
            {
                var response = await _httpClient.GetAsync(_baseUrl);
                if (response.IsSuccessStatusCode)
                {
                    _isConnected = true;
                    NotifyStatus(true, string.Format("MTConnect 已连接 ({0})", _baseUrl));
                    Logger.Debug(string.Format("MTConnect 连接成功: {0}", _baseUrl));
                    return true;
                }
                else
                {
                    _isConnected = false;
                    NotifyStatus(false, string.Format("MTConnect HTTP {0}", response.StatusCode));
                    return false;
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                NotifyStatus(false, string.Format("MTConnect 连接失败: {0}", ex.Message));
                Logger.Warn(string.Format("MTConnect 连接失败 [{0}]: {1}", _config.Name, ex.Message));
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            _isConnected = false;
            NotifyStatus(false, "已断开");
            return Task.CompletedTask;
        }

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null || string.IsNullOrWhiteSpace(_baseUrl)) return;
            int pollInterval = _interval > 0 ? _interval : 1000;
            if (pollInterval < 500) pollInterval = 500;

            Logger.Debug(string.Format("MTConnect 采集开始: {0}, URL={1}, 间隔={2}ms",
                _config.Name, _baseUrl, pollInterval));

            while (!token.IsCancellationRequested)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    var response = await _httpClient.GetAsync(_baseUrl, token);
                    if (!response.IsSuccessStatusCode)
                    {
                        _isConnected = false;
                        await Task.Delay(3000, token);
                        continue;
                    }

                    _isConnected = true;
                    var jsonStr = await response.Content.ReadAsStringAsync();
                    JToken rootToken;

                    try
                    {
                        rootToken = JToken.Parse(jsonStr);
                    }
                    catch
                    {
                        Logger.Warn(string.Format("MTConnect JSON 解析失败 [{0}]", _config.Name));
                        await Task.Delay(pollInterval, token);
                        continue;
                    }

                    var cycleItems = new List<CycleDataItem>();

                    foreach (var point in _config.DataPoints)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!point.IsActive) continue;

                        object value = ExtractValue(rootToken, point);
                        if (value == null) continue;

                        var data = new CollectedData
                        {
                            DeviceId = _config.Id,
                            DeviceName = _config.Name,
                            VariableName = point.Name,
                            DataType = point.DataType,
                            Value = value?.ToString() ?? "N/A",
                            Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null,
                            Timestamp = DateTime.Now
                        };
                        OnDataReceived?.Invoke(this, new CollectedDataEventArgs(data));

                        cycleItems.Add(new CycleDataItem
                        {
                        VariableId = point.VariableId, 
                            Id = string.Format("{0}|{1}", _config.Name, point.Name),
                            DataType = point.DataType,
                            Value = value ?? 0,
                            Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null
                        });
                    }

                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Driver = "mtconnect",
                        Device = _config.Name,
                        DeviceId = _config.Id, Values = cycleItems
                    }));

                    await Task.Delay(pollInterval, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _isConnected = false;
                    Logger.Warn(string.Format("MTConnect 采集异常 [{0}]: {1}", _config.Name, ex.Message));
                    await Task.Delay(3000, token);
                }
            }

            Logger.Debug(string.Format("MTConnect 采集结束: {0}", _config.Name));
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            return Task.FromResult<object>(0);
        }

        private object ExtractValue(JToken root, DataPoint point)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(point.Address) || point.Address == "0")
                {
                    return SelectTokenSafe(root, point.Name);
                }

                var token = root.SelectToken(point.Address);
                if (token != null)
                {
                    double doubleVal = Convert.ToDouble(token.ToString());
                    return point.ConvertValue(doubleVal);
                }

                token = SelectTokenSafe(root, point.Name);
                if (token != null)
                {
                    double doubleVal = Convert.ToDouble(token.ToString());
                    return point.ConvertValue(doubleVal);
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(string.Format("MTConnect 数据提取失败 [{0}]: {1}", point.Name, ex.Message));
                return null;
            }
        }

        private JToken SelectTokenSafe(JToken root, string name)
        {
            var token = root[name];
            if (token != null) return token;
            return FindByPropertyName(root, name);
        }

        private JToken FindByPropertyName(JToken token, string name)
        {
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                        return prop.Value;
                    var found = FindByPropertyName(prop.Value, name);
                    if (found != null) return found;
                }
            }
            else if (token is JArray arr)
            {
                foreach (var item in arr)
                {
                    var found = FindByPropertyName(item, name);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private void NotifyStatus(bool connected, string message)
        {
            OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(
                _config?.Id ?? "", _config?.Name ?? "", connected, message));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _isConnected = false;
        }
    }
}
