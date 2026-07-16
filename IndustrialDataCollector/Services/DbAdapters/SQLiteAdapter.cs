using System.Data;
using System.Data.SQLite;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Services.DbAdapters
{
    public class SQLiteAdapter : IDbAdapter
    {
        public string AdapterType => "SQLite";
        public IDbConnection CreateConnection(DataSourceConnection source, string port)
        {
            string cs = "Data Source=" + source.FilePath + ";Version=3;Read Only=" +
                (source.PermissionMode == "readonly" ? "True" : "False");
            if (!string.IsNullOrEmpty(source.Password))
                cs += ";Password=" + source.Password;
            return new SQLiteConnection(cs);
        }
        public string GetListTablesSql()
            => "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        public string GetDescribeTableSql(string tableName)
            => string.Format("PRAGMA table_info({0})", tableName);
    }
}
