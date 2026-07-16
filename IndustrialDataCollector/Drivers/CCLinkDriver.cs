using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services; using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    public class CCLinkDriver : IDriver
    {
        private TcpClient _client;
        private string _ipAddress = "127.0.0.1";
        private int _port = 6000;
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _readLock = new object();

        public string DriverType => "CCLink";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _ipAddress = config.GetParam("IP", "127.0.0.1");
            _port = config.GetIntParam("Port", 6000);
            try { _client = new TcpClient(); await _client.ConnectAsync(_ipAddress, _port); IsConnected = true; NotifyStatus(true, string.Format("CC-Link ������ ({0}:{1})", _ipAddress, _port)); Logger.Debug(string.Format("CC-Link ���ӳɹ�: {0}:{1}", _ipAddress, _port)); return true; }
            catch (Exception ex) { IsConnected = false; NotifyStatus(false, string.Format("����ʧ��: {0}", ex.Message)); Logger.Warn(string.Format("CC-Link ����ʧ�� [{0}]: {1}", _config.Name, ex.Message)); return false; }
        }

        public Task DisconnectAsync() { try { _client?.Close(); } catch { } IsConnected = false; NotifyStatus(false, "�ѶϿ�"); return Task.CompletedTask; }

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null) return;
            int pollInterval = _config.GetIntParam("PollInterval", 1000); if (pollInterval < 100) pollInterval = 100;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    if (!IsConnected) { await ConnectAsync(_config); if (!IsConnected) { await Task.Delay(3000, _cts.Token); continue; } }
                    var cycleItems = new List<CycleDataItem>();
                    foreach (var point in _config.DataPoints) { _cts.Token.ThrowIfCancellationRequested(); if (!point.IsActive) continue; object value = null; try { value = await ReadAsync(point); } catch { } double v = value is double dv ? dv : 0; var data = new CollectedData { DeviceId = _config.Id, DeviceName = _config.Name, VariableName = point.Name, DataType = point.DataType, Value = v.ToString("F6").TrimEnd('0').TrimEnd('.'), Unit = point.Unit, Tag = point.OutputTag ? point.Tag : null, TagCn = point.OutputTagCn ? point.TagCn : null, Timestamp = DateTime.Now }; OnDataReceived?.Invoke(this, new CollectedDataEventArgs(data)); cycleItems.Add(new CycleDataItem {
                        VariableId = point.VariableId,  Id = string.Format("{0}|{1}", _config.Name, point.Name), DataType = point.DataType, Value = v, Unit = point.Unit, Tag = point.OutputTag ? point.Tag : null, TagCn = point.OutputTagCn ? point.TagCn : null }); }
                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Driver = "cclink", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn(string.Format("CC-Link �ɼ��쳣 [{0}]: {1}", _config.Name, ex.Message)); await Task.Delay(1000, _cts.Token); }
            }
        }

        public async Task<object> ReadAsync(DataPoint point)
        {
            string address = point.Address;
            if (string.IsNullOrEmpty(address))
                throw new InvalidOperationException("CC-Link: address is empty");

            ParseCCLinkAddress(address, out byte station, out byte memType, out ushort memAddr, out byte length);

            // CC-Link remote device station read frame via TCP gateway
            // memType: 0=RX, 1=RY, 2=RWr, 3=RWw
            byte[] request = new byte[]
            {
                0xCC, station, 0x01, memType, (byte)((memAddr >> 8) & 0xFF), (byte)(memAddr & 0xFF), length
            };
            byte[] response = await SendReceiveAsync(request, 1000);

            if (response == null || response.Length < 4)
                throw new InvalidOperationException($"CC-Link: no response for station {station}");
            if (response[0] != 0xCC)
                throw new InvalidOperationException($"CC-Link: invalid response header 0x{response[0]:X2}");
            if (response[2] != 0x00)
                throw new InvalidOperationException($"CC-Link: station {station} error code 0x{response[2]:X2}");

            return ParseResponseValue(response, 3, point.DataType);
        }

        private async Task<byte[]> SendReceiveAsync(byte[] request, int timeoutMs)
        {
            lock (_readLock)
            {
                if (_client == null || !_client.Connected)
                    throw new InvalidOperationException("CC-Link: TCP gateway not connected");

                var stream = _client.GetStream();
                stream.ReadTimeout = timeoutMs;
                stream.WriteTimeout = timeoutMs;
                stream.Write(request, 0, request.Length);
                stream.Flush();

                byte[] buf = new byte[256];
                int len = stream.Read(buf, 0, buf.Length);
                if (len <= 0) return null;
                byte[] result = new byte[len];
                Buffer.BlockCopy(buf, 0, result, 0, len);
                return result;
            }
        }

        private static void ParseCCLinkAddress(string address, out byte station, out byte memType, out ushort memAddr, out byte length)
        {
            station = 1; memType = 2; memAddr = 0; length = 4;
            // Address format: "station,memType,memAddr,length" or "station|RWr|0|4"
            string[] parts = address.Split(new[] { ',', '.', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1)
            {
                if (!byte.TryParse(parts[0], out station))
                {
                    // First part might be a memory type string like "RWr"
                    memType = ParseCCLinkMemType(parts[0]);
                    station = 1;
                }
            }
            if (parts.Length >= 2)
            {
                if (station == 1 && memType != 2)
                {
                    // parts[0] was memType, parts[1] is address
                    if (!ushort.TryParse(parts[1], out memAddr))
                        memType = ParseCCLinkMemType(parts[1]);
                }
                else
                {
                    memType = ParseCCLinkMemType(parts[1]);
                }
            }
            if (parts.Length >= 3) ushort.TryParse(parts[2], out memAddr);
            if (parts.Length >= 4) byte.TryParse(parts[3], out length);
            if (station < 0 || station > 64) station = 1;
            if (length == 0) length = 4;
        }

        private static byte ParseCCLinkMemType(string type)
        {
            if (string.IsNullOrEmpty(type)) return 2;
            string t = type.Trim().ToUpper();
            if (t == "RX" || t == "RXI") return 0;
            if (t == "RY" || t == "RYO") return 1;
            if (t == "RWR" || t == "RWRR") return 2;
            if (t == "RWW" || t == "RWWW") return 3;
            if (t == "D" || t == "WR") return 4;
            return 2;
        }

        private static object ParseResponseValue(byte[] response, int offset, string dataType)
        {
            byte len = response.Length > offset ? response[offset] : (byte)4;
            int dataOffset = offset + 1;
            if (dataOffset + len > response.Length) return 0.0;

            switch (dataType?.ToLower())
            {
                case "float": case "real":
                    if (len >= 4)
                    {
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(response, dataOffset, 4);
                        return (double)BitConverter.ToSingle(response, dataOffset);
                    }
                    break;
                case "int16": case "short":
                    if (len >= 2) return (double)BitConverter.ToInt16(response, dataOffset);
                    break;
                case "uint16": case "ushort":
                    if (len >= 2) return (double)BitConverter.ToUInt16(response, dataOffset);
                    break;
                case "int32": case "dint":
                    if (len >= 4) return (double)BitConverter.ToInt32(response, dataOffset);
                    break;
                case "bool":
                    return len >= 1 ? (double)(response[dataOffset] != 0 ? 1 : 0) : 0.0;
                default:
                    if (len >= 4)
                    {
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(response, dataOffset, 4);
                        return (double)BitConverter.ToSingle(response, dataOffset);
                    }
                    if (len >= 2) return (double)BitConverter.ToInt16(response, dataOffset);
                    break;
            }
            return 0.0;
        }
        private void NotifyStatus(bool connected, string message) { OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(_config?.Id ?? "", _config?.Name ?? "", connected, message)); }
        public void Dispose() { if (_disposed) return; _disposed = true; DisconnectAsync().Wait(); }
    }
}
