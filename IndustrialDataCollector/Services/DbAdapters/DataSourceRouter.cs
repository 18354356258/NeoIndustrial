using System;
using System.Collections.Generic;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Services.DbAdapters
{
    /// <summary>
    /// 数据源路由器 — 根据 DbType 自动选择对应适配器
    /// </summary>
    public class DataSourceRouter
    {
        private static readonly Lazy<DataSourceRouter> _instance =
            new Lazy<DataSourceRouter>(() => new DataSourceRouter());

        public static DataSourceRouter Instance => _instance.Value;

        private readonly Dictionary<string, IDbAdapter> _adapters
            = new Dictionary<string, IDbAdapter>(StringComparer.OrdinalIgnoreCase);

        private DataSourceRouter()
        {
            Register(new MySqlAdapter());
            Register(new SQLiteAdapter());
            Register(new SqlServerAdapter());
            Register(new PostgreSqlAdapter());
            Register(new TDengineAdapter());
        }

        /// <summary>动态注册适配器（Oracle 按需加载）</summary>
        public void Register(IDbAdapter adapter)
        {
            _adapters[adapter.AdapterType] = adapter;
        }

        /// <summary>根据数据源类型获取适配器</summary>
        public IDbAdapter GetAdapter(string dbType)
        {
            if (_adapters.TryGetValue(dbType, out var adapter))
                return adapter;

            // Oracle 延迟加载
            if (string.Equals(dbType, "Oracle", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var oracle = new OracleAdapter();
                    Register(oracle);
                    return oracle;
                }
                catch { }
            }

            throw new NotSupportedException("Unsupported DB type: " + dbType);
        }

        /// <summary>是否支持该数据库类型</summary>
        public bool Supports(string dbType)
            => _adapters.ContainsKey(dbType)
            || string.Equals(dbType, "Oracle", StringComparison.OrdinalIgnoreCase);

        /// <summary>列出所有已注册的适配器类型</summary>
        public IEnumerable<string> RegisteredTypes => _adapters.Keys;
    }
}
