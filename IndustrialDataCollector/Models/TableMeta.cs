using System.Collections.Generic;

namespace IndustrialDataCollection.Models
{
    /// <summary>
    /// Metadata for a database table discovered during data source analysis
    /// </summary>
    public class TableMeta
    {
        public string TableName { get; set; }
        public string Tag { get; set; }
        public string TagCn { get; set; }
        public string Purpose { get; set; }
        public long RowCount { get; set; }
        public List<ColumnMeta> Columns { get; set; }

        /// <summary>用户是否勾选此表进行结构分析和语义同步（默认选中）</summary>
        [Newtonsoft.Json.JsonProperty("isAnalyzed")]
        public bool IsAnalyzed { get; set; } = true;
    }

    /// <summary>
    /// Metadata for a single column within a table
    /// </summary>
    public class ColumnMeta
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public string Tag { get; set; }
        public string TagCn { get; set; }
        public bool IsNullable { get; set; }
        public string Comment { get; set; }
        public string LinkedVariable { get; set; }
    }
}
