using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Data.SqlClient;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IndustrialDataCollection.Drivers;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Services
{
    public class DatabaseWriteService
    {
        private static readonly Lazy<DatabaseWriteService> _instance =
            new Lazy<DatabaseWriteService>(() => new DatabaseWriteService());
        public static DatabaseWriteService Instance = _instance.Value;

        public class DbEntryConfig
        {
            public string DbType { get; set; }
            public string Server { get; set; }
            public string Port { get; set; }
            public string Database { get; set; }
            public string User { get; set; }
            public string Password { get; set; }
            public string FilePath { get; set; }
            public bool EnableWrite { get; set; }
            /// <summary>v2.6.1: Fabric 历史分析时是否使用此数据库</summary>
            public bool EnableFabricHistory { get; set; }
            public List<string> SelectedDevices { get; set; } = new List<string>();
        }

        public class DbConfigRoot
        {
            public Dictionary<string, DbEntryConfig> Configs { get; set; }
                = new Dictionary<string, DbEntryConfig>();
            public string CurrentDbType { get; set; } = "SQLite";
            /// <summary>数据保留天数，0 或负数 = 永久保留。默认 90 天。</summary>
            public int RetentionDays { get; set; } = 7;
        }

        private const string TABLE_NAME = "industrial_data";

        private static readonly string SQL_CREATE_SQLITE = 
@"CREATE TABLE industrial_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    db_type NVARCHAR(50) NOT NULL,
    device NVARCHAR(200) NOT NULL,
    variable NVARCHAR(200) NOT NULL,
    data_type NVARCHAR(50),
    value NVARCHAR(2000),
    unit NVARCHAR(50),
    tag NVARCHAR(500),
    tag_zh NVARCHAR(500),
    timestamp DATETIME NOT NULL
)";

        private static readonly string SQL_CREATE_OTHER =
@"CREATE TABLE industrial_data (
    id INT IDENTITY(1,1) PRIMARY KEY,
    db_type NVARCHAR(50) NOT NULL,
    device NVARCHAR(200) NOT NULL,
    variable NVARCHAR(200) NOT NULL,
    data_type NVARCHAR(50),
    value NVARCHAR(2000),
    unit NVARCHAR(50),
    tag NVARCHAR(500),
    tag_zh NVARCHAR(500),
    timestamp DATETIME NOT NULL
)";

        private static readonly string SQL_CREATE_TABLE_DOC =
@"CREATE TABLE IF NOT EXISTS _table_doc (
    table_name TEXT PRIMARY KEY,
    description TEXT,
    description_cn TEXT
)";

        private static readonly string SQL_INSERT_TABLE_DOC =
@"INSERT OR REPLACE INTO _table_doc (table_name, description, description_cn)
VALUES (@table_name, @description, @description_cn)";

        private static readonly string SQL_INSERT =
@"INSERT INTO industrial_data
    (db_type, device, variable, data_type, value, unit, tag, tag_zh, timestamp)
