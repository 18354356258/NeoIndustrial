using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// Fanuc FOCAS 驱动（社区版）——标准端口 8193。
    ///
    /// ## 当前状态（务必知悉）
    /// 本驱动目前是**协议骨架**：ConnectAsync 会真实建立 TCP 连接，
    /// 但 ReadAsync 返回的是**模拟值**，还没有对接 FANUC 的读取接口。
    /// 原因：FANUC 的 FOCAS 走 8193 私有二进制协议，未公开；官方只提供 Fwlib 库封装，
    /// 而该库受厂商授权约束，不能随本仓库（AGPL）分发。
    ///
    /// ## 要让社区版真读，需要两步
    ///   1. 自行向 FANUC 或其授权代理取得 FOCAS2 Library（Fwlib32.dll / Fwlib64.dll，含同目录
    ///      fwlib*32/64.dll 机型库），放进程序目录（不能提交到本仓库）；
    ///   2. 用 DllImport 绑定 cnc_allclibhndl3 / cnc_rdmacro 等导出后替换 ReadAsync 的模拟分支。
    ///
    /// ## 点位地址写法（与企业版保持一致，便于以后迁移）
    ///   · 宏变量：直接填宏号，如 100 或 #100。机床侧需先把该量写进对应宏变量
    ///     （例如 #100=#4111 取主轴转速、#101=#4109 取进给、#105-107=#5001-5003 取机床坐标）。
    ///   · 动态点位（企业版已支持，社区版待实现）：
    ///       @SPINDLE 主轴转速 · @FEED 实际进给 · @X @Y @Z 绝对坐标
    ///       @ALARM 报警号 · @RUN 运行状态（文字：复位/停止/保持/运行/急停）
    ///     对应 FOCAS2 的 cnc_acts / cnc_actf / cnc_absolute / cnc_alarm2 / cnc_statinfo，
    ///     好处是不需要在机床上写宏程序。
    ///
    /// 企业版（WEB，闭源商业授权）已实现上述两条通道，社区版此处留作对接点。
    /// </summary>
    public class FanucFocasDriver : IDriver
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private string _ipAddress = "127.0.0.1";
        private int _port = 8193;
        private string _cncType = "0i";
        private DeviceConfig _config;
        private bool _disposed;
        private int _simCounter;

        // FOCAS 库句柄（rev465 社区版）：库加载成功且有句柄时才走真实读取，否则保持模拟。
        private ushort _flibHndl;
        private bool _hasHandle;
        private bool _libTried;

        public string DriverType { get { return "FanucFocas"; } }
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _ipAddress = config.GetParam("IP", "127.0.0.1");
            _port = config.GetIntParam("Port", 8193);
            _cncType = config.GetParam("CNCType", "0i");

            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_ipAddress, _port);
                _stream = _tcpClient.GetStream();
                _stream.ReadTimeout = 3000;
                _stream.WriteTimeout = 3000;

                IsConnected = true;

                // rev465：TCP 通了之后尝试装 FOCAS 库并分配句柄。
                // 拿不到库不影响「已连接」，只是读值仍为模拟——并在日志里写清原因，不静默。
                TryInitFocas();
                NotifyStatus(true, string.Format("Fanuc Focas 已连接 ({0}:{1}, CNC={2})", _ipAddress, _port, _cncType));
                Logger.Debug(string.Format("Fanuc Focas 连接成功: {0}:{1}, CNC={2}", _ipAddress, _port, _cncType));
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, string.Format("连接失败: {0}", ex.Message));
                Logger.Warn(string.Format("Fanuc Focas 连接失败 [{0}]: {1}", _config.Name, ex.Message));
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            try
            {
                _stream?.Close();
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

            Logger.Debug(string.Format("Fanuc Focas 采集开始: {0}, IP={1}:{2}, 间隔={3}ms",
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
                            Logger.Debug(string.Format("Fanuc Focas 读取失败 [{0}.{1}]: {2}", _config.Name, point.Name, ex.Message));
                        }

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
                        Driver = "fanucfocas",
                        Device = _config.Name,
                        DeviceId = _config.Id, Values = cycleItems
                    }));

                    await Task.Delay(pollInterval, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    IsConnected = false;
                    Logger.Warn(string.Format("Fanuc Focas 采集异常 [{0}]: {1}", _config.Name, ex.Message));
                    await Task.Delay(1000, token);
                }
            }

            Logger.Debug(string.Format("Fanuc Focas 采集结束: {0}", _config.Name));
        }


        /// <summary>rev465：装载 FOCAS 库并分配句柄；失败只记日志（读值退回模拟），不影响 TCP 连接状态。</summary>
        private void TryInitFocas()
        {
            if (_libTried) return;
            _libTried = true;

            try
            {
                var libPath = _config == null ? "" : _config.GetParam("FocasLibPath", "");
                if (!FocasNative.TryLoad(libPath))
                {
                    Logger.Warn("Fanuc FOCAS[" + (_config == null ? "" : _config.Name) + "] 库不可用，读值为模拟：" + FocasNative.LastError);
                    return;
                }

                ushort h;
                short ret = FocasNative.Alloc(_ipAddress, (ushort)_port, 10, out h);
                if (ret != 0)
                {
                    Logger.Warn("Fanuc FOCAS[" + (_config == null ? "" : _config.Name) + "] cnc_allclibhndl3 失败：" + FocasNative.DescribeError(ret));
                    return;
                }

                _flibHndl = h;
                _hasHandle = true;
                Logger.Info("Fanuc FOCAS[" + (_config == null ? "" : _config.Name) + "] 已装载 " + FocasNative.LoadedPath + "，句柄 " + h);
            }
            catch (Exception ex)
            {
                Logger.Warn("Fanuc FOCAS 初始化异常：" + ex.Message);
            }
        }

        /// <summary>rev465：真实读取。地址 @ 开头走动态点位，纯数字走宏变量。</summary>
        private object ReadReal(DataPoint point)
        {
            var addr = (point.Address ?? "").Trim();
            if (addr.StartsWith("@")) return ReadDynamic(point, addr.ToUpperInvariant());
            return ReadMacro(point, addr);
        }

        /// <summary>宏变量读取：地址填宏号（100 或 #100）。</summary>
        private object ReadMacro(DataPoint point, string addr)
        {
            int macroNo = ParseMacroNo(addr);
            FocasNative.OdbMacro m;
            short ret = FocasNative.RdmAcro(_flibHndl, (short)macroNo, 1, out m);
            if (ret != 0)
                throw new Exception("cnc_rdmacro #" + macroNo + " 返回 " + FocasNative.DescribeError(ret));

            // dec_val 表示小数位数：mcr_val 是放大后的整数
            if (m.dec_val > 0)
            {
                double v = m.mcr_val / Math.Pow(10, Math.Min(9, (int)m.dec_val));
                if (string.Equals(point.DataType, "string", StringComparison.OrdinalIgnoreCase))
                    return v.ToString("F" + Math.Min(9, (int)m.dec_val));
                return v;
            }

            if (string.Equals(point.DataType, "string", StringComparison.OrdinalIgnoreCase))
                return m.mcr_val.ToString();
            return m.mcr_val;
        }

        /// <summary>动态点位：@SPINDLE @FEED @X @Y @Z @ALARM @RUN（不需要机床写宏程序）。</summary>
        private object ReadDynamic(DataPoint point, string addr)
        {
            switch (addr)
            {
                case "@SPINDLE":
                {
                    if (FocasNative.Acts == null) throw new Exception("当前 FOCAS 库不含 cnc_acts 导出");
                    FocasNative.OdbAct a;
                    short ret = FocasNative.Acts(_flibHndl, out a);
                    if (ret != 0) throw new Exception("cnc_acts 返回 " + FocasNative.DescribeError(ret));
                    return a.data;
                }
                case "@FEED":
                {
                    if (FocasNative.Actf == null) throw new Exception("当前 FOCAS 库不含 cnc_actf 导出");
                    FocasNative.OdbAct a;
                    short ret = FocasNative.Actf(_flibHndl, out a);
                    if (ret != 0) throw new Exception("cnc_actf 返回 " + FocasNative.DescribeError(ret));
                    return a.data;
                }
                case "@X":
                case "@Y":
                case "@Z":
                {
                    if (FocasNative.Absolute == null) throw new Exception("当前 FOCAS 库不含 cnc_absolute 导出");
                    short axis = (short)(addr == "@X" ? 1 : (addr == "@Y" ? 2 : 3));
                    FocasNative.OdbAxis ax;
                    short ret = FocasNative.Absolute(_flibHndl, axis, 1, out ax);
                    if (ret != 0) throw new Exception("cnc_absolute(轴 " + axis + ") 返回 " + FocasNative.DescribeError(ret));
                    // FOCAS 坐标单位 0.001mm
                    double mm = (ax.data != null && ax.data.Length > 0) ? ax.data[0] / 1000.0 : 0.0;
                    return mm;
                }
                case "@ALARM":
                {
                    if (FocasNative.Alarm2 == null) throw new Exception("当前 FOCAS 库不含 cnc_alarm2 导出");
                    int no;
                    short ret = FocasNative.Alarm2(_flibHndl, out no);
                    if (ret != 0) throw new Exception("cnc_alarm2 返回 " + FocasNative.DescribeError(ret));
                    return no;
                }
                case "@RUN":
                {
                    if (FocasNative.Statinfo == null) throw new Exception("当前 FOCAS 库不含 cnc_statinfo 导出");
                    FocasNative.OdbSt st;
                    short ret = FocasNative.Statinfo(_flibHndl, out st);
                    if (ret != 0) throw new Exception("cnc_statinfo 返回 " + FocasNative.DescribeError(ret));
                    if (st.emergency == 1) return "急停";
                    if (st.run == 3 || st.run == 4) return "运行";
                    if (st.run == 2) return "保持";
                    if (st.run == 1) return "停止";
                    return "复位";
                }
                default:
                    throw new Exception("未知的动态点位地址 " + addr +
                        "（支持 @SPINDLE / @FEED / @X / @Y / @Z / @ALARM / @RUN，或填宏号如 100）");
            }
        }

        private static int ParseMacroNo(string address)
        {
            var m = System.Text.RegularExpressions.Regex.Match(address ?? "", "(\\d+)");
            if (!m.Success) throw new Exception("宏变量号无法解析: " + address + "（地址填宏号，例如 100 或 #100）");
            return int.Parse(m.Groups[1].Value);
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            // rev465：库与句柄就绪时走真实读取（宏变量 / 动态点位），不再返回模拟值。
            if (_hasHandle && FocasNative.IsLoaded)
            {
                return Task.FromResult<object>(ReadReal(point));
            }

            object result = 0;
            _simCounter++;

            // ⚠ 社区版尚未对接 FOCAS 库：以下为模拟值，不是机床真实数据。
            // TODO: 绑定 Fwlib 的 cnc_rdmacro / cnc_acts / cnc_actf / cnc_absolute 后替换本分支
            //（地址约定见类注释：宏号 100 / @SPINDLE / @FEED / @X @Y @Z / @ALARM / @RUN）
            // 当前返回模拟数据用于驱动框架验证
            switch (point.DataType.ToLower())
            {
                case "bool":
                    result = (_simCounter % 3 == 0);
                    break;
                case "byte":
                    result = (byte)((_simCounter + 25) % 256);
                    break;
                case "int16":
                case "short":
                    result = (short)((_simCounter * 7) % 2000 - 1000);
                    break;
                case "uint16":
                case "ushort":
                case "word":
                    result = (ushort)((_simCounter * 17) % 65535);
                    break;
                case "int32":
                case "int":
                    result = _simCounter * 10;
                    break;
                case "uint32":
                case "dword":
                    result = (uint)(_simCounter * 100);
                    break;
                case "float":
                case "real":
                    result = 150.0f + (_simCounter % 150) * 0.5f;
                    break;
                case "double":
                    result = 150.0 + (_simCounter % 150) * 0.5;
                    break;
                default:
                    result = (_simCounter * 7) % 1000;
                    break;
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
