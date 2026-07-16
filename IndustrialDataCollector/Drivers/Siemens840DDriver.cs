using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services; using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    public class Siemens840DDriver : IDriver
    {
        private TcpClient _client;
        private string _ipAddress = "127.0.0.1";
        private int _port = 102;
        private int _ncu = 1;
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;

        public string DriverType => "Siemens840D";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _ipAddress = config.GetParam("IP", "127.0.0.1");
            _port = config.GetIntParam("Port", 102);
            _ncu = config.GetIntParam("NCU", 1);
            try { _client = new TcpClient(); await _client.ConnectAsync(_ipAddress, _port); IsConnected = true; NotifyStatus(true, string.Format("Siemens 840D ������ ({0}:{1}, NCU={2})", _ipAddress, _port, _ncu)); Logger.Debug(string.Format("Siemens 840D ���ӳɹ�: {0}:{1}", _ipAddress, _port)); return true; }
            catch (Exception ex) { IsConnected = false; NotifyStatus(false, string.Format("����ʧ��: {0}", ex.Message)); Logger.Warn(string.Format("Siemens 840D ����ʧ�� [{0}]: {1}", _config.Name, ex.Message)); return false; }
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
                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Driver = "siemens840d", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn(string.Format("Siemens 840D �ɼ��쳣 [{0}]: {1}", _config.Name, ex.Message)); await Task.Delay(1000, _cts.Token); }
            }
        }

        public async Task<object> ReadAsync(DataPoint point)
        {
            if (_client == null || !_client.Connected)
                throw new InvalidOperationException("Siemens 840D δ����");

            string addr = point.Address?.Trim() ?? "";

            // NC������ģʽ: ·��ʽ (�� /Channel/State/opMode)
            if (addr.StartsWith("/"))
                throw new NotSupportedException("NC������ģʽ��ʹ��DB��ַ (�� DB120.DBX0.0)");

            // S7 DB��ַ��ʽ: DBxxx.DB...
            if (!TryParseDbAddress(addr, out int dbNumber, out int byteOffset, out int bitOffset))
                throw new ArgumentException($"�޷�����S7 DB��ַ: {addr}, ��ʽ: DBxxx.DByyy.z");

            var stream = _client.GetStream();

            // ȷ�� S7 ��������
            byte transportSize;
            int varLen;
            switch (point.DataType.ToLower())
            {
                case "bool":   transportSize = 0x01; varLen = 1; break;  // BIT
                case "byte":   transportSize = 0x02; varLen = 1; break;  // BYTE
                case "int16":
                case "short":
                case "uint16":
                case "ushort":
                case "word":   transportSize = 0x04; varLen = 1; break;  // WORD
                case "int32":
                case "int":
                case "uint32":
                case "dword":
                case "float":
                case "real":   transportSize = 0x05; varLen = 1; break;  // DWORD
                default:       transportSize = 0x05; varLen = 1; break;
            }

            // 12 byte S7 Read Parameter item
            byte[] item = new byte[12];
            item[0] = 0x12;                 // Variable Specification
            item[1] = 0x0A;                 // Length of remaining
            item[2] = 0x10;                 // Syntax ID = S7ANY
            item[3] = transportSize;        // Transport size
            item[4] = (byte)(varLen >> 8);  // Length (HI)
            item[5] = (byte)(varLen & 0xFF);// Length (LO)
            item[6] = (byte)(dbNumber >> 8);// DB Number (HI)
            item[7] = (byte)(dbNumber & 0xFF);// DB Number (LO)
            item[8] = 0x84;                 // Area = DB
            item[9] = (byte)((byteOffset >> 16) & 0xFF);  // Address byte 2
            item[10] = (byte)((byteOffset >> 8) & 0xFF);   // Address byte 1
            item[11] = (byte)((byteOffset & 0xFF) | ((bitOffset & 0x07) << 4)); // Addr byte 0 + bit

            int paramLen = 2 + item.Length;  // Function(1) + Count(1) + Items
            int dataLen = 0;

            int s7HeaderLen = 10;
            int tpktLen = 4 + 3 + s7HeaderLen + paramLen + dataLen;

            byte[] frame = new byte[tpktLen];
            int pos = 0;

            // TPKT header
            frame[pos++] = 0x03;                                    // Version
            frame[pos++] = 0x00;                                    // Reserved
            frame[pos++] = (byte)(tpktLen >> 8);                    // Length (HI)
            frame[pos++] = (byte)(tpktLen & 0xFF);                  // Length (LO)

            // ISO-COTP
            frame[pos++] = 0x02;  // Length
            frame[pos++] = 0xF0;  // PDU type = DT
            frame[pos++] = 0x80;  // Reserved

            // S7 header
            frame[pos++] = 0x32;   // Protocol ID
            frame[pos++] = 0x01;   // Message Type = Job Request
            frame[pos++] = 0x00; frame[pos++] = 0x00;  // Reserved
            frame[pos++] = 0x00; frame[pos++] = 0x01;  // PDU Reference (sequence)
            frame[pos++] = (byte)(paramLen >> 8);      // Param length (HI)
            frame[pos++] = (byte)(paramLen & 0xFF);    // Param length (LO)
            frame[pos++] = (byte)(dataLen >> 8);       // Data length (HI)
            frame[pos++] = (byte)(dataLen & 0xFF);     // Data length (LO)

            // S7 Read Parameter
            frame[pos++] = 0x04;         // Function = Read Variable
            frame[pos++] = 0x01;         // Item count = 1
            Array.Copy(item, 0, frame, pos, item.Length);

            await stream.WriteAsync(frame, 0, frame.Length);

            // Read response: TPKT(4) + COTP(3) + S7 header(10) + params + data
            byte[] tpktHeader = new byte[4];
            await ReadExactAsync(stream, tpktHeader, 0, 4);
            int respLen = ((tpktHeader[2] << 8) | tpktHeader[3]) - 4;

            byte[] rest = new byte[respLen];
            await ReadExactAsync(stream, rest, 0, respLen);

            // Check S7 response
            int s7Start = 3; // skip ISO-COTP
            if (rest[s7Start + 0] != 0x32) throw new Exception("S7 protocol header mismatch");
            byte msgType = rest[s7Start + 1];
            int s7ParamLen = (rest[s7Start + 6] << 8) | rest[s7Start + 7];
            int s7DataLen = (rest[s7Start + 8] << 8) | rest[s7Start + 9];

            // Error check
            if (msgType == 0x03) // Ack-Data
            {
                int func = rest[s7Start + s7HeaderLen];
                int itemCount = rest[s7Start + s7HeaderLen + 1];
                if (func == 0x04 && itemCount > 0)
                {
                    byte returnCode = rest[s7Start + s7HeaderLen + 2];
                    if (returnCode != 0xFF)
                        throw new Exception($"S7 Read error, return code: 0x{returnCode:X2}");

                    // Success - parse data after parameter section
                    int dataOffset = s7Start + s7HeaderLen + s7ParamLen;
                    int dataAvail = s7DataLen;
                    // Response data for one item: 1 byte return(0xFF) + transportSize + dataLen bytes
                    int itemDataOffset = 1 + 1; // skip return code + transport size
                    int itemDataLen = dataAvail - itemDataOffset;

                    if (itemDataLen > 0)
                    {
                        byte[] valueBytes = new byte[itemDataLen];
                        Array.Copy(rest, dataOffset + itemDataOffset, valueBytes, 0, itemDataLen);
                        // S7 data is big-endian, swap to little-endian
                        if (valueBytes.Length >= 2) Array.Reverse(valueBytes);
                        if (valueBytes.Length == 4)
                        {
                            // swap back correctly for 4-byte: reverse byte pairs
                            byte tmp = valueBytes[0]; valueBytes[0] = valueBytes[1]; valueBytes[1] = tmp;
                            tmp = valueBytes[2]; valueBytes[2] = valueBytes[3]; valueBytes[3] = tmp;
                        }
                        return ParseS7Value(valueBytes, point.DataType);
                    }
                }
            }
            else if (msgType == 0x02 || msgType == 0x05)
            {
                throw new Exception($"S7 communication error, msgType=0x{msgType:X2}");
            }

            return (object)0.0;
        }

        private bool TryParseDbAddress(string addr, out int dbNumber, out int byteOffset, out int bitOffset)
        {
            dbNumber = 0; byteOffset = 0; bitOffset = 0;
            if (string.IsNullOrEmpty(addr)) return false;

            addr = addr.ToUpper().Trim();
            if (!addr.StartsWith("DB")) return false;

            int dotIdx = addr.IndexOf('.');
            if (dotIdx < 0) return false;

            if (!int.TryParse(addr.Substring(2, dotIdx - 2), out dbNumber))
                return false;

            string rest = addr.Substring(dotIdx + 1);
            // Format: DBX0.0, DBW2, DBD4, DBB0
            if (rest.Length < 3) return false;

            string memType = rest.Substring(0, 3); // DBX, DBW, DBD, DBB
            string offsetStr = rest.Substring(3);

            // Check for bit offset (DBX style: DBX0.0)
            int bitDotIdx = offsetStr.IndexOf('.');
            if (bitDotIdx >= 0)
            {
                if (!int.TryParse(offsetStr.Substring(0, bitDotIdx), out byteOffset)) return false;
                if (!int.TryParse(offsetStr.Substring(bitDotIdx + 1), out bitOffset)) return false;
            }
            else
            {
                if (!int.TryParse(offsetStr, out byteOffset)) return false;
                bitOffset = 0;
            }

            return true;
        }

        private object ParseS7Value(byte[] data, string dataType)
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

        private async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = await stream.ReadAsync(buffer, offset + read, count - read);
                if (n == 0) throw new System.IO.IOException("Siemens 840D ���ӹر�");
                read += n;
            }
        }
        private void NotifyStatus(bool connected, string message) { OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(_config?.Id ?? "", _config?.Name ?? "", connected, message)); }
        public void Dispose() { if (_disposed) return; _disposed = true; DisconnectAsync().Wait(); }
    }
}
