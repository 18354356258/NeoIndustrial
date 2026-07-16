using System;
using System.Text;

namespace IndustrialDataCollection.Services
{
    public class McpPlatformIntroService
    {
        // 驱动总数，暂时硬编码
        private const int DriverCount = 41;
        private const int ToolCount = 48;

        public string IntroducePlatform(string topic = "overview")
        {
            switch (topic?.ToLowerInvariant())
            {
                case "drivers": return GetDriversIntro();
                case "tools": return GetToolsIntro();
                case "semantic": return GetSemanticIntro();
                case "fabric": return GetFabricIntro();
                case "tdengine": return GetTdengineIntro();
                case "best_practices": return GetBestPractices();
                default: return GetOverview();
            }
        }

        private string GetOverview()
        {
            return string.Join("\n",
                "╔═══════════════════════════════════════════════════════════════════════════════════╗",
                "║ IndustrialDataCollector v2.5.1 - 工业数采 AI 平台                              ║",
                "╠═══════════════════════════════════════════════════════════════════════════════════╣",
                "║                                                                                 ║",
                "║  【核心能力】                                                                   ║",
                "║  • 41 种工业通信驱动                                                           ║",
                "║    覆盖：西门子/三菱/Modbus/OPC UA/CNC/楼宇/电力/仪表/现场总线/物联网           ║",
                "║                                                                                 ║",
                "║  • 48 个 MCP 工具                                                              ║",
                "║    分类：设备管理 / 变量管理 / 数据查询 / 语义查询 / 语义写入                   ║",
                "║          数据源分析 / Fabric分析 / 数据库直查                                   ║",
                "║                                                                                 ║",
                "║  • 语义建模体系                                                                 ║",
                "║    车间 → 产线 → 设备 → 变量 四级层级                                           ║",
                "║    17 种变量关系类型                                                            ║",
                "║                                                                                 ║",
                "║  • Fabric 分析引擎                                                              ║",
                "║    8 种分析算子：窗口聚合/趋势检测/阈值报警/生产日报                             ║",
                "║    异常检测/相关性矩阵/根因定位/预测                                             ║",
                "║                                                                                 ║",
                "║  • 5 种数据库                                                                    ║",
                "║    SQLite / MySQL / SQL Server / PostgreSQL / TDengine(时序)                     ║",
                "║                                                                                 ║",
                "║  【快速上手】                                                                    ║",
                "║  1. 用户说「加设备」→ 调用 add_device，自动识别协议类型                          ║",
                "║  2. 用户说「配变量」→ 调用 add_variables，批量创建                               ║",
                "║  3. 用户说「查数据」→ query_realtime_data / query_history_data                  ║",
                "║  4. 用户说「分析」→ 语义分析用 semantic_* / 深度分析用 fabric_execute            ║",
                "║  5. 用户说「日报」→ fabric_execute(daily_report)                                 ║",
                "║                                                                                 ║",
                "║  【约束说明】                                                                    ║",
                "║  ⚠ 设备删除、采集启停、变量关系删除请在 UI 操作                                  ║",
                "║  ⚠ 批量变量建议 200 个/次以内                                                    ║",
                "║                                                                                 ║",
                "║  【获取更详细指南】                                                              ║",
                "║  drivers / tools / semantic / fabric / tdengine / best_practices                 ║",
                "╚═══════════════════════════════════════════════════════════════════════════════════╝"
            );
        }

        private string GetDriversIntro()
        {
            return string.Join("\n",
                "╔═══════════════════════════════════════════════════════════════════════════════════╗",
                "║ 41 种工业通信驱动                                                               ║",
                "╠═══════════════════════════════════════════════════════════════════════════════════╣",
                "║ 【工业以太网 8 种】                                                             ║",
                "║ ModbusTCP / ModbusRTU / Siemens S7 / Siemens 840D                                ║",
                "║ OPC UA / OPC DA / OPC UA PubSub / EtherNet/IP                                   ║",
                "║                                                                                 ║",
                "║ 【CNC 数控 4 种】                                                               ║",
                "║ Fanuc FOCAS / Haas CNC / Mazak / Heidenhain                                     ║",
                "║                                                                                 ║",
                "║ 【日系 PLC 8 种】                                                               ║",
                "║ Mitsubishi FX / MELSEC MC / Keyence KV / Panasonic Mewtocol                     ║",
                "║ Omron FINS / HostLink / CODESYS(Modbus) / Beckhoff ADS(TwinCAT)                 ║",
                "║                                                                                 ║",
                "║ 【楼宇/电力/仪表 8 种】                                                          ║",
                "║ BACnet / IEC 61850 / IEC 104 / DNP3                                             ║",
                "║ DLMS(电表) / M-Bus(仪表总线) / KNX(楼宇) / DALI(照明)                           ║",
                "║                                                                                 ║",
                "║ 【现场总线 4 种】                                                               ║",
                "║ PROFIBUS(网关) / DeviceNet(CIP) / CC-Link / HART IP                             ║",
                "║                                                                                 ║",
                "║ 【物联网/中继 5 种】                                                             ║",
                "║ MQTT Subscribe / Sparkplug B / HTTP REST / SECS/GEM / MTConnect                 ║",
                "║                                                                                 ║",
                "║ 【其他 4 种】                                                                   ║",
                "║ Profinet / LonWorks / Simulator / 扩展自定义                                     ║",
                "╚═══════════════════════════════════════════════════════════════════════════════════╝"
            );
        }

