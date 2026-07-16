using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services; using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    public class HARTIPDriver : IDriver
    {
        private TcpClient _client;
        private string _ipAddress = "127.0.0.1";
        private int _port = 5094;
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _readLock = new object();
        private ushort _hartMsgId = 1;

        public string DriverType => "HARTIP";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _ipAddress = config.GetParam("IP", "127.0.0.1");
            _port = config.GetIntParam("Port", 5094);
            try { _client = new TcpClient(); await _client.ConnectAsync(_ipAddress, _port); IsConnected = true; NotifyStatus(true, string.Format("HART-IP ������ ({0}:{1})", _ipAddress, _port)); Logger.Debug(string.Format("HART-IP ���ӳɹ�: {0}:{1}", _ipAddress, _port)); return true; }
            catch (Exception ex) { IsConnected = false; NotifyStatus(false, string.Format("����ʧ��: {0}", ex.Message)); Logger.Warn(string.Format("HART-IP ����ʧ�� [{0}]: {1}", _config.Name, ex.Message)); return false; }
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
                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Driver = "hart_ip", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn(string.Format("HART-IP �ɼ��쳣 [{0}]: {1}", _config.Name, ex.Message)); await Task.Delay(1000, _cts.Token); }
            }
        }

        public async Task<object> ReadAsync(DataPoint point)
        {
            string address = point.Address;
            if (string.IsNullOrEmpty(address))
                throw new InvalidOperationException("HART-IP: address is empty");

            ParseHARTAddress(address, out byte deviceAddr, out byte variableSlot);

            // Build HART-IP header + HART Command 3 (Read Dynamic Variables)
            byte[] frame = BuildHARTIPCommand3(deviceAddr);
            byte[] response = await SendReceiveAsync(frame, 2000);

            if (response == null || response.Length < 8)
                throw new InvalidOperationException($"HART-IP: no response from device {deviceAddr}");

            // Parse HART-IP header
            // Byte 0: Version, Byte 1: MessageType (0x01=Response), Bytes 2-3: MessageID LE
            // Byte 4: Status, Bytes 5-6: SeqNum, Byte 7: Reserved
            if (response[1] != 0x01)
                throw new InvalidOperationException($"HART-IP: unexpected message type 0x{response[1]:X2}");
            byte hartIpStatus = response[4];
            if (hartIpStatus != 0x00)
                throw new InvalidOperationException($"HART-IP: status error 0x{hartIpStatus:X2}");

            // Extract HART response frame (after 8-byte HART-IP header)
            byte[] hartRsp = new byte[response.Length - 8];
            Buffer.BlockCopy(response, 8, hartRsp, 0, hartRsp.Length);

            return ParseHARTCommand3Response(hartRsp, deviceAddr, variableSlot);
        }

        private byte[] BuildHARTIPCommand3(byte deviceAddr)
        {
            // HART Command 3 request frame
            byte[] hartCmd = new byte[]
            {
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF,  // Preamble (5 bytes)
                0x82,                            // Delimiter: master to slave, short frame
                deviceAddr,                      // Address (polling address)
                0x03,                            // Command 3: Read Dynamic Variables
                0x00,                            // Data length = 0
                0x00                             // Checksum placeholder
            };
            hartCmd[hartCmd.Length - 1] = CalcHARTChecksum(hartCmd, 5);

            // HART-IP header (8 bytes)
            ushort msgId = _hartMsgId++;
            if (_hartMsgId == 0) _hartMsgId = 1;
            byte[] header = new byte[]
            {
                0x01,                           // Version
                0x00,                           // Message Type: Request
                (byte)(msgId & 0xFF),           // Message ID LO
                (byte)((msgId >> 8) & 0xFF),    // Message ID HI
                0x00,                           // Status
                0x00, 0x00,                     // Sequence Number
                0x00                            // Reserved
            };

            byte[] frame = new byte[header.Length + hartCmd.Length];
            Buffer.BlockCopy(header, 0, frame, 0, header.Length);
            Buffer.BlockCopy(hartCmd, 0, frame, header.Length, hartCmd.Length);
            return frame;
        }

        private static byte CalcHARTChecksum(byte[] frame, int startOffset)
        {
            byte xor = 0;
            for (int i = startOffset; i < frame.Length - 1; i++)
                xor ^= frame[i];
            return xor;
        }

        private static double ParseHARTCommand3Response(byte[] hartRsp, byte expectedAddr, byte variableSlot)
        {
            // HART response frame structure:
            // Preamble(5+): 0xFF * N
            // Delimiter(1):  0x86 (slave to master, short)
            // Address(1):    polling address
            // Command(1):    0x03
            // DataLen(1):    23 (response code 2 + status 1 + 4*(unit 1 + float 4) = 23)
            // Data(23):      [RC0,RC1,Status, PVunit,PV(4B), SVunit,SV(4B), TVunit,TV(4B), QVunit,QV(4B)]
            // Checksum(1):   XOR of delimiter through data

            int preambleLen = 0;
            for (int i = 0; i < hartRsp.Length && hartRsp[i] == 0xFF; i++)
                preambleLen++;

            int dataStart = preambleLen + 5; // preamble + delimiter + addr + cmd + dataLen
            if (dataStart >= hartRsp.Length)
                return 0.0;

            // Verify address and command
            byte addr = hartRsp[preambleLen + 2];
            byte cmd = hartRsp[preambleLen + 3];
            if (cmd != 0x03)
                throw new InvalidOperationException($"HART: unexpected response command 0x{cmd:X2} for addr {addr}");

            byte dataLen = hartRsp[preambleLen + 4];
            if (dataLen < 2)
                return 0.0;

            // Parse response code (2 bytes)
            byte rc0 = hartRsp[dataStart];
            byte rc1 = hartRsp[dataStart + 1];
            if (rc0 != 0x00 || rc1 != 0x00)
            {
                // Error response - data contains error info
                int errOffset = dataStart + 2;
                if (errOffset < hartRsp.Length)
                    Logger.Warn($"HART command 3 error: RC=({rc0},{rc1}), extended={hartRsp[errOffset]:X2}");
                return 0.0;
            }

            // Data: RC(2) + Status(1) + 4 * [Unit(1) + Float(4)] = 23 bytes
            int varOffset = dataStart + 3; // skip RC + Status

            // Select variable by slot (0=PV, 1=SV, 2=TV, 3=QV)
            int slot = variableSlot >= 0 && variableSlot < 4 ? variableSlot : 0;
            int valueOffset = varOffset + slot * 5 + 1; // skip preceding variables + unit byte

            if (valueOffset + 4 > hartRsp.Length)
                return 0.0;

            byte[] floatBytes = new byte[4];
            Buffer.BlockCopy(hartRsp, valueOffset, floatBytes, 0, 4);

            // HART uses big-endian IEEE 754
            if (BitConverter.IsLittleEndian)
                Array.Reverse(floatBytes);

            float value = BitConverter.ToSingle(floatBytes, 0);

            // Check for NaN / Inf
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.0;

            return (double)value;
        }

        private static void ParseHARTAddress(string address, out byte deviceAddr, out byte variableSlot)
        {
            deviceAddr = 0; variableSlot = 0;
            // Address format: "deviceAddr.slot" e.g. "0.0" = device 0, PV
            string[] parts = address.Split(new[] { ',', '.', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1) byte.TryParse(parts[0], out deviceAddr);
            if (parts.Length >= 2) byte.TryParse(parts[1], out variableSlot);
            if (variableSlot > 3) variableSlot = 0;
            if (deviceAddr > 63) deviceAddr = 0;
        }

        private async Task<byte[]> SendReceiveAsync(byte[] request, int timeoutMs)
        {
            lock (_readLock)
            {
                if (_client == null || !_client.Connected)
                    throw new InvalidOperationException("HART-IP: TCP gateway not connected");

                var stream = _client.GetStream();
                stream.ReadTimeout = timeoutMs;
                stream.WriteTimeout = timeoutMs;
                stream.Write(request, 0, request.Length);
                stream.Flush();

                byte[] buf = new byte[512];
                int len = stream.Read(buf, 0, buf.Length);
                if (len <= 0) return null;
                byte[] result = new byte[len];
                Buffer.BlockCopy(buf, 0, result, 0, len);
                return result;
            }
        }
        private void NotifyStatus(bool connected, string message) { OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(_config?.Id ?? "", _config?.Name ?? "", connected, message)); }
        public void Dispose() { if (_disposed) return; _disposed = true; DisconnectAsync().Wait(); }
    }
}
