using System.Data;
using System.Data.SQLite;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Services.DbAdapters
{
    public class MySqlAdapter : IDbAdapter
    {
        public string AdapterType => "MySQL";
        public IDbConnection CreateConnection(DataSourceConnection source, string port)
        {
            return new MySql.Data.MySqlClient.MySqlConnection(
                string.Format("Server={0};Port={1};Database={2};Uid={3};Pwd={4};CharSet=utf8mb4;AllowUserVariables=True;",
                    source.Server, port, source.Database, source.User, source.Password));
        }
        public string GetListTablesSql()
            => "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME";
        public string GetDescribeTableSql(string tableName)
            => string.Format("SHOW FULL COLUMNS FROM {0}", QuoteName(tableName));
        private static string QuoteName(string name) => "" + name.Replace("", "`") + "";
    }
}
