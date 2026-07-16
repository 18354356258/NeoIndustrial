using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using S7.Net;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// Siemens S7 驱动 - 支持通过 S7 协议采集西门子 PLC 数据
    /// </summary>
    public class SiemensS7Driver : IDriver
    {
        private Plc _plc;
        private string _ipAddress = "192.168.0.1";
        private short _rack = 0;
        private short _slot = 1;
        private int _port = 102;
        private CpuType _cpuType = CpuType.S71500;
        private DeviceConfig _config;
        private bool _disposed;

        public string DriverType => "SiemensS7";
        public bool IsConnected => _plc?.IsConnected ?? false;

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _ipAddress = config.GetParam("IP", "192.168.0.1");
            _rack = (short)config.GetIntParam("Rack", 0);
            _slot = (short)config.GetIntParam("Slot", 1);
            int s7Port = config.GetIntParam("Port", 102);
            _port = s7Port; // S7 标准端口 102；S7netplus 0.20.0 暂不支持自定义端口，预留供升级使用

            // 读取 CPU 型号
            string cpuTypeStr = config.GetParam("CpuType", "S7-1500");
            switch (cpuTypeStr)
            {
                case "S7-1200": _cpuType = CpuType.S71200; break;
                case "S7-300":  _cpuType = CpuType.S7300;  break;
                case "S7-400":  _cpuType = CpuType.S7400;  break;
                default:        _cpuType = CpuType.S71500; break;
            }

            try
            {
                _plc = new Plc(_cpuType, _ipAddress, _rack, _slot);
                _plc.Open();

                bool connected = _plc.IsConnected;
                NotifyStatus(connected, connected ? "S7 已连接" : "S7 连接失败");
                return Task.FromResult(connected);
            }
            catch (Exception ex)
            {
                NotifyStatus(false, $"连接失败: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public Task DisconnectAsync()
        {
            try
            {
                _plc?.Close();
            }
            catch { }
            NotifyStatus(false, "已断开");
            return Task.CompletedTask;
        }

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null) return;
            int pollInterval = _config.GetIntParam("PollInterval", 1000);
            if (pollInterval < 100) pollInterval = 100;

            Logger.Debug($"S7 采集开始: {_config.Name}, IP={_ipAddress}, Rack={_rack}, Slot={_slot}, 间隔={pollInterval}ms");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    if (!IsConnected)
                    {
                        await ConnectAsync(_config);
                        if (!IsConnected)
                        {
                            await Task.Delay(3000, token);
                            continue;
                        }
                    }

                    var cycleItems = new System.Collections.Generic.List<CycleDataItem>();

                    foreach (var point in _config.DataPoints)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!point.IsActive) continue;

                        object value = null;
                        object rawValue = null;
                        try
                        {
                            rawValue = await ReadAsync(point);
                            value = rawValue;
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug(string.Format("S7 读取失败 [{0}.{1}]: {2}", _config.Name, point.Name, ex.Message));
                        }

                        var data = new CollectedData
                        {
                            DeviceId = _config.Id,
                            DeviceName = _config.Name,
                            VariableName = point.Name,
                            DataType = point.DataType,
                            Value = value?.ToString() ?? "ERR",
                            Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null,
                            Timestamp = DateTime.Now
                        };
                        OnDataReceived?.Invoke(this, new CollectedDataEventArgs(data));

                        cycleItems.Add(new CycleDataItem
                        {
                        VariableId = point.VariableId, 
                            Id = string.Format("{0}|{1}", _config.Name, point.Name),
                            DataType = point.DataType,
                            Value = rawValue ?? 0,
                            Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null
                        });
                    }

                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Driver = "s7net",
                        Device = _config.Name,
                        DeviceId = _config.Id, Values = cycleItems
                    }));

                    await Task.Delay(pollInterval, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.Warn($"S7 采集异常 [{_config.Name}]: {ex.Message}");
                    await Task.Delay(1000, token);
                }
            }

            Logger.Debug($"S7 采集结束: {_config.Name}");
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            try
            {
                // 1. 解析地址：DB3.124 → db=3, byteOffset=124
                var addrMatch = System.Text.RegularExpressions.Regex.Match(point.Address, @"^DB(\d+)\.(\d+)(?:\.(\d+))?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!addrMatch.Success)
                {
                    Logger.Warn(string.Format("S7 地址格式无法解析: [{0}.{1}] addr={2}", _config?.Name, point.Name, point.Address));
                    return Task.FromResult<object>(null);
                }

                int dbNum = int.Parse(addrMatch.Groups[1].Value);
                int byteOffset = int.Parse(addrMatch.Groups[2].Value);
                int bitNum = addrMatch.Groups[3].Success ? int.Parse(addrMatch.Groups[3].Value) : 0;

                string dtype = point.DataType.ToLower();
                VarType varType;
                int varCount = 1;

                switch (dtype)
                {
                    case "bool":
                    case "coil":
                        varType = VarType.Bit;
                        break;
                    case "int16":
                    case "short":
                        varType = VarType.Int;
                        break;
                    case "uint16":
                    case "ushort":
                    case "word":
                        varType = VarType.Word;
                        break;
                    case "int32":
                    case "int":
                        varType = VarType.DInt;
                        break;
                    case "uint32":
                    case "dword":
                        varType = VarType.DWord;
                        break;
                    case "float":
                    case "real":
                        varType = VarType.Real;
                        break;
                    case "double":
                        // S7 无原生 double，读 2 个 DWord 手动拼
                        varType = VarType.DWord;
                        varCount = 2;
                        break;
                    case "string":
                        varType = VarType.String;
                        varCount = point.Length > 0 ? point.Length : 1;
                        break;
                    default:
                        // 未知类型，按 DWord 读取
                        varType = VarType.DWord;
                        break;
                }

                Logger.Debug(string.Format("S7 Read: {0}.{1} → DB{2}.{3} byteOffset={3}, varType={4}",
                    _config.Name, point.Name, dbNum, byteOffset, varType));

                object result = _plc.Read(DataType.DataBlock, dbNum, byteOffset, varType, varCount);

                // 字符串类型：清理非法字符后返回
                if (varType == VarType.String)
                {
                    string str = result?.ToString() ?? "";
                    // 去除 ASCII 控制字符（\0, \r, \n, 等）+ DEL(0x7F)
                    str = System.Text.RegularExpressions.Regex.Replace(str, @"[\u0000-\u001F\u007F]", "");
                    // 去除首尾空格
                    str = str.Trim();

                    Logger.Debug(string.Format("S7 Read String: {0}.{1} = '{2}'",
                        _config.Name, point.Name, str));
                    return Task.FromResult((object)str);
                }

                double doubleVal = 0;
                if (result != null)
                {
                    doubleVal = Convert.ToDouble(result);
                }

                Logger.Debug(string.Format("S7 Read OK: {0}.{1} = {2} (raw type={3})",
                    _config.Name, point.Name, doubleVal, result?.GetType().Name));

                doubleVal = point.ConvertValue(doubleVal);
                Logger.Debug(string.Format("S7 Read FINAL: {0}.{1} = {2} (scale={3}, offset={4})",
                    _config.Name, point.Name, doubleVal, point.ScaleFactor, point.Offset));
                return Task.FromResult<object>(doubleVal);
            }
            catch (Exception ex)
            {
                Logger.Warn(string.Format("S7 读取失败 [{0}.{1}] addr={2} type={3}: {4}",
                    _config?.Name, point.Name, point.Address, point.DataType, ex.Message));
                return Task.FromResult<object>(null);
            }
        }

        /// <summary>
        /// 将 TIA Portal 格式地址转换为 S7.Net 格式
        /// DB3.124 + uint16 → DB3.DBW124
        /// DB5.0   + float  → DB5.DBD0
        /// </summary>
        private string ParseS7Address(DataPoint point)
        {
            string addr = point.Address;
            if (string.IsNullOrWhiteSpace(addr)) return "DB1.DBW0";

            // 已有完整格式 (如 DB1.DBW0, MW100, IW0)，直接返回
            if (addr.Contains(".") && (
                addr.Contains("DBX") || addr.Contains("DBW") || addr.Contains("DBD") || addr.Contains("DBB") ||
                addr.StartsWith("M", StringComparison.OrdinalIgnoreCase) ||
                addr.StartsWith("I", StringComparison.OrdinalIgnoreCase) ||
                addr.StartsWith("Q", StringComparison.OrdinalIgnoreCase)))
            {
                return addr;
            }

            // 纯内存/IO 地址 (如 MW100, IW0, QW4)
            if (System.Text.RegularExpressions.Regex.IsMatch(addr, @"^[MIQ][WDBX]\d+"))
                return addr;

            // DB3.124 格式 → 根据数据类型转换
            var match = System.Text.RegularExpressions.Regex.Match(addr, @"^DB(\d+)\.(\d+)(?:\.(\d+))?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int dbNum = int.Parse(match.Groups[1].Value);
                int byteOffset = int.Parse(match.Groups[2].Value);
                
                string dtype = point.DataType.ToLower();
                switch (dtype)
                {
                    case "bool":
                    case "coil":
                        int bitNum = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                        return string.Format("DB{0}.DBX{1}.{2}", dbNum, byteOffset, bitNum);
                    case "int16":
                    case "uint16":
                    case "word":
                    case "ushort":
                    case "short":
                        return string.Format("DB{0}.DBW{1}", dbNum, byteOffset);
                    case "string":
                        return string.Format("DB{0}.DBB{1}", dbNum, byteOffset);
                    default: // int32, uint32, int64, float, double, dword, real, int
                        return string.Format("DB{0}.DBD{1}", dbNum, byteOffset);
                }
            }

            // 无法识别，原样返回
            return addr;
        }

        private void NotifyStatus(bool connected, string message)
        {
            OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(
                _config?.Id ?? "", _config?.Name ?? "", connected, message));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisconnectAsync().Wait();
        }
    }
}
