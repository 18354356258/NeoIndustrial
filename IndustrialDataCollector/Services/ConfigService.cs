using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 配置服务 - 使用 JSON 文件持久化设备与 MQTT 配置
    /// </summary>
    public class ConfigService
    {
        private static readonly Lazy<ConfigService> _instance =
            new Lazy<ConfigService>(() => new ConfigService());
        public static ConfigService Instance
        {
            get { return _instance.Value; }
        }

        /// <summary>
        /// 配置保存后触发（MCP 工具保存后 UI 自动刷新）
        /// </summary>
        public static event Action OnSaved;

        /// <summary>
        /// v2.6.0: 设备层级结构变更版本号——任意 SaveDevices 调用后自增
        /// 所有持有设备树的窗体用此判断是否需要重建
        /// </summary>
        public static long DeviceVersion => _deviceVersion;
        private static long _deviceVersion;
        /// <summary>
        /// v2.6.0: 设备层级结构变更事件（move/rename/add/delete）
        /// </summary>
        public static event Action DeviceHierarchyChanged;

        private readonly string _configDir;
        private readonly string _devicesFile;
        private readonly string _mqttFile;
        private readonly string _groupsFile;
        private MqttConfig _mqttConfig;

        private static readonly System.Text.UTF8Encoding Utf8WithBom = new System.Text.UTF8Encoding(true);

        private ConfigService()
        {
            _configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection", "config");
            _devicesFile = Path.Combine(_configDir, "devices.json");
            _mqttFile = Path.Combine(_configDir, "mqtt.json");
            _groupsFile = Path.Combine(_configDir, "groups.json");
        }

        /// <summary>
        /// 初始化配置目录
        /// </summary>
        public void Init()
        {
            try
            {
                if (!Directory.Exists(_configDir))
                {
                    Directory.CreateDirectory(_configDir);
                    Logger.Info("已创建配置目录: " + _configDir);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("创建配置目录失败: " + ex.Message);
            }
        }

        // ========== 设备配置 ==========

        /// <summary>
        /// 保存所有设备配置（异步）
        /// </summary>
        public async Task SaveDevicesAsync(List<DeviceConfig> devices)
        {
            try
            {
                var json = JsonConvert.SerializeObject(devices, Formatting.Indented);

                // 轮转历史版本
                if (File.Exists(_devicesFile))
                {
                    await Task.Run(() => RotateHistoryBackups());
                    string bak1 = _devicesFile + ".bak.1";
                    try { await Task.Run(() => File.Copy(_devicesFile, bak1, true)); } catch { }
                }

                await Task.Run(() => File.WriteAllText(_devicesFile, json, Utf8WithBom));
                Logger.Info("设备配置已保存: " + devices.Count + " 台设备");
                OnSaved?.Invoke();

                System.Threading.Interlocked.Increment(ref _deviceVersion);
                DeviceHierarchyChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Error("保存设备配置失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 加载所有设备配置（异步）
        /// </summary>
        public async Task<List<DeviceConfig>> LoadDevicesAsync()
        {
            try
            {
                if (!File.Exists(_devicesFile))
                {
                    Logger.Info("设备配置文件不存在，返回空列表");
                    return new List<DeviceConfig>();
                }
                var json = await Task.Run(() => File.ReadAllText(_devicesFile));
                var devices = JsonConvert.DeserializeObject<List<DeviceConfig>>(json);
                return devices ?? new List<DeviceConfig>();
            }
            catch (Exception ex)
            {
                Logger.Error($"[CRITICAL] 设备配置文件损坏（异步加载）: {ex.Message}");
                // 退回到同步加载（含备份恢复逻辑）
                return LoadDevices();
            }
        }

        /// <summary>
        /// 加载所有设备配置（同步）
        /// </summary>
        /// <summary>
        /// 加载设备配置（同步）— 带损坏恢复机制
        /// </summary>
        public List<DeviceConfig> LoadDevices()
        {
            try
            {
                if (!File.Exists(_devicesFile))
                {
                    return new List<DeviceConfig>();
                }
                var json = File.ReadAllText(_devicesFile);
                var devices = JsonConvert.DeserializeObject<List<DeviceConfig>>(json);
                if (devices == null) return new List<DeviceConfig>();
                // Auto-repair: fix null fields that would cause NullReferenceException
                int repaired = 0;
                foreach (var d in devices)
                {
                    if (d.DataPoints == null) { d.DataPoints = new List<DataPoint>(); repaired++; }
                    if (string.IsNullOrEmpty(d.Id)) { d.Id = Guid.NewGuid().ToString(); repaired++; }
                }
                if (repaired > 0)
                {
                    Logger.Warn($"[ConfigService] Auto-repaired {repaired} null fields in devices.json");
                    try { SaveDevices(devices); } catch { }
                }
                return devices;
            }
            catch (Exception ex)
            {
                Logger.Error($"[CRITICAL] 设备配置文件损坏，无法加载: {ex.Message}");
                // 尝试从备份恢复
                string bak = _devicesFile + ".bak";
                if (File.Exists(bak))
                {
                    try
                    {
                        Logger.Info("[RECOVERY] 尝试从备份文件恢复设备配置...");
                        var bakJson = File.ReadAllText(bak);
                        var recovered = JsonConvert.DeserializeObject<List<DeviceConfig>>(bakJson);
                        if (recovered != null && recovered.Count > 0)
                        {
                            // 备份损坏文件，用 bak 恢复
                            string corrupted = _devicesFile + ".corrupted";
                            try { File.Copy(_devicesFile, corrupted, true); } catch { }
                            File.WriteAllText(_devicesFile, bakJson, Utf8WithBom);
                            Logger.Info($"[RECOVERY] 成功从备份恢复 {recovered.Count} 台设备，损坏文件已保存至 .corrupted");
                            return recovered;
                        }
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error($"[RECOVERY] 备份恢复也失败: {ex2.Message}");
                    }
                }
                Logger.Error("[CRITICAL] 无可恢复的备份，返回空设备列表——请检查 devices.json 文件！");
                // 尝试历史轮转备份
                for (int i = 2; i <= 10; i++)
                {
                    string histBak = _devicesFile + ".bak." + i;
                    if (!File.Exists(histBak)) continue;
                    try
                    {
                        var histJson = File.ReadAllText(histBak);
                        var histDevices = JsonConvert.DeserializeObject<List<DeviceConfig>>(histJson);
                        if (histDevices != null && histDevices.Count > 0)
                        {
                            File.WriteAllText(_devicesFile, histJson, Utf8WithBom);
                            Logger.Info($"[RECOVERY] 从历史备份 .bak.{i} 恢复 {histDevices.Count} 台设备");
                            return histDevices;
                        }
                    }
                    catch { }
                }
                return new List<DeviceConfig>();
            }
        }

        /// <summary>
        /// 获取所有设备配置（LoadDevices 别名，供 MCP 等外部查询）
        /// </summary>
        public List<DeviceConfig> GetAllDevices() => LoadDevices();

        /// <summary>
        /// 轮转历史版本最大保留数
        /// </summary>
        private const int MaxHistoryVersions = 50;

        /// <summary>
        /// 保存设备配置（同步）— 轮转历史版本备份（.bak.1 ~ .bak.50）+ 语义层数据库联动备份
        /// </summary>
        public void SaveDevices(List<DeviceConfig> devices)
        {
            try
            {
                var json = JsonConvert.SerializeObject(devices, Formatting.Indented);

                // 轮转历史版本：bak.49→bak.50, ..., bak.1→bak.2, 当前→bak.1
                if (File.Exists(_devicesFile))
                {
                    RotateHistoryBackups();
                    string bak1 = _devicesFile + ".bak.1";
                    try { File.Copy(_devicesFile, bak1, true); } catch { }
                }

                File.WriteAllText(_devicesFile, json, Utf8WithBom);

                // v2.6.0: 语义层数据库联动备份
                string semDb = Path.Combine(_configDir, "semantic_v2.db");
                if (File.Exists(semDb))
                {
                    string semBak1 = semDb + ".bak.1";
                    try { File.Copy(semDb, semBak1, true); } catch { }
                }

                // 语义层 v2: 同步设备配置到节点树
                try
                {
                    SemanticService.Instance.SyncFromDeviceConfigs(devices);
                }
                catch { }

                OnSaved?.Invoke();

                // v2.6.0: 设备层级结构变更通知
                System.Threading.Interlocked.Increment(ref _deviceVersion);
                DeviceHierarchyChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Error("SaveDevices 失败: " + ex.Message);
            }
        }

        // ========== MQTT 配置 ==========

        /// <summary>
        /// 保存 MQTT 配置（异步）
        /// </summary>
        public async Task SaveMqttConfigAsync(MqttConfig config)
        {
            try
            {
                _mqttConfig = config;
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                await Task.Run(() => File.WriteAllText(_mqttFile, json, Utf8WithBom));
                Logger.Info("MQTT 配置已保存");
            }
            catch (Exception ex)
            {
                Logger.Error("保存 MQTT 配置失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 加载 MQTT 配置（异步）
        /// </summary>
        public async Task<MqttConfig> LoadMqttConfigAsync()
        {
            try
            {
                if (!File.Exists(_mqttFile))
                {
                    _mqttConfig = new MqttConfig();
                    return _mqttConfig;
                }
                var json = await Task.Run(() => File.ReadAllText(_mqttFile));
                _mqttConfig = JsonConvert.DeserializeObject<MqttConfig>(json) ?? new MqttConfig();
                return _mqttConfig;
            }
            catch (Exception ex)
            {
                Logger.Error("加载 MQTT 配置失败: " + ex.Message);
                _mqttConfig = new MqttConfig();
                return _mqttConfig;
            }
        }

        /// <summary>
        /// 加载 MQTT 配置（同步）
        /// </summary>
        public MqttConfig LoadMqttConfig()
        {
            try
            {
                if (!File.Exists(_mqttFile))
                {
                    _mqttConfig = new MqttConfig();
                    return _mqttConfig;
                }
                var json = File.ReadAllText(_mqttFile);
                _mqttConfig = JsonConvert.DeserializeObject<MqttConfig>(json) ?? new MqttConfig();
                return _mqttConfig;
            }
            catch
            {
                _mqttConfig = new MqttConfig();
                return _mqttConfig;
            }
        }

        /// <summary>
        /// 保存 MQTT 配置（同步）
        /// </summary>
        public void SaveMqttConfig(MqttConfig config)
        {
            try
            {
                _mqttConfig = config;
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_mqttFile, json, Utf8WithBom);
            }
            catch { }
        }

        /// <summary>
        /// 获取当前 MQTT 配置
        /// </summary>
        public MqttConfig GetMqttConfig()
        {
            if (_mqttConfig != null)
                return _mqttConfig;
            return new MqttConfig();
        }

        public string GroupsFilePath => _groupsFile;

        // ========== 配置历史版本管理 ==========

        /// <summary>
        /// 轮转历史备份：bak.9→bak.10, bak.8→bak.9, ..., bak.1→bak.2，删除超过最大保留数的旧备份
        /// v2.6.0: 同时轮转语义层数据库（semantic_v2.db）备份
        /// </summary>
        private void RotateHistoryBackups()
        {
            // 设备配置备份轮转
            for (int i = MaxHistoryVersions; i >= 1; i--)
            {
                string oldFile = _devicesFile + ".bak." + i;
                if (File.Exists(oldFile))
                {
                    if (i >= MaxHistoryVersions)
                    {
                        try { File.Delete(oldFile); } catch { }
                    }
                    else
                    {
                        string newFile = _devicesFile + ".bak." + (i + 1);
                        try { File.Copy(oldFile, newFile, true); } catch { }
                    }
                }
            }

            // 语义层数据库备份轮转
            string semDb = Path.Combine(_configDir, "semantic_v2.db");
            if (File.Exists(semDb))
            {
                for (int i = MaxHistoryVersions; i >= 1; i--)
                {
                    string oldFile = semDb + ".bak." + i;
                    if (File.Exists(oldFile))
                    {
                        if (i >= MaxHistoryVersions)
                        {
                            try { File.Delete(oldFile); } catch { }
                        }
                        else
                        {
                            string newFile = semDb + ".bak." + (i + 1);
                            try { File.Copy(oldFile, newFile, true); } catch { }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取所有可用历史版本列表（版本号 + 最后修改时间 + 行数/设备数估算）
        /// </summary>
        public List<ConfigHistoryVersion> GetHistoryVersions()
        {
            var versions = new List<ConfigHistoryVersion>();
            for (int i = 1; i <= MaxHistoryVersions; i++)
            {
                string file = _devicesFile + ".bak." + i;
                if (File.Exists(file))
                {
                    var fi = new FileInfo(file);
                    int deviceCount = 0;
                    try
                    {
                        var json = File.ReadAllText(file);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var devices = JsonConvert.DeserializeObject<List<DeviceConfig>>(json);
                            deviceCount = devices?.Count ?? 0;
                        }
                    }
                    catch { }
                    versions.Add(new ConfigHistoryVersion
                    {
                        Version = i,
                        Timestamp = fi.LastWriteTime,
                        FileSize = fi.Length,
                        DeviceCount = deviceCount
                    });
                }
            }
            versions.Reverse(); // 最新在前
            return versions;
        }

        /// <summary>
        /// 从指定版本恢复配置到 devices.json（同步）
        /// </summary>
        public bool RestoreFromHistory(int version)
        {
            string bakFile = _devicesFile + ".bak." + version;
            if (!File.Exists(bakFile))
                return false;

            try
            {
                // 先验证备份 JSON 有效
                var json = File.ReadAllText(bakFile);
                var devices = JsonConvert.DeserializeObject<List<DeviceConfig>>(json);
                if (devices == null || devices.Count == 0)
                {
                    Logger.Error($"[HISTORY] 版本 {version} 的备份为空白，拒绝恢复");
                    return false;
                }

                // 恢复前先保存当前版本（防止恢复操作本身造成损失）
                SaveDevices(devices);
                Logger.Info($"[HISTORY] 成功从历史版本 {version} 恢复 {devices.Count} 台设备配置");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[HISTORY] 从版本 {version} 恢复失败: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 配置历史版本元数据
    /// </summary>
    public class ConfigHistoryVersion
    {
        public int Version { get; set; }
        public DateTime Timestamp { get; set; }
        public long FileSize { get; set; }
        public int DeviceCount { get; set; }
        public string DisplayText => $"版本 {Version} — {Timestamp:yyyy-MM-dd HH:mm:ss} — {DeviceCount} 台设备 ({FileSize / 1024}KB)";
    }
}
