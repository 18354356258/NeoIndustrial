using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IndustrialDataCollection.Drivers;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// v2.1 离线缓存服务 — MQTT / DB 双路独立缓存，各自恢复互不阻塞。
    /// 每条数据写两行（MQTT 行 + DB 行），各自独立标记、独立恢复、独立清理。
    /// </summary>
    public class OfflineCacheService
    {
        private static readonly Lazy<OfflineCacheService> _instance =
            new Lazy<OfflineCacheService>(() => new OfflineCacheService());
        public static OfflineCacheService Instance => _instance.Value;

        private string _dbPath;
        private readonly object _lock = new object();
        private System.Threading.Timer _recoveryTimer;
        private volatile bool _recoveryRunning;

        // MQTT 补发委托
        public Func<CycleDataBatch, Task<bool>> MqttFlushHandler { get; set; }
        public Func<bool> IsMqttConnected { get; set; }

        // DB 补发委托
        public Func<CycleDataBatch, Task<bool>> DbFlushHandler { get; set; }
        public Func<bool> IsDbConnected { get; set; }

        // 心跳事件
        public event Action<DeviceHeartbeatInfo> OnHeartbeatChanged;

        private readonly Dictionary<string, bool> _lastHeartbeatState
            = new Dictionary<string, bool>();

        private OfflineCacheService() { }

        public void Init()
        {
            _dbPath = Path.Combine(Application.StartupPath, "Data", "offline_cache.db");
            string dir = Path.GetDirectoryName(_dbPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            CreateTables();

            // 每 10 秒同时检查 MQTT 和 DB 补发
            _recoveryTimer = new System.Threading.Timer(_ => _ = TryRecoverAsync(), null, 10000, 10000);
        }

        public void Stop()
        {
            try { _recoveryTimer?.Dispose(); } catch { }
        }

        // ======================== 建表 ========================

        private void CreateTables()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS offline_mqtt_cache (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            timestamp DATETIME NOT NULL,
                            device_name VARCHAR(200) NOT NULL,
                            driver VARCHAR(50),
                            values_json TEXT NOT NULL,
                            sent INTEGER DEFAULT 0,
                            created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                        );

                        CREATE TABLE IF NOT EXISTS offline_db_cache (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            timestamp DATETIME NOT NULL,
                            device_name VARCHAR(200) NOT NULL,
                            driver VARCHAR(50),
                            values_json TEXT NOT NULL,
                            sent INTEGER DEFAULT 0,
                            created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                        );

                        CREATE TABLE IF NOT EXISTS device_heartbeat (
                            device_name VARCHAR(200) PRIMARY KEY,
                            driver VARCHAR(50),
                            last_success DATETIME,
                            last_failure DATETIME,
                            is_online INTEGER DEFAULT 0,
                            fail_count INTEGER DEFAULT 0,
                            error_msg TEXT,
                            updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
                        );

                        CREATE INDEX IF NOT EXISTS idx_mqtt_pending 
                            ON offline_mqtt_cache(sent) WHERE sent=0;
                        CREATE INDEX IF NOT EXISTS idx_db_pending 
                            ON offline_db_cache(sent) WHERE sent=0;
                    ";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private SQLiteConnection GetConnection()
        {
            var conn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", _dbPath));
            return conn;
        }

        // ======================== 缓存写入 ========================

        /// <summary>
        /// v2.1: MQTT 和 DB 各自独立写一行，互不干扰
        /// </summary>
        public CacheRecordIds StoreBatch(CycleDataBatch batch, bool needMqtt, bool needDb)
        {
            var ids = new CacheRecordIds();
            if (batch == null || batch.Values == null || batch.Values.Count == 0) return ids;

            string json = JsonConvert.SerializeObject(batch.Values);
            string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string dev = batch.Device ?? "";
            string drv = batch.Driver ?? "";

            lock (_lock)
            {
                try
                {
                    using (var conn = GetConnection())
                    {
                        conn.Open();
                        using (var tx = conn.BeginTransaction())
                        {
                            try
                            {
                                if (needMqtt)
                                {
                                    using (var cmd = conn.CreateCommand())
                                    {
                                        cmd.Transaction = tx;
                                        cmd.CommandText = @"INSERT INTO offline_mqtt_cache 
                                            (timestamp, device_name, driver, values_json) 
                                            VALUES (@ts, @dev, @drv, @json);
                                            SELECT last_insert_rowid();";
                                        cmd.Parameters.AddWithValue("@ts", ts);
                                        cmd.Parameters.AddWithValue("@dev", dev);
                                        cmd.Parameters.AddWithValue("@drv", drv);
                                        cmd.Parameters.AddWithValue("@json", json);
                                        ids.MqttId = Convert.ToInt64(cmd.ExecuteScalar());
                                    }
                                }
                                if (needDb)
                                {
                                    using (var cmd = conn.CreateCommand())
                                    {
                                        cmd.Transaction = tx;
                                        cmd.CommandText = @"INSERT INTO offline_db_cache 
                                            (timestamp, device_name, driver, values_json) 
                                            VALUES (@ts, @dev, @drv, @json);
                                            SELECT last_insert_rowid();";
                                        cmd.Parameters.AddWithValue("@ts", ts);
                                        cmd.Parameters.AddWithValue("@dev", dev);
                                        cmd.Parameters.AddWithValue("@drv", drv);
                                        cmd.Parameters.AddWithValue("@json", json);
                                        ids.DbId = Convert.ToInt64(cmd.ExecuteScalar());
                                    }
                                }
                                tx.Commit();
                            }
                            catch { tx.Rollback(); throw; }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("WAL 写入失败: " + ex.Message);
                }
            }
            return ids;
        }

        // ======================== 标记已发送 ========================

        /// <summary>标记 MQTT 缓存已发送</summary>
        public void MarkMqttSent(long recordId)
        {
            MarkSent("offline_mqtt_cache", recordId);
        }

        /// <summary>标记 DB 缓存已写入</summary>
        public void MarkDbSent(long recordId)
        {
            MarkSent("offline_db_cache", recordId);
        }

        private void MarkSent(string table, long recordId)
        {
            if (recordId <= 0) return;
            lock (_lock)
            {
                try
                {
                    using (var conn = GetConnection())
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = string.Format(
                                "UPDATE {0} SET sent=1 WHERE id=@id", table);
                            cmd.Parameters.AddWithValue("@id", recordId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("标记缓存发送失败: " + ex.Message);
                }
            }
        }

        private void BulkMarkSent(string table, List<long> ids)
        {
            if (ids == null || ids.Count == 0) return;
            lock (_lock)
            {
                try
                {
                    using (var conn = GetConnection())
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = string.Format(
                                "UPDATE {0} SET sent=1 WHERE id IN ({1})",
                                table, string.Join(",", ids));
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("批量标记缓存发送失败: " + ex.Message);
                }
            }
        }

        // ======================== 缓存查询 ========================

        public int GetPendingCount()
        {
            lock (_lock)
            {
                try
                {
                    using (var conn = GetConnection())
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"
                                SELECT (SELECT COUNT(*) FROM offline_mqtt_cache WHERE sent=0)
                                     + (SELECT COUNT(*) FROM offline_db_cache WHERE sent=0)";
                            return Convert.ToInt32(cmd.ExecuteScalar());
                        }
                    }
                }
                catch { return 0; }
            }
        }

        private List<CacheEntry> GetPendingEntries(string table, int limit = 500)
        {
            var result = new List<CacheEntry>();
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = string.Format(
                            "SELECT id, timestamp, device_name, driver, values_json FROM {0} WHERE sent=0 ORDER BY id ASC LIMIT {1}",
                            table, limit);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(new CacheEntry
                                {
                                    Id = reader.GetInt64(0),
                                    Timestamp = reader.GetString(1),
                                    DeviceName = reader.GetString(2),
                                    Driver = reader.GetString(3),
                                    ValuesJson = reader.GetString(4)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("查询离线缓存失败: " + ex.Message);
            }
            return result;
        }

        // ======================== 清理 ========================

        private void CleanCompleted(string table)
        {
            lock (_lock)
            {
                try
                {
                    using (var conn = GetConnection())
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = string.Format(
                                "DELETE FROM {0} WHERE sent=1", table);
                            int deleted = cmd.ExecuteNonQuery();
                            if (deleted > 0)
                                Logger.Debug(string.Format("{0} 清理: {1} 条已完成", table, deleted));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("清理 {0} 失败: {1}", table, ex.Message));
                }
            }
        }

        // ======================== 补发逻辑 ========================

        private async Task TryRecoverAsync()
        {
            if (_recoveryRunning) return;
            _recoveryRunning = true;
            try
            {
                // --- MQTT 独立恢复 ---
                if (IsMqttConnected != null && IsMqttConnected())
                {
                    var entries = GetPendingEntries("offline_mqtt_cache");
                    if (entries.Count > 0)
                    {
                        Logger.Info(string.Format("[MQTT缓存] 补发开始: {0} 条", entries.Count));
                        var sentIds = await FlushEntries(entries, MqttFlushHandler, "MQTT");
                        if (sentIds.Count > 0)
                        {
                            BulkMarkSent("offline_mqtt_cache", sentIds);
                            Logger.Info(string.Format("[MQTT缓存] 补发完成: {0}/{1}", sentIds.Count, entries.Count));
                            CleanCompleted("offline_mqtt_cache");
                        }
                    }
                }

                // --- DB 独立恢复 ---
                if (IsDbConnected != null && IsDbConnected())
                {
                    var entries = GetPendingEntries("offline_db_cache");
                    if (entries.Count > 0)
                    {
                        Logger.Info(string.Format("[DB缓存] 补发开始: {0} 条", entries.Count));
                        var sentIds = await FlushEntries(entries, DbFlushHandler, "DB");
                        if (sentIds.Count > 0)
                        {
                            BulkMarkSent("offline_db_cache", sentIds);
                            Logger.Info(string.Format("[DB缓存] 补发完成: {0}/{1}", sentIds.Count, entries.Count));
                            CleanCompleted("offline_db_cache");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("补发检查异常: " + ex.Message);
            }
            finally
            {
                _recoveryRunning = false;
            }
        }

        private static async Task<List<long>> FlushEntries(
            List<CacheEntry> entries, Func<CycleDataBatch, Task<bool>> handler, string label)
        {
            var sentIds = new List<long>();
            if (handler == null) return sentIds;

            foreach (var entry in entries)
            {
                try
                {
                    var batch = entry.ToBatch();
                    if (await handler(batch))
                        sentIds.Add(entry.Id);
                }
                catch (Exception ex)
                {
                    Logger.Warn(string.Format("[{0}缓存] 补发失败 id={1}: {2}，暂停本次补发",
                        label, entry.Id, ex.Message));
                    break;
                }
            }
            return sentIds;
        }

        // ======================== 心跳跟踪 ========================

        public void RecordHeartbeat(string deviceName, string driver, bool success, string errorMsg = null)
        {
            bool prevState;
            lock (_lock)
            {
                if (_lastHeartbeatState.TryGetValue(deviceName, out prevState) && prevState == success)
                    return;
                _lastHeartbeatState[deviceName] = success;
            }

            lock (_lock)
            {
                try
                {
                    using (var conn = GetConnection())
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                            if (success)
                            {
                                cmd.CommandText = @"INSERT OR REPLACE INTO device_heartbeat 
                                    (device_name, driver, last_success, is_online, fail_count, error_msg, updated_at)
                                    VALUES (@dev, @drv, @ts, 1, 0, '', @ts2)";
                            }
                            else
                            {
                                cmd.CommandText = @"INSERT INTO device_heartbeat 
                                    (device_name, driver, last_success, last_failure, is_online, fail_count, error_msg, updated_at)
                                    VALUES (@dev, @drv, COALESCE((SELECT last_success FROM device_heartbeat WHERE device_name=@dev), ''), 
                                     @ts, 0, 
                                     COALESCE((SELECT fail_count FROM device_heartbeat WHERE device_name=@dev), 0) + 1,
                                     @err, @ts2)
                                    ON CONFLICT(device_name) DO UPDATE SET
                                      last_failure=@ts, is_online=0, 
                                      fail_count=COALESCE(fail_count,0)+1,
                                      error_msg=@err, updated_at=@ts2";
                            }
                            cmd.Parameters.AddWithValue("@dev", deviceName);
                            cmd.Parameters.AddWithValue("@drv", driver ?? "");
                            cmd.Parameters.AddWithValue("@ts", now);
                            cmd.Parameters.AddWithValue("@ts2", now);
                            cmd.Parameters.AddWithValue("@err", (errorMsg ?? "").Substring(0, Math.Min((errorMsg ?? "").Length, 500)));
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug("心跳写入失败: " + ex.Message);
                }
            }

            Logger.Info(string.Format("心跳: {0} {1}", deviceName, success ? "在线" : "离线"));

            OnHeartbeatChanged?.Invoke(new DeviceHeartbeatInfo
            {
                DeviceName = deviceName,
                Driver = driver,
                IsOnline = success,
                ErrorMessage = errorMsg,
                Timestamp = DateTime.Now
            });
        }

        public List<DeviceHeartbeatInfo> GetAllHeartbeats()
        {
            var result = new List<DeviceHeartbeatInfo>();
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT device_name, driver, last_success, last_failure, is_online, fail_count, error_msg FROM device_heartbeat ORDER BY device_name";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(new DeviceHeartbeatInfo
                                {
                                    DeviceName = reader.GetString(0),
                                    Driver = reader.GetString(1),
                                    LastSuccess = reader.IsDBNull(2) ? (DateTime?)null : DateTime.Parse(reader.GetString(2)),
                                    LastFailure = reader.IsDBNull(3) ? (DateTime?)null : DateTime.Parse(reader.GetString(3)),
                                    IsOnline = reader.GetInt32(4) == 1,
                                    FailCount = reader.GetInt32(5),
                                    ErrorMessage = reader.IsDBNull(6) ? "" : reader.GetString(6)
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        // ======================== 内部类型 ========================

        private class CacheEntry
        {
            public long Id { get; set; }
            public string Timestamp { get; set; }
            public string DeviceName { get; set; }
            public string Driver { get; set; }
            public string ValuesJson { get; set; }

            public CycleDataBatch ToBatch()
            {
                return new CycleDataBatch
                {
                    Timestamp = DateTimeOffset.Parse(Timestamp).ToUnixTimeMilliseconds(),
                    Device = DeviceName,
                    DeviceId = "",
                    Driver = Driver,
                    Values = JsonConvert.DeserializeObject<List<CycleDataItem>>(ValuesJson)
                };
            }
        }
    }

    /// <summary>
    /// 缓存记录 ID 对（MQTT 行 + DB 行各自独立）
    /// </summary>
    public class CacheRecordIds
    {
        public long MqttId { get; set; }
        public long DbId { get; set; }
    }

    /// <summary>
    /// 设备心跳信息
    /// </summary>
    public class DeviceHeartbeatInfo
    {
        public string DeviceName { get; set; }
        public string Driver { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSuccess { get; set; }
        public DateTime? LastFailure { get; set; }
        public int FailCount { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
