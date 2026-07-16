using System.Collections.Generic;
using System.Data;

namespace IndustrialDataCollection.Services.DbAdapters
{
    /// <summary>
    /// 数据库适配器接口 — 每种数据库提供方言实现
    /// </summary>
    public interface IDbAdapter
    {
        /// <summary>适配器类型标识（与 DataSourceConnection.DbType 一致）</summary>
        string AdapterType { get; }

        /// <summary>根据数据源配置创建 IDbConnection</summary>
        IDbConnection CreateConnection(Models.DataSourceConnection source, string port);

        /// <summary>列出所有用户表的 SQL</summary>
        string GetListTablesSql();

        /// <summary>描述表结构的 SQL（返回列名、类型、注释）</summary>
        string GetDescribeTableSql(string tableName);
    }
}
