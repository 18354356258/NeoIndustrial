using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// OPC DA ���� - COM-based OPC Data Access (simulated)
    /// </summary>
    public class OPCDADriver : IDriver
    {
        private string _progId = "OPC.SimaticNET.1";
        private DeviceConfig _config;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly object _readLock = new object();

        public string DriverType => "OPCDA";
        public bool IsConnected { get; private set; }

        public event EventHandler<CollectedDataEventArgs> OnDataReceived;
        public event EventHandler<CycleDataEventArgs> OnCycleCompleted;
        public event EventHandler<DriverStatusEventArgs> OnStatusChanged;

        public async Task<bool> ConnectAsync(DeviceConfig config)
        {
            _config = config;
            _progId = config.GetParam("ProgID", "OPC.SimaticNET.1");
            try
            {
                // OPC DA requires COM interop - simulated connection
                await Task.Delay(50);
                IsConnected = true;
                NotifyStatus(true, string.Format("OPC DA ������ (ProgID={0})", _progId));
                Logger.Debug(string.Format("OPC DA ���ӳɹ�: ProgID={0}", _progId));
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                NotifyStatus(false, string.Format("����ʧ��: {0}", ex.Message));
                Logger.Warn(string.Format("OPC DA ����ʧ�� [{0}]: {1}", _config.Name, ex.Message));
                return false;
            }
        }

        public Task DisconnectAsync()
        {
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
            Logger.Debug(string.Format("OPC DA �ɼ���ʼ: {0}", _config.Name));

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
                    OnCycleCompleted?.Invoke(this, new CycleDataEventArgs(new CycleDataBatch { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Driver = "opcda", Device = _config.Name, DeviceId = _config.Id, Values = cycleItems }));
                    await Task.Delay(pollInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { IsConnected = false; Logger.Warn(string.Format("OPC DA �ɼ��쳣 [{0}]: {1}", _config.Name, ex.Message)); await Task.Delay(1000, _cts.Token); }
            }
        }

        public Task<object> ReadAsync(DataPoint point)
        {
            return Task.Run(() => ReadSync(point));
        }

        private object ReadSync(DataPoint point)
        {
            string address = point.Address;
            if (string.IsNullOrEmpty(address))
                throw new InvalidOperationException("OPC DA: address is empty");

            object opcServer = null;
            object opcGroups = null;
            object opcGroup = null;
            object opcItems = null;
            object opcItem = null;

            lock (_readLock)
            {
                try
                {
                    Type opcServerType = Type.GetTypeFromProgID(_progId);
                    if (opcServerType == null)
                        throw new COMException($"OPC DA: Cannot find ProgID '{_progId}'. Ensure OPC server is registered.");

                    opcServer = Activator.CreateInstance(opcServerType);
                    Type serverType = opcServer.GetType();

                    string nodeName = _config.GetParam("OpcNode", "");
                    object[] connectArgs = string.IsNullOrEmpty(nodeName)
                        ? new object[] { _progId }
                        : new object[] { _progId, nodeName };
                    serverType.InvokeMember("Connect", BindingFlags.InvokeMethod, null, opcServer, connectArgs);

                    opcGroups = serverType.InvokeMember("OPCGroups", BindingFlags.GetProperty, null, opcServer, null);
                    Type groupsType = opcGroups.GetType();

                    opcGroup = groupsType.InvokeMember("Add", BindingFlags.InvokeMethod, null, opcGroups,
                        new object[] { "AutoClawGrp" });
                    Type groupType = opcGroup.GetType();
                    groupType.InvokeMember("UpdateRate", BindingFlags.SetProperty, null, opcGroup, new object[] { 100 });
                    groupType.InvokeMember("IsActive", BindingFlags.SetProperty, null, opcGroup, new object[] { true });
                    groupType.InvokeMember("IsSubscribed", BindingFlags.SetProperty, null, opcGroup, new object[] { false });

                    opcItems = groupType.InvokeMember("OPCItems", BindingFlags.GetProperty, null, opcGroup, null);
                    Type itemsType = opcItems.GetType();

                    opcItem = itemsType.InvokeMember("AddItem", BindingFlags.InvokeMethod, null, opcItems,
                        new object[] { address, 1 });
                    Type itemType = opcItem.GetType();

                    object[] readArgs = new object[] { 1, null, null, null };
                    ParameterModifier[] mods = new ParameterModifier[]
                    {
                        new ParameterModifier(4) { [1] = true, [2] = true, [3] = true }
                    };
                    itemType.InvokeMember("Read", BindingFlags.InvokeMethod, null, opcItem, readArgs, mods, null, null);

                    object rawValue = readArgs[1];
                    object quality = readArgs[2];
                    int qualityBits = quality != null ? Convert.ToInt32(quality) : 0;
                    if (qualityBits != 192)
                        Logger.Debug($"OPC DA quality warning [{_config.Name}] {address}: 0x{qualityBits:X2}");

                    return ConvertToDouble(rawValue);
                }
                catch (COMException comEx)
                {
                    throw new InvalidOperationException(
                        $"OPC DA read failed [{_config.Name}] {address}: COM error 0x{comEx.ErrorCode:X8} - {comEx.Message}",
                        comEx);
                }
                catch (TargetInvocationException tiEx)
                {
                    var inner = tiEx.InnerException ?? tiEx;
                    throw new InvalidOperationException(
                        $"OPC DA read failed [{_config.Name}] {address}: {inner.Message}", inner);
                }
                finally
                {
                    if (opcItem != null)
                    {
                        try
                        {
                            opcItems?.GetType().InvokeMember("Remove", BindingFlags.InvokeMethod, null, opcItems,
                                new object[] { 1, null });
                        }
                        catch { }
                        Marshal.ReleaseComObject(opcItem);
                    }
                    if (opcItems != null) Marshal.ReleaseComObject(opcItems);
                    if (opcGroup != null)
                    {
                        try
                        {
                            opcGroups?.GetType().InvokeMember("Remove", BindingFlags.InvokeMethod, null, opcGroups,
                                new object[] { "AutoClawGrp" });
                        }
                        catch { }
                        Marshal.ReleaseComObject(opcGroup);
                    }
                    if (opcGroups != null)
                    {
                        try
                        {
                            opcGroups.GetType().InvokeMember("RemoveAll", BindingFlags.InvokeMethod, null, opcGroups, null);
                        }
                        catch { }
                        Marshal.ReleaseComObject(opcGroups);
                    }
                    if (opcServer != null)
                    {
                        try
                        {
                            opcServer.GetType().InvokeMember("Disconnect", BindingFlags.InvokeMethod, null, opcServer, null);
                        }
                        catch { }
                        Marshal.ReleaseComObject(opcServer);
                    }
                }
            }
        }

        private static object ConvertToDouble(object rawValue)
        {
            if (rawValue == null || rawValue == DBNull.Value)
                return 0.0;
            if (rawValue is double dv) return dv;
            if (rawValue is float fv) return (double)fv;
            if (rawValue is int iv) return (double)iv;
            if (rawValue is short sv) return (double)sv;
            if (rawValue is long lv) return (double)lv;
            if (rawValue is bool bv) return bv ? 1.0 : 0.0;
            if (rawValue is byte btv) return (double)btv;
            if (rawValue is decimal dec) return (double)dec;
            if (double.TryParse(rawValue.ToString(), out double parsed))
                return parsed;
            return 0.0;
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
