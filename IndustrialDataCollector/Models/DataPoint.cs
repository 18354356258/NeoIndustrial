using System;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Models
{
    public enum ByteOrder
    {
        ABCD = 0, DCBA = 1, BADC = 2, CDAB = 3
    }

    public class DataPoint
    {
        // ── 基本字段（已有） ──
        [JsonProperty("id")] public string Id { get; set; } = Guid.NewGuid().ToString();
        /// <summary>v2.0: 永久唯一变量身份，Tag 系统关联键</summary>
        [JsonProperty("variableId")] public string VariableId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 12);
        [JsonProperty("name")] public string Name { get; set; } = "";
        /// <summary>v2.2: 保留字段，仅用于 JSON 反序列化兼容。新增点使用 TagCn。</summary>
        [JsonProperty("nameEn")]
        public string NameEn { get; set; } = "";
        [JsonProperty("address")] public string Address { get; set; } = "0";
        [JsonProperty("dataType")] public string DataType { get; set; } = "int";
        [JsonProperty("unit")] public string Unit { get; set; } = "";
        [JsonProperty("scaleFactor")] public double ScaleFactor { get; set; } = 1.0;
        [JsonProperty("offset")] public double Offset { get; set; } = 0.0;
        [JsonProperty("isActive")] public bool IsActive { get; set; } = true;
        [JsonProperty("length")] public int Length { get; set; } = 0;
        [JsonProperty("byteOrder")] public ByteOrder ByteOrder { get; set; } = ByteOrder.ABCD;

        // ── 语义标签 ──
        [JsonProperty("tag")] public string Tag { get; set; } = "";
        [JsonProperty("tagCn")] public string TagCn { get; set; } = "";
        /// <summary>控制英文 tag 字段是否输出到 MQTT/DB。默认 true（向后兼容）。</summary>
        [JsonProperty("outputTag")] public bool OutputTag { get; set; } = true;
        [JsonProperty("outputTagCn")] public bool OutputTagCn { get; set; } = false;

        // ── 修约 ──
        [JsonProperty("roundingEnabled")] public bool RoundingEnabled { get; set; } = false;
        [JsonProperty("roundingMode")] public int RoundingMode { get; set; } = 0;  // 0=Round 1=Floor 2=Ceil 3=Truncate
        [JsonProperty("roundingDecimals")] public int RoundingDecimals { get; set; } = 2;

        // ── 滤波 ──
        [JsonProperty("filterEnabled")] public bool FilterEnabled { get; set; } = false;
        [JsonProperty("filterMode")] public int FilterMode { get; set; } = 0;  // 0=MovingAvg 1=Median 2=ExpSmooth
        [JsonProperty("filterWindow")] public int FilterWindow { get; set; } = 5;
        [JsonProperty("filterAlpha")] public double FilterAlpha { get; set; } = 0.3;

        // ── 数据清洗 ──
        [JsonProperty("cleanEnabled")] public bool CleanEnabled { get; set; } = false;
        [JsonProperty("deadBandEnabled")] public bool DeadBandEnabled { get; set; } = false;
        [JsonProperty("deadBand")] public double DeadBand { get; set; } = 0.0;
        [JsonProperty("clipEnabled")] public bool ClipEnabled { get; set; } = false;
        [JsonProperty("clipMin")] public double ClipMin { get; set; } = 0.0;
        [JsonProperty("clipMax")] public double ClipMax { get; set; } = 100.0;
        [JsonProperty("outlierEnabled")] public bool OutlierEnabled { get; set; } = false;
        [JsonProperty("sigmaThreshold")] public double SigmaThreshold { get; set; } = 3.0;
        // 空值过滤
        [JsonProperty("nanFilterEnabled")] public bool NanFilterEnabled { get; set; } = false;
        [JsonProperty("nanFilterNaN")] public bool NanFilterNaN { get; set; } = true;
        [JsonProperty("nanFilterInf")] public bool NanFilterInf { get; set; } = true;
        [JsonProperty("nanFilterNegative")] public bool NanFilterNegative { get; set; } = false;
        [JsonProperty("nanFilterReplacement")] public double NanFilterReplacement { get; set; } = 0.0;
        // 冻结检测
        [JsonProperty("freezeEnabled")] public bool FreezeEnabled { get; set; } = false;
        [JsonProperty("freezeWindow")] public int FreezeWindow { get; set; } = 10;
        // 尖峰抑制
        [JsonProperty("spikeEnabled")] public bool SpikeEnabled { get; set; } = false;
        [JsonProperty("spikeWindow")] public int SpikeWindow { get; set; } = 5;
        [JsonProperty("spikeThreshold")] public double SpikeThreshold { get; set; } = 3.0;
        // 变化率限制
        [JsonProperty("rocLimitEnabled")] public bool RocLimitEnabled { get; set; } = false;
        [JsonProperty("rocLimitMax")] public double RocLimitMax { get; set; } = 1.0;
        // IQR 检测
        [JsonProperty("iqrEnabled")] public bool IqrEnabled { get; set; } = false;
        [JsonProperty("iqrMultiplier")] public double IqrMultiplier { get; set; } = 1.5;
        // 量程合理性
        [JsonProperty("rangeEnabled")] public bool RangeEnabled { get; set; } = false;
        [JsonProperty("rangeMin")] public double RangeMin { get; set; } = 0.0;
        [JsonProperty("rangeMax")] public double RangeMax { get; set; } = 100.0;

        // ── 报警 ──
        [JsonProperty("alarmEnabled")] public bool AlarmEnabled { get; set; } = false;
        [JsonProperty("alarmHH_enabled")] public bool AlarmHH_Enabled { get; set; } = false;
        [JsonProperty("alarmHH")] public double AlarmHH { get; set; } = 100.0;
        [JsonProperty("alarmH_enabled")] public bool AlarmH_Enabled { get; set; } = false;
        [JsonProperty("alarmH")] public double AlarmH { get; set; } = 80.0;
        [JsonProperty("alarmL_enabled")] public bool AlarmL_Enabled { get; set; } = false;
        [JsonProperty("alarmL")] public double AlarmL { get; set; } = 20.0;
        [JsonProperty("alarmLL_enabled")] public bool AlarmLL_Enabled { get; set; } = false;
        [JsonProperty("alarmLL")] public double AlarmLL { get; set; } = 0.0;
        [JsonProperty("alarmDelay")] public int AlarmDelay { get; set; } = 0;

        // ── 公式计算 ──
        [JsonProperty("calculationEnabled")] public bool CalculationEnabled { get; set; } = false;
        [JsonProperty("calculationExpression")] public string CalculationExpression { get; set; } = "";

        // ── 信号变换 ──
        [JsonProperty("squareRootEnabled")] public bool SquareRootEnabled { get; set; } = false;
        [JsonProperty("absValueEnabled")] public bool AbsValueEnabled { get; set; } = false;
        [JsonProperty("rateOfChangeEnabled")] public bool RateOfChangeEnabled { get; set; } = false;

        // ── 自定义脚本 ──
        [JsonProperty("scriptEnabled")] public bool ScriptEnabled { get; set; } = false;
        [JsonProperty("scriptLanguage")] public string ScriptLanguage { get; set; } = "python";
        [JsonProperty("scriptPath")] public string ScriptPath { get; set; } = "";
        [JsonProperty("scriptArgs")] public string ScriptArgs { get; set; } = "";
        [JsonProperty("scriptPostProcess")] public bool ScriptPostProcess { get; set; } = true;

        // ── 存储策略 ──
        [JsonProperty("storageDbWriteEnabled")] public bool StorageDbWriteEnabled { get; set; } = true;
        [JsonProperty("storageCustomTopic")] public string StorageCustomTopic { get; set; } = "";
        [JsonProperty("storageChangeOnly")] public bool StorageChangeOnly { get; set; } = false;
        [JsonProperty("storageChangeDeadband")] public double StorageChangeDeadband { get; set; } = 0.1;
        [JsonProperty("storagePrecision")] public int StoragePrecision { get; set; } = 3;

        // ── SPC 统计过程控制 ──
        [JsonProperty("spcEnabled")] public bool SpcEnabled { get; set; } = false;
        [JsonProperty("spcUcl")] public double SpcUcl { get; set; } = 0;
        [JsonProperty("spcLcl")] public double SpcLcl { get; set; } = 0;
        [JsonProperty("spcUsl")] public double SpcUsl { get; set; } = 0;
        [JsonProperty("spcLsl")] public double SpcLsl { get; set; } = 0;
        [JsonProperty("spcTarget")] public double SpcTarget { get; set; } = 0;
        [JsonProperty("spcSubgroupSize")] public int SpcSubgroupSize { get; set; } = 5;

        public double ConvertValue(double rawValue)
        {
            return rawValue * ScaleFactor + Offset;
        }

        public DataPoint Clone()
        {
            // JSON 深拷贝 — 永不漏字段（规则 50）
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<DataPoint>(json);
        }

        public override string ToString() => $"{Name} ({Address})";
    }
}