        private string GetToolsIntro()
        {
            return string.Join("\n",
                "╔═══════════════════════════════════════════════════════════════════════════════════╗",
                "║ 48 个 MCP 工具分类说明                                                          ║",
                "╠═══════════════════════════════════════════════════════════════════════════════════╣",
                "║ 【设备管理 5 个】list_devices, get_device_config, get_device_status,            ║",
                "║   add_device, update_device                                                     ║",
                "║ 【变量管理 3 个】add_variables, update_variables, reload_config                  ║",
                "║ 【数据查询 4 个】query_realtime_data, query_history_data, get_database_status,    ║",
                "║   repair_database                                                                ║",
                "║ 【语义查询 11 个】semantic_list_workshops/production_lines/equipments/tags,       ║",
                "║   get_node_path, get_full_tree, list/device_variables, variable_relations,        ║",
                "║   node_relations, history_source, suggest_relations                             ║",
                "║ 【实时快照 7 个】semantic_get_realtime_snapshot, alarm_summary, data_flow,        ║",
                "║   upstream, downstream, impact_graph, suggest_relations                         ║",
                "║ 【语义写入 5 个】semantic_create_variable_relation, create_event_config,          ║",
                "║   execute_query, batch_update_nodes, list_events                                ║",
                "║ 【数据源分析 v2.5.1 4 个】datasource_list_all, table_info, latest_data,           ║",
                "║   query_timerange（适配 MySQL/SQLite/SQLSvr/PGSQL/TDengine/Oracle）              ║",
                "║ 【Fabric分析 2 个】fabric_list_operators, fabric_execute (8种算子)                ║",
                "║ 【数据库直查 v2.5.2】IDbAdapter 统一接口，DataSourceRouter 自动路由               ║",
                "║ 【平台自描述 v2.5.1 1 个】introduce_platform                                      ║",
                "╚═══════════════════════════════════════════════════════════════════════════════════╝"
            );
        }

        private string GetSemanticIntro()
        {
            return string.Join("\n",
                "╔═══════════════════════════════════════════════════════════════════════════════════╗",
                "║ 语义建模体系 - 使用指南                                                          ║",
                "╠═══════════════════════════════════════════════════════════════════════════════════╣",
                "║ 【核心概念】语义层让 AI 理解工业数据的含义，而非仅仅看到数值                     ║",
                "║ 层级：车间 → 产线 → 设备 → 变量（Tag）                                          ║",
                "║ 示例：一车间 → 挤压产线 → 28号挤压机 → 料筒温度                                 ║",
                "║ 【17 种变量关系类型】上限/下限/目标值/标准值/SOP步骤/质量判定/报警阈值/          ║",
                "║   补偿系数/计算公式/参考变量/业务关联/影响/约束/计算来源/关联设备/历史数据源      ║",
                "║ 【使用方式】右键空白区域添加文件夹建立层级 → 拖拽设备到语义树节点 →             ║",
                "║   变量自动同步 → 右键变量添加关系                                                ║",
                "║ 【注意事项】⚠ 语义层修改不反向写回设备分组                                        ║",
                "║   ⚠ 变量关系删除请在 UI 操作（MCP 已移除删除工具）                                ║",
                "╚═══════════════════════════════════════════════════════════════════════════════════╝"
            );
        }

        private string GetFabricIntro()
        {
            return string.Join("\n",
                "╔═══════════════════════════════════════════════════════════════════════════════════╗",
                "║ Fabric 分析引擎 - 8 种分析算子                                                   ║",
                "╠═══════════════════════════════════════════════════════════════════════════════════╣",
                "║ 1. window_aggregate — 滑动窗口聚合                                               ║",
                "║ 2. trend_detect — 趋势检测（线性回归）                                           ║",
                "║ 3. threshold_alarm — 统计阈值报警（μ±kσ）                                        ║",
                "║ 4. daily_report — 生产日报（HTML 格式）                                          ║",
                "║ 5. anomaly_detect — 异常模式识别（spike/drift/periodic）                         ║",
                "║ 6. correlation — 皮尔逊相关性矩阵                                                ║",
                "║ 7. root_cause — 根因定位（时滞相关性扫描）                                       ║",
                "║ 8. predict — 线性回归外推预测                                                    ║",
                "║ 【使用方式】AI 调用 fabric_list_operators → fabric_execute → 返回分析结果        ║",
                "╚═══════════════════════════════════════════════════════════════════════════════════╝"
            );
        }

