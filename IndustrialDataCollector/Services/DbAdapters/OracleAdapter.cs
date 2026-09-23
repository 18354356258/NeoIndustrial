using System;
using System.Data;
using System.Reflection;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Services.DbAdapters
{
    public class OracleAdapter : IDbAdapter
    {
        public string AdapterType => "Oracle";
        public IDbConnection CreateConnection(DataSourceConnection source, string port)
        {
            try
            {
                var asm = Assembly.Load("Oracle.ManagedDataAccess");
                var connType = asm.GetType("Oracle.ManagedDataAccess.Client.OracleConnection");
                var connStr = string.Format(
                    "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={0})(PORT={1}))(CONNECT_DATA=(SERVICE_NAME={2})));User Id={3};Password={4};",
                    source.Server, port, source.Database, source.User, source.Password);
                return (IDbConnection)Activator.CreateInstance(connType, connStr);
            }
            catch (Exception)
            {
                throw new Exception("Oracle.ManagedDataAccess is not installed.");
            }
        }
        public string GetListTablesSql()
            => "SELECT table_name FROM user_tables ORDER BY table_name";
        public string GetDescribeTableSql(string tableName)
            => string.Format("SELECT column_name, data_type, nullable FROM user_tab_columns WHERE table_name='{0}' ORDER BY column_id", tableName.ToUpper());
    }
}
