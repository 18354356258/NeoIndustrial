using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Opc.Ua;
using Opc.Ua.Client;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// OPC UA 驱动 - 通过 OPC UA 协议采集工业设备/服务器数据
    /// </summary>
    public class OpcUaDriver : IDriver
    {
        private Session _session;
        private string _serverUrl = "opc.tcp://localhost:4840";
        private DeviceConfig _config;
        private bool _disposed;
        private bool _isConnected;

        public string DriverType => "OpcUa";
        public bool IsConnected => _isConnected && _session?.Connected == true;

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _serverUrl = config.GetParam("ServerUrl", "opc.tcp://localhost:4840");

            try
            {
                // 证书目录: 程序目录下, 不存在则创建
                string certBasePath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "OPC UA");
                string certAppPath = System.IO.Path.Combine(certBasePath, "Certificates");
                string certIssuersPath = System.IO.Path.Combine(certBasePath, "Issuers");
                string certPeersPath = System.IO.Path.Combine(certBasePath, "Peers");
                System.IO.Directory.CreateDirectory(certAppPath);
                System.IO.Directory.CreateDirectory(certIssuersPath);
                System.IO.Directory.CreateDirectory(certPeersPath);

                var appConfig = new ApplicationConfiguration
                {
                    ApplicationName = "IndustrialDataCollection",
                    ApplicationUri = "urn:IndustrialDataCollection",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier
                        {
                            StoreType = "Directory",
                            StorePath = certAppPath,
                            SubjectName = "CN=IndustrialDataCollection"
                        },
                        TrustedIssuerCertificates = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = certIssuersPath
                        },
                        TrustedPeerCertificates = new CertificateTrustList
                        {
                            StoreType = "Directory",
                            StorePath = certPeersPath
                        },
                        AutoAcceptUntrustedCertificates = true,
                        RejectSHA1SignedCertificates = false,
                        MinimumCertificateKeySize = 1024
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas { OperationTimeout = 60000 },
                    ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 120000 }
                };

                await appConfig.Validate(ApplicationType.Client).ConfigureAwait(false);
                if (appConfig.CertificateValidator != null)
                {
                    appConfig.CertificateValidator.CertificateValidation += (s, e) =>
                    {
                        e.Accept = true;
                    };
                }

                // 先发现服务器端点, 列出所有可用的认证方式
                EndpointDescription endpoint = null;
                try
                {
                    var discoveryClient = DiscoveryClient.Create(new Uri(_serverUrl));
                    discoveryClient.OperationTimeout = 10000;
                    var endpoints = discoveryClient.GetEndpoints(null);
                    discoveryClient.Close();

                    if (endpoints != null && endpoints.Count > 0)
                    {
                        Logger.Debug(string.Format("OPC UA 服务器发现 {0} 个端点:", endpoints.Count));
                        foreach (var ep in endpoints)
                        {
                            string secMode = ep.SecurityMode.ToString();
                            string secPolicy = ep.SecurityPolicyUri != null
                                ? ep.SecurityPolicyUri.Substring(ep.SecurityPolicyUri.LastIndexOf('#') + 1)
                                : "None";
                            Logger.Debug(string.Format("  端点: {0} | 安全={1} | 策略={2}",
                                ep.EndpointUrl, secMode, secPolicy));

                            if (ep.UserIdentityTokens != null && ep.UserIdentityTokens.Count > 0)
                            {
                                foreach (var token in ep.UserIdentityTokens)
                                {
                                    Logger.Debug(string.Format("    认证方式: {0}", token.TokenType));
                                }
                            }
                            else
                            {
                                Logger.Debug("    认证方式: (无)");
                            }
                        }
                    }

                    // 选择最佳端点: 优先无安全 + 无加密
                    endpoint = CoreClientUtils.SelectEndpoint(
                        _serverUrl, useSecurity: false, discoverTimeout: 5000);
                    Logger.Debug(string.Format("OPC UA 已选端点: {0} | 安全: {1}",
                        endpoint.EndpointUrl, endpoint.SecurityMode));
                }
                catch (Exception discoverEx)
                {
                    Logger.Warn(string.Format("OPC UA 端点发现失败 ({0}), 跳过直连",
                        discoverEx.Message));
                }

                var endpointConfig = EndpointConfiguration.Create(appConfig);
                endpointConfig.OperationTimeout = 60000;

                ConfiguredEndpoint endpointDescription;
                if (endpoint != null)
                {
                    endpointDescription = new ConfiguredEndpoint(null, endpoint, endpointConfig);
                }
                else
                {
                    // 直连: 按配置选择安全模式和策略
                    string secModeStr = _config.GetParam("SecurityMode", "None");
                    string secPolicyStr = _config.GetParam("SecurityPolicy", "None");

                    MessageSecurityMode secMode = MessageSecurityMode.None;
                    if (secModeStr == "Sign") secMode = MessageSecurityMode.Sign;
                    else if (secModeStr == "SignAndEncrypt") secMode = MessageSecurityMode.SignAndEncrypt;

                    string secPolicyUri = SecurityPolicies.None;
                    if (secPolicyStr == "Basic256Sha256") secPolicyUri = SecurityPolicies.Basic256Sha256;
                    else if (secPolicyStr == "Aes128_Sha256_RsaOaep") secPolicyUri = SecurityPolicies.Aes128_Sha256_RsaOaep;
                    else if (secPolicyStr == "Aes256_Sha256_RsaPss") secPolicyUri = SecurityPolicies.Aes256_Sha256_RsaPss;
                    else if (secPolicyStr == "Basic128Rsa15") secPolicyUri = SecurityPolicies.Basic128Rsa15;
                    else if (secPolicyStr == "Basic256") secPolicyUri = SecurityPolicies.Basic256;

                    Logger.Debug(string.Format("OPC UA 安全配置: {0} / {1}", secModeStr, secPolicyStr));

                    var epDesc = new EndpointDescription
                    {
                        EndpointUrl = _serverUrl,
                        SecurityMode = secMode,
                        SecurityPolicyUri = secPolicyUri,
                        UserIdentityTokens = new UserTokenPolicyCollection
                        {
                            new UserTokenPolicy(UserTokenType.UserName),
                            new UserTokenPolicy(UserTokenType.Anonymous)
                        }
                    };
                    endpointDescription = new ConfiguredEndpoint(
                        null, epDesc, endpointConfig);
                }

                // 构建用户身份
                string username = _config.GetParam("Username", "");
                string password = _config.GetParam("Password", "");
                UserIdentity userIdentity = null;
                if (!string.IsNullOrEmpty(username))
                {
                    userIdentity = new UserIdentity(username, password);
                    Logger.Debug(string.Format("OPC UA 使用凭证登录: {0}", username));
                }
                else
                {
                    Logger.Debug("OPC UA 匿名连接 (未填写凭证)");
                }

                _session = await Session.Create(
                    appConfig, endpointDescription, false,
                    "IndustrialDataCollection", 60000, userIdentity, null
                ).ConfigureAwait(false);

                if (_session != null && _session.Connected)
                {
                    _isConnected = true;
                    Logger.Debug(string.Format("OPC UA 连接成功: {0}", _serverUrl));
                    NotifyStatus(true, string.Format("OPC UA 已连接 ({0})", _serverUrl));
                    return true;
                }
                else
                {
                    _isConnected = false;
                    NotifyStatus(false, "OPC UA 连接失败: 会话未建立");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Logger.Error(string.Format("OPC UA 连接异常 [{0}]: {1}",
                    _config != null ? _config.Name : "", ex.Message));
                if (ex.InnerException != null)
                {
                    Logger.Error(string.Format("OPC UA 内部异常: {0}", ex.InnerException.Message));
                }
                NotifyStatus(false, string.Format("连接失败: {0}", ex.Message));
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            try
            {
                _session?.Close();
                _session?.Dispose();
            }
            catch { }
            _isConnected = false;
            _session = null;
            NotifyStatus(false, "已断开");
            return Task.CompletedTask;
        }

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null) return;
            int pollInterval = _config.GetIntParam("PollInterval", 1000);
            if (pollInterval < 200) pollInterval = 200;

            Logger.Debug(string.Format("OPC UA 采集开始: {0}, URL={1}, 间隔={2}ms",
                _config.Name, _serverUrl, pollInterval));

            // 预解析所有 NodeId
            var nodeIds = new List<NodeIdEntry>();
            foreach (var point in _config.DataPoints)
            {
                if (!point.IsActive) continue;
                try
                {
                    var nodeId = NodeId.Parse(point.Address);
                    nodeIds.Add(new NodeIdEntry { Point = point, NodeId = nodeId });
                }
                catch
                {
                    Logger.Warn(string.Format("OPC UA NodeId 解析失败: {0}", point.Address));
                }
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    if (!IsConnected)
                    {
                        bool connected = await ConnectAsync(_config);
                        if (!connected)
                        {
                            await Task.Delay(3000, token);
                            continue;
                        }
                    }

                    var cycleItems = new List<CycleDataItem>();

                    foreach (var entry in nodeIds)
                    {
                        token.ThrowIfCancellationRequested();

                        object value = await ReadAsync(entry.Point);
                        var data = new CollectedData
                        {
                            DeviceId = _config.Id,
                            DeviceName = _config.Name,
                            VariableName = entry.Point.Name,
                            DataType = entry.Point.DataType,
                            Value = value != null ? value.ToString() : "ERR",
                            Unit = entry.Point.Unit,
                            Tag = entry.Point.OutputTag ? entry.Point.Tag : null,
                            TagCn = entry.Point.OutputTagCn ? entry.Point.TagCn : null,
                            Timestamp = DateTime.Now
                        };
                        OnDataReceived?.Invoke(this, new CollectedDataEventArgs(data));

                        cycleItems.Add(new CycleDataItem
                        {
                            VariableId = entry.Point?.VariableId ?? "",
                            Id = string.Format("{0}|{1}", _config.Name, entry.Point.Name),
                            DataType = entry.Point.DataType,
                            Value = value ?? 0,
                            Unit = entry.Point.Unit,
                            Tag = entry.Point.OutputTag ? entry.Point.Tag : null,
                            TagCn = entry.Point.OutputTagCn ? entry.Point.TagCn : null
                        });
                    }

                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Driver = "opcua",
                        Device = _config.Name,
                        DeviceId = _config.Id, Values = cycleItems
                    }));

                    await Task.Delay(pollInterval, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _isConnected = false;
                    Logger.Warn(string.Format("OPC UA 采集异常 [{0}]: {1}",
                        _config.Name, ex.Message));
                    await Task.Delay(1000, token);
                }
            }

            Logger.Debug(string.Format("OPC UA 采集结束: {0}", _config.Name));
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            try
            {
                var nodeId = NodeId.Parse(point.Address);
                var value = _session.ReadValue(nodeId);

                double doubleVal = 0;
                if (value != null)
                {
                    try { doubleVal = Convert.ToDouble(value); }
                    catch
                    {
                        return Task.FromResult<object>(
                            value != null ? value.ToString() : "N/A");
                    }
                }

                return Task.FromResult<object>(point.ConvertValue(doubleVal));
            }
            catch (Exception ex)
            {
                Logger.Warn(string.Format("OPC UA 读取失败 [{0}]: {1}",
                    point.Address, ex.Message));
                return Task.FromResult<object>(null);
            }
        }

        private void NotifyStatus(bool connected, string message)
        {
            OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(
                _config != null ? _config.Id : "",
                _config != null ? _config.Name : "",
                connected, message));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                DisconnectAsync().Wait();
            }
            catch { }
        }

        /// <summary>
        /// NodeId 预解析条目
        /// </summary>
        private class NodeIdEntry
        {
            public DataPoint Point { get; set; }
            public NodeId NodeId { get; set; }
        }
    }
}
