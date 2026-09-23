using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// TDengine REST API 轻量连接器 — 实现 IDbConnection 最小子集
    /// </summary>
    public class TdengineConnection : IDbConnection
    {
        private readonly string _baseUrl;
        private readonly string _auth;
        private readonly string _database;
        private bool _opened;

        public TdengineConnection(string server, int port, string database, string user, string password)
        {
            _database = database;
            _baseUrl = string.Format("http://{0}:{1}/rest/sql", server, port);
            string creds = string.Format("{0}:{1}", user, password);
            _auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(creds));
        }

        public string ConnectionString { get; set; }
        public int ConnectionTimeout => 30;
        public string Database => _database;
        public ConnectionState State => _opened ? ConnectionState.Open : ConnectionState.Closed;

        public IDbTransaction BeginTransaction() { throw new NotSupportedException(); }
        public IDbTransaction BeginTransaction(IsolationLevel il) { throw new NotSupportedException(); }
        public void ChangeDatabase(string databaseName) { throw new NotSupportedException(); }

        public void Open()
        {
            // 测试连接：执行简单查询
            var result = ExecuteRest(string.Format("{0}/{1}", _baseUrl, _database), "SELECT 1");
            _opened = true;
        }

        public void Close() { _opened = false; }
        public void Dispose() { _opened = false; }

        public IDbCommand CreateCommand()
        {
            return new TdengineCommand(this, _baseUrl + "/" + _database, _auth);
        }

        public int ExecuteNonQuery(string sql)
        {
            var result = ExecuteRest(_baseUrl + "/" + _database, sql);
            var obj = JObject.Parse(result);
            int code = obj["code"]?.Value<int>() ?? -1;
            if (code != 0)
                throw new Exception("TDengine: " + result);
            return result.Contains("succ") ? 1 : 0;
        }

        private string ExecuteRest(string url, string sql)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.Headers["Authorization"] = "Basic " + _auth;
            req.Timeout = ConnectionTimeout * 1000;

            byte[] body = Encoding.UTF8.GetBytes(sql);
            req.ContentLength = body.Length;
            using (var stream = req.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string text = reader.ReadToEnd();
                var obj = JObject.Parse(text);
                int code = obj["code"]?.Value<int>() ?? -1;
                if (code != 0)
                    throw new Exception("TDengine fail: " + text);
                return text;
            }
        }
    }

    public class TdengineCommand : IDbCommand
    {
        private readonly TdengineConnection _conn;
        private readonly string _url;
        private readonly string _auth;
        private string _commandText;

        public TdengineCommand(TdengineConnection conn, string url, string auth)
        {
            _conn = conn;
            _url = url;
            _auth = auth;
        }

        public string CommandText { get { return _commandText; } set { _commandText = value; } }
        public int CommandTimeout { get; set; } = 30;
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection Connection { get; set; }
        public IDataParameterCollection Parameters { get { return null; } }
        public IDbTransaction Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() { return null; }

        public int ExecuteNonQuery()
        {
            if (string.IsNullOrEmpty(_commandText)) return 0;

            var req = (HttpWebRequest)WebRequest.Create(_url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.Headers["Authorization"] = "Basic " + _auth;
            req.Timeout = CommandTimeout * 1000;

            byte[] body = Encoding.UTF8.GetBytes(_commandText);
            req.ContentLength = body.Length;
            using (var stream = req.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string text = reader.ReadToEnd();
                var result = JObject.Parse(text);
                int code = result["code"]?.Value<int>() ?? -1;
                if (code != 0)
                    throw new Exception("TDengine: " + text);
                return 1;
            }
        }

        public IDataReader ExecuteReader() { return ExecuteReader(default(CommandBehavior)); }
        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            if (string.IsNullOrEmpty(_commandText)) return new TdengineDataReader();

            var req = (HttpWebRequest)WebRequest.Create(_url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.Headers["Authorization"] = "Basic " + _auth;
            req.Timeout = CommandTimeout * 1000;

            byte[] body = Encoding.UTF8.GetBytes(_commandText);
            req.ContentLength = body.Length;
            using (var stream = req.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var respReader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string text = respReader.ReadToEnd();
                var obj = JObject.Parse(text);
                int code = obj["code"]?.Value<int>() ?? -1;
                if (code != 0)
                    throw new Exception("TDengine: " + text);
                return new TdengineDataReader(text);
            }
        }
        public object ExecuteScalar()
        {
            if (string.IsNullOrEmpty(_commandText)) return null;

            var req = (HttpWebRequest)WebRequest.Create(_url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.Headers["Authorization"] = "Basic " + _auth;
            req.Timeout = CommandTimeout * 1000;

            byte[] body = Encoding.UTF8.GetBytes(_commandText);
            req.ContentLength = body.Length;
            using (var stream = req.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string text = reader.ReadToEnd();
                var result = JObject.Parse(text);
                int code = result["code"]?.Value<int>() ?? -1;
                if (code != 0)
                    throw new Exception("TDengine: " + text);

                var data = result["data"];
                if (data != null && data.HasValues && data.First is JArray arr && arr.Count > 0)
                    return arr[0].ToString();
                return null;
            }
        }

        public void Dispose() { }
        public void Prepare() { }
    }

    /// <summary>
    /// TDengine REST JSON 响应 DataReader — 解析 head+data 数组为列式数据结构
    /// </summary>
    public class TdengineDataReader : IDataReader
    {
        private readonly string[] _columns;
        private readonly List<object[]> _rows;
        private int _cursor = -1;
        private bool _closed;

        public TdengineDataReader() : this("{\"column_meta\":[],\"data\":[]}") { }

        public TdengineDataReader(string json)
        {
            _columns = new string[0];
            _rows = new List<object[]>();

            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                var obj = JObject.Parse(json);
                var columnMeta = obj["column_meta"] as JArray;
                var data = obj["data"] as JArray;

                if (columnMeta != null)
                    _columns = columnMeta.Select(cm => cm is JArray arr && arr.Count > 0 ? arr[0].ToString() : cm.ToString()).ToArray();

                if (data != null)
                {
                    foreach (JArray row in data)
                    {
                        var values = new object[row.Count];
                        for (int i = 0; i < row.Count; i++)
                        {
                            var token = row[i];
                            if (token.Type == JTokenType.Null || string.IsNullOrEmpty(token.ToString()))
                                values[i] = DBNull.Value;
                            else
                                values[i] = token.ToString();
                        }
                        _rows.Add(values);
                    }
                }
            }
            catch
            {
                // 解析失败返回空结果集
            }
        }

        public object this[int i] => GetValue(i);
        public object this[string name] => GetValue(GetOrdinal(name));
        public int FieldCount => _columns.Length;
        public int Depth => 0;
        public bool IsClosed => _closed;
        public int RecordsAffected => -1;

        public void Close() { _closed = true; }
        public void Dispose() { _closed = true; }

        public bool Read()
        {
            _cursor++;
            return _cursor < _rows.Count;
        }

        public bool NextResult() { return false; }

        public string GetName(int i) => i >= 0 && i < _columns.Length ? _columns[i] : "";
        public string GetDataTypeName(int i) => "NCHAR";
        public Type GetFieldType(int i) => typeof(string);
        public int GetOrdinal(string name)
        {
            for (int i = 0; i < _columns.Length; i++)
                if (string.Equals(_columns[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        public bool IsDBNull(int i)
        {
            if (_cursor < 0 || _cursor >= _rows.Count) return true;
            var v = _rows[_cursor][i];
            return v == null || v == DBNull.Value;
        }

        public object GetValue(int i)
        {
            if (_cursor < 0 || _cursor >= _rows.Count) return null;
            return _rows[_cursor][i];
        }

        public int GetValues(object[] values)
        {
            if (_cursor < 0 || _cursor >= _rows.Count) return 0;
            var row = _rows[_cursor];
            int count = Math.Min(values.Length, row.Length);
            Array.Copy(row, values, count);
            return count;
        }

        public bool GetBoolean(int i) => Convert.ToBoolean(GetValue(i));
        public byte GetByte(int i) => Convert.ToByte(GetValue(i));
        public char GetChar(int i) => Convert.ToChar(GetValue(i));
        public DateTime GetDateTime(int i) => Convert.ToDateTime(GetValue(i));
        public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i));
        public double GetDouble(int i) => Convert.ToDouble(GetValue(i));
        public float GetFloat(int i) => Convert.ToSingle(GetValue(i));
        public Guid GetGuid(int i) => new Guid(GetString(i));
        public short GetInt16(int i) => Convert.ToInt16(GetValue(i));
        public int GetInt32(int i) => Convert.ToInt32(GetValue(i));
        public long GetInt64(int i) => Convert.ToInt64(GetValue(i));
        public string GetString(int i) => GetValue(i)?.ToString() ?? "";

        public long GetChars(int i, long fieldOffset, char[] buffer, int bufferoffset, int length) => 0;
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => null;

        public DataTable GetSchemaTable()
        {
            var dt = new DataTable();
            foreach (var c in _columns) dt.Columns.Add(c);
            foreach (var r in _rows) dt.Rows.Add(r);
            return dt;
        }
    }
}
