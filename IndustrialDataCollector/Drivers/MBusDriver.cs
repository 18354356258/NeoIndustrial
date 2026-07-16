using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services; using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// M-Bus 仪表总线协议驱动 (串口 + M-Bus电平转换器, 2400bps)
    /// 协议流程: Wakeup(0x55) → SND_NKE(初始化) → SND_UD(请求数据) → RSP_UD(响应)
    /// 从站地址: 0-250, 广播地址: 254/255
    /// </summary>
    public class MBusDriver : IDriver
    {
        private SerialPort _serialPort;
        private string _portName = "COM1";
        private int _baudRate = 2400;
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _lock = new object();

        public string DriverType => "MBus";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _portName = config.GetParam("PortName", "COM1");
            _baudRate = config.GetIntParam("BaudRate", 2400);

            try
            {
                lock (_lock)
                {
                    _serialPort = new SerialPort(_portName, _baudRate, Parity.Even, 8, StopBits.One)
                    {
                        ReadTimeout = 5000,
                        WriteTimeout = 3000,
                        NewLine = "\r\n"
                    };
                    _serialPort.Open();
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();

                    // M-Bus 唤醒: 发送至少2个字节的0x55 (~33ms @ 2400bps)
                    byte[] wakeup = new byte[] { 0x55, 0x55, 0x55, 0x55 };
                    _serialPort.Write(wakeup, 0, wakeup.Length);
                    System.Threading.Thread.Sleep(50);
                }
                IsConnected = true;
                NotifyStatus(true, $"M-Bus 已连接 ({_portName}, {_baudRate}bps)");
                Logger.Debug($"M-Bus 连接成功: {_portName}, {_baudRate}bps");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, $"连接失败: {ex.Message}");
                Logger.Warn($"M-Bus 连接失败 [{_config.Name}]: {ex.Message}");
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            lock (_lock)
            {
                try { _serialPort?.Close(); } catch { }
                try { _serialPort?.Dispose(); } catch { }
                _serialPort = null;
            }
            IsConnected = false;
            NotifyStatus(false, "已断开");
            return Task.CompletedTask;
        }

        public async Task StartCollectAsync(CancellationToken token)
        {
            if (_config == null) return;
            int pollInterval = _config.GetIntParam("PollInterval", 1000);
            if (pollInterval < 100) pollInterval = 100;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);

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
                        var data = new CollectedData
                        {
                            DeviceId = _config.Id, DeviceName = _config.Name, VariableName = point.Name,
                            DataType = point.DataType,
                            Value = v.ToString("F6").TrimEnd('0').TrimEnd('.'),
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
                            DataType = point.DataType, Value = v, Unit = point.Unit,
                            Tag = point.OutputTag ? point.Tag : null,
                            TagCn = point.OutputTagCn ? point.TagCn : null
                        });
                    }
                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Driver = "mbus", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems
                    }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn($"M-Bus 采集异常 [{_config.Name}]: {ex.Message}"); await Task.Delay(1000, _cts.Token); }
            }
        }

        /// <summary>
        /// 通过串口发送M-Bus命令读取仪表数据
        /// </summary>
        public async Task<object> ReadAsync(DataPoint point)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("串口未打开");

            // 从站地址: point.Address 如 "1", "10" 等
            byte slaveAddr = 1;
            string addrStr = point.Address ?? "1";
            if (!byte.TryParse(addrStr, out slaveAddr))
                slaveAddr = 1;
            if (slaveAddr > 250) slaveAddr = 1;

            byte[] response = await Task.Run(() =>
            {
                lock (_lock)
                {
                    // Step 1: SND_NKE (初始化从站) - 短帧
                    byte[] sndNke = BuildShortFrame(0x40, slaveAddr);
                    _serialPort.DiscardInBuffer();
                    _serialPort.Write(sndNke, 0, sndNke.Length);
                    Logger.Debug($"M-Bus SND_NKE -> addr:{slaveAddr}: {BitConverter.ToString(sndNke)}");

                    // 等待E5确认(0xE5 = 单字节确认)
                    try { WaitForAck(); }
                    catch (TimeoutException)
                    {
                        // NKE无响应也可能正常(某些从站不需要NKE)，继续尝试SND_UD
                        Logger.Debug($"M-Bus SND_NKE无响应(addr:{slaveAddr}), 继续请求");
                    }

                    System.Threading.Thread.Sleep(50);

                    // Step 2: SND_UD (请求用户数据) - 长帧/控制帧
                    byte[] sndUd = BuildSndUdFrame(slaveAddr);
                    _serialPort.Write(sndUd, 0, sndUd.Length);
                    Logger.Debug($"M-Bus SND_UD -> addr:{slaveAddr}: {BitConverter.ToString(sndUd)}");
                }
                return ReadResponse(slaveAddr);
            });

            return ParseResponse(response);
        }

        #region M-Bus Protocol Implementation

        /// <summary>
        /// 构建M-Bus短帧 (固定长度5字节)
        /// 格式: Start(0x10) + C-Field + A-Field + CS + Stop(0x16)
        /// </summary>
        private byte[] BuildShortFrame(byte controlField, byte address)
        {
            byte[] frame = new byte[5];
            frame[0] = 0x10;  // Start
            frame[1] = controlField; // C-Field: 0x40=SND_NKE
            frame[2] = address; // A-Field
            frame[3] = (byte)(controlField + address); // CS (checksum)
            frame[4] = 0x16;  // Stop
            return frame;
        }

        /// <summary>
        /// 构建M-Bus SND_UD请求帧
        /// SND_UD: C-Field=0x53/0x73, FCB=1
        /// 长帧: Start(0x68)+L+L+0x68+C+A+CI+Data+CS+Stop(0x16)
        /// 控制帧: Start(0x68)+0x03+0x03+0x68+C+A+CI+CS+Stop(0x16)
        /// </summary>
        private byte[] BuildSndUdFrame(byte address)
        {
            // 控制帧(无数据): 请求从站发送数据
            byte[] frame = new byte[9];
            frame[0] = 0x68;  // Start
            frame[1] = 0x03;  // L-field (length=3)
            frame[2] = 0x03;  // L-field repeat
            frame[3] = 0x68;  // Start repeat
            frame[4] = 0x73;  // C-Field: SND_UD (FCB=1, FCV=1)
            frame[5] = address; // A-Field
            frame[6] = 0x7A;  // CI-Field: 应用层重置/数据请求

            // CS: C+A+CI = 0x73+addr+0x7A
            byte cs = (byte)(0x73 + address + 0x7A);
            frame[7] = cs;
            frame[8] = 0x16;  // Stop
            return frame;
        }

        /// <summary>
        /// 等待E5单字节确认(0xE5)
        /// </summary>
        private void WaitForAck()
        {
            int b = _serialPort.ReadByte();
            if (b == 0xE5) return; // ACK
            if (b == 0x00) throw new Exception("M-Bus从站忙");
            // 其他值忽略，继续
        }

        /// <summary>
        /// 读取M-Bus响应数据 (RSP_UD)
        /// </summary>
        private byte[] ReadResponse(byte slaveAddr)
        {
            // 读取长帧响应
            var buffer = new List<byte>();
            int b;

            // 等待第一个0x68 (长帧起始) 或 0xE5 (确认)
            while ((b = _serialPort.ReadByte()) != -1)
            {
                if (b == 0xE5)
                {
                    // 单字节确认(某些从站先回E5再发数据)
                    continue;
                }
                if (b == 0x68)
                {
                    buffer.Add((byte)b); // 0x68
                    // 读取长度
                    int l1 = _serialPort.ReadByte(); if (l1 < 0) break;
                    int l2 = _serialPort.ReadByte(); if (l2 < 0) break;
                    int frameStart = _serialPort.ReadByte(); if (frameStart < 0) break;

                    if (frameStart != 0x68)
                        throw new Exception($"M-Bus帧格式错误: 期望0x68, 收到0x{frameStart:X2}");

                    int length = l1;

                    buffer.Add((byte)l1);
                    buffer.Add((byte)l2);
                    buffer.Add((byte)frameStart);

                    // 读取剩余帧数据
                    for (int i = 0; i < length + 1; i++) // +1 for stop byte
                    {
                        b = _serialPort.ReadByte();
                        if (b < 0) break;
                        buffer.Add((byte)b);
                    }

                    Logger.Debug($"M-Bus RSP_UD <- : {BitConverter.ToString(buffer.ToArray())}");
                    return buffer.ToArray();
                }
            }

            throw new TimeoutException($"等待M-Bus从站{slaveAddr}响应超时");
        }

        /// <summary>
        /// 解析M-Bus响应数据
        /// RSP_UD帧结构: 68 L L 68 C A CI Data... CS 16
        /// </summary>
        private double ParseResponse(byte[] response)
        {
            if (response == null || response.Length < 9)
                throw new Exception("M-Bus响应帧太短");

            // 检查长帧结构: 68 L L 68 ...
            if (response[0] != 0x68 || response[3] != 0x68)
                throw new Exception($"M-Bus响应帧头错误: {BitConverter.ToString(response)}");

            int length = response[1]; // L-field

            // 检查C-Field是否为RSP_UD (0x08或0x18或0x28)
            byte cField = response[4];
            if ((cField & 0x0F) != 0x08)
            {
                // 错误响应: 检查是否为0x0A(应用层忙)等
                Logger.Debug($"M-Bus C-Field: 0x{cField:X2} (非标准RSP_UD)");
            }

            // 提取数据部分 (跳过: 68 L L 68 C A CI [Data] CS 16)
            int dataStart = 7; // after CI-field
            int dataLen = length - 3; // length includes C,A,CI

            if (dataLen <= 0 || dataStart + dataLen > response.Length - 2)
                throw new Exception("M-Bus响应数据长度异常");

            // 解析DIF/VIF编码的数据
            // 简单的数据提取: 跳过DIF(1B)+VIF(1B+), 获取值
            try
            {
                int pos = dataStart;
                while (pos < dataStart + dataLen)
                {
                    byte dif = response[pos++];
                    int dataFieldLen = (dif & 0x0F); // 低4位=数据长度(0=无,1=1B,2=2B,4=4B,6=6B...)
                    int funcField = (dif >> 4) & 0x03; // 功能字段

                    // 读取VIF (1~N字节)
                    byte vif = response[pos++];
                    bool vifExtend = (vif == 0xFD || vif == 0xFB || vif == 0xFF);
                    if (vifExtend) pos++; // 跳过扩展VIF

                    // 提取数据值
                    if (dataFieldLen > 0 && pos + dataFieldLen <= dataStart + dataLen)
                    {
                        long rawVal = 0;
                        for (int i = 0; i < dataFieldLen; i++)
                            rawVal |= (long)response[pos++] << (i * 8);

                        // 处理符号和BCD编码
                        double result = rawVal;

                        // BCD编码检测
                        if ((dataFieldLen == 4 || dataFieldLen == 6) && funcField == 1)
                        {
                            // BCD to decimal
                            decimal bcdVal = 0;
                            decimal multiplier = 1;
                            long temp = rawVal;
                            while (temp > 0)
                            {
                                bcdVal += (temp & 0xF) * multiplier;
                                temp >>= 4;
                                multiplier *= 10;
                            }
                            result = (double)bcdVal;
                        }

                        Logger.Debug($"M-Bus Parsed: DIF=0x{dif:X2} VIF=0x{vif:X2} raw=0x{rawVal:X} val={result}");
                        return result;
                    }
                    return 0;
                }
            }
            catch { }

            throw new Exception("无法解析M-Bus响应数据");
        }

        #endregion

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