        private string GetTdengineIntro()
        {
            return string.Join("\n",
                "╔═══════════════════════════════════════════════════════════════════════════════════╗",
                "║ TDengine 时序数据库 - 踩坑与最佳实践                                            ║",
                "╠═══════════════════════════════════════════════════════════════════════════════════╣",
                "║ 【核心陷阱 1：ts 主键碰撞】⚠️ 最重要！                                         ║",
                "║ TDengine 正常表(Normal Table)以第一列 TIMESTAMP 列为主键。                       ║",
                "║ 同一设备多变量采集时，如果 8 个变量共享同一个 batch.Timestamp，                  ║",
                "║ INSERT 后 TDengine 只保留最后一条——前 7 条被无声覆盖，                          ║",
                "║ 且不报任何错误（REST API code=0）。                                              ║",
                "║ 解决方案：每行 INSERT 加 1ms 偏移 dt.AddMilliseconds(rowIdx++)                   ║",
                "║ 对比：SQLite/MySQL/SQL Server/PostgreSQL 用自增 ID 做主键，不受此影响。          ║",
                "║                                                                                 ║",
                "║ 【核心陷阱 2：DDL 非标准】                                                      ║",
                "║ • TDengine 2.x 不支持 IF NOT EXISTS（3.x 部分支持）                              ║",
                "║ • 无 IDENTITY / AUTO_INCREMENT / SERIAL 自增列                                   ║",
                "║ • NVARCHAR / VARCHAR 应改用 NCHAR(n) / BINARY(n)                                ║",
                "║ • CREATE TABLE 模板不能复用 SQLite/MySQL/SQLSvr/PGSQL 的                         ║",
                "║                                                                                 ║",
                "║ 【核心陷阱 3：字段名映射】                                                       ║",
                "║ 应用层 SQL 使用标准列名（timestamp / value / tag），                              ║",
                "║ TDengine 实际列名（ts / val / tag_id），需 MapTdField 转换。                    ║",
                "║ • MapTdField(\"value\") → \"val\"                                                 ║",
                "║ • MapTdField(\"tag\") → \"tag_id\"                                                 ║",
                "║ • MapTdField(\"timestamp\") → \"ts\"                                              ║",
                "║ 查询时需反向替换：Replace(\"timestamp\", \"ts\")                                    ║",
                "║                                                                                 ║",
                "║ 【核心陷阱 4：REST API 无连接池】                                                ║",
                "║ TDengine 通过 HTTP REST 交互（非 ADO.NET 驱动），每个 ExecuteNonQuery            ║",
                "║ 发起一次 HTTP POST。无 TCP 长连接概念，不需要 DbConnection 生命周期管理。        ║",
                "║ 断连检测：指数退避 min(2^(n-1), 60s)，成功自动清零。                             ║",
                "║                                                                                 ║",
                "║ 【查询注意事项】                                                                 ║",
                "║ • WHERE 条件必须包含 ts 列（TDengine 强制要求）                                  ║",
                "║ • ORDER BY timestamp 在 TDengine 内部自动转为 ORDER BY ts                       ║",
                "║ • GROUP BY 支持按 tag_id 等 NCHAR 列分组                                        ║",
                "║ • 适合日均千万级数据点、面向时序分析和降采样                                     ║",
                "╚═══════════════════════════════════════════════════════════════════════════════════╝"
            );
        }

        private string GetBestPractices()
        {
            return string.Join("\n",
                "╔═══════════════════════════════════════════════════════════════════════════════════╗",
                "║ 最佳实践                                                                         ║",
                "╠═══════════════════════════════════════════════════════════════════════════════════╣",
                "║ • 设备名称用中文+编号（如「28号挤压机」），AI 识别更准                            ║",
                "║ • 变量名称用中文（如「料筒温度」而非「Temp_01」）                                  ║",
                "║ • 批量变量建议 200 个/次以内                                                      ║",
                "║ • 启用边缘计算 9 种清洗策略减少冗余数据                                           ║",
                "║ • 定期检查配置 10 代备份（Ctrl+Z 快速回退）                                       ║",
                "║ • TDengine 时序专属，踩坑指南见 introduce_platform(tdengine)                       ║",
                "║ • MQTT 推荐使用 Resolved 模式（批量+逐变量子话题）                                ║",
                "║ • 高危操作（删除/启停）请通过 UI 安全闸门                                          ║\n║ 【连接策略】MCP 端口自动抢占 5100→5110，连接失败时依次漂移重试，Token 不变（最新的token是啥就用啥） ║",
                "╚═══════════════════════════════════════════════════════════════════════════════════╝"
            );
        }
    }
}
