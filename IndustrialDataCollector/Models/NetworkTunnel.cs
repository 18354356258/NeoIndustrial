using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    public enum TunnelType { VPN, NAT }

    public class IpMappingEntry
    {
        public string OriginalIp { get; set; }
        public int OriginalPort { get; set; }
        public string MappedIp { get; set; }
        public int MappedPort { get; set; }
        public string Description { get; set; }
    }

    public class NetworkTunnel
    {
        public string Id { get; set; }              // GUID
        public string Name { get; set; }            // 通道名称 e.g. "华东工厂VPN"
        public TunnelType Type { get; set; }        // VPN or NAT
        public bool IsOnline { get; set; }          // 在线状态
        public DateTime CreatedAt { get; set; }

        // VPN 专有
        public string VpnType { get; set; }         // "OpenVPN" / "L2TP" / "IPsec"
        public string VpnTapMode { get; set; }      // "TUN"(L3) / "TAP"(L2, for industrial MAC-layer)
        public string LocalVirtualIp { get; set; }  // 本端虚拟IP
        public string RemoteNetwork { get; set; }   // 对端网络 e.g. "192.168.1.0/24"
        public string VpnConfigFile { get; set; }   // OpenVPN config path

        // NAT 专有
        public string NatDeviceIp { get; set; }     // NAT设备管理IP
        public int NatDevicePort { get; set; }      // NAT设备管理端口
        public string NatDeviceModel { get; set; }  // "华为AR" / "Moxa" / "蒲公英" / "通用"
        public string NatApiKey { get; set; }       // API Key or password
        public string NatUsername { get; set; }     // 管理用户名
        public string NatPassword { get; set; }     // 管理密码 (stored encrypted)

        // IP映射表 (VPN和NAT通用)
        public List<IpMappingEntry> IpMappings { get; set; }

        // 备注
        public string Notes { get; set; }

        public NetworkTunnel()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 12);
            CreatedAt = DateTime.Now;
            IpMappings = new List<IpMappingEntry>();
            NatDevicePort = 80;
            VpnTapMode = "TUN";
        }

        public NetworkTunnel Clone()
        {
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<NetworkTunnel>(json);
        }

        /// <summary>
        /// 根据原始IP在映射表中查找映射IP
        /// </summary>
        public string ResolveMappedIp(string originalIp)
        {
            if (IpMappings == null) return null;
            foreach (var m in IpMappings)
            {
                if (string.Equals(m.OriginalIp, originalIp, StringComparison.OrdinalIgnoreCase))
                    return m.MappedIp;
            }
            return null;
        }

        /// <summary>
        /// 根据原始IP:Port查找映射后的IP:Port
        /// </summary>
        public IpMappingEntry ResolveMapping(string originalIp, int originalPort)
        {
            if (IpMappings == null) return null;
            foreach (var m in IpMappings)
            {
                if (string.Equals(m.OriginalIp, originalIp, StringComparison.OrdinalIgnoreCase)
                    && m.OriginalPort == originalPort)
                    return m;
            }
            return null;
        }
    }
}
