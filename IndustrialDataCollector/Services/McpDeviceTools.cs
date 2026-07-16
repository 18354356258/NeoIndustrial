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
    //  设备 CRUD MCP 工具 — AI 可对话管理设备/变量配置
    // ================================================================

    [McpTool("add_device", "创建新采集设备。支持所有40种驱动协议。")]
    internal class AddDeviceTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            string name = args["name"]?.Value<string>() ?? "";
            string driver = args["driver"]?.Value<string>() ?? "Simulator";
            string ip = args["ip"]?.Value<string>() ?? "";
            int port = args["port"]?.Value<int>() ?? 502;
            string group = args["group"]?.Value<string>() ?? "";
            int scanInterval = args["scan_interval_ms"]?.Value<int>() ?? 1000;

            if (string.IsNullOrWhiteSpace(name))
                return Task.FromResult<object>(new { error = "参数 name（设备名称）不能为空" });

            var knownDrivers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "ModbusTcp","ModbusRtu","SiemensS7","Siemens840D","OpcUa","OPCDA","OpcUaPubSub",
                "BACnet","EtherNetIp","Profinet","PROFIBUS","BeckhoffADS","CODESYS",
                "MitsubishiFX","MELSECMc","KeyenceKV","PanasonicMewtocol","Fins","HostLink",
                "FanucFocas","HaasCNC","Mazak","Heidenhain","IEC104","IEC61850","DNP3",
                "KNX","DALI","LonWorks","MBus","DLMS","HARTIP","DeviceNet","CCLink",
                "SecsGem","MTConnect","MqttSubscribe","SparkplugB","HttpRest","Simulator"
            };
            if (!knownDrivers.Contains(driver))
                return Task.FromResult<object>(new { error = $"不支持的驱动: {driver}" });

            var dev = new DeviceConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                DriverType = driver,
                Group = group
            };
            if (!string.IsNullOrWhiteSpace(ip)) dev.ConnectionParams["ip"] = ip;
            if (port > 0) dev.ConnectionParams["port"] = port.ToString();
            dev.ConnectionParams["scanIntervalMs"] = scanInterval.ToString();

            var all = ConfigService.Instance.GetAllDevices();
            all.Add(dev);
            ConfigService.Instance.SaveDevices(all);
            Logger.Info($"[MCP CONFIG] AI add_device: {name} [{driver}] id={dev.Id}");
            try { SemanticService.Instance.SyncFromDeviceConfigs(all); } catch (Exception ex) { Logger.Error($"[MCP CONFIG] 语义层同步失败: {ex.Message}"); }
            try { TagMigrationService.Migrate(all); } catch { }

            return Task.FromResult<object>(new
            {
                success = true,
                device_id = dev.Id,
                name = dev.Name,
                driver = dev.DriverType,
                message = $"设备 '{name}' 已创建。调用 reload_config 使配置生效。"
            });
        }
    }

    [McpTool("update_device", "修改设备配置（名称、IP、端口、分组、扫描间隔）。")]
    internal class UpdateDeviceTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            var dev = McpService.ResolveDevice(args);
            if (dev == null) return Task.FromResult<object>(new { error = "未找到设备" });

            var all = ConfigService.Instance.GetAllDevices();
            var device = all.FirstOrDefault(d => d.Id == dev.Id);

            string nm = args["name"]?.Value<string>();
            string ip = args["ip"]?.Value<string>();
            int? pt = args["port"]?.Value<int>();
            string grp = args["group"]?.Value<string>();
            int? si = args["scan_interval_ms"]?.Value<int>();

            var changes = new List<string>();
            if (!string.IsNullOrWhiteSpace(nm) && nm != device.Name) { device.Name = nm; changes.Add("名称"); }
            if (!string.IsNullOrWhiteSpace(ip)) { device.ConnectionParams["ip"] = ip; changes.Add("IP"); }
            if (pt.HasValue && pt > 0) { device.ConnectionParams["port"] = pt.ToString(); changes.Add("端口"); }
            if (!string.IsNullOrWhiteSpace(grp)) { device.Group = grp; changes.Add("分组"); }
            if (si.HasValue && si > 0) { device.ConnectionParams["scanIntervalMs"] = si.ToString(); changes.Add("扫描间隔"); }

            if (changes.Count == 0)
                return Task.FromResult<object>(new { error = "未提供需要更新的字段" });

            ConfigService.Instance.SaveDevices(all);
            try { SemanticService.Instance.SyncFromDeviceConfigs(all); } catch (Exception ex) { Logger.Error($"[MCP CONFIG] 语义层同步失败(update_device): {ex.Message}"); }
            Logger.Info($"[MCP CONFIG] AI update_device: {device.Name} ({device.Id}) changes={string.Join(",", changes)}");

            return Task.FromResult<object>(new
            {
                success = true,
                device_id = device.Id,
                changes = changes,
                message = $"已更新设备 '{device.Name}'。reload_config 使变更生效。"
            });
        }
    }

    [McpTool("add_variables", "给设备批量添加变量点。格式: {device_id:\"xx\", points:[{name:\"温度\",address:\"0\",data_type:\"float\",unit:\"℃\"}]}")]
    internal class AddVariablesTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            var dev = McpService.ResolveDevice(args);
            if (dev == null) return Task.FromResult<object>(new { error = "未找到设备" });

            var all = ConfigService.Instance.GetAllDevices();
            var device = all.FirstOrDefault(d => d.Id == dev.Id);
            if (device == null) return Task.FromResult<object>(new { error = "设备不存在" });

            var ptsTok = args["points"];
            JArray ptsArr = ptsTok as JArray;
            if (ptsArr == null && ptsTok?.Type == JTokenType.String)
                ptsArr = JArray.Parse(ptsTok.Value<string>());
            if (ptsArr == null || ptsArr.Count == 0)
                return Task.FromResult<object>(new { error = "参数 points 不能为空" });

            var added = new List<object>();
            foreach (var pt in ptsArr)
            {
                var dp = new DataPoint
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = pt["name"]?.Value<string>() ?? "unnamed",
                    Address = pt["address"]?.Value<string>() ?? "0",
                    DataType = pt["data_type"]?.Value<string>() ?? "float",
                    Unit = pt["unit"]?.Value<string>() ?? "",
                    IsActive = true
                };
                if (pt["scale_factor"] != null) dp.ScaleFactor = pt["scale_factor"].Value<double>();
                if (pt["offset"] != null) dp.Offset = pt["offset"].Value<double>();
                if (pt["length"] != null) dp.Length = pt["length"].Value<int>();

                if (pt["alarm_h"] != null)
                { dp.AlarmHH = pt["alarm_h"].Value<double>(); dp.AlarmHH_Enabled = true; dp.AlarmEnabled = true; }
                if (pt["alarm_h_warn"] != null)
                { dp.AlarmH = pt["alarm_h_warn"].Value<double>(); dp.AlarmH_Enabled = true; dp.AlarmEnabled = true; }
                if (pt["alarm_l"] != null)
                { dp.AlarmL = pt["alarm_l"].Value<double>(); dp.AlarmL_Enabled = true; dp.AlarmEnabled = true; }
                if (pt["alarm_l_warn"] != null)
                { dp.AlarmLL = pt["alarm_l_warn"].Value<double>(); dp.AlarmLL_Enabled = true; dp.AlarmEnabled = true; }

                device.DataPoints.Add(dp);
                added.Add(new { point_id = dp.Id, name = dp.Name, address = dp.Address });
            }

            ConfigService.Instance.SaveDevices(all);
            try { SemanticService.Instance.SyncFromDeviceConfigs(all); } catch (Exception ex) { Logger.Error($"[MCP CONFIG] 语义层同步失败(add_variables): {ex.Message}"); }
            try { TagMigrationService.Migrate(all); } catch { }
            Logger.Info($"[MCP CONFIG] AI add_variables: device={device.Name}, count={added.Count}");

            return Task.FromResult<object>(new
            {
                success = true,
                device_id = dev.Id,
                added_count = added.Count,
                points = added,
                message = $"已添加 {added.Count} 个变量点到设备 '{device.Name}'。调用 reload_config 使变更生效。"
            });
        }
    }

    [McpTool("update_variables", "修改设备变量点参数。通过 point_id 或 name 定位。")]
    internal class UpdateVariablesTool : IMcpTool
    {
        public Task<object> ExecuteAsync(JObject args)
        {
            var dev = McpService.ResolveDevice(args);
            if (dev == null) return Task.FromResult<object>(new { error = "未找到设备" });

            var all = ConfigService.Instance.GetAllDevices();
            var device = all.FirstOrDefault(d => d.Id == dev.Id);
            if (device == null) return Task.FromResult<object>(new { error = "设备不存在" });

            var ptsTok = args["points"];
            JArray ptsArr = ptsTok as JArray;
            if (ptsArr == null && ptsTok?.Type == JTokenType.String)
                ptsArr = JArray.Parse(ptsTok.Value<string>());
            if (ptsArr == null || ptsArr.Count == 0)
                return Task.FromResult<object>(new { error = "参数 points 不能为空" });

            var updated = new List<string>();
            foreach (var pt in ptsArr)
            {
                string key = pt["point_id"]?.Value<string>() ?? pt["name"]?.Value<string>() ?? "";
                var dp = device.DataPoints.FirstOrDefault(p =>
                    p.Id == key || p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (dp == null) { updated.Add("?" + key); continue; }

                if (pt["name"] != null) dp.Name = pt["name"].Value<string>();
                if (pt["address"] != null) dp.Address = pt["address"].Value<string>();
                if (pt["data_type"] != null) dp.DataType = pt["data_type"].Value<string>();
                if (pt["unit"] != null) dp.Unit = pt["unit"].Value<string>();
                if (pt["scale_factor"] != null) dp.ScaleFactor = pt["scale_factor"].Value<double>();
                if (pt["offset"] != null) dp.Offset = pt["offset"].Value<double>();
                if (pt["alarm_h"] != null) { dp.AlarmHH = pt["alarm_h"].Value<double>(); dp.AlarmHH_Enabled = true; dp.AlarmEnabled = true; }
                if (pt["alarm_l"] != null) { dp.AlarmL = pt["alarm_l"].Value<double>(); dp.AlarmL_Enabled = true; dp.AlarmEnabled = true; }
                if (pt["is_active"] != null) dp.IsActive = pt["is_active"].Value<bool>();

                updated.Add(dp.Name);
            }

            ConfigService.Instance.SaveDevices(all);
            Logger.Info($"[MCP CONFIG] AI update_variables: device={device.Name}, points={string.Join(",", updated)}");

            return Task.FromResult<object>(new
            {
                success = true,
                device_id = dev.Id,
                updated = updated,
                message = $"已更新 {updated.Count} 个变量点。调用 reload_config 使变更生效。"
            });
        }
    }

    [McpTool("reload_config", "热重载设备配置，使 add_device/add_variables 等变更立即生效。优雅重载：暂停采集→等待当前周期→加载→恢复，不丢数据不断连。")]
    internal class ReloadConfigTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            try
            {
                // === 第1步：记录当前运行中的设备 ===
                var runningIds = DataCollectionService.Instance.GetRunningDeviceIds();
                Logger.Info($"[MCP CONFIG] 优雅重载开始，正在运行设备: {runningIds.Count}");

                // === 第2步：暂停采集（完成当前周期后停止） ===
                var stopTasks = new List<Task>();
                foreach (var rid in runningIds)
                {
                    Logger.Debug($"[MCP CONFIG] 暂停设备: {rid}");
                    stopTasks.Add(DataCollectionService.Instance.StopDeviceAsync(rid));
                }
                await Task.WhenAll(stopTasks);

                // === 第3步：等待当前采集周期完全结束 ===
                await Task.Delay(500); // 让未完成的 I/O 操作完成

                // === 第4步：注销所有旧注册 ===
                var oldAll = ConfigService.Instance.GetAllDevices();
                foreach (var d in oldAll)
                    DataProcessor.Instance.UnregisterDevicePoints(d.Id);

                // === 第5步：加载新配置 ===
                var all = ConfigService.Instance.LoadDevices();
                int tp = all.Sum(d => d.DataPoints?.Count ?? 0);

                // === 第6步：注册新配置 ===
                foreach (var d in all)
                {
                    if (d.DataPoints?.Count > 0)
                        DataProcessor.Instance.RegisterDevicePoints(d);
                }

                // === 第7步：同步语义层 ===
                try { SemanticService.Instance.SyncFromDeviceConfigs(all); } catch (Exception ex) { Logger.Error($"[MCP CONFIG] 语义层同步失败(reload_config): {ex.Message}"); }

                // === 第8步：恢复采集（新配置下重启运行的设备） ===
                var startTasks = new List<Task>();
                foreach (var rid in runningIds)
                {
                    var newDev = all.FirstOrDefault(d => d.Id == rid);
                    if (newDev != null && newDev.DataPoints?.Count > 0)
                    {
                        Logger.Debug($"[MCP CONFIG] 恢复设备: {newDev.Name} ({rid})");
                        startTasks.Add(DataCollectionService.Instance.StartDeviceAsync(newDev));
                    }
                    else
                    {
                        Logger.Info($"[MCP CONFIG] 设备 {rid} 在新配置中无变量点，不恢复采集");
                    }
                }
                var oldIds = new HashSet<string>(oldAll.Select(d => d.Id));
                foreach (var d in all)
                {
                    if (!oldIds.Contains(d.Id) && d.DataPoints?.Count > 0)
                    {
                        Logger.Info($"[MCP CONFIG] 启动新设备: {d.Name} ({d.Id})");
                        startTasks.Add(DataCollectionService.Instance.StartDeviceAsync(d));
                    }
                }
                await Task.WhenAll(startTasks);

                Logger.Info($"[MCP CONFIG] 优雅重载完成: {all.Count} 设备, {tp} 变量点, 恢复 {runningIds.Count} 设备");

                return new
                {
                    success = true,
                    device_count = all.Count,
                    total_variables = tp,
                    resumed_devices = runningIds.Count,
                    devices = all.Select(d => new { d.Id, d.Name, d.DriverType, vc = d.DataPoints?.Count ?? 0 }),
                    message = $"优雅重载完成：{all.Count} 台设备, {tp} 个变量点, 恢复采集 {runningIds.Count} 台。"
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"reload_config failed: {ex.Message}");
                return new { error = $"热重载失败: {ex.Message}" };
            }
        }
    }

    // ================================================================
    //  设备控制 MCP 工具 — AI 可启动/停止设备采集
    // ================================================================

    [McpTool("start_device", "启动指定设备的采集任务。不影响其他设备。")]
    internal class StartDeviceTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            string deviceId = args["device_id"]?.Value<string>() ?? "";
            string deviceName = args["device_name"]?.Value<string>() ?? "";

            var all = ConfigService.Instance.GetAllDevices();
            DeviceConfig dev = null;

            if (!string.IsNullOrWhiteSpace(deviceId))
                dev = all.FirstOrDefault(d => d.Id == deviceId);
            else if (!string.IsNullOrWhiteSpace(deviceName))
                dev = all.FirstOrDefault(d => d.Name == deviceName);

            if (dev == null)
                return new { error = "未找到设备，请提供 device_id 或 device_name" };

            if (dev.DataPoints == null || dev.DataPoints.Count == 0)
                return new { error = $"设备 {dev.Name} 没有变量点，无法启动采集。请先用 add_variables 添加。" };

            try
            {
                DataProcessor.Instance.RegisterDevicePoints(dev);
                await DataCollectionService.Instance.StartDeviceAsync(dev);
                Logger.Info($"[MCP] start_device: {dev.Name} ({dev.Id})");
                return new
                {
                    success = true,
                    device_id = dev.Id,
                    name = dev.Name,
                    driver = dev.DriverType,
                    variable_count = dev.DataPoints.Count,
                    message = $"设备 '{dev.Name}' 已启动，采集 {dev.DataPoints.Count} 个变量"
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"start_device failed: {ex.Message}");
                return new { error = $"启动失败: {ex.Message}" };
            }
        }
    }

    [McpTool("stop_device", "停止指定设备的采集任务。不影响其他设备。")]
    internal class StopDeviceTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(JObject args)
        {
            string deviceId = args["device_id"]?.Value<string>() ?? "";
            string deviceName = args["device_name"]?.Value<string>() ?? "";

            DeviceConfig dev = null;

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var all = ConfigService.Instance.GetAllDevices();
                dev = all.FirstOrDefault(d => d.Id == deviceId);
            }
            else if (!string.IsNullOrWhiteSpace(deviceName))
            {
                var all = ConfigService.Instance.GetAllDevices();
                dev = all.FirstOrDefault(d => d.Name == deviceName);
            }

            if (dev == null)
                return new { error = "未找到设备，请提供 device_id 或 device_name" };

            try
            {
                await DataCollectionService.Instance.StopDeviceAsync(dev.Id);
                Logger.Info($"[MCP] stop_device: {dev.Name} ({dev.Id})");
                return new
                {
                    success = true,
                    device_id = dev.Id,
                    name = dev.Name,
                    message = $"设备 '{dev.Name}' 已停止"
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"stop_device failed: {ex.Message}");
                return new { error = $"停止失败: {ex.Message}" };
            }
        }
    }

}
