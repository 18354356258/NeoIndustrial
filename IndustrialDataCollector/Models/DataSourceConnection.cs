using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 数据源连接配置 — 独立于现有数据库写入模块，专用于外部查询/分析
    /// </summary>
    public class DataSourceConnection
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("dbType")]
        public string DbType { get; set; } = "MySQL";

        [JsonProperty("server")]
        public string Server { get; set; } = "localhost";

        [JsonProperty("port")]
        public string Port { get; set; } = "";

        [JsonProperty("database")]
        public string Database { get; set; } = "";

        [JsonProperty("user")]
        public string User { get; set; } = "";

        [JsonProperty("password")]
        public string Password { get; set; } = "";

        [JsonProperty("filePath")]
        public string FilePath { get; set; } = "";

        /// <summary>文件夹路径 (/ 分隔，如 "生产系统/苏州工厂")</summary>
        [JsonProperty("folder")]
        public string Folder { get; set; } = "";

        [JsonProperty("tunnelId")]
        public string TunnelId { get; set; } = "";

        /// <summary>是否暴露给 MCP 服务（AI Agent 可调用）</summary>
        [JsonProperty("exposeToMcp")]
        public bool ExposeToMcp { get; set; } = false;

        /// <summary>MCP 工具名前缀（自动生成，可手动修改）</summary>
        [JsonProperty("mcpAlias")]
        public string McpAlias { get; set; } = "";

        /// <summary>权限模式: "readonly" | "fullcontrol"</summary>
        [JsonProperty("permissionMode")]
        public string PermissionMode { get; set; } = "readonly";

        /// <summary>查询结果最大行数（0 = 不限制）</summary>
        [JsonProperty("maxRows")]
        public int MaxRows { get; set; } = 1000;

        /// <summary>备注</summary>
        [JsonProperty("notes")]
        public string Notes { get; set; } = "";

        [JsonProperty("lastTestedAt")]
        public DateTime? LastTestedAt { get; set; }

        [JsonProperty("tables")]
        public List<TableMeta> Tables { get; set; }

        
        /// <summary>文件夹ID（映射到Folder字段）</summary>
        [Newtonsoft.Json.JsonIgnore]
        public string ParentFolderId
        {
            get => Folder;
            set => Folder = value ?? "";
        }
        public DataSourceConnection Clone()
        {
            // JSON 深拷贝 — 永不漏字段（规则 50）
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<DataSourceConnection>(json);
        }
    }
}
