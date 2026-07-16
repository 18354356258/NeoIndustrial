# IndustrialDataCollector Community Edition

<div align="center">

**工业数据采集平台 · 社区版**

[![.NET Framework 4.8](https://img.shields.io/badge/.NET-4.8-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-green)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)]()

**40 种工业协议 · MQTT 推送 · MCP AI 集成 · 多数据库支持**

</div>

---

## 📖 简介

IndustrialDataCollector（工业数采平台）是一款开源的工业数据采集软件，支持 **40 种工业协议**驱动，可连接 PLC、CNC、传感器、智能仪表、楼宇自动化设备，并将数据通过 MQTT 推送到云端或写入本地数据库。

**社区版**基于 Apache 2.0 协议开源，免费用于商业和非商业用途。企业版提供语义建模、Fabric 时序分析、配置模板、设备克隆等高级功能，请访问 [industrialdata.cn](https://industrialdata.cn) 了解。

## ✨ 核心功能

| 模块 | 功能 |
|------|------|
| 🔌 **多协议驱动** | 40 种工业协议，覆盖 PLC / CNC / 仪表 / 楼宇 / 电力 |
| 📡 **MQTT 推送** | 支持 MQTT 3.1.1，自定义主题，JSON 格式数据包 |
| 🗄️ **数据存储** | SQLite / MySQL / SQL Server / PostgreSQL |
| 🤖 **MCP 协议** | 50 个 AI 工具，支持远程设备管理与数据查询 |
| 🌍 **国际化** | 中/英文界面，运行时可切换 |
| 📊 **CSV 导入导出** | 批量变量点导入，Excel 兼容 UTF-8 |
| 🔄 **REST API** | HTTP 实时数据查询接口 |

## 🔌 支持的协议驱动

### PLC / 控制器
Modbus TCP/RTU, Siemens S7, Siemens 840D, Beckhoff ADS, CODESYS, Mitsubishi FX, Mitsubishi MELSEC, Keyence KV, Panasonic Mewtocol, Omron FINS, Omron HostLink

### CNC 机床
Fanuc FOCAS, Haas CNC, Mazak, Heidenhain

### 工业以太网
EtherNet/IP, Profinet, OPC UA, OPC DA

### 现场总线
PROFIBUS, DeviceNet, CC-Link

### 电力/能源
IEC 104, IEC 61850, DNP3

### 楼宇自动化
BACnet, KNX, DALI, LonWorks, MBus

### 物联网
MQTT Subscribe, Sparkplug B, HTTP REST, DLMS, HART IP, MTConnect, SECS/GEM

### 其他
OPC UA PubSub, Simulator（模拟器）

## 🚀 快速开始

### 环境要求
- Windows 7 SP1+ / Windows Server 2008 R2+
- .NET Framework 4.8
- 4 GB+ RAM

### 下载与运行

1. 从 [Releases](https://gitee.com/jede_master/IndustrialDataCollector/releases) 下载最新版本
2. 解压到任意目录
3. 双击 `IndustrialDataCollector.exe` 启动

### 编译

```bash
git clone https://gitee.com/jede_master/IndustrialDataCollector.git
cd IndustrialDataCollector
# 用 Visual Studio 2019+ 打开 IndustrialDataCollection.sln 编译
```

## 📁 项目结构

```
IndustrialDataCollector/
├── Drivers/          # 40 个工业协议驱动
├── Forms/            # WinForms UI 界面
├── Models/           # 数据模型
├── Services/         # 核心服务（采集/存储/MQTT/MCP/REST）
├── Resources/        # 中英文语言包
└── Utils/            # 工具类（日志/词汇表）
```

## 🆚 企业版对比

| 功能 | 社区版 | 企业版 |
|------|:---😐:---:|
| 40 工业协议驱动 | ✅ | ✅ |
| MQTT 数据推送 | ✅ | ✅ |
| SQLite 存储 | ✅ | ✅ |
| MySQL / SQL Server / PostgreSQL | ✅ | ✅ |
| MCP AI 协议（50 工具） | ✅ | ✅ |
| REST API 数据查询 | ✅ | ✅ |
| CSV 导入导出 | ✅ | ✅ |
| 中英文界面 | ✅ | ✅ |
| 语义层（数字孪生建模） | ❌ | ✅ |
| Fabric 时序分析引擎 | ❌ | ✅ |
| 配置模板与设备克隆 | ❌ | ✅ |
| TDengine 时序数据库 | ❌ | ✅ |
| 数据看板 Dashboard | ❌ | ✅ |
| 认证与硬件绑定 | ❌ | ✅ |
| 离线缓存与补发 | ❌ | ✅ |
| 事件规则引擎 | ❌ | ✅ |
| 边缘计算 | ❌ | ✅ |

👉 [了解更多企业版功能](https://industrialdata.cn)

## 🤝 贡献

欢迎提交 Issue 和 Pull Request。在发起 PR 前，请确保代码通过编译。

## 📄 许可证

本项目基于 [Apache License 2.0](LICENSE) 开源。

---

<div align="center">
  <b>IndustrialDataCollector</b> — 开源工业数据采集，让连接更简单
</div>
