using System;
using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Drivers;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Services.Sinks
{
    /// <summary>
    /// v2.0 数据库出口 —— 将归一化 DataPacket 写入已配置的数据库
    /// 通过重建 CycleDataBatch 复用 DatabaseWriteService 现有实现
    /// </summary>
    public class DatabaseSink : ISink
    {
        private readonly DatabaseWriteService _db;

        public DatabaseSink()
        {
            _db = DatabaseWriteService.Instance;
        }

        public string Name => "Database";

        public bool IsReady => _db.IsAnyEnabled;

        public async Task<bool> WriteAsync(DataBatch batch, CancellationToken cancellationToken)
        {
            if (batch == null || batch.Values == null || batch.Values.Count == 0)
                return true; // DB 未启用不算失败

            string deviceName = batch.Device ?? "";
            if (string.IsNullOrEmpty(deviceName)) return true;

            try
            {
                // 将 DataBatch 重建为 CycleDataBatch 以兼容 DatabaseWriteService
                var cycleBatch = new CycleDataBatch
                {
                    Timestamp = batch.Timestamp,
                    Driver = batch.Driver,
                    Device = batch.Device,
                    DeviceId = batch.DeviceId ?? ""
                };
                cycleBatch.Values = new System.Collections.Generic.List<CycleDataItem>();
                foreach (var p in batch.Values)
                {
                    cycleBatch.Values.Add(new CycleDataItem
                    {
                        Id = p.VariableName ?? "",
                        DataType = p.DataType,
                        Value = p.Value,
                        Unit = p.Unit,
                        VariableId = p.VariableId,
                        TagCn = p.TagCn
                    });
                }

                await _db.WriteBatchAsync(deviceName, cycleBatch);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Database sink error: " + ex.Message);
                return false;
            }
        }
    }
}
