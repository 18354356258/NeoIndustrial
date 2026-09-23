using System;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 站内消息 — 存储在本地 SQLite，可通过 REST API 查询
    /// </summary>
    public class InSiteMessage
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Level { get; set; } = "info";
        public string EventId { get; set; }
        public string NodeId { get; set; }
        public string EventType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; }
    }

    /// <summary>
    /// 工单 — 存储在本地 SQLite
    /// </summary>
    public class WorkOrder
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Priority { get; set; } = "normal";
        public string Assignee { get; set; }
        public string Category { get; set; }
        public string EventId { get; set; }
        public string NodeId { get; set; }
        public string Description { get; set; }
        public string Status { get; set; } = "待处理";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