VALUES
    (@db_type, @device, @variable, @data_type, @value, @unit, @tag, @tag_zh, @timestamp)";


        // TDengine reserved-word field mapping: tag->tag_id, value->val, timestamp->ts
        private static readonly Dictionary<string, string> TdFieldMap = new Dictionary<string, string>
        {
            { "tag", "tag_id" },
            { "value", "val" },
            { "timestamp", "ts" }
        };

        private static string MapTdField(string fieldName)
        {
            return TdFieldMap.TryGetValue(fieldName, out var mapped) ? mapped : fieldName;
        }

        private readonly object _lock = new object();
        private DbConfigRoot _root;
        private readonly Dictionary<string, IDbConnection> _connections
            = new Dictionary<string, IDbConnection>();
        private readonly Dictionary<string, bool> _tableReady
            = new Dictionary<string, bool>();
        private readonly Dictionary<string, DateTime> _lastFailureTime
            = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, int> _failureCount
            = new Dictionary<string, int>();
        private const int MAX_BACKOFF_SEC = 60;
        private System.Threading.Timer _retentionTimer;
        private const int RETENTION_BATCH_SIZE = 5000;

        private DatabaseWriteService() { }

        /// <summary>启动数据保留清理定时器</summary>
        public void InitRetentionCleanup()
        {
            StopRetentionCleanup();
            lock (_lock)
            {
                if (_root == null || _root.RetentionDays <= 0) return;
            }
            // 每 6 小时执行一次清理
            _retentionTimer = new System.Threading.Timer(
                _ => PerformRetentionCleanup(), null,
                TimeSpan.FromMinutes(5), TimeSpan.FromHours(6));
        }

        /// <summary>停止数据保留清理定时器</summary>
        public void StopRetentionCleanup()
        {
            try { _retentionTimer?.Dispose(); } catch { }
            _retentionTimer = null;
        }

        /// <summary>当数据写入数据库时触发，参数为数据库类型字符串</summary>
        public event Action<string> DataWritten;

        /// <summary>是否存在任意一个已打开的数据库连接</summary>
        public bool IsAnyConnected
        {
            get
            {
                lock (_lock)
                {
                    foreach (var kv in _connections)
                    {
                        try
                        {
                            if (kv.Value != null && kv.Value.State == ConnectionState.Open)
                                return true;
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"检查连接状态异常 [{kv.Key}]: {ex.Message}");
                        }
                    }
                    return false;
                }
            }
        }

        public bool IsAnyEnabled
        {
            get
            {
                lock (_lock)
                {
                    if (_root == null || _root.Configs == null) return false;
                    foreach (var kv in _root.Configs)
                        if (kv.Value.EnableWrite) return true;
                    return false;
                }
            }
        }

        /// <summary>
        /// 修复断开的连接并按值确认至少一个可用（供离线缓存补发调用）
        /// </summary>
        public bool EnsureConnectionsHealthy()
        {
            int repaired = AutoRepairConnections();
            if (repaired > 0) return true;

            // 即使没修复新连接，也可能本来就连着，再确认一次
            lock (_lock)
            {
                foreach (var kv in _connections)
                {
                    try
                    {
                        if (kv.Value != null && kv.Value.State == ConnectionState.Open)
                        {
                            using (var cmd = kv.Value.CreateCommand())
                            {
                                cmd.CommandText = "SELECT 1";
                                cmd.CommandTimeout = 3;
                                cmd.ExecuteScalar();
                            }
                            return true;
                        }
                    }
                    catch { }
                }
            }
            return false;
        }

        public void ReloadConfig()
        {
            lock (_lock)
            {
                DisconnectAll();

                string path = GetConfigPath();
                if (!File.Exists(path))
                {
                    Logger.Info("数据库配置: 无配置文件");
                    return;
                }

                try
                {
                    _root = JsonConvert.DeserializeObject<DbConfigRoot>(
                        File.ReadAllText(path));
                    if (_root == null || _root.Configs == null) return;

                    int connected = 0;
                    foreach (var kv in _root.Configs)
                    {
                        if (!kv.Value.EnableWrite) continue;

                        try
                        {
                            var conn = CreateConnection(kv.Value);
                            conn.Open();
                            _connections[kv.Key] = conn;
                            _tableReady[kv.Key] = false;
                            connected++;
                            Logger.Info(string.Format("数据库已连接: {0}, 设备 {1} 个",
                                kv.Key, kv.Value.SelectedDevices != null ? kv.Value.SelectedDevices.Count : 0));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(string.Format("数据库连接失败 [{0}]: {1}", kv.Key, ex.Message));
                        }
                    }

                    if (connected > 0)
                    {
                        Logger.Info(string.Format("数据库写入就绪: {0} 个连接", connected));
                        InitRetentionCleanup();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("加载数据库配置失败: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 自动修复断开的连接 — 逐个 ping 检查，断开的重连
        /// 返回修复成功的连接数
        /// </summary>
        public int AutoRepairConnections()
        {
            // 在锁内检测哪些连接断了，收集配置
            var needRepair = new List<KeyValuePair<string, DbEntryConfig>>();
            var oldConns = new Dictionary<string, IDbConnection>();

            lock (_lock)
            {
                if (_root == null || _root.Configs == null) return 0;

                foreach (var kv in _root.Configs)
                {
                    if (!kv.Value.EnableWrite) continue;
                    string dbKey = kv.Key;

                    bool broken = false;
                    if (_connections.TryGetValue(dbKey, out var conn) && conn != null)
                    {
                        oldConns[dbKey] = conn;
                        try
                        {
                            if (conn.State != ConnectionState.Open)
                                broken = true;
                            else
                            {
                                using (var cmd = conn.CreateCommand())
                                {
                                    cmd.CommandText = "SELECT 1";
                                    cmd.CommandTimeout = 3;
                                    cmd.ExecuteScalar();
                                }
                            }
                        }
                        catch
                        {
                            broken = true;
                        }
                    }
                    else
                    {
                        broken = true;
                    }

                    if (broken)
                        needRepair.Add(new KeyValuePair<string, DbEntryConfig>(dbKey, kv.Value));
                }
            }

            if (needRepair.Count == 0) return 0;

            // 在锁外建立新连接
            var repairedConns = new Dictionary<string, IDbConnection>();
            foreach (var kv in needRepair)
            {
                try
                {
                    var newConn = CreateConnection(kv.Value);
                    newConn.Open();
                    repairedConns[kv.Key] = newConn;
                }
                catch (Exception ex)
                {
                    Logger.Debug(string.Format("数据库仍不可用 [{0}]: {1}", kv.Key, ex.Message));
                }
            }

            if (repairedConns.Count == 0) return 0;

            // 在锁内替换
            lock (_lock)
            {
                int repaired = 0;
                foreach (var kv in repairedConns)
                {
                    string dbKey = kv.Key;
                    // 关闭旧连接
                    if (oldConns.TryGetValue(dbKey, out var oldConn) && oldConn != null)
                    {
                        try { oldConn.Close(); } catch { }
                        try { oldConn.Dispose(); } catch { }
                    }

                    _connections[dbKey] = kv.Value;
                    _tableReady[dbKey] = false;
                    repaired++;
                    Logger.Info(string.Format("数据库连接已恢复: {0}", dbKey));
                }
                return repaired;
            }
        }

        public async Task WriteBatchAsync(string deviceName, CycleDataBatch batch)
        {
            List<KeyValuePair<string, IDbConnection>> targets;

            lock (_lock)
            {
                if (_root == null || _root.Configs == null || _connections.Count == 0) return;

                targets = new List<KeyValuePair<string, IDbConnection>>();
                foreach (var kv in _connections)
                {
                    var cfg = _root.Configs[kv.Key];
                    var devs = cfg.SelectedDevices ?? new List<string>();
                    if (devs.Count == 0 || devs.Contains(deviceName))
                        targets.Add(kv);
                }
            }

            if (targets.Count == 0) return;

            foreach (var target in targets)
            {
                // TDengine/MySQL 等远程 DB 退避检查：连续失败时跳过此轮，由离线缓存接管
                lock (_lock)
                {
                    if (_failureCount.TryGetValue(target.Key, out int fc) && fc > 0)
                    {
                        double backoffSec = Math.Min(Math.Pow(2, fc - 1), MAX_BACKOFF_SEC);
                        if (_lastFailureTime.TryGetValue(target.Key, out DateTime lastFail) &&
                            (DateTime.Now - lastFail).TotalSeconds < backoffSec)
                        {
                            continue; // 仍在退避期内，跳过
                        }
                    }
                }

                try
                {
                    await Task.Run(() =>
                    {
                        lock (_lock)
                        {
                            if (!_connections.ContainsKey(target.Key)) return;
                            EnsureTable(target.Key);
                            InsertRows(target.Key, deviceName, batch);
                        }
                    });
                    // 写入成功 — 重置失败计数
                    lock (_lock)
                    {
                        _failureCount[target.Key] = 0;
                    }
                    // 触发数据统计事件
                    var handler = DataWritten;
                    if (handler != null)
                        handler(target.Key);
                }
                catch (Exception ex)
                {
                    Logger.Warn(string.Format("数据库写入失败 [{0}][{1}]: {2}",
                        target.Key, deviceName, ex.Message));

                    // 递增失败计数 + 记录失败时间
                    lock (_lock)
                    {
                        int fc = _failureCount.TryGetValue(target.Key, out int c) ? c + 1 : 1;
                        _failureCount[target.Key] = fc;
                        _lastFailureTime[target.Key] = DateTime.Now;
                    }

                    // 尝试重连并重试一次
                    if (TryReconnectOnce(target.Key))
                    {
                        Logger.Info(string.Format("数据库重连成功，重试写入 [{0}]", target.Key));
                        try
                        {
                            await Task.Run(() =>
                            {
                                lock (_lock)
                                {
                                    if (!_connections.ContainsKey(target.Key)) return;
                                    EnsureTable(target.Key);
                                    InsertRows(target.Key, deviceName, batch);
                                }
                            });
                            // 重试成功 — 重置失败计数
                            lock (_lock)
                            {
                                _failureCount[target.Key] = 0;
                            }
                            return; // 重试成功
                        }
                        catch (Exception ex2)
                        {
                            Logger.Error(string.Format("数据库重试仍然失败 [{0}]: {1}", target.Key, ex2.Message));
                        }
                    }

                    throw; // 重连失败或重试失败 → 抛给上层缓存
                }
            }
        }

        /// <summary>
        /// 尝试重连单个数据库连接（新建 → Open → 成功后替换旧的）
        /// </summary>
        private bool TryReconnectOnce(string dbKey)
        {
            DbEntryConfig cfg;
            lock (_lock)
            {
                if (_root == null || !_root.Configs.TryGetValue(dbKey, out cfg))
                    return false;
                if (!cfg.EnableWrite) return false;
            }

            // 建连 + Open 在锁外执行，避免阻塞其他操作
            IDbConnection newConn = null;
            try
            {
                newConn = CreateConnection(cfg);
                newConn.Open();
            }
            catch (Exception ex)
            {
                try { newConn?.Dispose(); } catch { }
                Logger.Debug(string.Format("数据库重连失败 [{0}]: {1}", dbKey, ex.Message));
                return false;
            }

            // 只在替换字典时持锁
            lock (_lock)
            {
                if (_connections.TryGetValue(dbKey, out var oldConn) && oldConn != null)
                {
                    try { oldConn.Close(); } catch { }
                    try { oldConn.Dispose(); } catch { }
                }

                _connections[dbKey] = newConn;
                _tableReady[dbKey] = false;
                return true;
            }
        }

        private void EnsureTable(string dbKey)
        {
            bool ready;
            if (_tableReady.TryGetValue(dbKey, out ready) && ready)
                return;

            var conn = _connections[dbKey];
            bool exists = false;
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = GetTableExistsSql(dbKey, TABLE_NAME);
                    cmd.CommandTimeout = 5;
                    var r = cmd.ExecuteScalar();
                    exists = r != null && r != DBNull.Value;
                }
            }
            catch (Exception ex)
            {
                Logger.Info($"[TDengine] 表不存在，开始创建 [{dbKey}]: {ex.Message}");
                // 不抛——走建表分支，CREATE TABLE 失败会在后面记载日志
            }

            if (!exists)
            {
                string sql = BuildCreateTableSql(conn);
                try
                {
                    Logger.Info("[TDengine] Creating table " + TABLE_NAME + " in " + dbKey + "...");
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                    }
                    Logger.Info("[TDengine] Table " + TABLE_NAME + " created successfully in " + dbKey);
                }
                catch (Exception createEx)
                {
                    Logger.Error("[TDengine] Failed to create table " + TABLE_NAME + " in " + dbKey + ": " + createEx.Message);
                    Logger.Error("[TDengine] SQL was: " + sql);
                    throw;
                }

                // Create documentation table and insert table/column comments (SQLite only)
                if (conn is SQLiteConnection)
                {
                    try
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = SQL_CREATE_TABLE_DOC;
                            cmd.ExecuteNonQuery();
                        }
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = SQL_INSERT_TABLE_DOC;
                            var p1 = cmd.CreateParameter();
                            p1.ParameterName = "@table_name";
                            p1.Value = TABLE_NAME;
                            cmd.Parameters.Add(p1);
                            var p2 = cmd.CreateParameter();
                            p2.ParameterName = "@description";
                            p2.Value = "Industrial data collection table - stores time-series measurement values from devices and sensors";
                            cmd.Parameters.Add(p2);
                            var p3 = cmd.CreateParameter();
                            p3.ParameterName = "@description_cn";
                            p3.Value = "工业数据采集表 - 存储设备和传感器的时序测量值";
                            cmd.Parameters.Add(p3);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Info("创建表文档失败（非致命）: " + ex.Message);
                    }
                }
            }
            else
            {
                EnsureColumns(dbKey, conn);
            }

            _tableReady[dbKey] = true;
        }

        private string BuildCreateTableSql(IDbConnection conn)
        {
            if (conn is SQLiteConnection)
                return SQL_CREATE_SQLITE;

            if (conn is MySql.Data.MySqlClient.MySqlConnection)
                return @"CREATE TABLE industrial_data (
    id INT AUTO_INCREMENT PRIMARY KEY COMMENT 'Auto-increment record ID',
    db_type VARCHAR(50) NOT NULL COMMENT 'Database type: MySQL/SQLite/PostgreSQL/etc',
    device VARCHAR(200) NOT NULL COMMENT 'Device name or identifier',
    variable VARCHAR(200) NOT NULL COMMENT 'Variable or data point name',
    data_type VARCHAR(50) COMMENT 'Data type: float/int/bool/string',
    value VARCHAR(2000) COMMENT 'Collected value as string',
    unit VARCHAR(50) COMMENT 'Unit of measurement',
    tag VARCHAR(500) COMMENT 'Semantic tag in English',
    tag_zh VARCHAR(500) COMMENT 'Semantic tag in Chinese',
    timestamp DATETIME NOT NULL COMMENT 'Collection timestamp'
) CHARACTER SET utf8mb4 COMMENT='Industrial data collection table - stores time-series measurement values from devices and sensors'";

            if (conn is Npgsql.NpgsqlConnection)
                return @"CREATE TABLE industrial_data (
    id SERIAL PRIMARY KEY,
    db_type VARCHAR(50) NOT NULL,
    device VARCHAR(200) NOT NULL,
    variable VARCHAR(200) NOT NULL,
    data_type VARCHAR(50),
    value VARCHAR(2000),
    unit VARCHAR(50),
    tag VARCHAR(500),
    tag_zh VARCHAR(500),
    timestamp TIMESTAMP NOT NULL
)";

            // TDengine / SQL Server / default
            if (conn is TdengineConnection)
                return "CREATE TABLE IF NOT EXISTS industrial_data (\n" +
                    "    " + MapTdField("timestamp") + " TIMESTAMP,\n" +
                    "    db_type NCHAR(50),\n" +
                    "    device NCHAR(200),\n" +
                    "    variable NCHAR(200),\n" +
                    "    data_type NCHAR(50),\n" +
                    "    " + MapTdField("value") + " NCHAR(2000),\n" +
                    "    unit NCHAR(50),\n" +
                    "    " + MapTdField("tag") + " NCHAR(500),\n" +
                    "    tag_zh NCHAR(500)\n" +
                    ")";

            return SQL_CREATE_OTHER;
        }

        private void EnsureColumns(string dbKey, IDbConnection conn)
        {
            // SQLite: PRAGMA table_info 列检查
            if (conn is SQLiteConnection)
            {
                var required = new Dictionary<string, string>
                {
                    { "id", "INTEGER" },
                    { "db_type", "NVARCHAR" },
                    { "device", "NVARCHAR" },
                    { "variable", "NVARCHAR" },
                    { "data_type", "NVARCHAR" },
                    { "value", "NVARCHAR" },
                    { "unit", "NVARCHAR" },
                    { "tag", "NVARCHAR(500)" },
                    { "tag_zh", "NVARCHAR(500)" },
                    { "timestamp", "DATETIME" }
                };

                var existing = new HashSet<string>();
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA table_info(" + TABLE_NAME + ")";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                existing.Add(reader.GetString(1).ToLower());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"读取表结构失败 [{dbKey}]: {ex.Message}");
                    return;
                }

                foreach (var kv in required)
                {
                    if (!existing.Contains(kv.Key.ToLower()))
                    {
                        try
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "ALTER TABLE " + TABLE_NAME
                                    + " ADD COLUMN " + kv.Key + " " + kv.Value;
                                cmd.ExecuteNonQuery();
                            }
                            Logger.Info("补全列 [" + dbKey + "]: " + kv.Key);
                        }
                        catch { }
                    }
                }
            }
            else
            {
                // 非 SQLite 数据库: try/catch ALTER TABLE 添加新列
                MigrateNonSqLite(dbKey, conn);
            }
        }

        private void MigrateNonSqLite(string dbKey, IDbConnection conn)
        {
            var migrations = new[]
            {
                ("tag", "ALTER TABLE " + TABLE_NAME + " ADD tag NVARCHAR(500)"),
                ("tag_zh", "ALTER TABLE " + TABLE_NAME + " ADD tag_zh NVARCHAR(500)")
            };

            foreach (var (col, sql) in migrations)
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                    }
                    Logger.Info(string.Format("数据库迁移 v1.7.0 [{0}]: 添加列 {1}", dbKey, col));
                }
                catch
                {
                    // 列已存在，忽略
                }
            }
        }

        private void InsertRows(string dbKey, string deviceName, CycleDataBatch batch)
        {
            if (batch.Values == null || batch.Values.Count == 0) return;

            var conn = _connections[dbKey];
            string deviceId = batch.DeviceId ?? "";
            var dt = DateTimeFromUnixMs(batch.Timestamp);
            bool isTDengine = conn is TdengineConnection;
            int rowIdx = 0;

            foreach (var item in batch.Values)
            {
                // TDengine: 内联 SQL（REST 不支持参数化）
                if (isTDengine)
                {
                    string valueStr = item.Value != null ? item.Value.ToString().Replace("'", "''") : "";
                    string unitStr = (item.Unit ?? "").Replace("'", "''");
                    string tagCn = (!string.IsNullOrWhiteSpace(item.TagCn) ? item.TagCn : (item.Id ?? "")).Replace("'", "''");
                    string varStr = (item.Id ?? "").Replace("'", "''");
                    string tagStr = (item.VariableId ?? item.Id ?? "").Replace("'", "''");
                    // TDengine 正常表以 ts 为唯一主键，同时间戳行会互相覆盖
                    // 给每行加 1ms 偏移确保 8 变量全部留存
                    var rowDt = dt.AddMilliseconds(rowIdx);
                    rowIdx++;
                    // TDengine mapped field names (value->val, tag->tag_id, timestamp->ts)
                    string rawSql = string.Format(
                        "INSERT INTO {0} (db_type, device, variable, data_type, {1}, unit, {2}, tag_zh, {3}) " +
                        "VALUES ('{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12:yyyy-MM-dd HH:mm:ss.fff}')",
                        TABLE_NAME,
                        MapTdField("value"), MapTdField("tag"), MapTdField("timestamp"),
                        dbKey, deviceName, varStr, item.DataType ?? "",
                        valueStr, unitStr, tagStr, tagCn, rowDt);
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = rawSql;
                        cmd.ExecuteNonQuery();
                    }
                    continue;
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = SQL_INSERT;
                    AddParam(cmd, conn, "@db_type", dbKey);
                    AddParam(cmd, conn, "@device", deviceName ?? string.Empty);
                    string varName = item.Id ?? "";
                    string tagId = item.VariableId ?? item.Id ?? "";
                    string tagCn = !string.IsNullOrWhiteSpace(item.TagCn) ? item.TagCn : (item.Id ?? "");
                    AddParam(cmd, conn, "@variable", varName);
                    AddParam(cmd, conn, "@data_type", item.DataType ?? string.Empty);
                    string valueStr = item.Value != null ? item.Value.ToString() : string.Empty;
                    AddParam(cmd, conn, "@value", valueStr);
                    AddParam(cmd, conn, "@unit", item.Unit ?? string.Empty);
                    AddParam(cmd, conn, "@tag", tagId);
                    AddParam(cmd, conn, "@tag_zh", tagCn);
                    AddParam(cmd, conn, "@timestamp", dt);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void AddParam(IDbCommand cmd, IDbConnection conn,
            string name, object value)
        {
            IDbDataParameter p;
            if (conn is SQLiteConnection)
                p = new SQLiteParameter(name, value);
            else if (conn is SqlConnection)
                p = new SqlParameter(name, value);
            else if (conn is MySql.Data.MySqlClient.MySqlConnection)
                p = new MySql.Data.MySqlClient.MySqlParameter(name, value);
            else if (conn is Npgsql.NpgsqlConnection)
                p = new Npgsql.NpgsqlParameter(name, value);
            else if (conn is TdengineConnection)
            {
                // TDengine: construct parameterized SQL inline, return null param
                // TDengine REST doesn't support real parameters, so we embed values in the command text
                p = new SQLiteParameter(name, value);
            }
            else
                p = new OdbcParameter(name, value);
            cmd.Parameters.Add(p);
        }

        /// <summary>
        /// 执行数据保留清理 — 删除超过 RetentionDays 的历史数据
        /// </summary>
        private void PerformRetentionCleanup()
        {
            try
            {
                int retentionDays;
                lock (_lock)
                {
                    if (_root == null || _root.RetentionDays <= 0) return;
                    retentionDays = _root.RetentionDays;
                }

                DateTime cutoff = DateTime.Now.AddDays(-retentionDays);
                string cutoffStr = cutoff.ToString("yyyy-MM-dd HH:mm:ss");
                int totalDeleted = 0;

                List<KeyValuePair<string, IDbConnection>> targets;
                lock (_lock)
                {
                    targets = new List<KeyValuePair<string, IDbConnection>>(_connections);
                }

                foreach (var kv in targets)
                {
                    try
                    {
                        int deleted;
                        do
                        {
                            deleted = DeleteBatch(kv.Value, kv.Key, cutoffStr, RETENTION_BATCH_SIZE);
                            totalDeleted += deleted;
                            if (deleted >= RETENTION_BATCH_SIZE)
                                System.Threading.Thread.Sleep(100); // 分批间隙
                        } while (deleted >= RETENTION_BATCH_SIZE);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(string.Format("数据清理 [{0}] 异常: {1}", kv.Key, ex.Message));
                    }
                }

                if (totalDeleted > 0)
                    Logger.Info(string.Format("数据保留清理: 已删除 {0} 条过期数据 (> {1} 天)",
                        totalDeleted, retentionDays));
            }
            catch (Exception ex)
            {
                Logger.Debug("数据保留清理异常: " + ex.Message);
            }
        }

        private int DeleteBatch(IDbConnection conn, string dbKey, string cutoffStr, int batchSize)
        {
            string sql;
            if (conn is SQLiteConnection)
            {
                sql = string.Format(
                    "DELETE FROM {0} WHERE id IN (SELECT id FROM {0} WHERE timestamp < @cutoff LIMIT {1})",
                    TABLE_NAME, batchSize);
            }
            else if (conn is MySql.Data.MySqlClient.MySqlConnection)
            {
                sql = string.Format("DELETE FROM {0} WHERE timestamp < @cutoff LIMIT {1}",
                    TABLE_NAME, batchSize);
            }
            else if (conn is SqlConnection)
            {
                sql = string.Format("DELETE TOP ({0}) FROM {1} WHERE timestamp < @cutoff",
                    batchSize, TABLE_NAME);
            }
            else
            {
                // PostgreSQL / TDengine / other
                sql = string.Format("DELETE FROM {0} WHERE timestamp < @cutoff", TABLE_NAME);
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                AddParam(cmd, conn, "@cutoff", cutoffStr);
                return cmd.ExecuteNonQuery();
            }
        }

        private void DisconnectAll()
        {
            foreach (var kv in _connections)
            {
                try { kv.Value.Close(); } catch { }
                try { kv.Value.Dispose(); } catch { }
            }
            _connections.Clear();
            _tableReady.Clear();
            _lastFailureTime.Clear();
        }

        public static IDbConnection CreateConnection(DbEntryConfig cfg)
        {
            switch (cfg.DbType)
            {
                case "SQLite":
                {
                    string path = string.IsNullOrEmpty(cfg.FilePath)
                        ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.db")
                        : cfg.FilePath;
                    return new SQLiteConnection("Data Source=" + path + ";Version=3;");
                }
                case "MySQL":
                    return new MySql.Data.MySqlClient.MySqlConnection(
                        "Server=" + cfg.Server + ";Port=" + cfg.Port + ";Database=" + cfg.Database
                        + ";Uid=" + cfg.User + ";Pwd=" + cfg.Password + ";CharSet=utf8mb4;Connect Timeout=5;");
                case "SQL Server":
                    return new SqlConnection(
                        "Server=" + cfg.Server + "," + cfg.Port + ";Database=" + cfg.Database
                        + ";User Id=" + cfg.User + ";Password=" + cfg.Password + ";TrustServerCertificate=True;Connect Timeout=5;");
                case "PostgreSQL":
                    return new Npgsql.NpgsqlConnection(
                        "Host=" + cfg.Server + ";Port=" + cfg.Port + ";Database=" + cfg.Database
                        + ";Username=" + cfg.User + ";Password=" + cfg.Password + ";Timeout=5;");
                case "TDengine":
                    int port = 6041;
                    int.TryParse(cfg.Port, out port);
                    if (port == 0) port = 6041;
                    return new TdengineConnection(cfg.Server, port, cfg.Database, cfg.User, cfg.Password);
                default:
                    throw new NotSupportedException("不支持的数据库类型: " + cfg.DbType);
            }
        }

        private static DateTime DateTimeFromUnixMs(long ms)
        {
            try
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddMilliseconds(ms).ToLocalTime();
            }
            catch { return DateTime.Now; }
        }

        private string GetTableExistsSql(string dbTypeKey, string tableName)
        {
            switch (dbTypeKey)
            {
                case "SQLite":
                    return "SELECT name FROM sqlite_master WHERE type='table' AND name='" + tableName + "'";
                case "MySQL":
                    return "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='" + tableName + "'";
                case "SQL Server":
                    return "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='" + tableName + "'";
                case "PostgreSQL":
                    return "SELECT 1 FROM information_schema.tables WHERE table_name='" + tableName + "'";
                case "TDengine":
                    return "SELECT 1 FROM " + tableName + " LIMIT 1";
                default:
                    return "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='" + tableName + "'";
            }
        }

        /// <summary>
        /// 获取所有数据库类型的连接状态（供 MCP 查询）
        /// </summary>
        public Dictionary<string, (bool enabled, bool connected, int deviceCount)> GetConnectionStatuses()
        {
            var result = new Dictionary<string, (bool, bool, int)>();
            lock (_lock)
            {
                if (_root?.Configs == null) return result;
                foreach (var kv in _root.Configs)
                {
                    bool connected = _connections.ContainsKey(kv.Key);
                    result[kv.Key] = (kv.Value.EnableWrite, connected, kv.Value.SelectedDevices?.Count ?? 0);
                }
            }
            return result;
        }

        /// <summary>
        /// 查询历史数据（优先 SQLite，兼容现有数据库结构）
        /// </summary>
        public async Task<List<HistoryRecord>> QueryHistoryAsync(
            string deviceName, string variableName, string startTime, string endTime, int limit)
        {
            var results = new List<HistoryRecord>();

            try
            {
                // v2.6.1: 优先 Fabric 历史分析勾选的 DB，其次启用写入的 DB
                var sources = DataSourceService.Instance.GetAll();
                var source = sources.FirstOrDefault(s =>
                {
                    lock (_lock)
                    {
                        if (_root?.Configs != null && _root.Configs.TryGetValue(s.DbType, out var cfg))
                            return cfg.EnableFabricHistory || cfg.EnableWrite;
                    }
                    return false;
                });

                if (source == null)
                    return results;

                // 构建方言适配 SQL
                var conditions = new List<string> { "1=1" };
                var isTdengine = source.DbType == "TDengine";
                string tsCol = isTdengine ? "ts" : "timestamp";
                string orderCol = isTdengine ? "ts" : "id";

                if (!string.IsNullOrEmpty(deviceName))
                    conditions.Add(string.Format("device='{0}'", deviceName.Replace("'", "''")));
                if (!string.IsNullOrEmpty(variableName))
                    conditions.Add(string.Format("variable='{0}'", variableName.Replace("'", "''")));
                if (!string.IsNullOrEmpty(startTime))
                    conditions.Add(string.Format("{0}>='{1}'", tsCol, startTime.Replace("'", "''")));
                if (!string.IsNullOrEmpty(endTime))
                    conditions.Add(string.Format("{0}<='{1}'", tsCol, endTime.Replace("'", "''")));

                string sql = string.Format(
                    "SELECT device, variable, value, unit, tag, tag_zh, {0} FROM industrial_data WHERE {1} ORDER BY {2} DESC LIMIT {3}",
                    tsCol, string.Join(" AND ", conditions), orderCol, limit);

                var qr = await DataSourceService.Instance.RunQueryAsync(source.Id, sql);
                if (qr?.rows != null)
                {
                    foreach (var row in qr.rows)
                    {
                        results.Add(new HistoryRecord
                        {
                            device = GetCol(row, 0),
                            variable = GetCol(row, 1),
                            value = GetCol(row, 2),
                            unit = GetCol(row, 3),
                            tag = GetCol(row, 4),
                            tag_cn = GetCol(row, 5),
                            timestamp = GetCol(row, 6)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("QueryHistoryAsync 查询失败: " + ex.Message);
            }

            return results;
        }

        private static string GetCol(List<object> row, int idx)
        {
            if (row == null || idx >= row.Count) return "";
            var v = row[idx];
            return v == null ? "" : v.ToString();
        }

        public static string GetConfigPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "dbConfig.json");
        }
    }
}
