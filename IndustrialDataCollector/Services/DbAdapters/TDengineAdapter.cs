using System.Data;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Services.DbAdapters
{
    public class TDengineAdapter : IDbAdapter
    {
        public string AdapterType => "TDengine";
        public IDbConnection CreateConnection(DataSourceConnection source, string port)
        {
            return new TdengineConnection(source.Server,
                int.TryParse(port, out int tdPort) ? tdPort : 6030,
                source.Database, source.User, source.Password);
        }
        public string GetListTablesSql()
            => "SELECT table_name FROM information_schema.ins_tables WHERE db_name=DATABASE() ORDER BY table_name";
        public string GetDescribeTableSql(string tableName)
            => string.Format("DESCRIBE {0}", tableName);
    }
}
