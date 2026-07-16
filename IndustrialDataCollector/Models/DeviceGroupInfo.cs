using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// 设备分组信息 — 为分组赋予唯一 GUID，支持跨系统引用与重命名追踪
    /// v2.6.1
    /// </summary>
    public class DeviceGroupInfo
    {
        /// <summary>全局唯一标识（GUID）</summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("D");

        /// <summary>完整分组路径，如 "一车间/挤压产线"</summary>
        [JsonProperty("path")]
        public string Path { get; set; } = "";

        /// <summary>显示名称（路径最后一段）</summary>
        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(Path)) return "";
                int idx = Path.LastIndexOf('/');
                return idx >= 0 ? Path.Substring(idx + 1) : Path;
            }
        }

        /// <summary>父路径（空字符串表示根级分组）</summary>
        [JsonIgnore]
        public string ParentPath
        {
            get
            {
                if (string.IsNullOrEmpty(Path)) return "";
                int idx = Path.LastIndexOf('/');
                return idx >= 0 ? Path.Substring(0, idx) : "";
            }
        }

        public DeviceGroupInfo() { }

        public DeviceGroupInfo(string path, string id = null)
        {
            Path = path ?? "";
            Id = string.IsNullOrEmpty(id) ? Id : id;
        }

        /// <summary>
        /// 反序列化辅助：兼容旧格式 string[] 和新格式 DeviceGroupInfo[]
        /// </summary>
        public static List<DeviceGroupInfo> DeserializeGroups(string json)
        {
            var result = new List<DeviceGroupInfo>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                var token = JToken.Parse(json);
                if (token.Type == JTokenType.Array)
                {
                    foreach (var item in (JArray)token)
                    {
                        if (item.Type == JTokenType.String)
                        {
                            // 旧格式：纯字符串路径 → 自动生成 GUID
                            result.Add(new DeviceGroupInfo(item.Value<string>()));
                        }
                        else if (item.Type == JTokenType.Object)
                        {
                            // 新格式：完整对象
                            var gi = item.ToObject<DeviceGroupInfo>();
                            if (!string.IsNullOrEmpty(gi.Path))
                                result.Add(gi);
                        }
                    }
                }
            }
            catch { /* 损坏数据 → 返回空列表 */ }
            return result;
        }

        /// <summary>序列化为 JSON 数组</summary>
        public static string SerializeGroups(IEnumerable<DeviceGroupInfo> groups)
        {
            return JsonConvert.SerializeObject(groups, Formatting.None);
        }

        public override bool Equals(object obj)
        {
            return obj is DeviceGroupInfo other && other.Path == Path;
        }

        public override int GetHashCode()
        {
            return Path?.GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            return $"[{Id}] {Path}";
        }
    }
}
