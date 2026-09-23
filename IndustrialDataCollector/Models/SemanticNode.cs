using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 语义层统一节点模型 — 替代原车间/产线/设备/标签四级固定模型，
    /// 支持任意深度的灵活层级树，所有节点类型均使用此模型。
    /// </summary>
    public class SemanticNode
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        [JsonProperty("parent_id")]
        public string ParentId { get; set; } = "";

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("code")]
        public string Code { get; set; } = "";

        [JsonProperty("kind")]
        public string Kind { get; set; } = "Custom";

        [JsonProperty("status")]
        public string Status { get; set; } = "Online";

        [JsonProperty("source_type")]
        public string SourceType { get; set; } = "";

        [JsonProperty("source_id")]
        public string SourceId { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("properties")]
        public string PropertiesJson { get; set; } = "{}";

        [JsonProperty("sort_order")]
        public int SortOrder { get; set; } = 0;

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 扩展属性字典（不直接序列化，通过 PropertiesJson 与数据库交互）
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, string> Properties
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(PropertiesJson)) return new Dictionary<string, string>();
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(PropertiesJson)
                        ?? new Dictionary<string, string>();
                }
                catch
                {
                    return new Dictionary<string, string>();
                }
            }
            set
            {
                PropertiesJson = (value != null && value.Count > 0)
                    ? JsonConvert.SerializeObject(value)
                    : "{}";
            }
        }

        /// <summary>
        /// 是否为根节点（parent_id 为空）
        /// </summary>
        [JsonIgnore]
        public bool IsRoot { get { return string.IsNullOrEmpty(ParentId); } }

        /// <summary>
        /// 是否为手动创建的节点（非设备/数据源自动同步）
        /// </summary>
        [JsonIgnore]
        public bool IsManual { get { return string.IsNullOrEmpty(SourceType); } }

        /// <summary>
        /// 是否为已删除状态
        /// </summary>
        [JsonIgnore]
        public bool IsDeleted { get { return Status == "Deleted"; } }

        /// <summary>
        /// 设置属性值
        /// </summary>
        public void SetProperty(string key, string value)
        {
            var props = Properties;
            props[key] = value;
            Properties = props;
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        public string GetProperty(string key, string defaultValue = "")
        {
            var props = Properties;
            string val;
            if (props.TryGetValue(key, out val)) return val;
            return defaultValue;
        }

        /// <summary>
        /// 深拷贝
        /// </summary>
        public SemanticNode Clone()
        {
            return new SemanticNode
            {
                Id = this.Id,
                ParentId = this.ParentId,
                Name = this.Name,
                Code = this.Code,
                Kind = this.Kind,
                Status = this.Status,
                SourceType = this.SourceType,
                SourceId = this.SourceId,
                Description = this.Description,
                PropertiesJson = this.PropertiesJson,
                SortOrder = this.SortOrder,
                CreatedAt = this.CreatedAt,
                UpdatedAt = this.UpdatedAt
            };
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1} ({2})", Kind, Name, Id);
        }
    }

    /// <summary>
    /// 节点种类枚举常量（字符串形式，兼容 C# 7）
    /// </summary>
    public static class NodeKind
    {
        public const string Company = "Company";
        public const string Division = "Division";
        public const string Factory = "Factory";
        public const string Workshop = "Workshop";
        public const string Zone = "Zone";
        public const string ProductionLine = "ProductionLine";
        public const string WorkStation = "WorkStation";
        public const string Equipment = "Equipment";
        public const string Variable = "Variable";
        public const string Datasource = "Datasource";
        public const string DataTable = "DataTable";
        public const string DataField = "DataField";
        public const string Custom = "Custom";

        /// <summary>
        /// 获取节点类型的中文显示名称
        /// </summary>
        public static string GetDisplayName(string kind)
        {
            switch (kind)
            {
                case Company: return "公司";
                case Division: return "事业部";
                case Factory: return "工厂";
                case Workshop: return "车间";
                case Zone: return "区域";
                case ProductionLine: return "产线";
                case WorkStation: return "工段";
                case Equipment: return "设备";
                case Variable: return "变量";
                case Datasource: return "数据源";
                case DataTable: return "数据表";
                case DataField: return "数据字段";
                case Custom: return "自定义";
                default: return kind;
            }
        }

        /// <summary>
        /// 获取所有节点类型列表
        /// </summary>
        public static string[] AllKinds = new string[]
        {
            Company, Division, Factory, Workshop, Zone, ProductionLine,
            WorkStation, Equipment, Variable, Datasource, DataTable, DataField, Custom
        };

        /// <summary>
        /// 中文显示名 → 英文代码反向映射；不在预定义列表则原样返回
        /// </summary>
        public static string ToCode(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return Custom;
            switch (displayName)
            {
                case "公司": return Company;
                case "事业部": return Division;
                case "工厂": return Factory;
                case "车间": return Workshop;
                case "区域": return Zone;
                case "产线": return ProductionLine;
                case "工段": return WorkStation;
                case "设备": return Equipment;
                case "变量": return Variable;
                case "数据源": return Datasource;
                case "数据表": return DataTable;
                case "数据字段": return DataField;
                case "自定义": return Custom;
                default: return displayName;
            }
        }
    }

    /// <summary>
    /// 节点状态枚举常量
    /// </summary>
    public static class NodeStatus
    {
        public const string Online = "Online";
        public const string Offline = "Offline";
        public const string Stopped = "Stopped";
        public const string Deleted = "Deleted";

        /// <summary>
        /// 获取状态中文显示名称
        /// </summary>
        public static string GetDisplayName(string status)
        {
            switch (status)
            {
                case Online: return "在线";
                case Offline: return "离线";
                case Stopped: return "已停止";
                case Deleted: return "已删除";
                default: return status;
            }
        }
    }
}
