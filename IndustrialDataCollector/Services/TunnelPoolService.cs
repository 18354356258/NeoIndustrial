using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Services
{
    public class TunnelPoolService
    {
        private static readonly Lazy<TunnelPoolService> _instance =
            new Lazy<TunnelPoolService>(() => new TunnelPoolService());
        public static TunnelPoolService Instance { get { return _instance.Value; } }

        private readonly string _dataDir;
        private readonly string _tunnelsFile;
        private readonly object _lock = new object();
        private List<NetworkTunnel> _tunnels;

        private TunnelPoolService()
        {
            _dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection");
            Directory.CreateDirectory(_dataDir);
            _tunnelsFile = Path.Combine(_dataDir, "tunnels.json");
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_tunnelsFile))
                {
                    var json = File.ReadAllText(_tunnelsFile, System.Text.Encoding.UTF8);
                    _tunnels = JsonConvert.DeserializeObject<List<NetworkTunnel>>(json) ?? new List<NetworkTunnel>();
                }
                else
                {
                    _tunnels = new List<NetworkTunnel>();
                }
            }
            catch (Exception ex)
            {
                Logger.Info("加载通道池失败: " + ex.Message);
                _tunnels = new List<NetworkTunnel>();
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_tunnels, Formatting.Indented);
                File.WriteAllText(_tunnelsFile, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error("保存通道池失败: " + ex.Message);
            }
        }

        public List<NetworkTunnel> GetAll()
        {
            lock (_lock)
            {
                return new List<NetworkTunnel>(_tunnels);
            }
        }

        public List<NetworkTunnel> GetByType(TunnelType type)
        {
            lock (_lock)
            {
                return _tunnels.Where(t => t.Type == type).ToList();
            }
        }

        public NetworkTunnel GetById(string id)
        {
            lock (_lock)
            {
                foreach (var t in _tunnels)
                {
                    if (t.Id == id) return t.Clone();
                }
                return null;
            }
        }

        public void SaveTunnel(NetworkTunnel tunnel)
        {
            lock (_lock)
            {
                var existing = -1;
                for (int i = 0; i < _tunnels.Count; i++)
                {
                    if (_tunnels[i].Id == tunnel.Id) { existing = i; break; }
                }
                if (existing >= 0)
                    _tunnels[existing] = tunnel.Clone();
                else
                    _tunnels.Add(tunnel.Clone());
                Save();
            }
        }

        public void DeleteTunnel(string id)
        {
            lock (_lock)
            {
                _tunnels.RemoveAll(t => t.Id == id);
                Save();
            }
        }

        /// <summary>
        /// 根据通道ID和原始IP解析映射后的IP
        /// </summary>
        public string ResolveMappedIp(string tunnelId, string originalIp)
        {
            if (string.IsNullOrEmpty(tunnelId) || string.IsNullOrEmpty(originalIp))
                return originalIp;

            var tunnel = GetById(tunnelId);
            if (tunnel == null) return originalIp;

            var mapped = tunnel.ResolveMappedIp(originalIp);
            return mapped ?? originalIp;
        }

        /// <summary>
        /// 添加IP映射规则
        /// </summary>
        public void AddIpMapping(string tunnelId, string originalIp, int originalPort,
            string mappedIp, int mappedPort, string description)
        {
            lock (_lock)
            {
                var tunnel = _tunnels.FirstOrDefault(t => t.Id == tunnelId);
                if (tunnel == null) return;

                // 去重：已有相同映射则更新
                var existing = tunnel.IpMappings.FirstOrDefault(m =>
                    m.OriginalIp == originalIp && m.OriginalPort == originalPort);
                if (existing != null)
                {
                    existing.MappedIp = mappedIp;
                    existing.MappedPort = mappedPort;
                    existing.Description = description;
                }
                else
                {
                    tunnel.IpMappings.Add(new IpMappingEntry
                    {
                        OriginalIp = originalIp,
                        OriginalPort = originalPort,
                        MappedIp = mappedIp,
                        MappedPort = mappedPort,
                        Description = description
                    });
                }
                Save();
            }
        }

        /// <summary>
        /// 批量导入映射规则 (NAT设备自动发现)
        /// </summary>
        public void ImportMappings(string tunnelId, List<IpMappingEntry> mappings)
        {
            lock (_lock)
            {
                var tunnel = _tunnels.FirstOrDefault(t => t.Id == tunnelId);
                if (tunnel == null) return;

                foreach (var m in mappings)
                {
                    var existing = tunnel.IpMappings.FirstOrDefault(x =>
                        x.OriginalIp == m.OriginalIp && x.OriginalPort == m.OriginalPort);
                    if (existing != null)
                    {
                        existing.MappedIp = m.MappedIp;
                        existing.MappedPort = m.MappedPort;
                        existing.Description = m.Description;
                    }
                    else
                    {
                        tunnel.IpMappings.Add(m);
                    }
                }
                Save();
            }
        }
    }
}
