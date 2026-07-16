using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;
using Npgsql;

using IndustrialDataCollection.Services.DbAdapters;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 数据源管理服务 — 管理外部数据库连接配置，提供连接测试、数据查询
    /// </summary>
    public class DataSourceService
    {
        private static readonly Lazy<DataSourceService> _instance =
            new Lazy<DataSourceService>(() => new DataSourceService());
        public static DataSourceService Instance => _instance.Value;

        private readonly string _configPath;
        private List<DataSourceConnection> _sources;
        private List<DataSourceFolder> _folders;
        private readonly Dictionary<string, IDbConnection> _activeConns
            = new Dictionary<string, IDbConnection>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        private DataSourceService()
        {
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection", "datasources.json");
            Load();
        }

        // ======================== CRUD ========================

        public List<DataSourceConnection> GetAll()
        {
            lock (_lock) { return _sources.ToList(); }
        }

        public DataSourceConnection Get(string id)
        {
            lock (_lock) { return _sources.FirstOrDefault(s => s.Id == id); }
        }

        public void Save(DataSourceConnection source)
        {
            lock (_lock)
            {
                var existing = _sources.FirstOrDefault(s => s.Id == source.Id);
                if (existing != null)
                {
                    var idx = _sources.IndexOf(existing);
                    _sources[idx] = source;
                }
                else
                {
                    // 去重：通过 Name+Server+Database 检测已有数据源
                    var dup = _sources.FirstOrDefault(s =>
                        s.Name == source.Name && s.Server == source.Server && s.Database == source.Database);
                    if (dup != null && !string.IsNullOrEmpty(source.Id) && string.IsNullOrEmpty(dup.Id))
                    {
                        // 之前测试连接时存了一条空 ID 的，现在用新 ID 替换
                        var dupIdx = _sources.IndexOf(dup);
                        source.Tables = dup.Tables; // 保留已分析的表结构
                        _sources[dupIdx] = source;
                    }
                    else
                    {
                        _sources.Add(source);
                    }
                }
                Persist();
            }
            // MCP 工具列表需要重建
            McpDataSourceRegistry.Rebuild();

            // 语义层 v2: 同步到节点树（只同步已分配 ID 的数据源）
            try
            {
                var validSources = _sources.Where(s => !string.IsNullOrEmpty(s.Id)).ToList();
                SemanticService.Instance.SyncFromDataSources(validSources, this);
            }
            catch { }
        }

        public void Delete(string id)
        {
            lock (_lock)
            {
                _sources.RemoveAll(s => s.Id == id);
                Persist();
            }
            Disconnect(id);
            McpDataSourceRegistry.Rebuild();

            // 语义层 v2: 标记对应节点为 Deleted
            try
            {
                var node = SemanticService.Instance.GetNodeBySource("datasource", id);
                if (node != null)
                    SemanticService.Instance.UpdateNodeStatus(node.Id, NodeStatus.Deleted);
            }
            catch { }
        }

        
        public void SaveFolder(DataSourceFolder folder)
        {
            lock (_lock)
            {
                var existing = _folders.FirstOrDefault(f => f.Id == folder.Id);
                if (existing == null)
                {
                    _folders.Add(folder);
                }
                else
                {
                    existing.Name = folder.Name;
                }
                Persist();
            }
        }

        public void RenameFolder(string folderId, string newName)
        {
            lock (_lock)
            {
                string parent = "";
                int lastSep = folderId.LastIndexOf('/');
                if (lastSep >= 0) parent = folderId.Substring(0, lastSep) + "/";
                string newPath = parent + newName;

                foreach (var src in _sources)
                {
                    if (src.Folder == folderId)
                        src.Folder = newPath;
                    else if (src.Folder.StartsWith(folderId + "/"))
                        src.Folder = newPath + src.Folder.Substring(folderId.Length);
                }

                // 更新文件夹列表中的条目
                var folder = _folders.FirstOrDefault(f => f.Id == folderId);
                if (folder != null)
                {
                    folder.Id = newPath;
                    folder.Name = newName;
                }

                Persist();
            }
        }

        public void DeleteFolder(string folder)
        {
            lock (_lock)
            {
                var toRemove = _sources.Where(s =>
                    s.Folder == folder || s.Folder.StartsWith(folder + "/")).ToList();
                foreach (var s in toRemove)
                {
                    _sources.Remove(s);
                    Disconnect(s.Id);
                }
                // 删除文件夹条目（包括子文件夹）
                _folders.RemoveAll(f => f.Id == folder || f.Id.StartsWith(folder + "/"));
                Persist();
            }
            McpDataSourceRegistry.Rebuild();
        }

        public void MoveToFolder(string sourceId, string folder)
        {
            lock (_lock)
            {
                var src = _sources.FirstOrDefault(s => s.Id == sourceId);
                if (src != null)
                {
                    src.Folder = folder;
                    Persist();
                }
            }
        }

        public void MoveFolderContents(string folderId, string targetFolderId)
        {
            if (string.IsNullOrEmpty(targetFolderId))
                return;  // 拖到根层级: 不做迁移

            lock (_lock)
            {
                string newBase = targetFolderId + "/";
                string oldPrefix = folderId + "/";

                foreach (var src in _sources)
                {
                    if (src.Folder == folderId)
                        src.Folder = targetFolderId;
                    else if (src.Folder.StartsWith(oldPrefix))
                        src.Folder = newBase + src.Folder.Substring(oldPrefix.Length);
                }

                // Update folders list: rename the folder itself + subfolders
                for (int i = 0; i < _folders.Count; i++)
                {
                    if (_folders[i].Id == folderId)
                    {
                        string name = targetFolderId.Contains("/") ? targetFolderId.Substring(targetFolderId.LastIndexOf('/') + 1) : targetFolderId;
                        _folders[i] = new DataSourceFolder { Id = targetFolderId, Name = name };
                    }
                    else if (_folders[i].Id.StartsWith(oldPrefix))
                    {
                        string newId = newBase + _folders[i].Id.Substring(oldPrefix.Length);
                        string name = newId.Contains("/") ? newId.Substring(newId.LastIndexOf('/') + 1) : newId;
                        _folders[i] = new DataSourceFolder { Id = newId, Name = name };
                    }
                }

                Persist();
            }
        }

        public List<DataSourceFolder> GetFolders()
        {
            lock (_lock)
            {
                var derived = _sources.Select(s => s.Folder)
                    .Where(f => !string.IsNullOrEmpty(f))
                    .Distinct()
                    .Select(f => new DataSourceFolder { Id = f, Name = f.Contains("/") ? f.Substring(f.LastIndexOf('/') + 1) : f })
                    .ToList();

                // 合并显式创建的文件夹（尚无数据源的空文件夹）
                var merged = new List<DataSourceFolder>(derived);
                foreach (var f in _folders)
                {
                    if (!merged.Any(m => m.Id == f.Id))
                        merged.Add(f);
                }
                return merged.OrderBy(f => f.Id).ToList();
            }
        }

        // ======================== 持久化 ========================

        private void Load()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath, System.Text.Encoding.UTF8);
                    try
                    {
                        var config = JsonConvert.DeserializeObject<DataSourceConfig>(json);
                        _sources = config.DataSources ?? new List<DataSourceConnection>();
                        _folders = (config.Folders ?? new List<DataSourceFolder>())
                            .GroupBy(f => f.Id)
                            .Select(g => g.First())
                            .ToList();
                    }
                    catch
                    {
                        // 兼容旧格式：扁平列表
                        _sources = JsonConvert.DeserializeObject<List<DataSourceConnection>>(json) ?? new List<DataSourceConnection>();
                        _folders = new List<DataSourceFolder>();
                    }
                }
                else
                {
                    _sources = new List<DataSourceConnection>();
                    _folders = new List<DataSourceFolder>();
                }
            }
            catch
            {
                _sources = new List<DataSourceConnection>();
                _folders = new List<DataSourceFolder>();
            }
        }

        private void Persist()
        {
            try
            {
                string dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var config = new DataSourceConfig { DataSources = _sources, Folders = _folders };
                File.WriteAllText(_configPath,
                    JsonConvert.SerializeObject(config, Formatting.Indented),
                    System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error("保存数据源配置失败: " + ex.Message);
            }
        }

        // ======================== 连接管理 ========================

        public async Task<string> TestConnectionAsync(DataSourceConnection source)
        {
            IDbConnection conn = null;
            try
            {
                conn = CreateConnection(source);
                await Task.Run(() => conn.Open());
                source.LastTestedAt = DateTime.Now;
                return "ok";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                try { conn?.Close(); conn?.Dispose(); } catch { }
            }
        }

        private IDbConnection GetOrOpenConnection(DataSourceConnection source)
        {
            lock (_lock)
            {
                IDbConnection conn;
                if (_activeConns.TryGetValue(source.Id, out conn))
                {
                    try
                    {
                        if (conn.State == ConnectionState.Open)
                            return conn;
                    }
                    catch { }
                    // 连接断开，重建
                    try { conn.Dispose(); } catch { }
                    _activeConns.Remove(source.Id);
                }
                conn = CreateConnection(source);
                conn.Open();
                _activeConns[source.Id] = conn;
                return conn;
            }
        }

        private void Disconnect(string id)
        {
            lock (_lock)
            {
                IDbConnection conn;
                if (_activeConns.TryGetValue(id, out conn))
                {
                    try { conn.Close(); } catch { }
                    try { conn.Dispose(); } catch { }
                    _activeConns.Remove(id);
                }
            }
        }

        public bool IsConnected(string id)
        {
            lock (_lock)
            {
                IDbConnection conn;
                if (_activeConns.TryGetValue(id, out conn))
                {
                    try { return conn.State == ConnectionState.Open; }
                    catch { return false; }
                }
                return false;
            }
        }

        /// <summary>获取数据源对应的数据库适配器（来自 IDbAdapter 架构）</summary>
        public static IDbAdapter GetAdapter(string dbType)
            => DataSourceRouter.Instance.GetAdapter(dbType);

        public static IDbConnection CreateConnection(DataSourceConnection source)
        {
            string port = CleanPort(source.Port, source.DbType);
            switch (source.DbType)
            {
                case "SQLite":
                    {
                        string cs = "Data Source=" + source.FilePath + ";Version=3;Read Only=" +
                            (source.PermissionMode == "readonly" ? "True" : "False");
                        if (!string.IsNullOrEmpty(source.Password))
                            cs += ";Password=" + source.Password;
                        return new SQLiteConnection(cs);
                    }
                case "MySQL":
                    return new MySql.Data.MySqlClient.MySqlConnection(
                        string.Format("Server={0};Port={1};Database={2};Uid={3};Pwd={4};CharSet=utf8mb4;AllowUserVariables=True;",
                            source.Server, port, source.Database, source.User, source.Password));
                case "SQL Server":
                    return new SqlConnection(
                        string.Format("Server={0}{1};Database={2};User Id={3};Password={4};TrustServerCertificate=True;{5}",
                            source.Server, string.IsNullOrEmpty(port) ? "" : ("," + port), source.Database, source.User, source.Password,
                            source.PermissionMode == "readonly" ? "ApplicationIntent=ReadOnly;" : ""));
                case "PostgreSQL":
                    return new NpgsqlConnection(
                        string.Format("Host={0};Port={1};Database={2};Username={3};Password={4};",
                            source.Server, port, source.Database, source.User, source.Password));
                case "TDengine":
                    return new TdengineConnection(source.Server, int.TryParse(port, out int tdPort) ? tdPort : 6030, source.Database, source.User, source.Password);
                case "Oracle":
                    return CreateOracleConnection(source, port);
                case "ODBC":
                    return new System.Data.Odbc.OdbcConnection(
                        string.Format("Driver={{MySQL ODBC 8.0 Unicode Driver}};Server={0};Port={1};Database={2};Uid={3};Pwd={4};",
                            source.Server, port, source.Database, source.User, source.Password));
                default:
                    throw new NotSupportedException("Unsupported DB type: " + source.DbType);
            }
        }

        private static string CleanPort(string port, string dbType)
        {
            if (!string.IsNullOrEmpty(port)) return port;
            switch (dbType)
            {
                case "MySQL": return "3306";
                case "SQL Server": return "";
                case "PostgreSQL": return "5432";
                case "TDengine": return "6030";
                case "Oracle": return "1521";
                default: return "";
            }
        }

        // ======================== 数据查询 ========================

        /// <summary>
        /// 列出数据源中所有表
        /// </summary>
        public async Task<List<string>> ListTablesAsync(string sourceId)
        {
            var source = Get(sourceId);
            if (source == null) throw new Exception("数据源不存在: " + sourceId);

            var tables = new List<string>();
            var conn = GetOrOpenConnection(source);
            string sql = GetListTablesSql(source.DbType);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                using (var reader = await Task.Run(() => cmd.ExecuteReader()))
                {
                    while (await Task.Run(() => reader.Read()))
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }
            return tables;
        }

        /// <summary>
        /// 同步获取表名列表
        /// </summary>
        public List<string> ListTables(string sourceId)
        {
            var source = Get(sourceId);
            if (source == null) throw new Exception("数据源不存在: " + sourceId);

            var tables = new List<string>();
            var conn = GetOrOpenConnection(source);
            string sql = GetListTablesSql(source.DbType);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        tables.Add(reader.GetString(0));
                }
            }
            return tables;
        }

        /// <summary>
        /// 同步获取表结构/列信息
        /// </summary>
        public List<ColumnInfo> DescribeTable(string sourceId, string tableName)
        {
            var source = Get(sourceId);
            if (source == null) throw new Exception("数据源不存在: " + sourceId);
            if (string.IsNullOrWhiteSpace(tableName))
                throw new Exception("无效的表名");

            var columns = new List<ColumnInfo>();
            var conn = GetOrOpenConnection(source);
            string sql = GetDescribeTableSql(source.DbType, tableName);

            if (string.IsNullOrEmpty(sql))
                sql = string.Format("SELECT * FROM {0} WHERE 1=0", QuoteName(tableName, source.DbType));

            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        bool isDescribe = sql.StartsWith("DESCRIBE") || sql.StartsWith("DESC ")
                            || sql.StartsWith("SHOW")
                            || (sql.StartsWith("SELECT") && sql.IndexOf("COLUMN_COMMENT", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (isDescribe)
                        {
                            // TDengine DESCRIBE 返回 [field, type, length, note] — 列名/列序与 MySQL 完全不同
                            bool isTdengine = source.DbType == "TDengine" && (reader.GetName(0) ?? "").ToLowerInvariant() == "field";
                            if (isTdengine)
                            {
                                while (reader.Read())
                                {
                                    columns.Add(new ColumnInfo
                                    {
                                        name = reader.GetString(0),
                                        type = reader.GetString(1),
                                        nullable = true,  // TDengine 正常表无 NULL 约束
                                        comment = reader.IsDBNull(3) ? "" : reader.GetString(3)
                                    });
                                }
                                return columns;
                            }

                            bool isShowFull = sql.StartsWith("SHOW");
                            int idxName = 0, idxType = 1, idxNull = isShowFull ? 3 : 2, idxComment = -1;
                            if (isShowFull)
                            {
                                // SHOW FULL COLUMNS: Field(0) Type(1) Collation(2) Null(3) Key(4) Default(5) Extra(6) Privileges(7) Comment(8)
                                idxComment = 8;
                            }
                            else
                            {
                                for (int i = 0; i < reader.FieldCount && idxComment < 0; i++)
                                {
                                    string cn = (reader.GetName(i) ?? "").ToUpperInvariant();
                                    if (cn == "COLUMN_COMMENT" || cn == "COMMENT")
                                        idxComment = i;
                                }
                            }
                            while (reader.Read())
                            {
                                var col = new ColumnInfo
                                {
                                    name = reader.GetString(idxName),
                                    type = reader.GetString(idxType),
                                    nullable = !reader.IsDBNull(idxNull) && reader.GetString(idxNull) == "YES"
                                };
                                if (idxComment >= 0 && idxComment < reader.FieldCount)
                                    col.comment = reader.IsDBNull(idxComment) ? "" : reader.GetString(idxComment);
                                columns.Add(col);
                            }
                        }
                        else
                        {
                            var schemaTable = reader.GetSchemaTable();
                            if (schemaTable != null)
                            {
                                foreach (DataRow row in schemaTable.Rows)
                                {
                                    columns.Add(new ColumnInfo
                                    {
                                        name = row["ColumnName"].ToString(),
                                        type = row["DataType"].ToString(),
                                        nullable = row["AllowDBNull"] != DBNull.Value && (bool)row["AllowDBNull"]
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 简单回退
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = string.Format("SELECT * FROM {0} WHERE 1=0", QuoteName(tableName, source.DbType));
                    using (var reader = cmd.ExecuteReader())
                    {
                        var schemaTable = reader.GetSchemaTable();
                        if (schemaTable != null)
                        {
                            foreach (DataRow row in schemaTable.Rows)
                                columns.Add(new ColumnInfo { name = row["ColumnName"].ToString(), type = row["DataType"].ToString() });
                        }
                    }
                }
            }
            return columns;
        }

        /// <summary>
        /// 获取表结构
        /// </summary>
        public async Task<List<ColumnInfo>> DescribeTableAsync(string sourceId, string tableName)
        {
            var source = Get(sourceId);
            if (source == null) throw new Exception("数据源不存在: " + sourceId);

            // 表名校验：非空即可，QuoteName 已做方言转义（反引号/方括号/双引号）
            if (string.IsNullOrWhiteSpace(tableName))
                throw new Exception("无效的表名");

            var columns = new List<ColumnInfo>();
            var conn = GetOrOpenConnection(source);
            string sql = GetDescribeTableSql(source.DbType, tableName);

            if (string.IsNullOrEmpty(sql))
            {
                // Fallback: SELECT * LIMIT 0
                sql = string.Format("SELECT * FROM {0} WHERE 1=0", QuoteName(tableName, source.DbType));
            }

            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    using (var reader = await Task.Run(() => cmd.ExecuteReader()))
                    {
                        bool isDescribe = sql.StartsWith("DESCRIBE") || sql.StartsWith("DESC ")
                            || sql.StartsWith("SHOW")
                            || (sql.StartsWith("SELECT") && sql.IndexOf("COLUMN_COMMENT", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (isDescribe)
                        {
                            // TDengine DESCRIBE 返回 [field, type, length, note] — 列名/列序与 MySQL 完全不同
                            bool isTdengine = source.DbType == "TDengine" && (reader.GetName(0) ?? "").ToLowerInvariant() == "field";
                            if (isTdengine)
                            {
                                while (await Task.Run(() => reader.Read()))
                                {
                                    columns.Add(new ColumnInfo
                                    {
                                        name = reader.GetString(0),
                                        type = reader.GetString(1),
                                        nullable = true,
                                        comment = reader.IsDBNull(3) ? "" : reader.GetString(3)
                                    });
                                }
                                return columns;
                            }

                            // SHOW FULL COLUMNS vs DESCRIBE 列序不同，按列名匹配
                            bool isShowFull = sql.StartsWith("SHOW");
                            int idxName = 0, idxType = 1, idxNull = isShowFull ? 3 : 2, idxComment = -1;
                            if (isShowFull)
                            {
                                // SHOW FULL COLUMNS: Field(0) Type(1) Collation(2) Null(3) Key(4) Default(5) Extra(6) Privileges(7) Comment(8)
                                idxComment = 8;
                            }
                            else
                            {
                                // DESCRIBE: Field(0) Type(1) Null(2) Key(3) Default(4) Extra(5) — 无 Comment
                                // PRAGMA table_info: cid(0) name(1) type(2) notnull(3) dflt_value(4) pk(5) — 无 Comment
                                // SELECT ... COLUMN_COMMENT: 列名(0) 类型(1) nullable(2) comment(3)
                                for (int i = 0; i < reader.FieldCount && idxComment < 0; i++)
                                {
                                    string cn = (reader.GetName(i) ?? "").ToUpperInvariant();
                                    if (cn == "COLUMN_COMMENT" || cn == "COMMENT" || cn == "COLUMN_COMMENT")
                                        idxComment = i;
                                }
                            }
                            while (await Task.Run(() => reader.Read()))
                            {
                                var col = new ColumnInfo
                                {
                                    name = reader.GetString(idxName),
                                    type = reader.GetString(idxType),
                                    nullable = !reader.IsDBNull(idxNull) && reader.GetString(idxNull) == "YES"
                                };
                                if (idxComment >= 0 && idxComment < reader.FieldCount)
                                    col.comment = reader.IsDBNull(idxComment) ? "" : reader.GetString(idxComment);
                                columns.Add(col);
                            }
                        }
                        else
                        {
                            var schemaTable = reader.GetSchemaTable();
                            if (schemaTable != null)
                            {
                                foreach (DataRow row in schemaTable.Rows)
                                {
                                    columns.Add(new ColumnInfo
                                    {
                                        name = row["ColumnName"].ToString(),
                                        type = row["DataType"].ToString(),
                                        nullable = row["AllowDBNull"] != DBNull.Value && (bool)row["AllowDBNull"]
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 如果读取 schema 失败，尝试直接用 reader
            }
            return columns;
        }

        /// <summary>
        /// 执行查询（仅 SELECT，权限受 PermissionMode 约束）
        /// </summary>
        public async Task<QueryResult> RunQueryAsync(string sourceId, string sql)
        {
            var source = Get(sourceId);
            if (source == null) throw new Exception("数据源不存在: " + sourceId);

            string trimmed = sql.Trim();
            string upper = trimmed.ToUpperInvariant();

            // 安全检查
            if (!upper.StartsWith("SELECT") && !upper.StartsWith("SHOW") &&
                !upper.StartsWith("DESCRIBE") && !upper.StartsWith("DESC") &&
                !upper.StartsWith("EXPLAIN") && !upper.StartsWith("PRAGMA"))
            {
                if (source.PermissionMode != "fullcontrol")
                {
                    throw new Exception("此数据源权限模式为只读，仅允许 SELECT / SHOW / DESCRIBE / EXPLAIN 语句。"
                        + "当前 SQL: " + trimmed.Substring(0, Math.Min(50, trimmed.Length)));
                }
            }

            // 禁止危险操作
            var forbidden = new[] { "DROP", "ALTER", "CREATE", "TRUNCATE", "GRANT", "REVOKE" };
            foreach (var f in forbidden)
            {
                if (upper.Contains(f))
                    throw new Exception("禁止执行 " + f + " 语句。此操作被拒绝。");
            }
            if (upper.Contains("DELETE") || upper.Contains("UPDATE") || upper.Contains("INSERT"))
            {
                if (source.PermissionMode != "fullcontrol")
                    throw new Exception("此数据源为只读模式，禁止 DELETE/UPDATE/INSERT。");
            }

            // 行数限制
            int maxRows = source.MaxRows > 0 ? source.MaxRows : int.MaxValue;
            if (maxRows < int.MaxValue && upper.StartsWith("SELECT") && !upper.Contains("LIMIT"))
            {
                sql = trimmed.TrimEnd(';') + " LIMIT " + maxRows;
            }

            // TDengine 时间列名转换：标准 timestamp → TDengine ts
            if (source.DbType == "TDengine")
            {
                sql = sql.Replace("timestamp", "ts");
            }

            var conn = GetOrOpenConnection(source);
            var result = new QueryResult();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.CommandTimeout = 30;
                using (var reader = await Task.Run(() => cmd.ExecuteReader()))
                {
                    // 列名
                    for (int i = 0; i < reader.FieldCount; i++)
                        result.columns.Add(reader.GetName(i));

                    // 数据行
                    int count = 0;
                    while (await Task.Run(() => reader.Read()) && count < maxRows)
                    {
                        var row = new List<object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var val = reader.GetValue(i);
                            row.Add(val == DBNull.Value ? null : val);
                        }
                        result.rows.Add(row);
                        count++;
                    }
                    result.truncated = count >= maxRows;
                }
            }

            sw.Stop();
            result.elapsedMs = sw.ElapsedMilliseconds;
            return result;
        }


        private static IDbConnection CreateOracleConnection(DataSourceConnection source, string port)
        {
            try
            {
                var asm = System.Reflection.Assembly.Load("Oracle.ManagedDataAccess");
                var connType = asm.GetType("Oracle.ManagedDataAccess.Client.OracleConnection");
                var connStr = string.Format(
                    "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={0})(PORT={1}))(CONNECT_DATA=(SERVICE_NAME={2})));User Id={3};Password={4};",
                    source.Server, port, source.Database, source.User, source.Password);
                return (IDbConnection)Activator.CreateInstance(connType, connStr);
            }
            catch (Exception)
            {
                throw new Exception("Oracle.ManagedDataAccess is not installed. Please install Oracle.ManagedDataAccess NuGet package via VS NuGet Manager.");
            }
        }
                // ======================== SQL 方言 ========================

        private string GetListTablesSql(string dbType)
        {
            switch (dbType)
            {
                case "SQLite": return "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
                case "MySQL": return "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME";
                case "SQL Server": return "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME";
                case "PostgreSQL": return "SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema') ORDER BY tablename";
                case "TDengine": return "SELECT table_name FROM information_schema.ins_tables WHERE db_name=DATABASE() ORDER BY table_name";
                case "Oracle": return "SELECT table_name FROM user_tables ORDER BY table_name";
                case "ODBC": return "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE=''BASE TABLE'' ORDER BY TABLE_NAME";
                default: return "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE=''BASE TABLE'' ORDER BY TABLE_NAME";
            }
        }

        private string GetDescribeTableSql(string dbType, string tableName)
        {
            switch (dbType)
            {
                case "SQLite": return string.Format("PRAGMA table_info({0})", tableName);
                // MySQL/MariaDB: 用 SHOW FULL COLUMNS 代替 DESCRIBE（DESCRIBE 不含 Comment 列）
                case "MySQL": return string.Format("SHOW FULL COLUMNS FROM {0}", QuoteName(tableName, dbType));
                // TDengine: DESCRIBE 返回 [field, type, length, note] — 在 DescribeTable 中专用解析
                case "TDengine": return string.Format("DESCRIBE {0}", QuoteName(tableName, dbType));
                // SQL Server: 加上 extended properties
                case "SQL Server":
                    return string.Format(@"SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE,
    CAST(COALESCE(ep.value, '') AS NVARCHAR(500)) AS COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS c
LEFT JOIN sys.extended_properties ep ON ep.major_id = OBJECT_ID('{0}') 
    AND ep.minor_id = c.ORDINAL_POSITION AND ep.name = 'MS_Description'
WHERE c.TABLE_NAME='{0}' ORDER BY c.ORDINAL_POSITION", tableName.Replace("'", "''"));
                // PostgreSQL: 加上 col_description
                case "PostgreSQL":
                    return string.Format(@"SELECT c.column_name, c.data_type, c.is_nullable,
    COALESCE(pd.description, '') AS column_comment
FROM information_schema.columns c
LEFT JOIN pg_catalog.pg_statio_all_tables st ON c.table_schema = st.schemaname AND c.table_name = st.relname
LEFT JOIN pg_catalog.pg_description pd ON pd.objoid = st.relid AND pd.objsubid = c.ordinal_position
WHERE c.table_name='{0}' ORDER BY c.ordinal_position", tableName.Replace("'", "''"));
                default: return "";
            }
        }

        // ======================== Tag & Schema Analysis ========================

        /// <summary>
        /// 仅列出表名和用途分类（不获取列信息、不计行数），用于数据源管理器初始展示。
        /// 返回的 TableMeta 中 IsAnalyzed=true 表示默认全选。
        /// </summary>
        public async Task<List<TableMeta>> ListTableMetasAsync(string sourceId)
        {
            var source = Get(sourceId);
            if (source == null) return new List<TableMeta>();

            var tables = await ListTablesAsync(sourceId);
            var results = new List<TableMeta>();
            using (var conn = CreateConnection(source))
            {
                conn.Open();
                bool isSqlite = source.DbType == "SQLite";
                bool isMysql = source.DbType == "MySQL";

                foreach (var tn in tables)
                {
                    if (tn.StartsWith("sqlite_")) continue;

                    var meta = new TableMeta
                    {
                        TableName = tn,
                        Tag = GenerateSlug(tn),
                        TagCn = tn,
                        Purpose = ClassifyTable(tn, conn, isSqlite, isMysql),
                        Columns = null,
                        RowCount = -1,
                        IsAnalyzed = false  // 默认不选，由用户手动勾选
                    };

                    // 尝试获取表注释覆盖 TagCn
                    try
                    {
                        string comment = GetTableComment(sourceId, tn);
                        if (!string.IsNullOrEmpty(comment))
                            meta.TagCn = comment;
                    }
                    catch { }

                    results.Add(meta);
                }
            }

            source.Tables = results;
            Save(source);
            return results;
        }

        /// <summary>
        /// 逐表分析结构（列信息、行数），可选只分析指定表名集合（null=全部）
        /// </summary>
        public async Task<List<TableMeta>> AnalyzeSourceAsync(string sourceId, HashSet<string> only = null)
        {
            var source = Get(sourceId);
            if (source == null) return new List<TableMeta>();

            var tables = await ListTablesAsync(sourceId);
            var results = new List<TableMeta>();
            using (var conn = CreateConnection(source))
            {
                conn.Open();
                bool isSqlite = source.DbType == "SQLite";
                bool isMysql = source.DbType == "MySQL";

                foreach (var tableName in tables)
                {
                    if (tableName.StartsWith("sqlite_")) continue;
                    if (only != null && !only.Contains(tableName)) continue;

                    var meta = new TableMeta { TableName = tableName, Columns = new List<ColumnMeta>() };

                    // Generate table tag
                    meta.Tag = GenerateSlug(tableName);
                    meta.TagCn = tableName;
                    meta.Purpose = ClassifyTable(tableName, conn, isSqlite, isMysql);

                    // 获取表注释
                    try
                    {
                        string tableComment = GetTableComment(sourceId, tableName);
                        if (!string.IsNullOrEmpty(tableComment))
                        {
                            meta.TagCn = tableComment;
                        }
                    }
                    catch { /* 取表注释失败不中断 */ }

                    // Get row count
                    try
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM " + QuoteName(tableName, isSqlite, isMysql);
                            meta.RowCount = Convert.ToInt64(cmd.ExecuteScalar());
                        }
                    }
                    catch { meta.RowCount = -1; }

                    // Get columns
                    try
                    {
                        var cols = await DescribeTableAsync(sourceId, tableName);
                        foreach (var col in cols)
                        {
                            var colMeta = new ColumnMeta
                            {
                                ColumnName = col.name,
                                DataType = col.type,
                                IsNullable = col.nullable,
                                Tag = TranslateAndSlug(col.name),
                                TagCn = !string.IsNullOrEmpty(col.comment) ? col.comment : col.name,
                                Comment = col.comment ?? ""
                            };
                            meta.Columns.Add(colMeta);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug("Analyze table " + tableName + " columns failed: " + ex.Message);
                    }

                    results.Add(meta);
                }
            }

            // Persist
            source.Tables = results;
            Save(source);
            return results;
        }

        private static string GenerateSlug(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in input.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == '/' || c == '-') sb.Append(c);
                else sb.Append('_');
            }
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
        }

        internal static string TranslateAndSlug(string segment)
        {
            string translated = IndustrialVocabulary.TranslateCompound(segment);
            return GenerateSlug(translated);
        }

        private string ClassifyTable(string tableName, IDbConnection conn, bool isSqlite, bool isMysql)
        {
            string lower = tableName.ToLowerInvariant();

            // Name-based heuristics
            if (lower.Contains("config") || lower.Contains("setting") || lower.Contains("setup"))
                return "config";
            if (lower.Contains("cache") || lower.Contains("temp") || lower.Contains("staging"))
                return "cache";
            if (lower.Contains("heartbeat") || lower.Contains("watchdog"))
                return "heartbeat";

            // Schema-based heuristics: try to get column names
            try
            {
                var colNames = new List<string>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM " + QuoteName(tableName, isSqlite, isMysql) + " WHERE 1=0";
                    using (var reader = cmd.ExecuteReader())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                            colNames.Add(reader.GetName(i).ToLowerInvariant());
                    }
                }

                bool hasTimestamp = colNames.Contains("timestamp") || colNames.Contains("ts") || colNames.Contains("time") || colNames.Contains("created_at");
                bool hasDevice = colNames.Contains("device") || colNames.Contains("device_id") || colNames.Contains("device_name");
                bool hasVariable = colNames.Contains("variable") || colNames.Contains("tag") || colNames.Contains("metric") || colNames.Contains("point");
                bool hasValue = colNames.Contains("value") || colNames.Contains("val") || colNames.Contains("data");
                bool hasStatus = colNames.Contains("status") || colNames.Contains("state") || colNames.Contains("online");
                bool hasNeedMqtt = colNames.Contains("need_mqtt");
                bool hasNeedDb = colNames.Contains("need_db");

                if (hasTimestamp && hasDevice && (hasVariable || hasValue))
                    return "history";
                if (hasDevice && hasStatus)
                    return "heartbeat";
                if (hasNeedMqtt && hasNeedDb)
                    return "cache";
                if (hasTimestamp && hasValue && !hasDevice)
                    return "history";
            }
            catch { }

            return "unknown";
        }

        /// <summary>
        /// 获取指定表的注释/备注（表级 COMMENT）
        /// </summary>
        public string GetTableComment(string sourceId, string tableName)
        {
            var source = Get(sourceId);
            if (source == null || string.IsNullOrWhiteSpace(tableName)) return "";

            try
            {
                var conn = GetOrOpenConnection(source);
                using (var cmd = conn.CreateCommand())
                {
                    switch (source.DbType)
                    {
                        case "MySQL":
                            cmd.CommandText = string.Format(
                                "SELECT TABLE_COMMENT FROM information_schema.TABLES WHERE TABLE_SCHEMA='{0}' AND TABLE_NAME='{1}'",
                                source.Database.Replace("'", "''"), tableName.Replace("'", "''"));
                            var obj = cmd.ExecuteScalar();
                            return obj?.ToString() ?? "";
                        case "SQL Server":
                            cmd.CommandText = string.Format(
                                "SELECT CAST(ep.value AS NVARCHAR(500)) FROM sys.extended_properties ep WHERE ep.major_id = OBJECT_ID('{0}') AND ep.minor_id = 0 AND ep.name = 'MS_Description'",
                                tableName.Replace("'", "''"));
                            var obj2 = cmd.ExecuteScalar();
                            return obj2?.ToString() ?? "";
                        case "PostgreSQL":
                            cmd.CommandText = string.Format(
                                "SELECT obj_description('{0}'::regclass, 'pg_class')", tableName.Replace("'", "''"));
                            var obj3 = cmd.ExecuteScalar();
                            return obj3?.ToString() ?? "";
                        default:
                            return "";
                    }
                }
            }
            catch { return ""; }
        }

        private string QuoteName(string name, bool isSqlite, bool isMysql)
        {
            if (isSqlite) return "\"" + name + "\"";
            if (isMysql) return "`" + name + "`";
            return name;
        }

        private string QuoteName(string name, string dbType)
        {
            switch (dbType)
            {
                case "MySQL": return "`" + name + "`";
                case "SQL Server": return "[" + name + "]";
                case "PostgreSQL": return "\"" + name + "\"";
                case "Oracle": return "\"" + name + "\"";
                case "ODBC": return "\"" + name + "\"";
                default: return name;
            }
        }

        private class DataSourceConfig
        {
            public List<DataSourceConnection> DataSources { get; set; }
            public List<DataSourceFolder> Folders { get; set; }
        }
    }

    // ======================== 辅助类 ========================

    public class ColumnInfo
    {
        public string name { get; set; }
        public string type { get; set; }
        public bool nullable { get; set; }
        public string comment { get; set; }
    }

    public class QueryResult
    {
        public List<string> columns { get; set; } = new List<string>();
        public List<List<object>> rows { get; set; } = new List<List<object>>();
        public int rowCount { get { return rows.Count; } }
        public bool truncated { get; set; }
        public long elapsedMs { get; set; }
    }

    // ======================== MCP 数据源工具注册表 ========================

    /// <summary>
    /// 管理暴露给 MCP 的数据源工具（动态注册/注销）
    /// </summary>
    public static class McpDataSourceRegistry
    {
        private static readonly HashSet<string> _registeredAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 重建所有暴露给 MCP 的数据源工具
        /// </summary>
        public static void Rebuild()
        {
            // 先清旧工具
            foreach (var alias in _registeredAliases.ToList())
            {
                McpToolRegistry.Instance?.Unregister(alias + "_list_tables");
                McpToolRegistry.Instance?.Unregister(alias + "_describe_table");
                McpToolRegistry.Instance?.Unregister(alias + "_run_query");
                _registeredAliases.Remove(alias);
            }

            // 重新注册
            var sources = DataSourceService.Instance.GetAll().Where(s => s.ExposeToMcp);
            foreach (var src in sources)
            {
                string alias = string.IsNullOrWhiteSpace(src.McpAlias)
                    ? SanitizeAlias(src.Name)
                    : SanitizeAlias(src.McpAlias);

                _registeredAliases.Add(alias);

                McpToolRegistry.Instance?.Register(
                    alias + "_list_tables",
                    string.Format("列出数据源 [{0}] ({1}) 中的所有表。", src.Name, src.DbType),
                    new ListTablesMcpTool(src.Id),
                    null);

                McpToolRegistry.Instance?.Register(
                    alias + "_describe_table",
                    string.Format("获取数据源 [{0}] 中指定表的结构：列名、类型、是否可空。", src.Name),
                    new DescribeTableMcpTool(src.Id),
                    new Dictionary<string, (string, string, bool)>
                    {
                        ["table_name"] = ("string", "表名，必填", true)
                    });

                string permNote = src.PermissionMode == "readonly" ? "（只读）" : "（完全控制）";
                McpToolRegistry.Instance?.Register(
                    alias + "_run_query",
                    string.Format("在数据源 [{0}] {1}上执行 SQL 查询。{2}",
                        src.Name, src.DbType, permNote),
                    new RunQueryMcpTool(src.Id),
                    new Dictionary<string, (string, string, bool)>
                    {
                        ["sql"] = ("string", "要执行的 SQL 语句。仅允许 SELECT / SHOW / DESCRIBE。", true)
                    });
            }
        }

        private static string SanitizeAlias(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "ds";
            var sb = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
                else sb.Append('_');
            }
            string result = sb.ToString().Trim('_').ToLowerInvariant();
            return string.IsNullOrEmpty(result) ? "ds" : result;
        }
    }

    // ======================== MCP 工具实现 ========================

    internal class ListTablesMcpTool : IMcpTool
    {
        private readonly string _sourceId;
        public ListTablesMcpTool(string sourceId) { _sourceId = sourceId; }

        public async Task<object> ExecuteAsync(Newtonsoft.Json.Linq.JObject args)
        {
            try
            {
                var source = DataSourceService.Instance.Get(_sourceId);
                var tableNames = await DataSourceService.Instance.ListTablesAsync(_sourceId);
                var enriched = new List<object>();
                foreach (var tbl in tableNames)
                {
                    var meta = source != null && source.Tables != null
                        ? source.Tables.FirstOrDefault(t => t.TableName == tbl)
                        : null;
                    if (meta != null)
                    {
                        enriched.Add(new
                        {
                            name = meta.TableName,
                            tag = meta.Tag,
                            tag_cn = meta.TagCn,
                            purpose = meta.Purpose,
                            row_count = meta.RowCount,
                            column_count = meta.Columns != null ? meta.Columns.Count : 0
                        });
                    }
                    else
                    {
                        enriched.Add(new { name = tbl, tag = "", tag_cn = "", purpose = "", row_count = 0 });
                    }
                }
                return new { tables = enriched, count = enriched.Count };
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }
    }

    internal class DescribeTableMcpTool : IMcpTool
    {
        private readonly string _sourceId;
        public DescribeTableMcpTool(string sourceId) { _sourceId = sourceId; }

        public async Task<object> ExecuteAsync(Newtonsoft.Json.Linq.JObject args)
        {
            string tableName = args["table_name"] != null ? args["table_name"].ToString() : null;
            if (string.IsNullOrEmpty(tableName)) return new { error = "参数 table_name 是必填的" };
            try
            {
                var source = DataSourceService.Instance.Get(_sourceId);
                var cols = await DataSourceService.Instance.DescribeTableAsync(_sourceId, tableName);
                var enriched = new List<object>();
                var tableMeta = source != null && source.Tables != null
                    ? source.Tables.FirstOrDefault(t => t.TableName == tableName)
                    : null;
                foreach (var col in cols)
                {
                    var colMeta = tableMeta != null && tableMeta.Columns != null
                        ? tableMeta.Columns.FirstOrDefault(c => c.ColumnName == col.name)
                        : null;
                    if (colMeta != null)
                    {
                        enriched.Add(new
                        {
                            name = col.name,
                            type = col.type,
                            nullable = col.nullable,
                            tag = colMeta.Tag,
                            tag_cn = colMeta.TagCn
                        });
                    }
                    else
                    {
                        enriched.Add(new
                        {
                            name = col.name,
                            type = col.type,
                            nullable = col.nullable,
                            tag = "",
                            tag_cn = ""
                        });
                    }
                }
                return new { table = tableName, columns = enriched, column_count = enriched.Count };
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }
    }

    internal class RunQueryMcpTool : IMcpTool
    {
        private readonly string _sourceId;
        public RunQueryMcpTool(string sourceId) { _sourceId = sourceId; }

        public async Task<object> ExecuteAsync(Newtonsoft.Json.Linq.JObject args)
        {
            string sql = args["sql"]?.ToString();
            if (string.IsNullOrEmpty(sql)) return new { error = "参数 sql 是必填的" };
            try
            {
                var result = await DataSourceService.Instance.RunQueryAsync(_sourceId, sql);
                return new
                {
                    columns = result.columns,
                    rows = result.rows,
                    row_count = result.rowCount,
                    truncated = result.truncated,
                    elapsed_ms = result.elapsedMs
                };
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }
    }

    /// <summary>
    /// MCP 工具：列出所有数据源
    /// </summary>
    internal class DataSourceListAllTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(Newtonsoft.Json.Linq.JObject args)
        {
            try
            {
                var sources = DataSourceService.Instance.GetAll();
                var list = new List<object>();
                foreach (var s in sources)
                {
                    int tableCount = s.Tables?.Count ?? 0;
                    list.Add(new
                    {
                        id = s.Id,
                        name = s.Name,
                        db_type = s.DbType,
                        server = s.Server + ":" + s.Port,
                        database = s.Database,
                        folder = s.Folder,
                        table_count = tableCount,
                        expose_mcp = s.ExposeToMcp,
                        last_tested = s.LastTestedAt?.ToString("yyyy-MM-dd HH:mm") ?? "从未测试"
                    });
                }
                return new { data_sources = list, total = list.Count };
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }
    }

    /// <summary>
    /// MCP 工具：表统计信息（行数、列清单、最新数据摘要）
    /// </summary>
    internal class DataSourceTableInfoTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(Newtonsoft.Json.Linq.JObject args)
        {
            string sourceId = args["source_id"]?.ToString();
            string tableName = args["table_name"]?.ToString();
            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(tableName))
                return new { error = "source_id 和 table_name 是必填的" };

            try
            {
                var source = DataSourceService.Instance.Get(sourceId);
                if (source == null) return new { error = "数据源不存在: " + sourceId };

                var info = new Dictionary<string, object>();
                info["source_name"] = source.Name;
                info["db_type"] = source.DbType;
                info["table_name"] = tableName;

                // 行数
                var r1 = await DataSourceService.Instance.RunQueryAsync(sourceId,
                    "SELECT COUNT(*) AS cnt FROM " + tableName);
                if (r1.rows.Count > 0 && r1.rows[0].Count > 0)
                    info["row_count"] = r1.rows[0][0];

                // 列信息
                var cols = await DataSourceService.Instance.DescribeTableAsync(sourceId, tableName);
                var colList = cols.Select(c => new
                {
                    name = c.name,
                    type = c.type,
                    nullable = c.nullable,
                    comment = c.comment ?? ""
                }).ToList();
                info["columns"] = colList;
                info["column_count"] = colList.Count;

                // 最新时间戳
                try
                {
                    var r2 = await DataSourceService.Instance.RunQueryAsync(sourceId,
                        "SELECT MAX(timestamp) FROM " + tableName);
                    if (r2.rows.Count > 0 && r2.rows[0].Count > 0)
                        info["latest_timestamp"] = r2.rows[0][0];
                }
                catch
                {
                    // 没有 timestamp 列或 MAX 函数不支持则跳过
                }

                return info;
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }
    }

    /// <summary>
    /// MCP 工具：查询最新 N 条数据
    /// </summary>
    internal class DataSourceLatestDataTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(Newtonsoft.Json.Linq.JObject args)
        {
            string sourceId = args["source_id"]?.ToString();
            string tableName = args["table_name"]?.ToString();
            int limit = args["limit"] != null ? Convert.ToInt32(args["limit"].ToString()) : 10;
            if (limit < 1) limit = 1;
            if (limit > 200) limit = 200;

            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(tableName))
                return new { error = "source_id 和 table_name 是必填的" };

            try
            {
                string sql = string.Format("SELECT * FROM {0} ORDER BY timestamp DESC LIMIT {1}", tableName, limit);
                var result = await DataSourceService.Instance.RunQueryAsync(sourceId, sql);
                return new
                {
                    source_id = sourceId,
                    table = tableName,
                    limit = limit,
                    columns = result.columns,
                    rows = result.rows,
                    row_count = result.rowCount,
                    elapsed_ms = result.elapsedMs
                };
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }
    }

    /// <summary>
    /// MCP 工具：按时间范围查询数据
    /// </summary>
    internal class DataSourceQueryTimerangeTool : IMcpTool
    {
        public async Task<object> ExecuteAsync(Newtonsoft.Json.Linq.JObject args)
        {
            string sourceId = args["source_id"]?.ToString();
            string tableName = args["table_name"]?.ToString();
            string timeCol = args["time_column"]?.ToString() ?? "timestamp";
            string timeStart = args["time_start"]?.ToString();
            string timeEnd = args["time_end"]?.ToString();
            string columns = args["columns"]?.ToString() ?? "*";
            int limit = args["limit"] != null ? Convert.ToInt32(args["limit"].ToString()) : 100;
            if (limit < 1) limit = 1;
            if (limit > 1000) limit = 1000;

            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(tableName)
                || string.IsNullOrEmpty(timeStart) || string.IsNullOrEmpty(timeEnd))
                return new { error = "source_id, table_name, time_start, time_end 是必填的" };

            try
            {
                string sql = string.Format(
                    "SELECT {0} FROM {1} WHERE {2} >= '{3}' AND {2} <= '{4}' ORDER BY {2} ASC LIMIT {5}",
                    columns, tableName, timeCol, timeStart.Replace("'", "''"),
                    timeEnd.Replace("'", "''"), limit);

                var result = await DataSourceService.Instance.RunQueryAsync(sourceId, sql);
                return new
                {
                    source_id = sourceId,
                    table = tableName,
                    time_column = timeCol,
                    time_start = timeStart,
                    time_end = timeEnd,
                    limit = limit,
                    columns = result.columns,
                    rows = result.rows,
                    row_count = result.rowCount,
                    elapsed_ms = result.elapsedMs
                };
            }
            catch (Exception ex) { return new { error = ex.Message }; }
        }
    }
}
