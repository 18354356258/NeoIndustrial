using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using NModbus;
using IndustrialDataCollection.Services; using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// Modbus TCP 驱动 - 支持通过 Modbus TCP 协议采集 PLC 数据
    /// </summary>
    public class ModbusTcpDriver : IDriver
    {
        private TcpClient _tcpClient;
        private IModbusMaster _master;
        private string _ipAddress = "127.0.0.1";
        private int _port = 502;
        private byte _stationId = 1;
        private DeviceConfig _config;
        private bool _disposed;

        public string DriverType => "ModbusTcp";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _ipAddress = config.GetParam("IP", "127.0.0.1");
            _port = config.GetIntParam("Port", 502);
            _stationId = (byte)config.GetIntParam("Station", 1);

            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_ipAddress, _port);

                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_tcpClient);
                _master.Transport.ReadTimeout = 3000;
                _master.Transport.WriteTimeout = 3000;

                IsConnected = true;
                NotifyStatus(true, string.Format("Modbus TCP 已连接 ({0}:{1})", _ipAddress, _port));
                Logger.Debug(string.Format("Modbus TCP 连接成功: {0}:{1}", _ipAddress, _port));
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, string.Format("连接失败: {0}", ex.Message));
                Logger.Warn(string.Format("Modbus TCP 连接失败 [{0}]: {1}", _config.Name, ex.Message));
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            try
            {
                _master?.Dispose();
                _tcpClient?.Close();
            }
            catch { }
            IsConnected = false;
            NotifyStatus(false, "已断开");
            return Task.CompletedTask;
        }

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null) return;
            int pollInterval = _config.GetIntParam("PollInterval", 1000);
            if (pollInterval < 100) pollInterval = 100;

            Logger.Debug(string.Format("Modbus TCP 采集开始: {0}, IP={1}:{2}, 间隔={3}ms",
                _config.Name, _ipAddress, _port, pollInterval));

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
                            Logger.Debug(string.Format("Modbus TCP 读取失败 [{0}.{1}]: {2}", _config.Name, point.Name, ex.Message));
                        }

                        // 边缘计算处理
                        double processedValue = value is double dv ? dv : 0;
                        if (value != null && value.ToString() != "ERR")
                        {
                            try { processedValue = DataProcessor.Instance.ApplyEdgeProcessing(_config.Id + "_" + point.Name, Convert.ToDouble(value), point); }
                            catch (Exception ex) { Logger.Debug("Edge processing failed for " + point.Name + ": " + ex.Message); }
                        }

                        var data = new CollectedData
                        {
                            DeviceId = _config.Id,
                            DeviceName = _config.Name,
                            VariableName = point.Name,
                            DataType = point.DataType,
                            Value = processedValue.ToString("F6").TrimEnd('0').TrimEnd('.'),
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
                        Driver = "modbus",
                        Device = _config.Name,
                        DeviceId = _config.Id, Values = cycleItems
                    }));

                    await Task.Delay(pollInterval, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    IsConnected = false;
                    Logger.Warn(string.Format("Modbus TCP 采集异常 [{0}]: {1}", _config.Name, ex.Message));
                    await Task.Delay(1000, token);
                }
            }

            Logger.Debug(string.Format("Modbus TCP 采集结束: {0}", _config.Name));
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            object result = 0;

            switch (point.DataType.ToLower())
            {
                case "bool":
                case "coil":
                    {
                        bool[] coils = _master.ReadCoils(_stationId, ushort.Parse(point.Address), 1);
                        result = coils[0];
                        break;
                    }
                case "byte":
                    {
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), 1);
                        result = (byte)(reg[0] & 0xFF);
                        break;
                    }
                case "int16":
                case "short":
                    {
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), 1);
                        result = (short)reg[0];
                        break;
                    }
                case "uint16":
                case "ushort":
                case "word":
                    {
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), 1);
                        result = reg[0];
                        break;
                    }
                case "int32":
                case "int":
                    {
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), 2);
                        byte[] bytes = ModbusHelper.RegistersToBytes(reg, point.ByteOrder);
                        result = BitConverter.ToInt32(bytes, 0);
                        break;
                    }
                case "uint32":
                case "dword":
                    {
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), 2);
                        byte[] bytes = ModbusHelper.RegistersToBytes(reg, point.ByteOrder);
                        result = BitConverter.ToUInt32(bytes, 0);
                        break;
                    }
                case "int64":
                    {
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), 4);
                        byte[] bytes = ModbusHelper.RegistersToBytes(reg, point.ByteOrder);
                        result = BitConverter.ToInt64(bytes, 0);
                        break;
                    }
                case "uint64":
                    {
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), 4);
                        byte[] bytes = ModbusHelper.RegistersToBytes(reg, point.ByteOrder);
                        result = BitConverter.ToUInt64(bytes, 0);
                        break;
                    }
                case "float":
                case "real":
                    {
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), 2);
                        byte[] bytes = ModbusHelper.RegistersToBytes(reg, point.ByteOrder);
                        result = BitConverter.ToSingle(bytes, 0);
                        break;
                    }
                case "string":
                    {
                        int strLen = point.Length > 0 ? point.Length : 1;
                        int regCount = (strLen + 1) / 2;
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), (ushort)regCount);
                        byte[] strBytes = ModbusHelper.RegistersToBytes(reg, point.ByteOrder);
                        string str = System.Text.Encoding.ASCII.GetString(strBytes).TrimEnd('\0', ' ');
                        return Task.FromResult<object>(str);
                    }
                case "double":
                    {
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), 4);
                        byte[] bytes = ModbusHelper.RegistersToBytes(reg, point.ByteOrder);
                        result = BitConverter.ToDouble(bytes, 0);
                        break;
                    }
                default:
                    {
                        ushort[] reg = _master.ReadHoldingRegisters(_stationId, ushort.Parse(point.Address), 1);
                        result = reg[0];break;
                    }
            }

            double doubleVal = Convert.ToDouble(result);
            result = point.ConvertValue(doubleVal);
            return Task.FromResult(result);
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
