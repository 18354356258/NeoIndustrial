using System.Data;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Services.DbAdapters
{
    public class PostgreSqlAdapter : IDbAdapter
    {
        public string AdapterType => "PostgreSQL";
        public IDbConnection CreateConnection(DataSourceConnection source, string port)
        {
            return new Npgsql.NpgsqlConnection(
                string.Format("Host={0};Port={1};Database={2};Username={3};Password={4};",
                    source.Server, port, source.Database, source.User, source.Password));
        }
        public string GetListTablesSql()
            => "SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema') ORDER BY tablename";
        public string GetDescribeTableSql(string tableName)
            => string.Format(@"SELECT column_name, data_type, is_nullable,
    COALESCE(col_description((SELECT c.oid FROM pg_class c WHERE c.relname='{0}'), ordinal_position), '') AS column_comment
FROM information_schema.columns WHERE table_name='{0}' ORDER BY ordinal_position", tableName);
    }
}
