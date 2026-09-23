using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IndustrialDataCollection.Drivers;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// v1.12 MCP Fabric 声明式分析引擎
    /// 将 AI 提交的 JSON 分析请求翻译为算子调用，返回结构化分析结果
    /// </summary>
    public class McpFabricEngine
    {
        private static readonly Lazy<McpFabricEngine> _instance =
            new Lazy<McpFabricEngine>(() => new McpFabricEngine());
        public static McpFabricEngine Instance => _instance.Value;

        private readonly DatabaseWriteService _db = DatabaseWriteService.Instance;

        /// <summary>算子元数据注册表（供 fabric_list_operators 使用）</summary>
        public static readonly List<FabricOperatorMeta> Operators = new List<FabricOperatorMeta>
        {
            new FabricOperatorMeta
            {
                Name = "window_aggregate",
                Description = "滑动窗口聚合分析 — 对指定变量在时间窗口内执行 avg/min/max/sum/count/stddev 统计",
                Category = "analysis",
                Params = new List<FabricParamMeta>
                {
                    new FabricParamMeta { Name = "device_name", Type = "string", Required = true, Description = "设备名称" },
                    new FabricParamMeta { Name = "variable", Type = "string", Required = true, Description = "变量名" },
                    new FabricParamMeta { Name = "window_seconds", Type = "int", Required = false, Description = "滑动窗口秒数，默认 300" },
                    new FabricParamMeta { Name = "aggregations", Type = "array", Required = false, Description = "聚合类型: avg,min,max,sum,count,stddev" },
                    new FabricParamMeta { Name = "time_range", Type = "string", Required = false, Description = "时间范围，默认 1h" }
                }
            },
            new FabricOperatorMeta
            {
                Name = "trend_detect",
                Description = "趋势检测 — 线性回归分析变量变化趋势，检测上升/下降速率是否超阈值",
                Category = "analysis",
                Params = new List<FabricParamMeta>
                {
                    new FabricParamMeta { Name = "device_name", Type = "string", Required = true, Description = "设备名称" },
                    new FabricParamMeta { Name = "variable", Type = "string", Required = true, Description = "变量名" },
                    new FabricParamMeta { Name = "window_seconds", Type = "int", Required = false, Description = "分析窗口秒数，默认 300" },
                    new FabricParamMeta { Name = "rise_rate_threshold", Type = "double", Required = false, Description = "上升速率报警阈值" },
                    new FabricParamMeta { Name = "fall_rate_threshold", Type = "double", Required = false, Description = "下降速率报警阈值" },
                    new FabricParamMeta { Name = "time_range", Type = "string", Required = false, Description = "时间范围，默认 1h" }
                }
            },
            new FabricOperatorMeta
            {
                Name = "threshold_alarm",
                Description = "统计阈值报警 — 基于滑动窗口均值与标准差检测异常（μ±kσ），超出即报警",
                Category = "alarm",
                Params = new List<FabricParamMeta>
                {
                    new FabricParamMeta { Name = "device_name", Type = "string", Required = true, Description = "设备名称" },
                    new FabricParamMeta { Name = "variable", Type = "string", Required = true, Description = "变量名" },
                    new FabricParamMeta { Name = "window_seconds", Type = "int", Required = false, Description = "统计窗口秒数，默认 300" },
                    new FabricParamMeta { Name = "sigma", Type = "double", Required = false, Description = "σ 倍数阈值，默认 3.0" },
                    new FabricParamMeta { Name = "time_range", Type = "string", Required = false, Description = "时间范围，默认 1h" }
                }
            },
            new FabricOperatorMeta
            {
                Name = "anomaly_detect",
                Description = "时序异常模式识别 — 检测突跳(spike)、漂移(drift)、周期异常(periodic)三类异常模式",
                Category = "analysis",
                Params = new List<FabricParamMeta>
                {
                    new FabricParamMeta { Name = "device_name", Type = "string", Required = true, Description = "设备名称" },
                    new FabricParamMeta { Name = "variable", Type = "string", Required = true, Description = "变量名" },
                    new FabricParamMeta { Name = "method", Type = "string", Required = false, Description = "检测模式：spike/drift/periodic/all，默认 all" },
                    new FabricParamMeta { Name = "threshold_sigma", Type = "double", Required = false, Description = "异常阈值 σ 倍数，默认 3.0" },
                    new FabricParamMeta { Name = "time_range", Type = "string", Required = false, Description = "时间范围，默认 1h" }
                }
            },
            new FabricOperatorMeta
            {
                Name = "correlation",
                Description = "多变量相关性分析 — 皮尔逊相关系数矩阵，发现变量间线性关联（如温度 vs 挤速 vs 压力）",
                Category = "analysis",
                Params = new List<FabricParamMeta>
                {
                    new FabricParamMeta { Name = "device_name", Type = "string", Required = true, Description = "设备名称" },
                    new FabricParamMeta { Name = "variables", Type = "array", Required = true, Description = "待分析变量名列表，至少 2 个" },
                    new FabricParamMeta { Name = "time_range", Type = "string", Required = false, Description = "时间范围，默认 1h" }
                }
            },
            new FabricOperatorMeta
            {
                Name = "root_cause",
                Description = "根因定位 — 当目标变量异常时，分析哪些变量与之最相关（含时滞相关性），辅助定位故障根源",
                Category = "analysis",
                Params = new List<FabricParamMeta>
                {
                    new FabricParamMeta { Name = "device_name", Type = "string", Required = true, Description = "设备名称" },
                    new FabricParamMeta { Name = "target_variable", Type = "string", Required = true, Description = "异常目标变量名" },
                    new FabricParamMeta { Name = "candidate_variables", Type = "array", Required = false, Description = "候选变量列表，为空则分析设备所有变量" },
                    new FabricParamMeta { Name = "max_lag_seconds", Type = "int", Required = false, Description = "最大时滞秒数，默认 60" },
                    new FabricParamMeta { Name = "time_range", Type = "string", Required = false, Description = "时间范围，默认 1h" }
                }
            },
            new FabricOperatorMeta
            {
                Name = "predict",
                Description = "简单预测 — 基于历史数据线性回归外推，预测未来 N 分钟趋势（含置信区间）",
                Category = "analysis",
                Params = new List<FabricParamMeta>
                {
                    new FabricParamMeta { Name = "device_name", Type = "string", Required = true, Description = "设备名称" },
                    new FabricParamMeta { Name = "variable", Type = "string", Required = true, Description = "变量名" },
                    new FabricParamMeta { Name = "predict_minutes", Type = "int", Required = false, Description = "预测未来分钟数，默认 10" },
                    new FabricParamMeta { Name = "time_range", Type = "string", Required = false, Description = "历史数据时间范围，默认 1h" }
                }
            },
            new FabricOperatorMeta
            {
                Name = "daily_report",
                Description = "生产日报 — 对指定设备群生成综合运行报告（产量统计/异常汇总/趋势概要）",
                Category = "report",
                Params = new List<FabricParamMeta>
                {
                    new FabricParamMeta { Name = "device_names", Type = "array", Required = false, Description = "设备名称列表，为空则查询全部" },
                    new FabricParamMeta { Name = "date", Type = "string", Required = false, Description = "报告日期 yyyy-MM-dd，空为当日" },
                    new FabricParamMeta { Name = "sections", Type = "array", Required = false, Description = "报告分节: summary,stats,alarms" },
                    new FabricParamMeta { Name = "output_path", Type = "string", Required = false, Description = "HTML 输出路径，空则返回 JSON" }
                }
            }
        };

        /// <summary>
        /// 执行 Fabric 声明式请求
        /// </summary>
        public async Task<FabricResult> ExecuteAsync(FabricRequest request)
        {
            var sw = Stopwatch.StartNew();
            var result = new FabricResult { Operator = request.Operator };

            try
            {
                switch (request.Operator)
                {
                    case "window_aggregate":
                        result.Data = await WindowAggregateAsync(request.Params, request.TimeRange);
                        break;
                    case "trend_detect":
                        result.Data = await TrendDetectAsync(request.Params, request.TimeRange);
                        break;
                    case "threshold_alarm":
                        result.Data = await ThresholdAlarmAsync(request.Params, request.TimeRange);
                        break;
                    case "anomaly_detect":
                        result.Data = await AnomalyDetectAsync(request.Params, request.TimeRange);
                        break;
                    case "correlation":
                        result.Data = await CorrelationAsync(request.Params, request.TimeRange);
                        break;
                    case "root_cause":
                        result.Data = await RootCauseAsync(request.Params, request.TimeRange);
                        break;
                    case "predict":
                        result.Data = await PredictAsync(request.Params, request.TimeRange);
                        break;
                    case "daily_report":
                        result.Data = await DailyReportAsync(request.Params, request.Output);
                        break;
                    default:
                        throw new NotSupportedException("不支持的算子: " + request.Operator);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                Logger.Error("FabricEngine [" + request.Operator + "] failed: " + ex.Message);
            }
            finally
            {
                result.ExecutionMs = sw.ElapsedMilliseconds;
            }

            return result;
        }

        #region 算子实现

        /// <summary>滑动窗口聚合</summary>
        private async Task<object> WindowAggregateAsync(Dictionary<string, object> p, string timeRange)
        {
            string deviceName = GetString(p, "device_name");
            string variable = GetString(p, "variable");
            int windowSec = GetInt(p, "window_seconds", 300);
            var aggTypes = GetList(p, "aggregations", new List<string> { "avg" });

            var (start, end) = ParseTimeRange(timeRange);
            var records = await _db.QueryHistoryAsync(deviceName, variable, start, end, 10000);

            if (records.Count == 0)
                return new { message = "无数据", device = deviceName, variable };

            // 将记录按时间排序
            var sorted = records.OrderBy(r => r.timestamp).ToList();

            // 滑动窗口聚合
            var windows = new List<object>();
            var windowValues = new List<double>();
            long windowStart = 0;

            for (int i = 0; i < sorted.Count; i++)
            {
                double val;
                if (!double.TryParse(sorted[i].value, out val)) continue;

                long ts = ParseTimestamp(sorted[i].timestamp);

                if (windowValues.Count == 0)
                    windowStart = ts;

                // 窗口滑动
                while (ts - windowStart > windowSec * 1000 && windowValues.Count > 0)
                {
                    windows.Add(BuildWindowAgg(aggTypes, windowValues, windowStart, windowStart + windowSec * 1000));
                    windowValues.RemoveAt(0);
                    windowStart = ParseTimestamp(sorted[sorted.Count - windowValues.Count].timestamp);
                }

                windowValues.Add(val);
            }

            // 尾窗口
            if (windowValues.Count > 0)
                windows.Add(BuildWindowAgg(aggTypes, windowValues, windowStart, windowStart + windowSec * 1000));

            return new
            {
                device = deviceName,
                variable,
                window_seconds = windowSec,
                total_records = sorted.Count,
                windows
            };
        }

        private static object BuildWindowAgg(List<string> aggs, List<double> vals, long start, long end)
        {
            var agg = new Dictionary<string, object>
            {
                ["window_start"] = start,
                ["window_end"] = end
            };
            if (aggs.Contains("avg")) agg["avg"] = Math.Round(vals.Average(), 4);
            if (aggs.Contains("min")) agg["min"] = vals.Min();
            if (aggs.Contains("max")) agg["max"] = vals.Max();
            if (aggs.Contains("sum")) agg["sum"] = Math.Round(vals.Sum(), 2);
            if (aggs.Contains("count")) agg["count"] = vals.Count;
            if (aggs.Contains("stddev"))
            {
                double mean = vals.Average();
                double sqrDiffs = vals.Sum(v => (v - mean) * (v - mean));
                agg["stddev"] = Math.Round(Math.Sqrt(sqrDiffs / vals.Count), 4);
            }
            return agg;
        }

        /// <summary>趋势检测（线性回归 + 速率判断）</summary>
        private async Task<object> TrendDetectAsync(Dictionary<string, object> p, string timeRange)
        {
            string deviceName = GetString(p, "device_name");
            string variable = GetString(p, "variable");
            int windowSec = GetInt(p, "window_seconds", 300);
            double? riseThreshold = GetDoubleOrNull(p, "rise_rate_threshold");
            double? fallThreshold = GetDoubleOrNull(p, "fall_rate_threshold");

            var (start, end) = ParseTimeRange(timeRange);
            var records = await _db.QueryHistoryAsync(deviceName, variable, start, end, 10000);

            if (records.Count < 2)
                return new { message = "数据不足，至少需要 2 条记录", device = deviceName, variable };

            var sorted = records.OrderBy(r => r.timestamp).ToList();

            // 线性回归: y = slope * x + intercept
            int n = sorted.Count;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            var points = new List<double>();
            for (int i = 0; i < n; i++)
            {
                double val;
                if (!double.TryParse(sorted[i].value, out val)) continue;
                double x = ParseTimestamp(sorted[i].timestamp) / 1000.0;
                sumX += x;
                sumY += val;
                sumXY += x * val;
                sumX2 += x * x;
                points.Add(val);
            }

            if (points.Count < 2)
                return new { message = "有效数值数据不足", device = deviceName, variable };

            int m = points.Count;
            double slope = (m * sumXY - sumX * sumY) / (m * sumX2 - sumX * sumX);
            double intercept = (sumY - slope * sumX) / m;

            // R² 拟合度
            double meanY = points.Average();
            double ssRes = 0, ssTot = 0;
            for (int i = 0; i < m; i++)
            {
                double x = ParseTimestamp(sorted[i].timestamp) / 1000.0;
                double predicted = slope * x + intercept;
                ssRes += (points[i] - predicted) * (points[i] - predicted);
                ssTot += (points[i] - meanY) * (points[i] - meanY);
            }
            double r2 = ssTot > 0 ? 1 - ssRes / ssTot : 0;

            // 趋势判定
            string direction = Math.Abs(slope) < 0.0001 ? "stable" : slope > 0 ? "rising" : "falling";
            double ratePerMin = slope * 60; // 每分钟变化率

            var alarms = new List<object>();
            if (direction == "rising" && riseThreshold.HasValue && ratePerMin > riseThreshold.Value)
                alarms.Add(new { level = "WARN", message = string.Format("上升速率 {0:F4}/min 超过阈值 {1}", ratePerMin, riseThreshold.Value) });
            if (direction == "falling" && fallThreshold.HasValue && Math.Abs(ratePerMin) > fallThreshold.Value)
                alarms.Add(new { level = "WARN", message = string.Format("下降速率 {0:F4}/min 超过阈值 {1}", Math.Abs(ratePerMin), fallThreshold.Value) });

            return new
            {
                device = deviceName,
                variable,
                record_count = m,
                slope,
                intercept,
                r_squared = Math.Round(r2, 4),
                direction,
                rate_per_minute = Math.Round(ratePerMin, 4),
                alarms
            };
        }

        /// <summary>统计阈值报警（μ±kσ）</summary>
        private async Task<object> ThresholdAlarmAsync(Dictionary<string, object> p, string timeRange)
        {
            string deviceName = GetString(p, "device_name");
            string variable = GetString(p, "variable");
            int windowSec = GetInt(p, "window_seconds", 300);
            double sigma = GetDouble(p, "sigma", 3.0);

            var (start, end) = ParseTimeRange(timeRange);
            var records = await _db.QueryHistoryAsync(deviceName, variable, start, end, 10000);

            if (records.Count < 2)
                return new { message = "数据不足", device = deviceName, variable };

            var sorted = records.OrderBy(r => r.timestamp).ToList();
            var values = new List<double>();
            var timestamps = new List<string>();

            foreach (var r in sorted)
            {
                double val;
                if (double.TryParse(r.value, out val))
                {
                    values.Add(val);
                    timestamps.Add(r.timestamp);
                }
            }

            if (values.Count < 2)
                return new { message = "有效数值数据不足", device = deviceName, variable };

            double mean = values.Average();
            double stddev = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
            double upper = mean + sigma * stddev;
            double lower = mean - sigma * stddev;

            var anomalies = new List<object>();
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] > upper || values[i] < lower)
                {
                    anomalies.Add(new
                    {
                        timestamp = timestamps[i],
                        value = values[i],
                        deviation = Math.Round((values[i] - mean) / stddev, 2),
                        level = values[i] > upper ? "HH" : "LL"
                    });
                }
            }

            return new
            {
                device = deviceName,
                variable,
                record_count = values.Count,
                statistics = new
                {
                    mean = Math.Round(mean, 4),
                    stddev = Math.Round(stddev, 4),
                    upper_threshold = Math.Round(upper, 4),
                    lower_threshold = Math.Round(lower, 4),
                    sigma
                },
                anomaly_count = anomalies.Count,
                anomalies
            };
        }

        /// <summary>生产日报</summary>
        private async Task<object> DailyReportAsync(Dictionary<string, object> p, FabricOutput output)
        {
            var deviceNames = GetList(p, "device_names", new List<string>());
            string singleDeviceName = GetString(p, "device_name");
            if (!string.IsNullOrEmpty(singleDeviceName) && !deviceNames.Contains(singleDeviceName))
                deviceNames.Add(singleDeviceName);
            string date = GetString(p, "date");
            var sections = GetList(p, "sections", new List<string> { "summary", "stats", "alarms" });

            if (string.IsNullOrEmpty(date))
                date = DateTime.Now.ToString("yyyy-MM-dd");

            string startTime = date + " 00:00:00";
            string endTime = date + " 23:59:59";

            // 获取设备列表
            var allDevices = ConfigService.Instance.GetAllDevices();
            var targetDevices = deviceNames.Count == 0
                ? allDevices
                : allDevices.Where(d => deviceNames.Contains(d.Name)).ToList();

            var summaries = new List<object>();
            var allInfos = new List<object>();

            foreach (var dev in targetDevices)
            {
                foreach (var pt in dev.DataPoints)
                {
                    var records = await _db.QueryHistoryAsync(dev.Name, pt.Name, startTime, endTime, 5000);
                    if (records.Count == 0) continue;

                    var numeric = records
                        .Select(r => { double v; return double.TryParse(r.value, out v) ? (double?)v : null; })
                        .Where(v => v.HasValue).Select(v => v.Value).ToList();

                    var info = new Dictionary<string, object>
                    {
                        ["device"] = dev.Name,
                        ["variable"] = pt.Name,
                        ["count"] = records.Count
                    };

                    if (numeric.Count > 0)
                    {
                        info["avg"] = Math.Round(numeric.Average(), 4);
                        info["min"] = numeric.Min();
                        info["max"] = numeric.Max();
                        info["last"] = numeric.Last();
                        info["unit"] = pt.Unit;
                    }

                    allInfos.Add(info);
                }
            }

            // 汇总
            int totalRecords = allInfos.Sum(i => (int)((Dictionary<string, object>)i).GetValueOrDefault("count", 0));

            summaries.Add(new
            {
                date,
                device_count = targetDevices.Count,
                total_records = totalRecords,
                generated_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });

            // 输出处理
            string outputPath = "";
            if (!string.IsNullOrEmpty(output?.Path))
            {
                outputPath = output.Path;
                await GenerateDailyReportHtml(summaries, allInfos, outputPath);
            }
            else if (!string.IsNullOrEmpty(GetString(p, "output_path")))
            {
                outputPath = GetString(p, "output_path");
                await GenerateDailyReportHtml(summaries, allInfos, outputPath);
            }

            return new
            {
                summary = summaries,
                details = allInfos,
                output_path = string.IsNullOrEmpty(outputPath) ? null : outputPath
            };
        }

        /// <summary>时序异常模式识别</summary>
        private async Task<object> AnomalyDetectAsync(Dictionary<string, object> p, string timeRange)
        {
            string deviceName = GetString(p, "device_name");
            string variable = GetString(p, "variable");
            string method = GetString(p, "method", "all");
            double sigma = GetDouble(p, "threshold_sigma", 3.0);

            var (start, end) = ParseTimeRange(timeRange);
            var records = await _db.QueryHistoryAsync(deviceName, variable, start, end, 10000);

            if (records.Count < 3)
                return new { message = "数据不足，至少需要 3 条记录", device = deviceName, variable };

            var sorted = records.OrderBy(r => r.timestamp).ToList();
            var values = new List<double>();
            var timestamps = new List<string>();
            foreach (var r in sorted)
            {
                if (double.TryParse(r.value, out double v))
                { values.Add(v); timestamps.Add(r.timestamp); }
            }

            if (values.Count < 3)
                return new { message = "有效数值数据不足", device = deviceName, variable };

            double mean = values.Average();
            double stddev = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
            if (stddev < 1e-10) stddev = 1e-10;

            var spikes = new List<object>();
            var driftPoints = new List<object>();
            var periodicAnomalies = new List<object>();

            // spike 检测：连续差分超过 sigma*stddev
            if (method == "spike" || method == "all")
            {
                for (int i = 1; i < values.Count; i++)
                {
                    double diff = Math.Abs(values[i] - values[i - 1]);
                    if (diff > sigma * stddev)
                    {
                        spikes.Add(new
                        {
                            timestamp = timestamps[i],
                            value = values[i],
                            prev_value = values[i - 1],
                            change = Math.Round(values[i] - values[i - 1], 4),
                            sigma_deviation = Math.Round(diff / stddev, 2),
                            type = "spike"
                        });
                    }
                }
            }

            // drift 检测：累积偏离均值持续增长
            if (method == "drift" || method == "all")
            {
                double cumDev = 0;
                double maxCumDev = 0;
                int driftStart = -1;
                for (int i = 0; i < values.Count; i++)
                {
                    cumDev += (values[i] - mean) / stddev;
                    if (Math.Abs(cumDev) > Math.Abs(maxCumDev))
                    {
                        maxCumDev = cumDev;
                        if (driftStart < 0) driftStart = i;
                    }
                }
                if (Math.Abs(maxCumDev) > sigma * 2)
                {
                    driftPoints.Add(new
                    {
                        start_time = timestamps[driftStart],
                        end_time = timestamps[values.Count - 1],
                        cumulative_deviation = Math.Round(maxCumDev, 2),
                        direction = maxCumDev > 0 ? "上升漂移" : "下降漂移",
                        type = "drift"
                    });
                }
            }

            // 周期异常：自相关检测周期性峰值
            if ((method == "periodic" || method == "all") && values.Count > 10)
            {
                var residuals = values.Select(v => v - mean).ToList();
                double acfThreshold = 0.3;
                var peaksFound = new List<int>();
                for (int lag = 2; lag <= Math.Min(values.Count / 3, 20); lag++)
                {
                    double num = 0, den = 0;
                    for (int i = 0; i < residuals.Count - lag; i++)
                    { num += residuals[i] * residuals[i + lag]; den += residuals[i] * residuals[i]; }
                    double acf = den > 0 ? num / den : 0;
                    if (Math.Abs(acf) > acfThreshold)
                        peaksFound.Add(lag);
                }
                if (peaksFound.Count > 0)
                {
                    int dominantLag = peaksFound.OrderByDescending(l => l).First();
                    periodicAnomalies.Add(new
                    {
                        detected_period = dominantLag,
                        period_seconds_estimate = dominantLag * 5,
                        autocorrelation_peaks = peaksFound.Count,
                        significance = peaksFound.Count > 1 ? "high" : "medium",
                        type = "periodic"
                    });
                }
            }

            return new
            {
                device = deviceName,
                variable,
                record_count = values.Count,
                statistics = new { mean = Math.Round(mean, 4), stddev = Math.Round(stddev, 4) },
                spike_count = spikes.Count,
                spikes,
                drift_detected = driftPoints.Count > 0,
                drift_points = driftPoints,
                periodic_anomalies = periodicAnomalies
            };
        }

        /// <summary>多变量相关性分析（皮尔逊矩阵）</summary>
        private async Task<object> CorrelationAsync(Dictionary<string, object> p, string timeRange)
        {
            string deviceName = GetString(p, "device_name");
            var variables = GetList(p, "variables", new List<string>());

            if (variables.Count < 2)
                return new { message = "至少需要 2 个变量进行相关性分析" };

            var (start, end) = ParseTimeRange(timeRange);

            // 按 timestamp 对齐多变量数据
            var varData = new Dictionary<string, Dictionary<string, double>>();

            foreach (var vr in variables)
            {
                varData[vr] = new Dictionary<string, double>();
                var records = await _db.QueryHistoryAsync(deviceName, vr, start, end, 10000);
                foreach (var r in records)
                {
                    if (double.TryParse(r.value, out double v))
                        varData[vr][r.timestamp] = v;
                }
            }

            var matrix = new List<object>();
            var pairs = new List<object>();

            for (int i = 0; i < variables.Count; i++)
            {
                for (int j = 0; j < variables.Count; j++)
                {
                    if (i > j) continue;
                    var xVals = new List<double>();
                    var yVals = new List<double>();

                    foreach (var ts in varData[variables[i]].Keys)
                    {
                        if (varData[variables[j]].ContainsKey(ts))
                        {
                            xVals.Add(varData[variables[i]][ts]);
                            yVals.Add(varData[variables[j]][ts]);
                        }
                    }

                    double r = 0;
                    if (xVals.Count >= 3)
                    {
                        int n = xVals.Count;
                        double sx = xVals.Sum(), sy = yVals.Sum();
                        double sxy = 0, sx2 = 0, sy2 = 0;
                        for (int k = 0; k < n; k++)
                        {
                            sxy += xVals[k] * yVals[k];
                            sx2 += xVals[k] * xVals[k];
                            sy2 += yVals[k] * yVals[k];
                        }
                        double num = n * sxy - sx * sy;
                        double den = Math.Sqrt((n * sx2 - sx * sx) * (n * sy2 - sy * sy));
                        r = den > 0 ? num / den : 0;
                    }

                    string strength = Math.Abs(r) >= 0.8 ? "极强" : Math.Abs(r) >= 0.6 ? "强" : Math.Abs(r) >= 0.4 ? "中等" : Math.Abs(r) >= 0.2 ? "弱" : "极弱";
                    string rel = r > 0 ? "正" : "负";

                    var entry = new { var_x = variables[i], var_y = variables[j], coefficient = Math.Round(r, 4), strength = strength + rel + "相关", sample_count = xVals.Count };
                    matrix.Add(entry);
                    if (i != j) pairs.Add(entry);
                }
            }

            var strongest = pairs.OrderByDescending(pp =>
            {
                var type = p.GetType();
                return Math.Abs((double)(type.GetProperty("coefficient")?.GetValue(p) ?? 0.0));
            }).Take(3).ToList();

            return new
            {
                device = deviceName,
                variable_count = variables.Count,
                correlation_matrix = matrix,
                strongest_pairs = strongest
            };
        }

        /// <summary>根因定位</summary>
        private async Task<object> RootCauseAsync(Dictionary<string, object> p, string timeRange)
        {
            string deviceName = GetString(p, "device_name");
            string targetVar = GetString(p, "target_variable");
            var candidateVars = GetList(p, "candidate_variables", new List<string>());
            int maxLag = GetInt(p, "max_lag_seconds", 60);

            var (start, end) = ParseTimeRange(timeRange);

            var targetData = new List<(string ts, double val)>();
            var targetRecords = await _db.QueryHistoryAsync(deviceName, targetVar, start, end, 10000);
            foreach (var r in targetRecords.OrderBy(r => r.timestamp))
            {
                if (double.TryParse(r.value, out double v))
                    targetData.Add((r.timestamp, v));
            }

            if (targetData.Count < 3)
                return new { message = "目标变量数据不足", device = deviceName, target_variable = targetVar };

            if (candidateVars.Count == 0)
            {
                var deviceCfg = ConfigService.Instance.GetAllDevices().FirstOrDefault(d => d.Name == deviceName);
                if (deviceCfg != null)
                    candidateVars = deviceCfg.DataPoints.Select(pt => pt.Name).Where(n => n != targetVar).ToList();
            }

            var results = new List<object>();

            foreach (var cv in candidateVars)
            {
                if (cv == targetVar) continue;

                var candData = new Dictionary<string, double>();
                var candRecords = await _db.QueryHistoryAsync(deviceName, cv, start, end, 10000);
                foreach (var r in candRecords)
                {
                    if (double.TryParse(r.value, out double v))
                        candData[r.timestamp] = v;
                }

                if (candData.Count < 3) continue;

                double r0 = CalcPearson(targetData, candData, 0);
                double bestR = r0;
                int bestLag = 0;

                int maxLagSteps = maxLag / 5;
                for (int lag = -maxLagSteps; lag <= maxLagSteps; lag++)
                {
                    if (lag == 0) continue;
                    double rl = CalcPearson(targetData, candData, lag);
                    if (Math.Abs(rl) > Math.Abs(bestR))
                    { bestR = rl; bestLag = lag * 5; }
                }

                string strength = Math.Abs(bestR) >= 0.8 ? "极强" : Math.Abs(bestR) >= 0.6 ? "强" : Math.Abs(bestR) >= 0.4 ? "中等" : Math.Abs(bestR) >= 0.2 ? "弱" : "极弱";
                string dir = bestR > 0 ? "正" : "负";
                string leadLag = bestLag == 0 ? "同步" : bestLag > 0 ? "领先" + Math.Abs(bestLag) + "s" : "滞后" + Math.Abs(bestLag) + "s";

                results.Add(new
                {
                    variable = cv,
                    correlation = Math.Round(bestR, 4),
                    abs_correlation = Math.Round(Math.Abs(bestR), 4),
                    lag_seconds = bestLag,
                    strength = strength + dir + "相关",
                    lead_lag = leadLag
                });
            }

            var ranked = results.OrderByDescending(rr =>
            {
                var type = rr.GetType();
                return (double)(type.GetProperty("abs_correlation")?.GetValue(rr) ?? 0.0);
            }).ToList();

            return new
            {
                device = deviceName,
                target_variable = targetVar,
                target_record_count = targetData.Count,
                candidates_analyzed = results.Count,
                root_cause_candidates = ranked
            };
        }

        /// <summary>简单预测（线性回归外推）</summary>
        private async Task<object> PredictAsync(Dictionary<string, object> p, string timeRange)
        {
            string deviceName = GetString(p, "device_name");
            string variable = GetString(p, "variable");
            int predictMin = GetInt(p, "predict_minutes", 10);

            var (start, end) = ParseTimeRange(timeRange);
            var records = await _db.QueryHistoryAsync(deviceName, variable, start, end, 10000);

            if (records.Count < 3)
                return new { message = "数据不足，至少需要 3 条记录进行预测", device = deviceName, variable };

            var sorted = records.OrderBy(r => r.timestamp).ToList();
            var points = new List<(double x, double y)>();
            foreach (var r in sorted)
            {
                if (DateTime.TryParse(r.timestamp, out DateTime dt) && double.TryParse(r.value, out double val))
                    points.Add((new DateTimeOffset(dt).ToUnixTimeMilliseconds() / 1000.0, val));
            }

            if (points.Count < 3)
                return new { message = "有效数值数据不足", device = deviceName, variable };

            int n = points.Count;
            double sx = 0, sy = 0, sxy = 0, sx2 = 0;
            for (int i = 0; i < n; i++)
            {
                sx += points[i].x; sy += points[i].y;
                sxy += points[i].x * points[i].y;
                sx2 += points[i].x * points[i].x;
            }

            double slope = (n * sxy - sx * sy) / (n * sx2 - sx * sx);
            double intercept = (sy - slope * sx) / n;

            // R² 和残差标准差
            double meanY = points.Average(pt => pt.y);
            double ssRes = 0, ssTot = 0;
            foreach (var pt in points)
            {
                double pred = slope * pt.x + intercept;
                ssRes += (pt.y - pred) * (pt.y - pred);
                ssTot += (pt.y - meanY) * (pt.y - meanY);
            }
            double r2 = ssTot > 0 ? 1 - ssRes / ssTot : 0;
            double rmse = Math.Sqrt(ssRes / n);

            // 预测
            double lastX = points.Last().x;
            int predictSteps = predictMin < 1 ? 6 : predictMin * 60 / 5;
            double stepSize = (points.Last().x - points.First().x) / n;
            if (stepSize < 1) stepSize = 5;

            var predictions = new List<object>();
            var now = DateTime.Now;
            for (int i = 1; i <= predictSteps; i++)
            {
                double futX = lastX + i * stepSize;
                double futY = slope * futX + intercept;
                predictions.Add(new
                {
                    timestamp = now.AddSeconds(i * stepSize).ToString("HH:mm:ss"),
                    predicted_value = Math.Round(futY, 4),
                    lower_bound = Math.Round(futY - 1.96 * rmse, 4),
                    upper_bound = Math.Round(futY + 1.96 * rmse, 4)
                });
            }

            string direction = Math.Abs(slope) < 0.0001 ? "平稳" : slope > 0 ? "上升" : "下降";

            return new
            {
                device = deviceName,
                variable,
                history_count = n,
                model = new
                {
                    slope = Math.Round(slope, 6),
                    intercept = Math.Round(intercept, 4),
                    r_squared = Math.Round(r2, 4),
                    rmse = Math.Round(rmse, 4),
                    direction
                },
                predict_minutes = predictMin,
                predictions
            };
        }

        /// <summary>皮尔逊相关系数（含时滞）</summary>
        private static double CalcPearson(List<(string ts, double val)> target, Dictionary<string, double> candDict, int lagSteps)
        {
            var xVals = new List<double>();
            var yVals = new List<double>();

            for (int i = 0; i < target.Count - Math.Abs(lagSteps); i++)
            {
                if (lagSteps >= 0)
                {
                    if (i + lagSteps < target.Count && candDict.TryGetValue(target[i + lagSteps].ts, out double cv))
                    { xVals.Add(target[i].val); yVals.Add(cv); }
                }
                else
                {
                    int j = i - lagSteps;
                    if (j < target.Count && candDict.TryGetValue(target[i].ts, out double cv))
                    { xVals.Add(target[j].val); yVals.Add(cv); }
                }
            }

            if (xVals.Count < 3) return 0;

            int n = xVals.Count;
            double sx = 0, sy = 0, sxy = 0, sx2 = 0, sy2 = 0;
            for (int i = 0; i < n; i++)
            { sx += xVals[i]; sy += yVals[i]; sxy += xVals[i] * yVals[i]; sx2 += xVals[i] * xVals[i]; sy2 += yVals[i] * yVals[i]; }

            double num = n * sxy - sx * sy;
            double den = Math.Sqrt((n * sx2 - sx * sx) * (n * sy2 - sy * sy));
            return den > 0 ? num / den : 0;
        }

        #endregion

        #region 辅助方法

        private static async Task GenerateDailyReportHtml(List<object> summaries, List<object> details, string path)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>生产日报</title>");
                sb.AppendLine("<style>body{font-family:Microsoft YaHei,sans-serif;margin:20px;color:#333;}");
                sb.AppendLine("h1{color:#1a73e8;}table{border-collapse:collapse;width:100%;margin:10px 0;}");
                sb.AppendLine("th,td{border:1px solid #ddd;padding:8px;text-align:left;}th{background:#f5f5f5;}");
                sb.AppendLine("</style></head><body>");
                sb.AppendLine("<h1>📊 生产日报</h1>");

                foreach (var summary in summaries)
                {
                    var s = (Dictionary<string, object>)summary;
                    sb.AppendFormat("<p><b>日期:</b> {0} | <b>设备数:</b> {1} | <b>总记录:</b> {2} | <b>生成时间:</b> {3}</p>",
                        s["date"], s["device_count"], s["total_records"], s["generated_at"]);
                }

                sb.AppendLine("<h2>变量统计</h2><table><tr><th>设备</th><th>变量</th><th>记录数</th><th>均值</th><th>最小值</th><th>最大值</th><th>最新值</th><th>单位</th></tr>");
                foreach (var info in details)
                {
                    var d = (Dictionary<string, object>)info;
                    sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td></tr>",
                        d.GetValueOrDefault("device"), d.GetValueOrDefault("variable"), d.GetValueOrDefault("count"),
                        d.GetValueOrDefault("avg"), d.GetValueOrDefault("min"), d.GetValueOrDefault("max"),
                        d.GetValueOrDefault("last"), d.GetValueOrDefault("unit"));
                }
                sb.AppendLine("</table></body></html>");

                await Task.Run(() => System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Logger.Error("生成日报 HTML 失败: " + ex.Message);
            }
        }

        private static (string start, string end) ParseTimeRange(string timeRange)
        {
            if (string.IsNullOrEmpty(timeRange) || timeRange == "realtime")
                return ("", "");

            // 精确范围 "2026-06-30 08:00/2026-06-30 20:00"
            if (timeRange.Contains("/"))
            {
                var parts = timeRange.Split('/');
                return (parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : "");
            }

            // 相对时间 "5m" "1h" "24h" "7d"
            DateTime now = DateTime.Now;
            char unit = timeRange[timeRange.Length - 1];
            int value;
            if (!int.TryParse(timeRange.Substring(0, timeRange.Length - 1), out value))
                value = 1;

            DateTime start;
            switch (unit)
            {
                case 'm': start = now.AddMinutes(-value); break;
                case 'h': start = now.AddHours(-value); break;
                case 'd': start = now.AddDays(-value); break;
                default: start = now.AddHours(-1); break;
            }

            return (start.ToString("yyyy-MM-dd HH:mm:ss"), now.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private static long ParseTimestamp(string ts)
        {
            if (DateTime.TryParse(ts, out var dt))
                return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
            return 0;
        }

        private static string GetString(Dictionary<string, object> d, string key, string def = "")
        {
            if (d.TryGetValue(key, out var v) && v != null) return v.ToString();
            return def;
        }

        private static int GetInt(Dictionary<string, object> d, string key, int def = 0)
        {
            if (d.TryGetValue(key, out var v) && v != null && int.TryParse(v.ToString(), out var iv))
                return iv;
            return def;
        }

        private static double GetDouble(Dictionary<string, object> d, string key, double def = 0)
        {
            if (d.TryGetValue(key, out var v) && v != null && double.TryParse(v.ToString(), out var dv))
                return dv;
            return def;
        }

        private static double? GetDoubleOrNull(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v != null && double.TryParse(v.ToString(), out var dv))
                return dv;
            return null;
        }

        private static List<string> GetList(Dictionary<string, object> d, string key, List<string> def)
        {
            if (d.TryGetValue(key, out var v) && v != null)
            {
                if (v is Newtonsoft.Json.Linq.JArray arr)
                    return arr.Select(x => x.ToString()).ToList();
            }
            return def ?? new List<string>();
        }

        #endregion
    }
}

internal static class DictHelper
{
    public static object GetValueOrDefault(this Dictionary<string, object> d, string key, object def = null)
    {
        return d.TryGetValue(key, out var v) ? v : def;
    }
}
