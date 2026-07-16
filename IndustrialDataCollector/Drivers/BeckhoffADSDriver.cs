using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services; using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    public class BeckhoffADSDriver : IDriver
    {
        private TcpClient _client;
        private string _ipAddress = "127.0.0.1";
        private int _port = 48898;
        private string _amsNetId = "127.0.0.1.1.1";
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;

        public string DriverType => "BeckhoffADS";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _ipAddress = config.GetParam("IP", "127.0.0.1");
            _port = config.GetIntParam("Port", 48898);
            _amsNetId = config.GetParam("AmsNetId", "127.0.0.1.1.1");
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_ipAddress, _port);
                IsConnected = true;
                NotifyStatus(true, string.Format("Beckhoff ADS ������ ({0}:{1}, AMS={2})", _ipAddress, _port, _amsNetId));
                Logger.Debug(string.Format("Beckhoff ADS ���ӳɹ�: {0}:{1}", _ipAddress, _port));
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, string.Format("����ʧ��: {0}", ex.Message));
                Logger.Warn(string.Format("Beckhoff ADS ����ʧ�� [{0}]: {1}", _config.Name, ex.Message));
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            try { _client?.Close(); } catch { }
            IsConnected = false;
            NotifyStatus(false, "�ѶϿ�");
            return Task.CompletedTask;
        }

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null) return;
            int pollInterval = _config.GetIntParam("PollInterval", 1000);
            if (pollInterval < 100) pollInterval = 100;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            Logger.Debug(string.Format("Beckhoff ADS �ɼ���ʼ: {0}", _config.Name));

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    if (!IsConnected) { await ConnectAsync(_config); if (!IsConnected) { await Task.Delay(3000, _cts.Token); continue; } }
                    var cycleItems = new List<CycleDataItem>();
                    foreach (var point in _config.DataPoints)
                    {
                        _cts.Token.ThrowIfCancellationRequested();
                        if (!point.IsActive) continue;
                        object value = null;
                        try { value = await ReadAsync(point); } catch { }
                        double v = value is double dv ? dv : 0;
                        var data = new CollectedData { DeviceId = _config.Id, DeviceName = _config.Name, VariableName = point.Name, DataType = point.DataType, Value = v.ToString("F6").TrimEnd('0').TrimEnd('.'), Unit = point.Unit, Tag = point.OutputTag ? point.Tag : null, TagCn = point.OutputTagCn ? point.TagCn : null, Timestamp = DateTime.Now };
                        OnDataReceived?.Invoke(this, new CollectedDataEventArgs(data));
                        cycleItems.Add(new CycleDataItem {
                        VariableId = point.VariableId,  Id = string.Format("{0}|{1}", _config.Name, point.Name), DataType = point.DataType, Value = v, Unit = point.Unit, Tag = point.OutputTag ? point.Tag : null, TagCn = point.OutputTagCn ? point.TagCn : null });
                    }
                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Driver = "beckhoff_ads", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn(string.Format("Beckhoff ADS �ɼ��쳣 [{0}]: {1}", _config.Name, ex.Message)); await Task.Delay(1000, _cts.Token); }
            }
        }

        public async Task<object> ReadAsync(DataPoint point)
        {
            if (_client == null || !_client.Connected)
                throw new InvalidOperationException("Beckhoff ADS δ����");

            var stream = _client.GetStream();

            // �����AMS NetIdΪ6�ֽ�
            byte[] amsNetIdBytes = ParseAmsNetId(_amsNetId);
            int amsPortTarget = _config.GetIntParam("AmsPort", 801);
            int amsPortSource = _config.GetIntParam("AmsPortSource", 802);

            // ������������ӳ�� IndexGroup
            uint indexGroup;
            int readLength;
            switch (point.DataType.ToLower())
            {
                case "bool":   indexGroup = 0x4022; readLength = 1; break;
                case "byte":   indexGroup = 0x4023; readLength = 1; break;
                case "int16":
                case "short":  indexGroup = 0x4023; readLength = 2; break;
                case "uint16":
                case "ushort":
                case "word":   indexGroup = 0x4023; readLength = 2; break;
                case "int32":
                case "int":
                case "dword":  indexGroup = 0x4025; readLength = 4; break;
                case "uint32": indexGroup = 0x4025; readLength = 4; break;
                case "float":
                case "real":   indexGroup = 0x4027; readLength = 4; break;
                default:       indexGroup = 0x4025; readLength = 4; break;
            }

            if (!uint.TryParse(point.Address, out uint indexOffset))
                throw new ArgumentException($"�޷�������ַ: {point.Address}");

            int adsDataLen = 12; // Read����: IndexGroup(4) + IndexOffset(4) + Length(4)
            int amsTotalLen = 32 + adsDataLen; // ADSͷ + ��������

            byte[] frame = new byte[6 + amsTotalLen];
            int pos = 0;

            // AMS/TCP Header (6 bytes)
            frame[pos++] = 0; frame[pos++] = 0;        // Reserved
            BitConverter.GetBytes((uint)amsTotalLen).CopyTo(frame, pos); pos += 4;

            // ADS Header (32 bytes)
            Array.Copy(amsNetIdBytes, 0, frame, pos, 6); pos += 6;   // AmsNetId Target
            BitConverter.GetBytes((ushort)amsPortTarget).CopyTo(frame, pos); pos += 2;
            Array.Copy(amsNetIdBytes, 0, frame, pos, 6); pos += 6;   // AmsNetId Source (use same for local)
            BitConverter.GetBytes((ushort)amsPortSource).CopyTo(frame, pos); pos += 2;
            BitConverter.GetBytes((ushort)0x0002).CopyTo(frame, pos); pos += 2;  // CommandId = Read
            BitConverter.GetBytes((ushort)0x0004).CopyTo(frame, pos); pos += 2;  // StateFlags = ADS Command
            BitConverter.GetBytes((uint)adsDataLen).CopyTo(frame, pos); pos += 4;  // Data Length
            BitConverter.GetBytes((ushort)0).CopyTo(frame, pos); pos += 2;        // ErrorCode
            BitConverter.GetBytes((ushort)1).CopyTo(frame, pos); pos += 2;        // InvokeId

            // ADS Read Command (12 bytes)
            BitConverter.GetBytes(indexGroup).CopyTo(frame, pos); pos += 4;
            BitConverter.GetBytes(indexOffset).CopyTo(frame, pos); pos += 4;
            BitConverter.GetBytes((uint)readLength).CopyTo(frame, pos); pos += 4;

            await stream.WriteAsync(frame, 0, frame.Length);

            // ��ȡ��Ӧ: AMS/TCP header(6) + ADS header(32)
            byte[] respHeader = new byte[6 + 32];
            await ReadExactAsync(stream, respHeader, 0, respHeader.Length);

            // �Ӧ����: ADSͷ�� offset 20 ��������Ӧ���ݳ���(4 bytes, LE)
            int respDataLen = BitConverter.ToInt32(respHeader, 6 + 20);
            int respError = BitConverter.ToInt16(respHeader, 6 + 24);

            if (respError != 0)
                throw new Exception($"ADS Read���� (0x{respError:X4})");

            if (respDataLen > 0 && respDataLen < 65536)
            {
                byte[] respData = new byte[respDataLen];
                await ReadExactAsync(stream, respData, 0, respData.Length);
                return ParseAdsValue(respData, point.DataType);
            }

            return (object)0.0;
        }

        private async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = await stream.ReadAsync(buffer, offset + read, count - read);
                if (n == 0) throw new System.IO.IOException("Beckhoff ADS ���ӹر�");
                read += n;
            }
        }

        private byte[] ParseAmsNetId(string netId)
        {
            var parts = netId.Split('.');
            if (parts.Length != 6)
                throw new ArgumentException($"�޷�AMS NetId��ʽ: {netId}, ӦΪ x.x.x.x.x.x");
            byte[] bytes = new byte[6];
            for (int i = 0; i < 6; i++)
                bytes[i] = byte.Parse(parts[i]);
            return bytes;
        }

        private object ParseAdsValue(byte[] data, string dataType)
        {
            switch (dataType.ToLower())
            {
                case "bool":   return (object)(data[0] != 0);
                case "byte":   return (object)(double)data[0];
                case "int16":
                case "short":  return (object)(double)BitConverter.ToInt16(data, 0);
                case "uint16":
                case "ushort":
                case "word":   return (object)(double)BitConverter.ToUInt16(data, 0);
                case "int32":
                case "int":
                case "dword":  return (object)(double)BitConverter.ToInt32(data, 0);
                case "uint32": return (object)(double)BitConverter.ToUInt32(data, 0);
                case "float":
                case "real":   return (object)(double)BitConverter.ToSingle(data, 0);
                default:       return (object)(double)BitConverter.ToInt32(data, 0);
            }
        }

        private void NotifyStatus(bool connected, string message) { OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(_config?.Id ?? "", _config?.Name ?? "", connected, message)); }
        public void Dispose() { if (_disposed) return; _disposed = true; DisconnectAsync().Wait(); }
    }
}
