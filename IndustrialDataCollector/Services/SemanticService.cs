using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 工业语义层服务 v2 — 基于灵活层级树模型，替代原固定四级模型
    /// 管理节点、变量关系、统一事件、节点关系的完整语义生命周期
    /// </summary>
    public class SemanticService
    {
        private static readonly Lazy<SemanticService> _instance =
            new Lazy<SemanticService>(() => new SemanticService());
        public static SemanticService Instance => _instance.Value;

        private readonly string _dbPath;
        private readonly object _lock = new object();

        private SemanticService()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection");
            Directory.CreateDirectory(dir);
            _dbPath = Path.Combine(dir, "semantic_v2.db");
            InitDatabase();
        }

        // ════════════════════════════════════════════════════════════════
        //  数据库初始化
        // ════════════════════════════════════════════════════════════════

        private void InitDatabase()
        {
            using (var conn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", _dbPath)))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        -- 统一节点主表
                        CREATE TABLE IF NOT EXISTS semantic_node (
                            id TEXT PRIMARY KEY,
                            parent_id TEXT DEFAULT '',
                            name TEXT NOT NULL,
                            code TEXT DEFAULT '',
                            kind TEXT NOT NULL DEFAULT 'Custom',
                            status TEXT DEFAULT 'Online',
                            source_type TEXT DEFAULT '',
                            source_id TEXT DEFAULT '',
                            description TEXT DEFAULT '',
                            properties TEXT DEFAULT '{}',
                            sort_order INTEGER DEFAULT 0,
                            created_at TEXT NOT NULL,
                            updated_at TEXT NOT NULL
                        );

                        -- 变量关系表
                        CREATE TABLE IF NOT EXISTS variable_relation (
                            id TEXT PRIMARY KEY,
                            variable_node_id TEXT NOT NULL,
                            relation_type TEXT NOT NULL,
                            target_type TEXT DEFAULT 'datasource_field',
                            target_datasource_id TEXT DEFAULT '',
                            target_table_name TEXT DEFAULT '',
                            target_field_name TEXT DEFAULT '',
                            constant_value TEXT DEFAULT '',
                            expression TEXT DEFAULT '',
                            unit TEXT DEFAULT '',
                            description TEXT DEFAULT '',
                            condition_variable_ids TEXT DEFAULT '[]',
                            target_variable_node_id TEXT DEFAULT ''
                        );

                        -- 统一事件表
                        CREATE TABLE IF NOT EXISTS semantic_event (
                            id TEXT PRIMARY KEY,
                            node_id TEXT NOT NULL,
                            variable_relation_id TEXT DEFAULT '',
                            event_type TEXT NOT NULL,
                            processing_method TEXT DEFAULT '仅记录',
                            processing_config TEXT DEFAULT '',
                            occurred_at TEXT NOT NULL,
                            ended_at TEXT,
                            description TEXT DEFAULT ''
                        );

                        -- 节点关系表
                        CREATE TABLE IF NOT EXISTS node_relation (
                            id TEXT PRIMARY KEY,
                            source_node_id TEXT NOT NULL,
                            target_node_id TEXT NOT NULL,
                            relation_type TEXT DEFAULT '',
                            description TEXT DEFAULT ''
                        );
                    ";
                    cmd.ExecuteNonQuery();

                    // v1.11.0 迁移：添加变量→变量关系字段
                    try
                    {
                        using (var alterCmd = new SQLiteCommand(
                            "ALTER TABLE variable_relation ADD COLUMN target_variable_node_id TEXT DEFAULT ''",
                            conn))
                        { alterCmd.ExecuteNonQuery(); }
                    }
                    catch { /* 列已存在则忽略 */ }
                }
            }
            Logger.Info("[Semantic v2] 语义层数据库初始化完成 (semantic_v2.db)");
        }

        /// <summary>
        /// 启动时初始化：建库 + 自动迁移旧数据
        /// </summary>
        public void Init()
        {
            Logger.Info("[Semantic v2] 语义层服务启动");
            MigrateFromLegacyTables();
        }

        private SQLiteConnection OpenConnection()
        {
            var conn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", _dbPath));
            conn.Open();
            return conn;
        }

        // ════════════════════════════════════════════════════════════════
        //  旧数据迁移
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 从旧的 workshop/production_line/equipment/semantic_tag 表迁移数据到 semantic_node
        /// </summary>
        public void MigrateFromLegacyTables()
        {
            lock (_lock)
            {
                try
                {
                    string legacyDbPath = _dbPath.Replace("semantic_v2.db", "semantic.db");
                    if (!File.Exists(legacyDbPath))
                    {
                        Logger.Info("[Semantic v2] 旧数据库不存在，跳过迁移");
                        return;
                    }

                    using (var legConn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", legacyDbPath)))
                    {
                        legConn.Open();

                        // 检查是否已迁移（semantic_node 表已有数据且 legacy 表中也有数据则跳过）
                        int newNodeCount = 0;
                        using (var conn = OpenConnection())
                        {
                            using (var cntCmd = conn.CreateCommand())
                            {
                                cntCmd.CommandText = "SELECT COUNT(*) FROM semantic_node";
                                newNodeCount = Convert.ToInt32(cntCmd.ExecuteScalar());
                            }
                        }
                        if (newNodeCount > 0)
                        {
                            Logger.Info("[Semantic v2] 节点表已有数据，跳过迁移");
                            return;
                        }

                        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        var workshopMap = new Dictionary<string, string>(); // oldId -> newNodeId

                        // 1. 迁移车间 → 根节点
                        using (var cmd = legConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id, name, code, description FROM workshop";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string oldId = r.GetString(0);
                                    string newId = Guid.NewGuid().ToString("N").Substring(0, 8);
                                    workshopMap[oldId] = newId;

                                    SaveNodeInternal(new SemanticNode
                                    {
                                        Id = newId,
                                        ParentId = "",
                                        Name = r.GetString(1),
                                        Code = r.IsDBNull(2) ? oldId : r.GetString(2),
                                        Kind = NodeKind.Workshop,
                                        Status = NodeStatus.Online,
                                        Description = r.IsDBNull(3) ? "" : r.GetString(3),
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    });
                                }
                            }
                        }

                        // 2. 迁移产线 → workshop 的子节点
                        using (var cmd = legConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id, workshopId, name, code, description FROM production_line";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string oldWsId = r.GetString(1);
                                    string parentId = "";
                                    if (workshopMap.ContainsKey(oldWsId))
                                        parentId = workshopMap[oldWsId];

                                    string oldId = r.GetString(0);
                                    string newId = Guid.NewGuid().ToString("N").Substring(0, 8);

                                    SaveNodeInternal(new SemanticNode
                                    {
                                        Id = newId,
                                        ParentId = parentId,
                                        Name = r.GetString(2),
                                        Code = r.IsDBNull(3) ? oldId : r.GetString(3),
                                        Kind = NodeKind.ProductionLine,
                                        Status = NodeStatus.Online,
                                        Description = r.IsDBNull(4) ? "" : r.GetString(4),
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    });
                                }
                            }
                        }

                        // 3. 迁移设备 → 按 workshopId/productionLineId 挂载
                        var equipMap = new Dictionary<string, string>();
                        using (var cmd = legConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id, name, code, equipmentType, workshopId, productionLineId, deviceConfigId, description FROM equipment";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string oldWsId = r.IsDBNull(4) ? "" : r.GetString(4);
                                    string oldLineId = r.IsDBNull(5) ? "" : r.GetString(5);
                                    string parentId = "";

                                    if (!string.IsNullOrEmpty(oldLineId))
                                        parentId = oldLineId; // 用旧产线ID，已在上面迁移
                                    else if (!string.IsNullOrEmpty(oldWsId) && workshopMap.ContainsKey(oldWsId))
                                        parentId = workshopMap[oldWsId];

                                    string oldId = r.GetString(0);
                                    string newId = Guid.NewGuid().ToString("N").Substring(0, 8);
                                    string deviceConfigId = r.IsDBNull(6) ? "" : r.GetString(6);

                                    var equipNode = new SemanticNode
                                    {
                                        Id = newId,
                                        ParentId = parentId,
                                        Name = r.GetString(1),
                                        Code = r.IsDBNull(2) ? oldId : r.GetString(2),
                                        Kind = NodeKind.Equipment,
                                        Status = NodeStatus.Online,
                                        SourceType = string.IsNullOrEmpty(deviceConfigId) ? "" : "device",
                                        SourceId = deviceConfigId,
                                        Description = r.IsDBNull(7) ? "" : r.GetString(7),
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    };
                                    equipNode.SetProperty("EquipmentType", r.IsDBNull(3) ? "" : r.GetString(3));
                                    equipNode.SetProperty("legacyWorkshopId", oldWsId);
                                    equipNode.SetProperty("legacyLineId", oldLineId);
                                    SaveNodeInternal(equipNode);

                                    equipMap[oldId] = newId;
                                }
                            }
                        }

                        // 4. 迁移标签 → equipment 的子节点 Variable
                        int tagCount = 0;
                        using (var cmd = legConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id, equipmentId, name, code, variableRole, unit, dataType, deviceConfigId, dataPointName FROM semantic_tag";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string oldEqId = r.GetString(1);
                                    string parentId = "";
                                    if (equipMap.ContainsKey(oldEqId))
                                        parentId = equipMap[oldEqId];

                                    string newId = Guid.NewGuid().ToString("N").Substring(0, 8);
                                    string dcId = r.IsDBNull(7) ? "" : r.GetString(7);
                                    string dpName = r.IsDBNull(8) ? "" : r.GetString(8);

                                    var varNode = new SemanticNode
                                    {
                                        Id = newId,
                                        ParentId = parentId,
                                        Name = r.GetString(2),
                                        Code = r.IsDBNull(3) ? "" : r.GetString(3),
                                        Kind = NodeKind.Variable,
                                        Status = NodeStatus.Online,
                                        SourceType = "device",
                                        SourceId = string.Format("{0}|{1}", dcId, dpName),
                                        Description = "",
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    };
                                    varNode.SetProperty("VariableRole", r.IsDBNull(4) ? "" : r.GetString(4));
                                    varNode.SetProperty("Unit", r.IsDBNull(5) ? "" : r.GetString(5));
                                    varNode.SetProperty("DataType", r.IsDBNull(6) ? "" : r.GetString(6));
                                    SaveNodeInternal(varNode);
                                    tagCount++;
                                }
                            }
                        }

                        // 5. 迁移设备关系 → node_relation
                        int relCount = 0;
                        using (var cmd = legConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id, sourceEquipmentId, targetEquipmentId, relationType, description FROM equipment_relation";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string srcEqId = r.GetString(1);
                                    string tgtEqId = r.GetString(2);
                                    string srcNodeId = equipMap.ContainsKey(srcEqId) ? equipMap[srcEqId] : srcEqId;
                                    string tgtNodeId = equipMap.ContainsKey(tgtEqId) ? equipMap[tgtEqId] : tgtEqId;

                                    SaveNodeRelationInternal(new SemanticEquipmentRelation
                                    {
                                        Id = r.GetString(0),
                                        SourceNodeId = srcNodeId,
                                        TargetNodeId = tgtNodeId,
                                        RelationType = r.IsDBNull(3) ? "" : r.GetString(3),
                                        Description = r.IsDBNull(4) ? "" : r.GetString(4)
                                    });
                                    relCount++;
                                }
                            }
                        }

                        // 6. 迁移设备事件 → semantic_event
                        int evtCount = 0;
                        using (var cmd = legConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id, equipmentId, eventType, occurredAt, endedAt, description FROM equipment_event";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string oldEqId = r.GetString(1);
                                    string nodeId = equipMap.ContainsKey(oldEqId) ? equipMap[oldEqId] : oldEqId;

                                    SaveEventInternal(new SemanticVariableEvent
                                    {
                                        Id = r.GetString(0),
                                        NodeId = nodeId,
                                        EventType = r.GetString(2),
                                        OccurredAt = DateTime.Parse(r.GetString(3)),
                                        EndedAt = r.IsDBNull(4) ? (DateTime?)null : DateTime.Parse(r.GetString(4)),
                                        Description = r.IsDBNull(5) ? "" : r.GetString(5)
                                    });
                                    evtCount++;
                                }
                            }
                        }

                        Logger.Info(string.Format(
                            "[Semantic v2] 旧数据迁移完成: 设备节点 {0}, 变量节点 {1}, 关系 {2}, 事件 {3}",
                            equipMap.Count, tagCount, relCount, evtCount));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info("[Semantic v2] 旧数据迁移异常: " + ex.Message);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  节点 CRUD
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 保存节点（INSERT OR REPLACE），自动管理 CreatedAt/UpdatedAt
        /// </summary>
        public void SaveNode(SemanticNode node)
        {
            lock (_lock)
            {
                var existing = GetNode(node.Id);
                if (existing != null)
                {
                    node.CreatedAt = existing.CreatedAt;
                }
                else if (node.CreatedAt == default(DateTime))
                {
                    node.CreatedAt = DateTime.Now;
                }
                node.UpdatedAt = DateTime.Now;
                SaveNodeInternal(node);
            }
        }

        private void SaveNodeInternal(SemanticNode node)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"INSERT OR REPLACE INTO semantic_node 
                    (id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at)
                    VALUES (@id, @pid, @name, @code, @kind, @status, @stype, @sid, @desc, @props, @sort, @cat, @uat)";
                cmd.Parameters.AddWithValue("@id", node.Id);
                cmd.Parameters.AddWithValue("@pid", (object)node.ParentId ?? "");
                cmd.Parameters.AddWithValue("@name", node.Name);
                cmd.Parameters.AddWithValue("@code", (object)node.Code ?? "");
                cmd.Parameters.AddWithValue("@kind", node.Kind);
                cmd.Parameters.AddWithValue("@status", (object)node.Status ?? "Online");
                cmd.Parameters.AddWithValue("@stype", (object)node.SourceType ?? "");
                cmd.Parameters.AddWithValue("@sid", (object)node.SourceId ?? "");
                cmd.Parameters.AddWithValue("@desc", (object)node.Description ?? "");
                cmd.Parameters.AddWithValue("@props", (object)node.PropertiesJson ?? "{}");
                cmd.Parameters.AddWithValue("@sort", node.SortOrder);
                cmd.Parameters.AddWithValue("@cat", node.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@uat", node.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 删除节点，cascade=true 时级联删除所有子节点
        /// </summary>
        public void DeleteNode(string id, bool cascade = true)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                {
                    if (cascade)
                    {
                        // 递归删除所有子节点
                        var children = GetChildrenInternal(conn, id, null);
                        foreach (var child in children)
                        {
                            DeleteNodeInternal(conn, child.Id, true);
                        }
                    }
                    DeleteNodeInternal(conn, id, false);
                }
            }
        }

        private void DeleteNodeInternal(SQLiteConnection conn, string id, bool cascade)
        {
            if (cascade)
            {
                var children = GetChildrenInternal(conn, id, null);
                foreach (var child in children)
                    DeleteNodeInternal(conn, child.Id, true);
            }

            // 删除关联的变量关系
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM variable_relation WHERE variable_node_id=@id";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            // 删除关联的事件
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM semantic_event WHERE node_id=@id";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            // 删除关联的节点关系
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM node_relation WHERE source_node_id=@id OR target_node_id=@id2";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@id2", id);
                cmd.ExecuteNonQuery();
            }
            // 删除节点本身
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM semantic_node WHERE id=@id";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 获取单个节点
        /// </summary>
        public SemanticNode GetNode(string id)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node WHERE id=@id";
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                            return ReadNodeFromReader(r);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 获取节点（按 SourceType + SourceId 匹配）
        /// </summary>
        public SemanticNode GetNodeBySource(string sourceType, string sourceId)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node WHERE source_type=@stype AND source_id=@sid LIMIT 1";
                    cmd.Parameters.AddWithValue("@stype", sourceType);
                    cmd.Parameters.AddWithValue("@sid", sourceId);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                            return ReadNodeFromReader(r);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 获取子节点列表
        /// </summary>
        public List<SemanticNode> GetChildren(string parentId, string kind = null)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                {
                    return GetChildrenInternal(conn, parentId, kind);
                }
            }
        }

        private List<SemanticNode> GetChildrenInternal(SQLiteConnection conn, string parentId, string kind)
        {
            var list = new List<SemanticNode>();
            using (var cmd = conn.CreateCommand())
            {
                string sql = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node WHERE parent_id=@pid AND status!='Deleted'";
                if (!string.IsNullOrEmpty(kind))
                    sql += " AND kind=@kind";
                sql += " ORDER BY sort_order, name";
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@pid", string.IsNullOrEmpty(parentId) ? "" : parentId);
                if (!string.IsNullOrEmpty(kind))
                    cmd.Parameters.AddWithValue("@kind", kind);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(ReadNodeFromReader(r));
                }
            }
            return list;
        }

        /// <summary>
        /// 单次查询加载全量节点（非删除），用于缓存构建
        /// </summary>
        public List<SemanticNode> GetAllNodes()
        {
            lock (_lock)
            {
                var list = new List<SemanticNode>();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node WHERE status!='Deleted' ORDER BY parent_id, sort_order, name";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            list.Add(ReadNodeFromReader(reader));
                    }
                }
                return list;
            }
        }

        /// <summary>
        /// 判断节点是否有子节点
        /// </summary>
        public bool HasChildren(string parentId)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM semantic_node WHERE parent_id=@pid AND status!='Deleted'";
                    cmd.Parameters.AddWithValue("@pid", parentId);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        /// <summary>
        /// 获取所有根节点（parentId 为空）
        /// </summary>
        public List<SemanticNode> GetRootNodes()
        {
            return GetChildren("");
        }

        /// <summary>
        /// 搜索节点（按名称或编码模糊匹配）
        /// </summary>
        public List<SemanticNode> SearchNodes(string keyword, string kind = null)
        {
            lock (_lock)
            {
                var list = new List<SemanticNode>();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    string sql = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node WHERE status!='Deleted' AND (name LIKE @kw OR code LIKE @kw2)";
                    if (!string.IsNullOrEmpty(kind))
                        sql += " AND kind=@kind";
                    sql += " ORDER BY kind, name";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@kw", "%" + keyword + "%");
                    cmd.Parameters.AddWithValue("@kw2", "%" + keyword + "%");
                    if (!string.IsNullOrEmpty(kind))
                        cmd.Parameters.AddWithValue("@kind", kind);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(ReadNodeFromReader(r));
                    }
                }
                return list;
            }
        }

        /// <summary>
        /// 移动节点到新的父节点，可同时调整排序
        /// </summary>
        public void MoveNode(string nodeId, string newParentId, int sortOrder = 0)
        {
            lock (_lock)
            {
                var node = GetNode(nodeId);
                if (node == null) return;
                node.ParentId = newParentId ?? "";
                node.SortOrder = sortOrder;
                node.UpdatedAt = DateTime.Now;
                // 标记为手动层级覆盖，后续同步不再自动还原
                node.SetProperty("parent_override", "true");
                SaveNodeInternal(node);
            }
        }

        /// <summary>
        /// 标记节点及所有子节点的 parent_override，防止同步还原层级
        /// </summary>
        public void SetSubtreeParentOverride(string nodeId)
        {
            lock (_lock)
            {
                var node = GetNode(nodeId);
                if (node == null) return;
                node.SetProperty("parent_override", "true");
                node.UpdatedAt = DateTime.Now;
                SaveNodeInternal(node);

                var children = GetChildren(nodeId);
                foreach (var child in children)
                    SetSubtreeParentOverride(child.Id);
            }
        }

        /// <summary>
        /// 获取某节点下所有后代节点（递归，包含自身可选）
        /// </summary>
        public List<SemanticNode> GetDescendants(string nodeId, bool includeSelf = false)
        {
            var result = new List<SemanticNode>();
            if (includeSelf)
            {
                var self = GetNode(nodeId);
                if (self != null) result.Add(self);
            }
            var children = GetChildren(nodeId);
            foreach (var child in children)
            {
                result.Add(child);
                result.AddRange(GetDescendants(child.Id, false));
            }
            return result;
        }

        /// <summary>
        /// 获取从根到指定节点的完整路径
        /// </summary>
        public List<SemanticNode> GetNodePath(string nodeId)
        {
            var path = new List<SemanticNode>();
            var current = GetNode(nodeId);
            while (current != null)
            {
                path.Insert(0, current);
                if (string.IsNullOrEmpty(current.ParentId)) break;
                current = GetNode(current.ParentId);
            }
            return path;
        }

        /// <summary>
        /// v2.0: 获取节点的完整语义路径字符串（用于 TagCn 派生）
        /// 示例: "创新精密科技有限公司/精密/二车间/挤压机/27号挤压机"
        /// </summary>
        public string GetFullPath(string nodeId)
        {
            var path = GetNodePath(nodeId);
            if (path == null || path.Count == 0) return "";
            return string.Join("/", path.Select(n => n.Name));
        }

        private SemanticNode ReadNodeFromReader(SQLiteDataReader r)
        {
            return new SemanticNode
            {
                Id = r.GetString(0),
                ParentId = r.IsDBNull(1) ? "" : r.GetString(1),
                Name = r.GetString(2),
                Code = r.IsDBNull(3) ? "" : r.GetString(3),
                Kind = r.IsDBNull(4) ? "Custom" : r.GetString(4),
                Status = r.IsDBNull(5) ? "Online" : r.GetString(5),
                SourceType = r.IsDBNull(6) ? "" : r.GetString(6),
                SourceId = r.IsDBNull(7) ? "" : r.GetString(7),
                Description = r.IsDBNull(8) ? "" : r.GetString(8),
                PropertiesJson = r.IsDBNull(9) ? "{}" : r.GetString(9),
                SortOrder = r.IsDBNull(10) ? 0 : r.GetInt32(10),
                CreatedAt = DateTime.Parse(r.GetString(11)),
                UpdatedAt = DateTime.Parse(r.GetString(12))
            };
        }

        // ════════════════════════════════════════════════════════════════
        //  变量关系 CRUD
        // ════════════════════════════════════════════════════════════════

        public void SaveVariableRelation(SemanticVariableRelation rel)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT OR REPLACE INTO variable_relation 
                        (id, variable_node_id, relation_type, target_type, target_datasource_id, target_table_name, target_field_name, target_variable_node_id, constant_value, expression, unit, description, condition_variable_ids)
                        VALUES (@id, @vnid, @rtype, @ttype, @tdsid, @ttbl, @tfld, @tvid, @cval, @expr, @unit, @desc, @cvids)";
                    cmd.Parameters.AddWithValue("@id", rel.Id);
                    cmd.Parameters.AddWithValue("@vnid", rel.VariableNodeId);
                    cmd.Parameters.AddWithValue("@rtype", rel.RelationType);
                    cmd.Parameters.AddWithValue("@ttype", (object)rel.TargetType ?? "datasource_field");
                    cmd.Parameters.AddWithValue("@tdsid", (object)rel.TargetDatasourceId ?? "");
                    cmd.Parameters.AddWithValue("@ttbl", (object)rel.TargetTableName ?? "");
                    cmd.Parameters.AddWithValue("@tfld", (object)rel.TargetFieldName ?? "");
                    cmd.Parameters.AddWithValue("@tvid", (object)rel.TargetVariableNodeId ?? "");
                    cmd.Parameters.AddWithValue("@cval", (object)rel.ConstantValue ?? "");
                    cmd.Parameters.AddWithValue("@expr", (object)rel.Expression ?? "");
                    cmd.Parameters.AddWithValue("@unit", (object)rel.Unit ?? "");
                    cmd.Parameters.AddWithValue("@desc", (object)rel.Description ?? "");
                    cmd.Parameters.AddWithValue("@cvids", (object)rel.ConditionVariableIdsJson ?? "[]");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteVariableRelation(string id)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM variable_relation WHERE id=@id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<SemanticVariableRelation> GetVariableRelations(string variableNodeId)
        {
            lock (_lock)
            {
                var list = new List<SemanticVariableRelation>();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, variable_node_id, relation_type, target_type, target_datasource_id, target_table_name, target_field_name, constant_value, expression, unit, description, condition_variable_ids, target_variable_node_id FROM variable_relation WHERE variable_node_id=@vnid ORDER BY relation_type";
                    cmd.Parameters.AddWithValue("@vnid", variableNodeId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(ReadRelationFromReader(r));
                    }
                }
                return list;
            }
        }

        public List<SemanticVariableRelation> GetVariableRelationsByType(string variableNodeId, string relationType)
        {
            lock (_lock)
            {
                var list = new List<SemanticVariableRelation>();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, variable_node_id, relation_type, target_type, target_datasource_id, target_table_name, target_field_name, constant_value, expression, unit, description, condition_variable_ids, target_variable_node_id FROM variable_relation WHERE variable_node_id=@vnid AND relation_type=@rtype ORDER BY relation_type";
                    cmd.Parameters.AddWithValue("@vnid", variableNodeId);
                    cmd.Parameters.AddWithValue("@rtype", relationType);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(ReadRelationFromReader(r));
                    }
                }
                return list;
            }
        }

        /// <summary>
        /// 获取所有变量关系
        /// </summary>
        public List<SemanticVariableRelation> GetAllVariableRelations()
        {
            lock (_lock)
            {
                var list = new List<SemanticVariableRelation>();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, variable_node_id, relation_type, target_type, target_datasource_id, target_table_name, target_field_name, constant_value, expression, unit, description, condition_variable_ids, target_variable_node_id FROM variable_relation ORDER BY relation_type";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(ReadRelationFromReader(r));
                    }
                }
                return list;
            }
        }

        private SemanticVariableRelation ReadRelationFromReader(SQLiteDataReader r)
        {
            string SafeStr(string col) { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? "" : r.GetString(i); }
            return new SemanticVariableRelation
            {
                Id = SafeStr("id"),
                VariableNodeId = SafeStr("variable_node_id"),
                RelationType = SafeStr("relation_type"),
                TargetType = SafeStr("target_type"),
                TargetDatasourceId = SafeStr("target_datasource_id"),
                TargetTableName = SafeStr("target_table_name"),
                TargetFieldName = SafeStr("target_field_name"),
                TargetVariableNodeId = SafeStr("target_variable_node_id"),
                ConstantValue = SafeStr("constant_value"),
                Expression = SafeStr("expression"),
                Unit = SafeStr("unit"),
                Description = SafeStr("description"),
                ConditionVariableIdsJson = SafeStr("condition_variable_ids")
            };
        }

        // ════════════════════════════════════════════════════════════════
        //  语义图谱分析 — 变量→变量关系图引擎
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 获取变量→变量关系的邻接表（内存缓存，避免重复查询）
        /// </summary>
        private Dictionary<string, List<SemanticVariableRelation>> GetGraphAdjacency()
        {
            var adj = new Dictionary<string, List<SemanticVariableRelation>>();
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, variable_node_id, relation_type, target_type, target_datasource_id, target_table_name, target_field_name, constant_value, expression, unit, description, condition_variable_ids, target_variable_node_id FROM variable_relation WHERE target_type='variable' AND target_variable_node_id!=''";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var rel = ReadRelationFromReader(r);
                            // 正向边: source → target
                            if (!adj.ContainsKey(rel.VariableNodeId))
                                adj[rel.VariableNodeId] = new List<SemanticVariableRelation>();
                            adj[rel.VariableNodeId].Add(rel);
                            // 反向索引: target → source（用于上游追溯）
                            if (!adj.ContainsKey("←" + rel.TargetVariableNodeId))
                                adj["←" + rel.TargetVariableNodeId] = new List<SemanticVariableRelation>();
                            adj["←" + rel.TargetVariableNodeId].Add(rel);
                        }
                    }
                }
            }
            return adj;
        }

        /// <summary>
        /// 影响分析：BFS 展开变量节点上下游关系
        /// </summary>
        public ImpactGraphResult GetImpactGraph(string variableNodeId, int maxDepth = 5)
        {
            var adj = GetGraphAdjacency();
            var result = new ImpactGraphResult
            {
                RootVariableId = variableNodeId,
                Downstream = new List<ImpactPath>(),
                Upstream = new List<ImpactPath>()
            };

            var visited = new HashSet<string>();

            // 下游：从当前变量出发，沿正向边 BFS
            var queue = new Queue<Tuple<string, int, SemanticVariableRelation>>();
            if (adj.ContainsKey(variableNodeId))
            {
                foreach (var rel in adj[variableNodeId])
                    queue.Enqueue(Tuple.Create(rel.TargetVariableNodeId, 1, rel));
            }
            visited.Add(variableNodeId);
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                string nodeId = item.Item1; int depth = item.Item2;
                if (visited.Contains(nodeId) || depth > maxDepth) continue;
                visited.Add(nodeId);
                result.Downstream.Add(new ImpactPath
                {
                    VariableNodeId = nodeId,
                    Depth = depth,
                    RelationType = item.Item3.RelationType,
                    RelationDescription = item.Item3.Description,
                    ViaSourceId = item.Item3.VariableNodeId
                });
                if (adj.ContainsKey(nodeId) && depth < maxDepth)
                {
                    foreach (var rel in adj[nodeId])
                        queue.Enqueue(Tuple.Create(rel.TargetVariableNodeId, depth + 1, rel));
                }
            }

            // 上游：反向边 BFS（用 "←" + nodeId 索引）
            visited = new HashSet<string> { variableNodeId };
            queue = new Queue<Tuple<string, int, SemanticVariableRelation>>();
            string upKey = "←" + variableNodeId;
            if (adj.ContainsKey(upKey))
            {
                foreach (var rel in adj[upKey])
                    queue.Enqueue(Tuple.Create(rel.VariableNodeId, 1, rel));
            }
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                string nodeId = item.Item1; int depth = item.Item2;
                if (visited.Contains(nodeId) || depth > maxDepth) continue;
                visited.Add(nodeId);
                result.Upstream.Add(new ImpactPath
                {
                    VariableNodeId = nodeId,
                    Depth = depth,
                    RelationType = item.Item3.RelationType,
                    RelationDescription = item.Item3.Description,
                    ViaSourceId = item.Item3.VariableNodeId
                });
                string upk = "←" + nodeId;
                if (adj.ContainsKey(upk) && depth < maxDepth)
                {
                    foreach (var rel in adj[upk])
                        queue.Enqueue(Tuple.Create(rel.VariableNodeId, depth + 1, rel));
                }
            }

            return result;
        }

        /// <summary>
        /// 获取变量节点名称映射（批量）
        /// </summary>
        public Dictionary<string, string> GetVariableNames(IEnumerable<string> nodeIds)
        {
            var map = new Dictionary<string, string>();
            if (nodeIds == null) return map;
            var ids = new HashSet<string>(nodeIds.Where(id => !string.IsNullOrEmpty(id)));
            if (ids.Count == 0) return map;
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    var inClause = string.Join(",", ids.Select(id => string.Format("'{0}'", id.Replace("'", "''"))));
                    cmd.CommandText = string.Format("SELECT id, name FROM semantic_node WHERE id IN ({0})", inClause);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            map[r.GetString(0)] = r.GetString(1);
                    }
                }
            }
            return map;
        }

        // ════════════════════════════════════════════════════════════════
        //  统一事件 CRUD
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 保存事件
        /// </summary>
        public void SaveEvent(SemanticVariableEvent evt)
        {
            lock (_lock)
            {
                if (evt.OccurredAt == default(DateTime))
                    evt.OccurredAt = DateTime.Now;
                SaveEventInternal(evt);
            }
        }

        private void SaveEventInternal(SemanticVariableEvent evt)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"INSERT OR REPLACE INTO semantic_event 
                    (id, node_id, variable_relation_id, event_type, processing_method, processing_config, occurred_at, ended_at, description)
                    VALUES (@id, @nid, @vrid, @etype, @pmethod, @pconfig, @oat, @eat, @desc)";
                cmd.Parameters.AddWithValue("@id", evt.Id);
                cmd.Parameters.AddWithValue("@nid", evt.NodeId);
                cmd.Parameters.AddWithValue("@vrid", (object)evt.VariableRelationId ?? "");
                cmd.Parameters.AddWithValue("@etype", evt.EventType);
                cmd.Parameters.AddWithValue("@pmethod", (object)evt.ProcessingMethod ?? "仅记录");
                cmd.Parameters.AddWithValue("@pconfig", (object)evt.ProcessingConfig ?? "");
                cmd.Parameters.AddWithValue("@oat", evt.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@eat", evt.EndedAt.HasValue ? (object)evt.EndedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)evt.Description ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 查询事件，支持按节点ID、时间范围、事件类型、数量限制过滤
        /// </summary>
        public List<SemanticVariableEvent> GetEvents(string nodeId = null, DateTime? from = null, DateTime? to = null, string eventType = null, int limit = 500)
        {
            lock (_lock)
            {
                var list = new List<SemanticVariableEvent>();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    var clauses = new List<string>();
                    var parameters = new List<Tuple<string, object>>();

                    if (!string.IsNullOrEmpty(nodeId))
                    {
                        clauses.Add("node_id=@nid");
                        parameters.Add(Tuple.Create("@nid", (object)nodeId));
                    }
                    if (from.HasValue)
                    {
                        clauses.Add("occurred_at>=@from");
                        parameters.Add(Tuple.Create("@from", (object)from.Value.ToString("yyyy-MM-dd HH:mm:ss")));
                    }
                    if (to.HasValue)
                    {
                        clauses.Add("occurred_at<=@to");
                        parameters.Add(Tuple.Create("@to", (object)to.Value.ToString("yyyy-MM-dd HH:mm:ss")));
                    }
                    if (!string.IsNullOrEmpty(eventType))
                    {
                        clauses.Add("event_type=@etype");
                        parameters.Add(Tuple.Create("@etype", (object)eventType));
                    }

                    string where = clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : "";
                    cmd.CommandText = string.Format("SELECT id, node_id, variable_relation_id, event_type, processing_method, processing_config, occurred_at, ended_at, description FROM semantic_event{0} ORDER BY occurred_at DESC LIMIT {1}", where, limit);

                    foreach (var p in parameters)
                        cmd.Parameters.AddWithValue(p.Item1, p.Item2);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(ReadEventFromReader(r));
                    }
                }
                return list;
            }
        }

        private SemanticVariableEvent ReadEventFromReader(SQLiteDataReader r)
        {
            return new SemanticVariableEvent
            {
                Id = r.GetString(0),
                NodeId = r.GetString(1),
                VariableRelationId = r.IsDBNull(2) ? "" : r.GetString(2),
                EventType = r.GetString(3),
                ProcessingMethod = r.IsDBNull(4) ? "仅记录" : r.GetString(4),
                ProcessingConfig = r.IsDBNull(5) ? "" : r.GetString(5),
                OccurredAt = DateTime.Parse(r.GetString(6)),
                EndedAt = r.IsDBNull(7) ? (DateTime?)null : DateTime.Parse(r.GetString(7)),
                Description = r.IsDBNull(8) ? "" : r.GetString(8)
            };
        }

        // ════════════════════════════════════════════════════════════════
        //  节点关系 CRUD
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 保存节点关系（复用 SemanticEquipmentRelation 模型）
        /// </summary>
        public void SaveNodeRelation(SemanticEquipmentRelation rel)
        {
            lock (_lock)
            {
                SaveNodeRelationInternal(rel);
            }
        }

        private void SaveNodeRelationInternal(SemanticEquipmentRelation rel)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"INSERT OR REPLACE INTO node_relation (id, source_node_id, target_node_id, relation_type, description)
                    VALUES (@id, @src, @tgt, @type, @desc)";
                cmd.Parameters.AddWithValue("@id", rel.Id);
                cmd.Parameters.AddWithValue("@src", rel.SourceNodeId);
                cmd.Parameters.AddWithValue("@tgt", rel.TargetNodeId);
                cmd.Parameters.AddWithValue("@type", (object)rel.RelationType ?? "");
                cmd.Parameters.AddWithValue("@desc", (object)rel.Description ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 获取节点的所有关系（作为源或目标）
        /// </summary>
        public List<SemanticEquipmentRelation> GetNodeRelations(string nodeId)
        {
            lock (_lock)
            {
                var list = new List<SemanticEquipmentRelation>();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, source_node_id, target_node_id, relation_type, description FROM node_relation WHERE source_node_id=@nid1 OR target_node_id=@nid2 ORDER BY relation_type";
                    cmd.Parameters.AddWithValue("@nid1", nodeId);
                    cmd.Parameters.AddWithValue("@nid2", nodeId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(new SemanticEquipmentRelation
                            {
                                Id = r.GetString(0),
                                SourceNodeId = r.GetString(1),
                                TargetNodeId = r.GetString(2),
                                RelationType = r.IsDBNull(3) ? "" : r.GetString(3),
                                Description = r.IsDBNull(4) ? "" : r.GetString(4)
                            });
                    }
                }
                return list;
            }
        }

        /// <summary>
        /// 获取所有节点关系
        /// </summary>
        public List<SemanticEquipmentRelation> GetAllNodeRelations()
        {
            lock (_lock)
            {
                var list = new List<SemanticEquipmentRelation>();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, source_node_id, target_node_id, relation_type, description FROM node_relation ORDER BY relation_type";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(new SemanticEquipmentRelation
                            {
                                Id = r.GetString(0),
                                SourceNodeId = r.GetString(1),
                                TargetNodeId = r.GetString(2),
                                RelationType = r.IsDBNull(3) ? "" : r.GetString(3),
                                Description = r.IsDBNull(4) ? "" : r.GetString(4)
                            });
                    }
                }
                return list;
            }
        }

        public void DeleteNodeRelation(string id)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM node_relation WHERE id=@id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 获取某个节点绑定的关系数量和事件数量（用于删除确认弹窗）
        /// </summary>
        public void GetBindingCounts(string nodeId, out int relationCount, out int eventCount)
        {
            relationCount = 0;
            eventCount = 0;
            lock (_lock)
            {
                using (var conn = OpenConnection())
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM node_relation WHERE source_node_id=@id OR target_node_id=@id2";
                        cmd.Parameters.AddWithValue("@id", nodeId);
                        cmd.Parameters.AddWithValue("@id2", nodeId);
                        relationCount = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM variable_relation WHERE variable_node_id=@id";
                        cmd.Parameters.AddWithValue("@id", nodeId);
                        relationCount += Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM semantic_event WHERE node_id=@id";
                        cmd.Parameters.AddWithValue("@id", nodeId);
                        eventCount = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  状态管理
        // ════════════════════════════════════════════════════════════════

        public void UpdateNodeStatus(string nodeId, string status)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE semantic_node SET status=@status, updated_at=@uat WHERE id=@id";
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@uat", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@id", nodeId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<SemanticNode> GetNodesByStatus(string status)
        {
            lock (_lock)
            {
                var list = new List<SemanticNode>();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node WHERE status=@status ORDER BY kind, name";
                    cmd.Parameters.AddWithValue("@status", status);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(ReadNodeFromReader(r));
                    }
                }
                return list;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  三向同步机制
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 从数采设备配置同步到语义节点树
        /// 按 Group/TagPathCn 建立层级，SourceType="device" + SourceId=DeviceConfig.Id 匹配
        /// </summary>
        public void SyncFromDeviceConfigs(List<DeviceConfig> configs)
        {
            if (configs == null || configs.Count == 0) return;
            lock (_lock)
            {
                try
                {
                    // 查找或创建「数采模块」大类根节点
                    string categoryRootId = FindOrCreateCategoryRoot("数采模块", "CATEGORY_DEVICE");

                    // 收集当前所有 source_type='device' 的节点ID集合
                    var existingDeviceNodes = new Dictionary<string, SemanticNode>();
                    var existingVarNodes = new Dictionary<string, SemanticNode>();
                    using (var conn = OpenConnection())
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node WHERE source_type='device' AND status!='Deleted'";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    var node = ReadNodeFromReader(r);
                                    if (node.Kind == NodeKind.Equipment || node.Kind == NodeKind.Variable)
                                        existingDeviceNodes[node.SourceId] = node;
                                }
                            }
                        }
                    }

                    var activeSourceIds = new HashSet<string>();

                    foreach (var dev in configs)
                    {
                        if (string.IsNullOrEmpty(dev.Id)) continue;

                        // --- 设备节点同步 ---
                        string deviceSourceId = dev.Id;
                        activeSourceIds.Add(deviceSourceId);

                        string parentId = categoryRootId;
                        // 按 Group/TagPathCn 解析层级 → 找到/创建父节点
                        string groupPath = !string.IsNullOrEmpty(dev.TagPathCn) ? dev.TagPathCn : dev.Group;
                        if (!string.IsNullOrEmpty(groupPath))
                        {
                            var parts = groupPath.Split('/');
                            string currentParentId = categoryRootId;
                            string currentPath = "";
                            for (int i = 0; i < parts.Length; i++)
                            {
                                string part = parts[i].Trim();
                                if (string.IsNullOrEmpty(part)) continue;
                                currentPath = i == 0 ? part : currentPath + "/" + part;

                                // 查找该层级节点
                                var existingParent = FindNodeByPath(currentPath, currentParentId);
                                if (existingParent == null)
                                {
                                    existingParent = new SemanticNode
                                    {
                                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                                        ParentId = currentParentId,
                                        Name = part,
                                        Code = currentPath,
                                        Kind = i == 0 ? NodeKind.Company : (i == 1 ? NodeKind.Workshop : NodeKind.ProductionLine),
                                        Status = NodeStatus.Online,
                                        SourceType = "device",
                                        SourceId = "group:" + currentPath,
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    };
                                    SaveNodeInternal(existingParent);
                                    Logger.Info(string.Format("[Semantic v2] 同步创建层级节点: {0}", currentPath));
                                }
                                else
                                {
                                    // 修复已有节点的 Kind（旧映射遗留：Workshop→Company 等）
                                    var expectedKind = i == 0 ? NodeKind.Company : (i == 1 ? NodeKind.Workshop : NodeKind.ProductionLine);
                                    if (existingParent.Kind != expectedKind)
                                    {
                                        existingParent.Kind = expectedKind;
                                        existingParent.UpdatedAt = DateTime.Now;
                                        SaveNodeInternal(existingParent);
                                    }
                                }
                                currentParentId = existingParent.Id;
                            }
                            parentId = currentParentId;
                        }

                        // 查找/创建设备节点
                        SemanticNode equipNode;
                        if (!existingDeviceNodes.TryGetValue(deviceSourceId, out equipNode))
                        {
                            equipNode = new SemanticNode
                            {
                                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                                ParentId = parentId,
                                Name = dev.Name,
                                Code = dev.Id,
                                Kind = NodeKind.Equipment,
                                Status = NodeStatus.Online,
                                SourceType = "device",
                                SourceId = deviceSourceId,
                                Description = string.Format("驱动类型: {0}", dev.DriverType),
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };
                            equipNode.SetProperty("DriverType", dev.DriverType);
                            equipNode.SetProperty("Group", dev.Group ?? "");
                            SaveNodeInternal(equipNode);
                            Logger.Info(string.Format("[Semantic v2] 同步创建设备节点: {0}", dev.Name));
                        }
                        else
                        {
                            // 更新已有设备节点 — 但如果用户手动移动过则不还原 parent
                            bool changed = false;
                            if (equipNode.Name != dev.Name) { equipNode.Name = dev.Name; changed = true; }
                            bool isParentOverride = equipNode.GetProperty("parent_override", "") == "true";
                            if (!isParentOverride && equipNode.ParentId != parentId)
                            {
                                equipNode.ParentId = parentId;
                                changed = true;
                            }
                            // 不覆写 Status — 运行时在线/离线由 DataProcessor.GetDeviceHealth 判定
                            if (changed)
                            {
                                equipNode.UpdatedAt = DateTime.Now;
                                SaveNodeInternal(equipNode);
                            }
                        }

                        // --- 变量节点同步 ---
                        if (dev.DataPoints != null)
                        {
                            var existingVars = GetChildren(equipNode.Id, NodeKind.Variable);
                            foreach (var dp in dev.DataPoints)
                            {
                                string varSourceId = string.Format("{0}|{1}", dev.Id, dp.Name);
                                activeSourceIds.Add(varSourceId);

                                var existingVar = existingVars.FirstOrDefault(v => v.SourceId == varSourceId);
                                if (existingVar == null)
                                {
                                    var varNode = new SemanticNode
                                    {
                                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                                        ParentId = equipNode.Id,
                                        Name = dp.Name,
                                        Code = dp.TagCn ?? dp.Name,
                                        Kind = NodeKind.Variable,
                                        Status = NodeStatus.Online,
                                        SourceType = "device",
                                        SourceId = varSourceId,
                                        Description = "",
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    };
                                    varNode.SetProperty("VariableRole", GuessVariableRole(dp.Name));
                                    varNode.SetProperty("Unit", dp.Unit ?? "");
                                    varNode.SetProperty("DataType", dp.DataType ?? "");
                                    SaveNodeInternal(varNode);
                                }
                                else
                                {
                                    // 更新变量名称等
                                    bool varChanged = false;
                                    if (existingVar.Name != dp.Name) { existingVar.Name = dp.Name; varChanged = true; }
                                    if (existingVar.GetProperty("Unit", "") != (dp.Unit ?? "")) { existingVar.SetProperty("Unit", dp.Unit ?? ""); varChanged = true; }
                                    // 不覆写 Status — 运行时在线/离线由 DataProcessor 判定
                                    if (varChanged)
                                    {
                                        existingVar.UpdatedAt = DateTime.Now;
                                        SaveNodeInternal(existingVar);
                                    }
                                }
                            }
                        }
                    }

                    // 标记不在活跃列表中的设备/变量节点为 Deleted
                    foreach (var kv in existingDeviceNodes)
                    {
                        if (!activeSourceIds.Contains(kv.Key))
                        {
                            UpdateNodeStatus(kv.Value.Id, NodeStatus.Deleted);
                            Logger.Info(string.Format("[Semantic v2] 标记已删除节点: {0} ({1})", kv.Value.Name, kv.Key));
                        }
                    }

                    // 清理无子女的自动目录节点（设备被移走后残留的空文件夹）
                    CleanOrphanGroupNodes();

                    Logger.Info(string.Format("[Semantic v2] 设备同步完成: {0} 台设备", configs.Count));
                }
                catch (Exception ex)
                {
                    Logger.Info("[Semantic v2] 设备同步异常: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 按路径查找节点
        /// </summary>
        private SemanticNode FindNodeByPath(string path, string parentId)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node WHERE code=@code AND parent_id=@pid AND status!='Deleted' LIMIT 1";
                cmd.Parameters.AddWithValue("@code", path);
                cmd.Parameters.AddWithValue("@pid", parentId ?? "");
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                        return ReadNodeFromReader(r);
                }
            }
            return null;
        }

        /// <summary>
        /// 查找或创建语义树顶层大类根节点（如「数采模块」「数据源模块」）
        /// </summary>
        private string FindOrCreateCategoryRoot(string name, string code)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id FROM semantic_node WHERE code=@code AND parent_id='' AND kind='Custom' AND status!='Deleted' LIMIT 1";
                cmd.Parameters.AddWithValue("@code", code);
                var result = cmd.ExecuteScalar();
                if (result != null)
                    return result.ToString();
            }

            // 不存在则创建
            var root = new SemanticNode
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                ParentId = "",
                Name = name,
                Code = code,
                Kind = NodeKind.Custom,
                Status = NodeStatus.Online,
                SourceType = "",
                SourceId = "",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            SaveNodeInternal(root);
            Logger.Info(string.Format("[Semantic v2] 创建大类根节点: {0}", name));
            return root.Id;
        }

        /// <summary>
        /// 从数据源配置同步到语义节点树
        /// </summary>
        public void SyncFromDataSources(List<DataSourceConnection> sources, DataSourceService dsService)
        {
            if (sources == null || sources.Count == 0) return;
            lock (_lock)
            {
                try
                {
                    // 查找或创建「数据源模块」大类根节点
                    string categoryRootId = FindOrCreateCategoryRoot("数据源模块", "CATEGORY_DATASOURCE");

                    var existingDsNodes = new Dictionary<string, SemanticNode>();
                    using (var conn = OpenConnection())
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node WHERE source_type='datasource' AND status!='Deleted'";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    var node = ReadNodeFromReader(r);
                                    existingDsNodes[node.SourceId] = node;
                                }
                            }
                        }
                    }

                    var activeSourceIds = new HashSet<string>();

                    foreach (var src in sources)
                    {
                        if (string.IsNullOrEmpty(src.Id)) continue;
                        string dsSourceId = src.Id;
                        activeSourceIds.Add(dsSourceId);

                        // 按 folder 层级找父节点
                        string parentId = categoryRootId;
                        if (!string.IsNullOrEmpty(src.Folder))
                        {
                            var parts = src.Folder.Split('/');
                            string currentParentId = categoryRootId;
                            string currentPath = "";
                            for (int i = 0; i < parts.Length; i++)
                            {
                                string part = parts[i].Trim();
                                if (string.IsNullOrEmpty(part)) continue;
                                currentPath = i == 0 ? part : currentPath + "/" + part;

                                var existingParent = FindNodeByPath(currentPath, currentParentId);
                                if (existingParent == null)
                                {
                                    existingParent = new SemanticNode
                                    {
                                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                                        ParentId = currentParentId,
                                        Name = part,
                                        Code = currentPath,
                                        Kind = NodeKind.Datasource,
                                        Status = NodeStatus.Online,
                                        SourceType = "datasource",
                                        SourceId = "folder:" + currentPath,
                                        CreatedAt = DateTime.Now,
                                        UpdatedAt = DateTime.Now
                                    };
                                    SaveNodeInternal(existingParent);
                                }
                                currentParentId = existingParent.Id;
                            }
                            parentId = currentParentId;
                        }

                        // 查找/创建数据源节点
                        bool isConnected = dsService.IsConnected(dsSourceId);
                        SemanticNode dsNode;
                        if (!existingDsNodes.TryGetValue(dsSourceId, out dsNode))
                        {
                            dsNode = new SemanticNode
                            {
                                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                                ParentId = parentId,
                                Name = src.Name,
                                Code = src.Id,
                                Kind = NodeKind.Datasource,
                                Status = isConnected ? NodeStatus.Online : NodeStatus.Offline,
                                SourceType = "datasource",
                                SourceId = dsSourceId,
                                Description = string.Format("{0} @ {1}:{2}/{3}", src.DbType, src.Server, src.Port, src.Database),
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };
                            dsNode.SetProperty("DbType", src.DbType);
                            dsNode.SetProperty("Server", src.Server ?? "");
                            dsNode.SetProperty("Database", src.Database ?? "");
                            SaveNodeInternal(dsNode);
                            Logger.Info(string.Format("[Semantic v2] 同步创建数据源节点: {0}", src.Name));
                        }
                        else
                        {
                            bool changed = false;
                            if (dsNode.Name != src.Name) { dsNode.Name = src.Name; changed = true; }
                            bool isParentOverride = dsNode.GetProperty("parent_override", "") == "true";
                            if (!isParentOverride && dsNode.ParentId != parentId) { dsNode.ParentId = parentId; changed = true; }
                            string realStatus = isConnected ? NodeStatus.Online : NodeStatus.Offline;
                            if (dsNode.Status != realStatus) { dsNode.Status = realStatus; changed = true; }
                            if (changed)
                            {
                                dsNode.UpdatedAt = DateTime.Now;
                                SaveNodeInternal(dsNode);
                            }
                        }

                        // 同步表和字段 — 若 Tables 未手动预载则从数据库实时加载
                        var tables = src.Tables;
                        if ((tables == null || tables.Count == 0) && isConnected)
                        {
                            try
                            {
                                var tableNames = dsService.ListTables(src.Id);
                                tables = new List<TableMeta>();
                                foreach (var tn in tableNames)
                                {
                                    var tm = new TableMeta { TableName = tn };
                                    try
                                    {
                                        var cols = dsService.DescribeTable(src.Id, tn);
                                        tm.Columns = cols.Select(c => new ColumnMeta
                                        {
                                            ColumnName = c.name,
                                            DataType = c.type,
                                            IsNullable = c.nullable,
                                            Tag = DataSourceService.TranslateAndSlug(c.name),
                                            TagCn = !string.IsNullOrEmpty(c.comment) ? c.comment : c.name,
                                            Comment = c.comment ?? ""
                                        }).ToList();
                                    }
                                    catch { /* 单表读取失败不中断 */ }
                                    tables.Add(tm);
                                }
                                src.Tables = tables; // 缓存回数据源对象
                            }
                            catch { /* 加载失败跳过 */ }
                        }
                        if (tables != null && tables.Count > 0)
                        {
                            SyncTableNodes(dsNode.Id, src.Id, tables.Where(t => t.IsAnalyzed).ToList(), activeSourceIds);
                        }
                    }

                    // 标记不在活跃列表中的数据源节点为 Deleted
                    foreach (var kv in existingDsNodes)
                    {
                        if (!activeSourceIds.Contains(kv.Key))
                        {
                            UpdateNodeStatus(kv.Value.Id, NodeStatus.Deleted);
                            Logger.Info(string.Format("[Semantic v2] 标记已删除数据源节点: {0}", kv.Value.Name));
                        }
                    }

                    Logger.Info(string.Format("[Semantic v2] 数据源同步完成: {0} 个数据源", sources.Count));
                }
                catch (Exception ex)
                {
                    Logger.Info("[Semantic v2] 数据源同步异常: " + ex.Message);
                }
            }
        }

        private void CleanOrphanGroupNodes()
        {
            try
            {
                using (var conn = OpenConnection())
                {
                    // 删除所有没有子节点的自动目录节点 (source_id 以 group: 开头)
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"DELETE FROM semantic_node
WHERE id IN (
    SELECT n.id FROM semantic_node n
    LEFT JOIN semantic_node c ON c.parent_id = n.id AND c.status != 'Deleted'
    WHERE n.source_type = 'device'
      AND n.source_id LIKE 'group:%'
      AND n.status != 'Deleted'
      AND c.id IS NULL
)";
                        int removed = cmd.ExecuteNonQuery();
                        if (removed > 0)
                            Logger.Info(string.Format("[Semantic v2] 清理空目录节点: {0} 个", removed));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info("[Semantic v2] 清理空目录节点异常: " + ex.Message);
            }
        }

        private void SyncTableNodes(string datasourceNodeId, string datasourceId, List<TableMeta> tables, HashSet<string> activeSourceIds)
        {
            var existingTables = GetChildren(datasourceNodeId, NodeKind.DataTable);
            foreach (var tbl in tables)
            {
                if (string.IsNullOrEmpty(tbl.TableName)) continue;
                string tblSourceId = string.Format("{0}|table:{1}", datasourceId, tbl.TableName);
                activeSourceIds.Add(tblSourceId);

                var existingTbl = existingTables.FirstOrDefault(t => t.SourceId == tblSourceId);
                if (existingTbl == null)
                {
                    var tblNode = new SemanticNode
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                        ParentId = datasourceNodeId,
                        Name = tbl.TableName,
                        Code = tbl.TableName,
                        Kind = NodeKind.DataTable,
                        Status = NodeStatus.Online,
                        SourceType = "datasource",
                        SourceId = tblSourceId,
                        Description = tbl.Purpose ?? "",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    if (tbl.RowCount > 0) tblNode.SetProperty("RowCount", tbl.RowCount.ToString());
                    if (!string.IsNullOrEmpty(tbl.TagCn)) tblNode.SetProperty("TagCn", tbl.TagCn);
                    SaveNodeInternal(tblNode);

                    // 同步字段
                    SyncFieldNodes(tblNode.Id, datasourceId, tbl, activeSourceIds);
                }
                else
                {
                    // 更新已存在表节点的注释
                    bool tblChanged = false;
                    if (!string.IsNullOrEmpty(tbl.TagCn))
                    {
                        string existingTagCn = existingTbl.GetProperty("TagCn", "");
                        if (existingTagCn != tbl.TagCn)
                        {
                            existingTbl.SetProperty("TagCn", tbl.TagCn);
                            tblChanged = true;
                        }
                    }
                    if (tblChanged)
                    {
                        existingTbl.UpdatedAt = DateTime.Now;
                        SaveNodeInternal(existingTbl);
                    }
                    // 同步字段（增量：新字段创建，已有字段更新注释）
                    SyncFieldNodes(existingTbl.Id, datasourceId, tbl, activeSourceIds);
                }
            }
        }

        private void SyncFieldNodes(string tableNodeId, string datasourceId, TableMeta tbl, HashSet<string> activeSourceIds)
        {
            if (tbl.Columns == null || tbl.Columns.Count == 0) return;
            string tblSourceId = string.Format("{0}|table:{1}", datasourceId, tbl.TableName);
            var existingFields = GetChildren(tableNodeId, NodeKind.DataField);

            foreach (var col in tbl.Columns)
            {
                string colSourceId = string.Format("{0}|col:{1}", tblSourceId, col.ColumnName);
                activeSourceIds.Add(colSourceId);

                var existingField = existingFields.FirstOrDefault(f => f.SourceId == colSourceId);
                if (existingField == null)
                {
                    var colNode = new SemanticNode
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                        ParentId = tableNodeId,
                        Name = col.ColumnName,
                        Code = col.ColumnName,
                        Kind = NodeKind.DataField,
                        Status = NodeStatus.Online,
                        SourceType = "datasource",
                        SourceId = colSourceId,
                        Description = col.DataType ?? "",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    if (!string.IsNullOrEmpty(col.TagCn)) colNode.SetProperty("TagCn", col.TagCn);
                    if (!string.IsNullOrEmpty(col.LinkedVariable)) colNode.SetProperty("LinkedVariable", col.LinkedVariable);
                    SaveNodeInternal(colNode);
                }
                else
                {
                    // 更新已有字段的注释
                    bool colChanged = false;
                    if (!string.IsNullOrEmpty(col.TagCn))
                    {
                        string existingTagCn = existingField.GetProperty("TagCn", "");
                        if (existingTagCn != col.TagCn)
                        {
                            existingField.SetProperty("TagCn", col.TagCn);
                            colChanged = true;
                        }
                    }
                    if (colChanged)
                    {
                        existingField.UpdatedAt = DateTime.Now;
                        SaveNodeInternal(existingField);
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  导入导出
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 导出语义层数据到 JSON 文件
        /// </summary>
        public void ExportToJson(string filePath, bool includeRelations = true, bool includeEvents = true)
        {
            lock (_lock)
            {
                try
                {
                    var nodes = new List<SemanticNode>();
                    var relations = new List<SemanticVariableRelation>();
                    var events = new List<SemanticVariableEvent>();
                    var nodeRels = new List<SemanticEquipmentRelation>();

                    using (var conn = OpenConnection())
                    {
                        // 导出所有非删除节点
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node ORDER BY kind, sort_order, name";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                    nodes.Add(ReadNodeFromReader(r));
                            }
                        }

                        if (includeRelations)
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "SELECT id, variable_node_id, relation_type, target_type, target_datasource_id, target_table_name, target_field_name, constant_value, expression, unit, description, condition_variable_ids, target_variable_node_id FROM variable_relation ORDER BY relation_type";
                                using (var r = cmd.ExecuteReader())
                                {
                                    while (r.Read())
                                        relations.Add(ReadRelationFromReader(r));
                                }
                            }

                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "SELECT id, source_node_id, target_node_id, relation_type, description FROM node_relation ORDER BY relation_type";
                                using (var r = cmd.ExecuteReader())
                                {
                                    while (r.Read())
                                        nodeRels.Add(new SemanticEquipmentRelation
                                        {
                                            Id = r.GetString(0),
                                            SourceNodeId = r.GetString(1),
                                            TargetNodeId = r.GetString(2),
                                            RelationType = r.IsDBNull(3) ? "" : r.GetString(3),
                                            Description = r.IsDBNull(4) ? "" : r.GetString(4)
                                        });
                                }
                            }
                        }

                        if (includeEvents)
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "SELECT id, node_id, variable_relation_id, event_type, processing_method, processing_config, occurred_at, ended_at, description FROM semantic_event ORDER BY occurred_at DESC LIMIT 10000";
                                using (var r = cmd.ExecuteReader())
                                {
                                    while (r.Read())
                                        events.Add(ReadEventFromReader(r));
                                }
                            }
                        }
                    }

                    var export = new
                    {
                        version = "2.0",
                        exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        nodes = nodes,
                        variableRelations = includeRelations ? relations : null,
                        nodeRelations = includeRelations ? nodeRels : null,
                        events = includeEvents ? events : null
                    };

                    File.WriteAllText(filePath, JsonConvert.SerializeObject(export, Formatting.Indented), new System.Text.UTF8Encoding(true));
                    Logger.Info(string.Format("[Semantic v2] 导出完成: {0} 个节点, {1} 个关系, {2} 个事件 → {3}",
                        nodes.Count, relations.Count + nodeRels.Count, events.Count, filePath));
                }
                catch (Exception ex)
                {
                    Logger.Info("[Semantic v2] 导出异常: " + ex.Message);
                    throw;
                }
            }
        }

        /// <summary>
        /// 导出为 CSV（Excel 可打开），树层级展平
        /// </summary>
        public void ExportToExcel(string filePath)
        {
            lock (_lock)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("路径,节点ID,父节点ID,名称,编码,类型,状态,来源类型,来源ID,描述,属性,排序,创建时间,更新时间");

                    var allNodes = new List<SemanticNode>();
                    using (var conn = OpenConnection())
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node ORDER BY kind, sort_order, name";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                    allNodes.Add(ReadNodeFromReader(r));
                            }
                        }
                    }

                    var nodeMap = allNodes.ToDictionary(n => n.Id, n => n);
                    foreach (var node in allNodes)
                    {
                        var path = BuildNodePath(node, nodeMap);
                        sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\",{11},\"{12}\",\"{13}\"",
                            path,
                            node.Id,
                            node.ParentId,
                            node.Name.Replace("\"", "\"\""),
                            node.Code.Replace("\"", "\"\""),
                            NodeKind.GetDisplayName(node.Kind),
                            NodeStatus.GetDisplayName(node.Status),
                            node.SourceType,
                            node.SourceId,
                            node.Description.Replace("\"", "\"\""),
                            node.PropertiesJson.Replace("\"", "\"\""),
                            node.SortOrder,
                            node.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            node.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")));
                    }

                    File.WriteAllText(filePath, sb.ToString(), new System.Text.UTF8Encoding(true));
                    Logger.Info(string.Format("[Semantic v2] CSV导出完成: {0} 行 → {1}", allNodes.Count, filePath));
                }
                catch (Exception ex)
                {
                    Logger.Info("[Semantic v2] CSV导出异常: " + ex.Message);
                    throw;
                }
            }
        }

        private string BuildNodePath(SemanticNode node, Dictionary<string, SemanticNode> nodeMap)
        {
            var parts = new List<string>();
            var current = node;
            int maxDepth = 20; // 防循环
            while (current != null && maxDepth > 0)
            {
                parts.Insert(0, current.Name);
                if (string.IsNullOrEmpty(current.ParentId)) break;
                if (!nodeMap.TryGetValue(current.ParentId, out current)) break;
                maxDepth--;
            }
            return string.Join(" / ", parts);
        }

        /// <summary>
        /// 导入模式
        /// </summary>
        public enum ImportMode
        {
            FullReplace, // 清空所有手动节点，替换
            Append,      // 只添加新节点，不修改已有
            Update,      // 更新已有节点（按ID），不添加新节点
            Merge        // 添加新 + 更新已有（按名称+路径匹配）
        }

        /// <summary>
        /// 从 JSON 文件导入语义层数据
        /// </summary>
        public void ImportFromJson(string filePath, ImportMode mode)
        {
            lock (_lock)
            {
                try
                {
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    var import = JsonConvert.DeserializeObject<ImportData>(json);
                    if (import == null)
                    {
                        Logger.Info("[Semantic v2] 导入失败: 文件为空");
                        return;
                    }

                    Logger.Info(string.Format("[Semantic v2] 开始导入，模式: {0}, 节点: {1}, 关系: {2}, 事件: {3}",
                        mode, import.nodes?.Count ?? 0, import.variableRelations?.Count ?? 0, import.events?.Count ?? 0));

                    if (mode == ImportMode.FullReplace)
                    {
                        // 清空所有手动节点（SourceType=""）
                        var manualNodes = new List<SemanticNode>();
                        using (var conn = OpenConnection())
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "SELECT id FROM semantic_node WHERE source_type='' OR source_type IS NULL";
                                using (var r = cmd.ExecuteReader())
                                {
                                    while (r.Read()) manualNodes.Add(new SemanticNode { Id = r.GetString(0) });
                                }
                            }
                        }
                        foreach (var n in manualNodes)
                            DeleteNode(n.Id, true);

                        // 清空所有变量关系和节点关系
                        using (var conn = OpenConnection())
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "DELETE FROM variable_relation";
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "DELETE FROM node_relation";
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "DELETE FROM semantic_event";
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 导入所有节点
                        if (import.nodes != null)
                        {
                            foreach (var node in import.nodes)
                            {
                                node.CreatedAt = DateTime.Now;
                                node.UpdatedAt = DateTime.Now;
                                SaveNodeInternal(node);
                            }
                        }
                    }
                    else if (mode == ImportMode.Append)
                    {
                        if (import.nodes != null)
                        {
                            foreach (var node in import.nodes)
                            {
                                var existing = GetNode(node.Id);
                                if (existing == null)
                                {
                                    node.CreatedAt = DateTime.Now;
                                    node.UpdatedAt = DateTime.Now;
                                    SaveNodeInternal(node);
                                }
                            }
                        }
                    }
                    else if (mode == ImportMode.Update)
                    {
                        if (import.nodes != null)
                        {
                            foreach (var node in import.nodes)
                            {
                                var existing = GetNode(node.Id);
                                if (existing != null)
                                {
                                    node.CreatedAt = existing.CreatedAt;
                                    node.UpdatedAt = DateTime.Now;
                                    SaveNodeInternal(node);
                                }
                            }
                        }
                    }
                    else if (mode == ImportMode.Merge)
                    {
                        if (import.nodes != null)
                        {
                            // 构建已有节点索引（名称+父节点ID）
                            var existingIndex = new Dictionary<string, SemanticNode>();
                            using (var conn = OpenConnection())
                            {
                                using (var cmd = conn.CreateCommand())
                                {
                                    cmd.CommandText = "SELECT id, parent_id, name, code, kind, status, source_type, source_id, description, properties, sort_order, created_at, updated_at FROM semantic_node";
                                    using (var r = cmd.ExecuteReader())
                                    {
                                        while (r.Read())
                                        {
                                            var n = ReadNodeFromReader(r);
                                            string key = (n.ParentId ?? "") + "|" + n.Name;
                                            if (!existingIndex.ContainsKey(key))
                                                existingIndex[key] = n;
                                        }
                                    }
                                }
                            }

                            foreach (var node in import.nodes)
                            {
                                string key = (node.ParentId ?? "") + "|" + node.Name;
                                SemanticNode existing;
                                if (existingIndex.TryGetValue(key, out existing))
                                {
                                    // 更新已有节点（保留原ID）
                                    node.Id = existing.Id;
                                    node.CreatedAt = existing.CreatedAt;
                                    node.UpdatedAt = DateTime.Now;
                                    SaveNodeInternal(node);
                                }
                                else
                                {
                                    // 添加新节点
                                    node.CreatedAt = DateTime.Now;
                                    node.UpdatedAt = DateTime.Now;
                                    SaveNodeInternal(node);
                                    existingIndex[key] = node;
                                }
                            }
                        }
                    }

                    // 导入变量关系
                    if (import.variableRelations != null)
                    {
                        foreach (var rel in import.variableRelations)
                        {
                            SaveVariableRelation(rel);
                        }
                    }

                    // 导入节点关系
                    if (import.nodeRelations != null)
                    {
                        foreach (var rel in import.nodeRelations)
                        {
                            SaveNodeRelationInternal(rel);
                        }
                    }

                    // 导入事件
                    if (import.events != null)
                    {
                        foreach (var evt in import.events)
                        {
                            SaveEventInternal(evt);
                        }
                    }

                    Logger.Info("[Semantic v2] 导入完成");
                }
                catch (Exception ex)
                {
                    Logger.Info("[Semantic v2] 导入异常: " + ex.Message);
                    throw;
                }
            }
        }

        /// <summary>
        /// 导入数据容器类
        /// </summary>
        private class ImportData
        {
            public string version { get; set; }
            public string exportedAt { get; set; }
            public List<SemanticNode> nodes { get; set; }
            public List<SemanticVariableRelation> variableRelations { get; set; }
            public List<SemanticEquipmentRelation> nodeRelations { get; set; }
            public List<SemanticVariableEvent> events { get; set; }
        }

        // ════════════════════════════════════════════════════════════════
        //  辅助方法
        // ════════════════════════════════════════════════════════════════

        private string GuessVariableRole(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (name.Contains("温度")) return "温度";
            if (name.Contains("压力")) return "压力";
            if (name.Contains("流量")) return "流量";
            if (name.Contains("液位") || name.Contains("水位")) return "液位";
            if (name.Contains("电流")) return "电流";
            if (name.Contains("电压")) return "电压";
            if (name.Contains("功率")) return "功率";
            if (name.Contains("频率")) return "频率";
            if (name.Contains("转速")) return "转速";
            if (name.Contains("振动")) return "振动";
            if (name.Contains("湿度")) return "湿度";
            if (name.Contains("能耗") || name.Contains("电量")) return "能耗";
            if (name.Contains("状态") || name.Contains("运行")) return "状态";
            if (name.Contains("报警") || name.Contains("故障")) return "报警";
            if (name.Contains("位置") || name.Contains("行程")) return "位置";
            return "";
        }

        // ════════════════════════════════════════════════════════════════
        //  向后兼容方法（旧 API 适配新模型）
        // ════════════════════════════════════════════════════════════════

        [Obsolete("请使用 GetRootNodes() 或 GetChildren() 配合 kind 过滤")]
        public List<SemanticWorkshop> GetWorkshops()
        {
            return SearchNodes("", NodeKind.Workshop).Select(n => new SemanticWorkshop
            {
                Id = n.Id, Name = n.Name, Code = n.Code, Description = n.Description
            }).ToList();
        }

        [Obsolete("请使用 GetNode()")]
        public SemanticWorkshop GetWorkshop(string id)
        {
            var node = GetNode(id);
            if (node == null) return null;
            return new SemanticWorkshop
            {
                Id = node.Id, Name = node.Name, Code = node.Code, Description = node.Description
            };
        }

        [Obsolete("请使用 GetChildren() 配合 kind 过滤")]
        public List<SemanticProductionLine> GetProductionLines(string workshopId = null)
        {
            var children = string.IsNullOrEmpty(workshopId)
                ? SearchNodes("", NodeKind.ProductionLine)
                : GetChildren(workshopId, NodeKind.ProductionLine);
            return children.Select(n => new SemanticProductionLine
            {
                Id = n.Id, WorkshopId = n.ParentId, Name = n.Name, Code = n.Code, Description = n.Description
            }).ToList();
        }

        [Obsolete("请使用 SearchNodes() 或 GetChildren() 配合 kind=Equipment 过滤")]
        public List<SemanticEquipment> GetEquipments(string workshopId = null, string productionLineId = null)
        {
            var candidates = new List<SemanticNode>();
            if (!string.IsNullOrEmpty(productionLineId))
                candidates = GetChildren(productionLineId, NodeKind.Equipment);
            else if (!string.IsNullOrEmpty(workshopId))
                candidates = GetDescendants(workshopId, false).Where(n => n.Kind == NodeKind.Equipment).ToList();
            else
                candidates = SearchNodes("", NodeKind.Equipment);

            return candidates.Select(n => new SemanticEquipment
            {
                Id = n.Id,
                Name = n.Name,
                Code = n.Code,
                EquipmentType = n.GetProperty("EquipmentType", ""),
                WorkshopId = n.GetProperty("legacyWorkshopId", ""),
                ProductionLineId = n.GetProperty("legacyLineId", ""),
                DeviceConfigId = n.SourceType == "device" ? n.SourceId : "",
                Description = n.Description
            }).ToList();
        }

        [Obsolete("请使用 GetNode() 配合 kind=Equipment 过滤")]
        public SemanticEquipment GetEquipment(string id)
        {
            var node = GetNode(id);
            if (node == null) return null;
            return new SemanticEquipment
            {
                Id = node.Id,
                Name = node.Name,
                Code = node.Code,
                EquipmentType = node.GetProperty("EquipmentType", ""),
                WorkshopId = node.GetProperty("legacyWorkshopId", ""),
                ProductionLineId = node.GetProperty("legacyLineId", ""),
                DeviceConfigId = node.SourceType == "device" ? node.SourceId : "",
                Description = node.Description
            };
        }

        [Obsolete("请使用 GetNodeBySource(\"device\", deviceConfigId)")]
        public SemanticEquipment GetEquipmentByDeviceConfigId(string deviceConfigId)
        {
            var node = GetNodeBySource("device", deviceConfigId);
            if (node == null) return null;
            return new SemanticEquipment
            {
                Id = node.Id,
                Name = node.Name,
                Code = node.Code,
                EquipmentType = node.GetProperty("EquipmentType", ""),
                WorkshopId = node.GetProperty("legacyWorkshopId", ""),
                ProductionLineId = node.GetProperty("legacyLineId", ""),
                DeviceConfigId = node.SourceType == "device" ? node.SourceId : "",
                Description = node.Description
            };
        }

        [Obsolete("请使用 GetChildren() 配合 kind=Variable 过滤")]
        public List<SemanticTag> GetTags(string equipmentId = null)
        {
            var variables = string.IsNullOrEmpty(equipmentId)
                ? SearchNodes("", NodeKind.Variable)
                : GetChildren(equipmentId, NodeKind.Variable);
            return variables.Select(v => new SemanticTag
            {
                Id = v.Id,
                EquipmentId = v.ParentId,
                Name = v.Name,
                Code = v.Code,
                VariableRole = v.GetProperty("VariableRole", ""),
                Unit = v.GetProperty("Unit", ""),
                DataType = v.GetProperty("DataType", ""),
                DeviceConfigId = v.SourceType == "device" ? (v.SourceId.Contains("|") ? v.SourceId.Split('|')[0] : v.SourceId) : "",
                DataPointName = v.SourceType == "device" ? (v.SourceId.Contains("|") ? v.SourceId.Substring(v.SourceId.IndexOf('|') + 1) : "") : ""
            }).ToList();
        }

        [Obsolete("请使用 GetNodeRelations()")]
        public List<SemanticEquipmentRelation> GetRelations(string equipmentId = null)
        {
            if (string.IsNullOrEmpty(equipmentId))
                return GetAllNodeRelations();
            return GetNodeRelations(equipmentId);
        }

        [Obsolete("请使用 GetNodeRelations()")]
        public List<SemanticEquipmentRelation> GetUpstreamEquipment(string equipmentId)
        {
            return GetNodeRelations(equipmentId)
                .Where(r => r.SourceNodeId == equipmentId && r.RelationType == "上游设备")
                .ToList();
        }

        [Obsolete("请使用 GetNodeRelations()")]
        public List<SemanticEquipmentRelation> GetDownstreamEquipment(string equipmentId)
        {
            return GetNodeRelations(equipmentId)
                .Where(r => r.SourceNodeId == equipmentId && r.RelationType == "下游设备")
                .ToList();
        }

        [Obsolete("请使用 SaveNode()")]
        public void SaveWorkshop(SemanticWorkshop workshop)
        {
            var existing = GetWorkshops().FirstOrDefault(w => w.Id == workshop.Id);
            var node = new SemanticNode
            {
                Id = workshop.Id,
                ParentId = "",
                Name = workshop.Name,
                Code = workshop.Code,
                Kind = NodeKind.Workshop,
                Status = NodeStatus.Online,
                Description = workshop.Description
            };
            SaveNode(node);
        }

        [Obsolete("请使用 DeleteNode()")]
        public void DeleteWorkshop(string id)
        {
            // 取消关联子节点
            foreach (var child in GetChildren(id))
            {
                child.ParentId = "";
                SaveNodeInternal(child);
            }
            DeleteNode(id, false);
        }

        [Obsolete("请使用 SaveNode()")]
        public void SaveProductionLine(SemanticProductionLine line)
        {
            var node = new SemanticNode
            {
                Id = line.Id,
                ParentId = line.WorkshopId,
                Name = line.Name,
                Code = line.Code,
                Kind = NodeKind.ProductionLine,
                Status = NodeStatus.Online,
                Description = line.Description
            };
            SaveNode(node);
        }

        [Obsolete("请使用 DeleteNode()")]
        public void DeleteProductionLine(string id)
        {
            foreach (var child in GetChildren(id))
            {
                child.ParentId = "";
                SaveNodeInternal(child);
            }
            DeleteNode(id, false);
        }

        [Obsolete("请使用 SaveNode()")]
        public void SaveEquipment(SemanticEquipment equipment)
        {
            var node = new SemanticNode
            {
                Id = equipment.Id,
                ParentId = string.IsNullOrEmpty(equipment.ProductionLineId) ? equipment.WorkshopId : equipment.ProductionLineId,
                Name = equipment.Name,
                Code = equipment.Code,
                Kind = NodeKind.Equipment,
                Status = NodeStatus.Online,
                SourceType = string.IsNullOrEmpty(equipment.DeviceConfigId) ? "" : "device",
                SourceId = equipment.DeviceConfigId,
                Description = equipment.Description
            };
            node.SetProperty("EquipmentType", equipment.EquipmentType);
            node.SetProperty("legacyWorkshopId", equipment.WorkshopId);
            node.SetProperty("legacyLineId", equipment.ProductionLineId);
            SaveNode(node);
        }

        [Obsolete("请使用 DeleteNode()")]
        public void DeleteEquipment(string id)
        {
            DeleteNode(id, true);
        }

        [Obsolete("请使用 SaveNode()")]
        public void SaveTag(SemanticTag tag)
        {
            var node = new SemanticNode
            {
                Id = tag.Id,
                ParentId = tag.EquipmentId,
                Name = tag.Name,
                Code = tag.Code,
                Kind = NodeKind.Variable,
                Status = NodeStatus.Online,
                SourceType = "device",
                SourceId = string.Format("{0}|{1}", tag.DeviceConfigId, tag.DataPointName),
                Description = ""
            };
            node.SetProperty("VariableRole", tag.VariableRole);
            node.SetProperty("Unit", tag.Unit);
            node.SetProperty("DataType", tag.DataType);
            SaveNode(node);
        }

        [Obsolete("请使用 DeleteNode()")]
        public void DeleteTag(string id)
        {
            DeleteNode(id, false);
        }

        [Obsolete("请使用 SaveNodeRelation()")]
        public void SaveRelation(SemanticEquipmentRelation relation)
        {
            // 兼容：确保使用 NodeId 字段
            if (string.IsNullOrEmpty(relation.SourceNodeId) && !string.IsNullOrEmpty(relation.SourceEquipmentId))
                relation.SourceNodeId = relation.SourceEquipmentId;
            if (string.IsNullOrEmpty(relation.TargetNodeId) && !string.IsNullOrEmpty(relation.TargetEquipmentId))
                relation.TargetNodeId = relation.TargetEquipmentId;
            SaveNodeRelation(relation);
        }

        [Obsolete("请使用 DeleteNodeRelation()")]
        public void DeleteRelation(string id)
        {
            DeleteNodeRelation(id);
        }

        /// <summary>
        /// [向后兼容] 旧的 GetEvents - 委托给新的统一事件查询
        /// </summary>
        [Obsolete("请使用 GetEvents(nodeId, from, to, eventType, limit)")]
        public List<SemanticEquipmentEvent> GetEvents(string equipmentId, int limit = 100)
        {
            var events = GetEvents(equipmentId, null, null, null, limit);
            return events.Select(ev => new SemanticEquipmentEvent
            {
                Id = ev.Id,
                EquipmentId = ev.NodeId,
                EventType = ev.EventType,
                OccurredAt = ev.OccurredAt,
                EndedAt = ev.EndedAt,
                Description = ev.Description
            }).ToList();
        }

        /// <summary>
        /// [向后兼容] 旧的 SaveEvent - 委托给新的 SaveEvent
        /// </summary>
        [Obsolete("请使用 SaveEvent(SemanticVariableEvent)")]
        public void SaveEvent(SemanticEquipmentEvent evt)
        {
            SaveEvent(new SemanticVariableEvent
            {
                Id = evt.Id,
                NodeId = evt.EquipmentId,
                EventType = evt.EventType,
                OccurredAt = evt.OccurredAt,
                EndedAt = evt.EndedAt,
                Description = evt.Description
            });
        }

        /// <summary>
        /// [向后兼容] 从数据源树迁移（委派给 MigrateFromLegacyTables）
        /// </summary>
        [Obsolete("请使用 MigrateFromLegacyTables()")]
        public void MigrateFromDataSourceTree()
        {
            // 新版本在 Init() 中自动调用 MigrateFromLegacyTables
            Logger.Info("[Semantic v2] MigrateFromDataSourceTree 已废弃，使用 MigrateFromLegacyTables");
        }

        /// <summary>
        /// [向后兼容] 从设备同步（委派给 SyncFromDeviceConfigs）
        /// </summary>
        [Obsolete("请使用 SyncFromDeviceConfigs()")]
        public void SyncFromDevices(List<DeviceConfig> devices)
        {
            SyncFromDeviceConfigs(devices);
        }

        #region 站内消息 & 工单

        /// <summary>保存站内消息</summary>
        public void SaveInSiteMessage(InSiteMessage msg)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS in_site_messages (
                            id TEXT PRIMARY KEY,
                            title TEXT,
                            body TEXT,
                            level TEXT,
                            event_id TEXT,
                            node_id TEXT,
                            event_type TEXT,
                            created_at TEXT,
                            is_read INTEGER DEFAULT 0
                        )";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"INSERT OR REPLACE INTO in_site_messages
                            (id, title, body, level, event_id, node_id, event_type, created_at, is_read)
                            VALUES (@Id, @Title, @Body, @Level, @EventId, @NodeId, @EventType, @CreatedAt, @IsRead)";
                        cmd.Parameters.AddWithValue("@Id", msg.Id);
                        cmd.Parameters.AddWithValue("@Title", msg.Title);
                        cmd.Parameters.AddWithValue("@Body", msg.Body);
                        cmd.Parameters.AddWithValue("@Level", msg.Level);
                        cmd.Parameters.AddWithValue("@EventId", msg.EventId);
                        cmd.Parameters.AddWithValue("@NodeId", msg.NodeId);
                        cmd.Parameters.AddWithValue("@EventType", msg.EventType);
                        cmd.Parameters.AddWithValue("@CreatedAt", msg.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@IsRead", msg.IsRead ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>获取未读站内消息</summary>
        public List<InSiteMessage> GetInSiteMessages(int limit = 100)
        {
            var list = new List<InSiteMessage>();
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM in_site_messages ORDER BY created_at DESC LIMIT @Limit";
                    cmd.Parameters.AddWithValue("@Limit", limit);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new InSiteMessage
                            {
                                Id = reader["id"]?.ToString(),
                                Title = reader["title"]?.ToString(),
                                Body = reader["body"]?.ToString(),
                                Level = reader["level"]?.ToString(),
                                EventId = reader["event_id"]?.ToString(),
                                NodeId = reader["node_id"]?.ToString(),
                                EventType = reader["event_type"]?.ToString(),
                                CreatedAt = DateTime.TryParse(reader["created_at"]?.ToString(), out var dt1) ? dt1 : DateTime.Now,
                                IsRead = reader["is_read"]?.ToString() == "1"
                            });
                        }
                    }
                }
            }
            return list;
        }

        /// <summary>保存工单</summary>
        public void SaveWorkOrder(WorkOrder order)
        {
            lock (_lock)
            {
                using (var conn = OpenConnection())
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS work_orders (
                            id TEXT PRIMARY KEY,
                            title TEXT,
                            priority TEXT,
                            assignee TEXT,
                            category TEXT,
                            event_id TEXT,
                            node_id TEXT,
                            description TEXT,
                            status TEXT,
                            created_at TEXT
                        )";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"INSERT OR REPLACE INTO work_orders
                            (id, title, priority, assignee, category, event_id, node_id, description, status, created_at)
                            VALUES (@Id, @Title, @Priority, @Assignee, @Category, @EventId, @NodeId, @Description, @Status, @CreatedAt)";
                        cmd.Parameters.AddWithValue("@Id", order.Id);
                        cmd.Parameters.AddWithValue("@Title", order.Title);
                        cmd.Parameters.AddWithValue("@Priority", order.Priority);
                        cmd.Parameters.AddWithValue("@Assignee", order.Assignee);
                        cmd.Parameters.AddWithValue("@Category", order.Category);
                        cmd.Parameters.AddWithValue("@EventId", order.EventId);
                        cmd.Parameters.AddWithValue("@NodeId", order.NodeId);
                        cmd.Parameters.AddWithValue("@Description", order.Description);
                        cmd.Parameters.AddWithValue("@Status", order.Status);
                        cmd.Parameters.AddWithValue("@CreatedAt", order.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>获取工单列表</summary>
        public List<WorkOrder> GetWorkOrders(int limit = 100)
        {
            var list = new List<WorkOrder>();
            lock (_lock)
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM work_orders ORDER BY created_at DESC LIMIT @Limit";
                    cmd.Parameters.AddWithValue("@Limit", limit);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new WorkOrder
                            {
                                Id = reader["id"]?.ToString(),
                                Title = reader["title"]?.ToString(),
                                Priority = reader["priority"]?.ToString(),
                                Assignee = reader["assignee"]?.ToString(),
                                Category = reader["category"]?.ToString(),
                                EventId = reader["event_id"]?.ToString(),
                                NodeId = reader["node_id"]?.ToString(),
                                Description = reader["description"]?.ToString(),
                                Status = reader["status"]?.ToString(),
                                CreatedAt = DateTime.TryParse(reader["created_at"]?.ToString(), out var dt2) ? dt2 : DateTime.Now
                            });
                        }
                    }
                }
            }
            return list;
        }

        #endregion
    }
}
