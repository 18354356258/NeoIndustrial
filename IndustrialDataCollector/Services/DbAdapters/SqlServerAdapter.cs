using System.Data;
using System.Data.SqlClient;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Services.DbAdapters
{
    public class SqlServerAdapter : IDbAdapter
    {
        public string AdapterType => "SQL Server";
        public IDbConnection CreateConnection(DataSourceConnection source, string port)
        {
            return new SqlConnection(
                string.Format("Server={0}{1};Database={2};User Id={3};Password={4};TrustServerCertificate=True;{5}",
                    source.Server, string.IsNullOrEmpty(port) ? "" : ("," + port), source.Database,
                    source.User, source.Password,
                    source.PermissionMode == "readonly" ? "ApplicationIntent=ReadOnly;" : ""));
        }
        public string GetListTablesSql()
            => "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME";
        public string GetDescribeTableSql(string tableName)
            => string.Format(@"SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE,
    CAST(COALESCE(ep.value, '') AS NVARCHAR(500)) AS COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS c
LEFT JOIN sys.extended_properties ep ON ep.major_id = OBJECT_ID('{0}')
    AND ep.minor_id = c.ORDINAL_POSITION AND ep.name = 'MS_Description'
WHERE c.TABLE_NAME = '{0}' ORDER BY c.ORDINAL_POSITION", tableName);
    }
}
