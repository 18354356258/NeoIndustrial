using System.Threading;
using System.Threading.Tasks;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// v2.0 数据流出口接口 —— 可插拔的异步数据消费端
    /// 实现类: MqttSink, DatabaseSink, 未来可扩展 Kafka/REST/WebSocket 等
    /// </summary>
    public interface ISink
    {
        /// <summary>出口名称（用于日志和诊断）</summary>
        string Name { get; }

        /// <summary>是否就绪可用</summary>
        bool IsReady { get; }

        /// <summary>异步写入一个数据批次</summary>
        /// <returns>是否写入成功</returns>
        Task<bool> WriteAsync(DataBatch batch, CancellationToken cancellationToken);
    }
}
