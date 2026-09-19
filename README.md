<div align="center">

# 18354356258

**把车间的机器，翻译成 AI 能读懂的知识。**

工业设备五花八门，协议各自为政。我做的事只有一件：**把 39 种工业协议接进来，把数据整理成机器能理解、人能信任的语义本体。**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Blazor](https://img.shields.io/badge/Blazor_Server-InteractiveServer-512BD4?logo=blazor&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![SQLite](https://img.shields.io/badge/SQLite-003B57?logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![MCP](https://img.shields.io/badge/MCP-Model_Context_Protocol-3B82F6)](https://modelcontextprotocol.io/)
[![Linux](https://img.shields.io/badge/Linux-x64%20%7C%20arm64-FCC624?logo=linux&logoColor=black)](https://www.kernel.org/)

</div>

---

## 🏭 代表项目 · NeoIndustrial

> **工业数据采集与语义治理平台** —— 从设备协议接入、时序落库，到语义本体与知识图谱的一条完整闭环。
> 闭源商用交付，Windows 试点包与信创自测包双包发布。

| 能力域 | 已落地的事实 |
| --- | --- |
| **协议接入** | 内置 **39 种工业协议驱动，分 7 大类**：工业常用 / PLC / CNC / 楼宇自动化 / 电力能源 / 半导体 / 通用。覆盖 Modbus TCP·RTU、西门子 S7、OPC UA·UA PubSub·OPC DA、三菱 MC·FX、欧姆龙 FINS·HostLink、基恩士 KV、IEC 104、IEC 61850、DNP3、BACnet、EtherNet/IP、PROFIBUS、DeviceNet、CC-Link、HART-IP、MQTT·Sparkplug B、MTConnect、SECS/GEM、Fanuc FOCAS、CODESYS、Beckhoff ADS、Heidenhain、KNX、DALI、M-Bus、DLMS、SNMP、Haas·Mazak 机床、HttpRest、模拟器 |
| **AI · MCP** | 内置 **150 个 MCP 工具**（AI 对话侧可见 140 个，其中可写 59 个）。AI 可自助完成语义/本体维护，甚至**联网检索后自行安装驱动**；写工具受三层闸门管控 |
| **数据落库** | 关系型 / 时序 / 国产信创 / 嵌入式·文件型 / 其他 **五大类数据库驱动**，含 MySQL、SQL Server、PostgreSQL、**Oracle（支持模式/schema 选择）**、达梦、人大金仓、SQLite、TDengine |
| **语义与图谱** | 语义本体 + 三维球面布局图谱预览；关系类型三级目录 **8 大类 / 48 族 / 268 细分**，支持自定义与变更留痕 |
| **多智能体** | 内置总控 Hermes 与虚拟子智能体派单，把长链路任务拆给专门角色执行 |
| **信创适配** | 银河麒麟 / 统信 UOS；**x64（兆芯·海光·Intel）与 arm64（飞腾·鲲鹏）双架构**，Linux 自包含发布 |
| **交付形态** | Windows 试点包 + 信创自测包，含校验值、交付说明、第三方组件许可清单；界面三语（简体中文 / English / Tiếng Việt） |
| **架构特征** | 驱动插件化（上传 / AI 安装 / 影子副本加载 / 启停 / 删除）、数据库驱动插件化、AI 写工具三层闸门、Service Worker 缓存版本管理 |

**📦 项目仓库（GitHub / Gitee 同名双仓）**
[![GitHub](https://img.shields.io/badge/GitHub-NeoIndustrial-181717?logo=github&logoColor=white)](https://github.com/18354356258/NeoIndustrial)
[![Gitee](https://img.shields.io/badge/Gitee-JEDI__MASTER-C71D23?logo=gitee&logoColor=white)](https://gitee.com/JEDI_MASTER/neoIndustrial)

---

## 🧩 技术栈

- **工业协议** — PLC / CNC / 电力 / 楼宇 / 半导体 / 通用，共 39 种驱动，开源实现与平台自研并行
- **后端与前端** — C# / .NET 8、Blazor Server（InteractiveServer 逐页交互模式）、插件化驱动加载
- **数据** — 关系型 + 时序 + 信创 + 嵌入式多形态落库；SQLite 承载元数据
- **AI · 工具化** — MCP 服务端（150 工具）、多智能体协同、AI 驱动的语义与本体自主维护
- **信创与交付** — 麒麟 / 统信，x64 + arm64，Linux 自包含发布，双包交付与校验清单
- **工程方法** — AI 结对 / 集群化开发；自建验收流水线：隔离实例 + 浏览器端到端 CDP 验收 + 几何量断言 + 生产只读复验

---

## 📊 GitHub

<div align="center">

![Stats](https://github-readme-stats.vercel.app/api?username=18354356258&show_icons=true&theme=ocean_dark&hide_border=true&count_private=true)
![Top Langs](https://github-readme-stats.vercel.app/api/top-langs/?username=18354356258&layout=compact&theme=ocean_dark&hide_border=true)

</div>

---

## 🎯 正在做什么

- 让工业现场的**接入成本继续下降**：驱动越插件化，新设备上线就越接近"上传即可"
- 让**语义层真正可用**：从"采到数据"走向"数据自带含义"，让 AI 能基于本体而不是猜来做事
- 让**信创环境下的交付**保持同等完整度：双架构、三语界面、可核验的交付清单

> 工业软件不缺炫技的功能列表，缺的是**每一台设备、每一种协议、每一次交付都能被复现和核验**。

<div align="center">

**接得进、看得懂、交付得出。**

</div>

<!--
==================== 事实核对清单（发布前逐条核对，勿删） ====================
1) GitHub 账号 18354356258 / Gitee 账号 JEDI_MASTER
   → 来源：用户提供（同名项目双仓库发布）
2) 项目名 NeoIndustrial；定位「工业数据采集与语义治理平台」；闭源商用交付
   → 来源：用户提供
3) 39 种工业协议驱动 / 7 大类（工业常用·PLC·CNC·楼宇自动化·电力能源·半导体·通用）
   → 来源：用户提供（已实测数字）
4) 协议清单（Modbus TCP·RTU、S7、OPC UA·UA PubSub·OPC DA、三菱 MC·FX、欧姆龙 FINS·HostLink、
   基恩士 KV、IEC 104、IEC 61850、DNP3、BACnet、EtherNet/IP、PROFIBUS、DeviceNet、CC-Link、
   HART-IP、MQTT·Sparkplug B、MTConnect、SECS/GEM、Fanuc FOCAS、CODESYS、Beckhoff ADS、
   Heidenhain、KNX、DALI、M-Bus、DLMS、SNMP、Haas·Mazak、HttpRest、模拟器）
   → 来源：用户提供的协议举例清单（未新增任何未列出的协议名）
   ※ 清单为「举例」，正文已注明 39 种总数，列举项少于 39，属正常，不得据列举项反推总数
5) MCP 工具 150 个 / AI 对话侧可见 140 个 / 可写 59 个
   → 来源：用户提供（已实测数字）
6) 数据库五大类：关系型 / 时序 / 国产信创 / 嵌入式·文件型 / 其他（源码 `DbDriverCategories.Order` = relational/timeseries/domestic/embedded/other）
   → 来源：用户提供；示例含 MySQL、SQL Server、PostgreSQL、Oracle（含模式/schema 选择）、
     达梦、人大金仓、SQLite、TDengine
   ※ 核对提示（已闭环 · 2026-09-19）：数据库五类名已列全 —— 关系型 / 时序 / 国产信创 / 嵌入式·文件型 / 其他，依据源码 `DbDriverCategories.Order` = relational/timeseries/domestic/embedded/other。 + "等"，若与实际分类命名不符请以源码口径为准修正
7) 知识图谱：语义本体 + 三维球面布局预览；关系类型三级目录 8 大类 / 48 族 / 268 细分
   → 来源：用户提供（已实测数字）
8) 多智能体协同：内置总控 Hermes + 虚拟子智能体派单
   → 来源：用户提供
9) 信创：银河麒麟 / 统信 UOS；x64（兆芯·海光·Intel）+ arm64（飞腾·鲲鹏）；Linux 自包含发布
   → 来源：用户提供
10) 界面三语：简体中文 / English / Tiếng Việt
   → 来源：用户提供
11) 交付：Windows 试点包 + 信创自测包双包；含校验值、交付说明、第三方组件许可清单
   → 来源：用户提供
12) 架构：驱动插件化（上传/AI安装/影子副本加载/启停/删除）、数据库驱动插件化、
    AI 写工具三层闸门、Service Worker 缓存版本管理
   → 来源：用户提供
13) 开发方式：AI 结对/集群化开发；验收流水线 = 隔离实例 + 浏览器端到端 CDP 验收 +
    几何量断言 + 生产只读复验
   → 来源：用户提供（真实事实）
14) 技术栈 .NET 8 / C# / Blazor Server（InteractiveServer）/ SQLite 元数据
   → 来源：用户提供
15) 徽章均为 shields.io 静态徽章，无外部数据依赖；统计卡为 github-readme-stats，用户名
    hardcode = 18354356258
16) 禁止项自查：无真实姓名 / 无公司职位学历工龄城市 / 无奖项论文专利客户名 /
    无邮箱手机号 / 无"全球领先"类虚高词 / 无用户数装机量 star 数等未验证数字
=============================================================================
-->
