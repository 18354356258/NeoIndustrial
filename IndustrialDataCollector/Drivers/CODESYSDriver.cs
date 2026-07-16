using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services; using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    public class CODESYSDriver : IDriver
    {
        private TcpClient _client;
        private string _ipAddress = "127.0.0.1";
        private int _port = 502;
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;

        public string DriverType => "CODESYS";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _ipAddress = config.GetParam("IP", "127.0.0.1");
            _port = config.GetIntParam("Port", 502);
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_ipAddress, _port);
                IsConnected = true;
                NotifyStatus(true, string.Format("CODESYS ������ ({0}:{1})", _ipAddress, _port));
                Logger.Debug(string.Format("CODESYS ���ӳɹ�: {0}:{1}", _ipAddress, _port));
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, string.Format("����ʧ��: {0}", ex.Message));
                Logger.Warn(string.Format("CODESYS ����ʧ�� [{0}]: {1}", _config.Name, ex.Message));
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
            Logger.Debug(string.Format("CODESYS �ɼ���ʼ: {0}, IP={1}:{2}", _config.Name, _ipAddress, _port));

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    if (!IsConnected)
                    {
                        await ConnectAsync(_config);
                        if (!IsConnected) { await Task.Delay(3000, _cts.Token); continue; }
                    }
                    var cycleItems = new List<CycleDataItem>();
                    foreach (var point in _config.DataPoints)
                    {
                        _cts.Token.ThrowIfCancellationRequested();
                        if (!point.IsActive) continue;
                        object value = null;
                        try { value = await ReadAsync(point); } catch { }
                        double v = value is double dv ? dv : 0;
                        var data = new CollectedData
                        {
                            DeviceId = _config.Id, DeviceName = _config.Name,
                            VariableName = point.Name, DataType = point.DataType,
                            Value = v.ToString("F6").TrimEnd('0').TrimEnd('.'), Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null, TagCn = point.OutputTagCn ? point.TagCn : null,
                            Timestamp = DateTime.Now
                        };
                        OnDataReceived?.Invoke(this, new CollectedDataEventArgs(data));
                        cycleItems.Add(new CycleDataItem {
                        VariableId = point.VariableId,  Id = string.Format("{0}|{1}", _config.Name, point.Name), DataType = point.DataType, Value = v, Unit = point.Unit, Tag = point.OutputTag ? point.Tag : null, TagCn = point.OutputTagCn ? point.TagCn : null });
                    }
                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Driver = "codesys", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    IsConnected = false;
                    Logger.Warn(string.Format("CODESYS �ɼ��쳣 [{0}]: {1}", _config.Name, ex.Message));
                    await Task.Delay(1000, _cts.Token);
                }
            }
            Logger.Debug(string.Format("CODESYS �ɼ�����: {0}", _config.Name));
        }

        public async Task<object> ReadAsync(DataPoint point)
        {
            if (_client == null || !_client.Connected)
                throw new InvalidOperationException("CODESYS δ����");

            var stream = _client.GetStream();

            // ȷ���Ĵ�������
            int regCount;
            switch (point.DataType.ToLower())
            {
                case "int32": case "int":
                case "uint32": case "dword":
                case "float": case "real":
                    regCount = 2;
                    break;
                case "int64": case "uint64":
                case "double":
                    regCount = 4;
                    break;
                default:
                    regCount = 1;
                    break;
            }

            // У��ע���ַ
            if (!ushort.TryParse(point.Address, out ushort startAddr))
                throw new ArgumentException($"�޷�������ַ: {point.Address}");

            byte stationId = (byte)_config.GetIntParam("Station", 1);

            // Modbus TCP ֡: TxnID(2) + ProtoID(2) + Len(2) + UnitID(1) + FC(1) + Addr(2) + Count(2)
            // Total = 12 bytes
            byte[] frame = new byte[12];
            frame[0] = 0; frame[1] = 1;                        // Transaction ID = 1
            frame[2] = 0; frame[3] = 0;                        // Protocol ID = 0
            frame[4] = 0; frame[5] = 6;                         // Length = 6 (UnitID+FC+Addr+Count)
            frame[6] = stationId;                               // Unit ID
            frame[7] = 0x03;                                    // Function Code = Read Holding Registers
            frame[8] = (byte)(startAddr >> 8);                  // Start Address (HI)
            frame[9] = (byte)(startAddr & 0xFF);                // Start Address (LO)
            frame[10] = (byte)(regCount >> 8);                  // Register Count (HI)
            frame[11] = (byte)(regCount & 0xFF);                // Register Count (LO)

            await stream.WriteAsync(frame, 0, frame.Length);

            // Read response: MBAP header (7 bytes) + FC (1 byte) = 8 bytes minimum
            byte[] respHeader = new byte[9];
            await ReadExactAsync(stream, respHeader, 0, 9);

            // Validate function code (byte offset 7 = respHeader[7])
            if (respHeader[7] != 0x03)
            {
                // Check for exception response (FC = 0x83)
                if (respHeader[7] == 0x83)
                    throw new Exception($"Modbus exception code: 0x{respHeader[8]:X2}");
                throw new Exception($"Modbus unexpected function code: 0x{respHeader[7]:X2}");
            }

            int dataLen = respHeader[8];  // Byte count
            if (dataLen <= 0 || dataLen > 256)
                throw new Exception($"Modbus data length error: {dataLen}");

            byte[] respData = new byte[dataLen];
            await ReadExactAsync(stream, respData, 0, dataLen);

            // Build register array from big-endian Modbus response data
            ushort[] registers = new ushort[regCount];
            for (int i = 0; i < regCount; i++)
                registers[i] = (ushort)((respData[i * 2] << 8) | respData[i * 2 + 1]);

            // Use ModbusHelper for byte order conversion
            byte[] valueBytes = ModbusHelper.RegistersToBytes(registers, point.ByteOrder);

            switch (point.DataType.ToLower())
            {
                case "bool":
                    return (object)((respData[0] & 0x01) != 0);
                case "byte":
                    return (object)(double)respData[0];
                case "int16": case "short":
                    return (object)(double)BitConverter.ToInt16(valueBytes, 0);
                case "uint16": case "ushort": case "word":
                    return (object)(double)BitConverter.ToUInt16(valueBytes, 0);
                case "int32": case "int": case "dword":
                    return (object)(double)BitConverter.ToInt32(valueBytes, 0);
                case "uint32":
                    return (object)(double)BitConverter.ToUInt32(valueBytes, 0);
                case "float": case "real":
                    return (object)(double)BitConverter.ToSingle(valueBytes, 0);
                case "double":
                    return (object)BitConverter.ToDouble(valueBytes, 0);
                default:
                    return (object)(double)registers[0];
            }
        }

        private async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = await stream.ReadAsync(buffer, offset + read, count - read);
                if (n == 0) throw new System.IO.IOException("CODESYS Modbus ���ӹر�");
                read += n;
            }
        }

        private void NotifyStatus(bool connected, string message)
        {
            OnStatusChanged?.Invoke(this, new DriverStatusEventArgs(_config?.Id ?? "", _config?.Name ?? "", connected, message));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisconnectAsync().Wait();
        }
    }
}
