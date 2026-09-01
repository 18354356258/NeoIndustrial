# NeoIndustrial Community Edition

[中文](README.md) | **English**

<div align="center">

**NeoIndustrial Data Acquisition Platform · Community Edition**

[![.NET Framework 4.8](https://img.shields.io/badge/.NET-4.8-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-green)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)]()
[![Gitee](https://img.shields.io/badge/Gitee-jede_master-red)](https://gitee.com/jede_master/IndustrialDataCollector)

**MCP + Industrial Data Acquisition = World's First** · 40 protocols completely free · Millisecond MQTT to cloud · AI-native control · Industry 4.0 ready

</div>

--- --- Download the Enterprise Edition client below, located in the 'Get Started in 5 Minutes' area.

## 🎨 The Story Behind This Software

In the spring of 2026, I was developing the UI prototypes and driver logic for a digital twin system at home.

I'm not a computer science major. I'm an **art student** — color, light, and composition are my native language. But in the digital twin world, pretty pictures alone get you nowhere: without real data flowing in, even the most gorgeous 3D model is just an exquisite empty shell.

My aluminum extrusion digital twin project is already live and iterating like crazy — trilingual (Chinese/English/Vietnamese), with AI analysis, temperature trend dashboards, extrusion cycle data automatically written to the database, data export, second-level data sync, and millimeter-level model displacement, all mirroring the live equipment on the shop floor (it may be open-sourced in the future too — stay tuned).

While building that digital twin system, I needed something that could connect to every device in the factory. PLCs, CNCs, sensors, meters, power cabinets, building controllers... they all speak completely different languages: Modbus, S7, FOCAS, BACnet, IEC 61850... The options out there were either absurdly expensive (one Kepware license costs as much as a machine tool), too heavy for an industrial PC to run (installing Ignition takes longer than building the production line), or so old they had never even heard of AI.

**I couldn't find anything usable. So I put down the brush and opened Visual Studio.**

(Some background: I joined a steel plant right after graduation, and have now spent 3 years in the aluminum extrusion industry — 12 years in IT in total.)

In under three months, one person — one art student — built from scratch: 40 industrial protocol drivers, parallel writes to 4 kinds of databases, MQTT two-layer millisecond push, 50 MCP AI atomic tools, a REST API, and a dashboard monitoring wall. Not to prove anything — I simply needed a set of nerve endings that could connect to every industrial device, to bring the digital twin to life.

**This is not "yet another piece of industrial software". This is the data foundation an art student built for the digital twin world.**

Now, I'm open-sourcing the Community Edition in full — Apache 2.0, permanently and genuinely free.

Because I know how hard it is to do open-source industrial software in China. When you're walking alone through the workshop at midnight, with nothing but the PLC indicator lights blinking, what you need is not more paywalls and gatekeeping — it's a tool that works, that's sincere, and that asks for nothing in return.

I hope you like it. I hope it's useful. And to friends hoping to transition from art/design/frontend into IT: I sincerely hope this software helps you get there.

---
![Login Page](picture/%E7%99%BB%E5%BD%95%E9%A1%B5.png)


## 📖 Why Choose It

This is not another Modbus debugging tool.

IndustrialDataCollector is a **production-grade industrial data acquisition engine** — rock-solid, and already running on real production floors.

From PLCs in stamping workshops to IEC 61850 substations at offshore wind farms, from Fanuc controllers on 5-axis CNC machining centers to semiconductor SECS/GEM tools — **one codebase covers them all.**

40 drivers, 4 databases, MQTT two-layer topic push, 50 MCP AI atomic tools — zero barriers to getting started. No encryption, no authentication, no license.

**The Community Edition is open source under Apache 2.0, free forever.**

You should spend your time on process optimization and business-scenario analysis — not on the nonsense of "who can connect to whom".

---

 **Conversational AI for real-time data, adding devices and variables, and historical data analysis** 

![AI Conversation](picture/AI%E5%AF%B9%E8%AF%9D.png)


![AI Conversation 1](picture/AI%E5%AF%B9%E8%AF%9D1.png)


![AI Conversation 2](picture/AI%E5%AF%B9%E8%AF%9D2.png)



## 🧠 Architecture at a Glance

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  PLC / CNC  │    │  Sensors &  │    │  Power &    │
│ 40 protocols│    │  Meters     │    │  Building   │
│             │    │ Modbus RTU  │    │ BACnet/DLT  │
└──────┬──────┘    └──────┬──────┘    └──────┬──────┘
       │                  │                  │
       └──────────────────┼──────────────────┘
                          ▼
              ┌───────────────────────┐
              │  DataCollectionEngine │
              │ millisecond polling · │
              │ adaptive byte order   │
              └───────────┬───────────┘
                          │
          ┌───────────────┼───────────────┐
          ▼               ▼               ▼
   ┌──────────┐   ┌──────────┐   ┌──────────────┐
   │   MQTT   │   │  4 DBs   │   │  MCP / REST  │
   │  ms push │   │ parallel │   │ AI data plane│
   └──────────┘   └──────────┘   └──────────────┘
```


## ✨ Core Capabilities

| Module | Community Edition |
|------|:---:|
| 🔌 **40 industrial protocol drivers** — PLC / CNC / meters / building / power / semiconductor | ✅ |
| 📡 **MQTT two-layer topics** — bulk JSON + per-variable subtopics, Sparkplug B compatible | ✅ |
| 🗄️ **Parallel writes to 4 databases** — SQLite / MySQL / SQL Server / PostgreSQL | ✅ |
| 🤖 **50 tools over MCP** — AI manages devices, queries data, and starts/stops collection in natural language | ✅ |
| 🔄 **REST API** — HTTP real-time data queries with Bearer Token authentication | ✅ |
| 📊 **Bulk CSV management** — Excel-compatible UTF-8, one-click import/export of data points | ✅ |
| 🌍 **Bilingual UI (Chinese/English)** — switch at runtime without restart | ✅ |
| 🧪 **Simulator driver** — 20+ simulated variables, run the full pipeline with no hardware | ✅ |
| 🚀 **Zero barriers** — no encryption, no authentication, no license, no hardware lock; unzip and run | ✅ |

Device management:

![Device Management](picture/%E8%AE%BE%E5%A4%87%E7%AE%A1%E7%90%86.png)


Device configuration:

![Configuration](picture/%E8%AE%BE%E5%A4%87%E6%A8%A1%E6%9D%BF%E7%94%9F%E6%88%90-%E9%85%8D%E7%BD%AE%E7%AE%A1%E7%90%86.png)

## 🔌 The Complete List of 40 Protocol Drivers

### ⚙️ PLC / Controllers (11)
| Driver | Supported brands/protocols |
|------|---------------|
| **Modbus TCP** | Schneider, Siemens, Mitsubishi, Delta, Inovance, Xinje, and all devices supporting Modbus TCP |
| **Modbus RTU** | Serial RS-232/485, same as above |
| **Siemens S7** | S7-200/300/400/1200/1500, supporting DB/Input/Output/Merker areas |
| **Siemens 840D** | Direct connection to Sinumerik 840D/840Di CNC systems |
| **Beckhoff ADS** | The full TwinCAT 2/3 range |
| **CODESYS** | CODESYS V3+ compatible controllers (Beckhoff, Hollysys, Inovance, etc.) |
| **Mitsubishi FX** | FX1S/1N/2N/3U/5U series |
| **Mitsubishi MELSEC** | iQ-R/iQ-F/Q/L series (MC protocol) |
| **Keyence KV** | KV-5000/7000/8000 series |
| **Panasonic Mewtocol** | The full FP series |
| **Omron FINS / HostLink** | The full CJ/CS/CP range |

### 🔧 CNC Machine Tools (4)
| Driver | Description |
|------|------|
| **Fanuc FOCAS** | 0i/16i/18i/21i/30i/31i/32i — read/write macro variables + spindle load + alarm numbers |
| **Haas CNC** | The full NGC controller range |
| **Mazak** | Mazatrol controllers (Smooth series) |
| **Heidenhain** | TNC series (HEIDENHAIN Remo Tools) |

### 🌐 Industrial Ethernet (3)
**EtherNet/IP** — Rockwell AB ControlLogix/CompactLogix · **Profinet** — Siemens PROFINET IO · **OPC UA** — cross-platform data modeling

### 🔗 Field Buses (3)
**PROFIBUS** — Siemens PROFIBUS DP · **DeviceNet** — Allen-Bradley · **CC-Link** — Mitsubishi

### ⚡ Power / Energy (3)
**IEC 104** — power telecontrol protocol · **IEC 61850** — smart substations · **DNP3** — North American power SCADA

### 🏢 Building Automation (5)
**BACnet** — Honeywell/Johnson Controls/Siemens building control · **KNX** — smart building bus · **DALI** — digital addressable lighting · **LonWorks** — building control networks · **MBus** — heat/water/electricity meters

### ☁️ IoT / Semiconductor / Others (11)
| Driver | Description |
|------|------|
| **MQTT Subscribe** | Subscribe to third-party MQTT brokers (reverse acquisition) |
| **Sparkplug B** | Industrial IoT MQTT sub-protocol |
| **HTTP REST** | Poll and collect from RESTful JSON APIs |
| **DLMS/COSEM** | International standard for smart meters |
| **HART IP** | IP variant of HART instruments |
| **MTConnect** | CNC machine connectivity standard |
| **SECS/GEM** | Semiconductor equipment communication standard |
| **OPC DA** | Classic OPC Data Access |
| **OPC UA PubSub** | OPC UA publish/subscribe mode |
| **Simulator** | Built-in 20+ simulated variables (sine wave/square wave/random/increment) |



---

## 🚀 Get Started in 5 Minutes

### Requirements
- Windows 7 SP1+ / Windows Server 2008 R2+
- .NET Framework 4.8 ([official Microsoft download](https://dotnet.microsoft.com/download/dotnet-framework/net48))
- 4 GB+ RAM

### Installation

> ⚠️ **If you downloaded the zip, read this first!** Windows marks downloaded files as unsafe, which breaks the VS build.

**⬇️ Download the Enterprise Edition client (no install, unzip and run)**

The Enterprise Edition client is available for direct download — License (obtain via email at bottom), no build required:

> 📦 [Download Enterprise Client QY_Client.rar](https://github.com/18354356258/NeoIndustrial/raw/main/QY_Client.rar)

- Unzip and run `IndustrialDataCollection.exe` to start (pre-built — no installation, no Visual Studio needed)
- Bilingual language pack (Chinese/English) built in; if Windows SmartScreen blocks the first launch, click "Run anyway"

**Option 1: Download the Release package (recommended, no build needed)**

Go to [Releases](https://gitee.com/jede_master/IndustrialDataCollector/releases), download the latest zip, unzip it, and **double-click `setup.bat`** (removes the web mark + restores NuGet packages), then open the `.sln` in Visual Studio and build.

Or run `Release/IndustrialDataCollector.exe` directly (pre-built, no VS needed).

**Option 2: Git clone**

```bash
git clone https://gitee.com/jede_master/IndustrialDataCollector.git
cd IndustrialDataCollector
setup.bat          # One-click NuGet restore
```

Then open `IndustrialDataCollector.sln` in Visual Studio 2019+ → Build → Run.

> If `setup.bat` won't run, do these two steps manually:
> 1. Run PowerShell as administrator: `Get-ChildItem -Recurse | Unblock-File`
> 2. In Visual Studio: right-click the solution → Restore NuGet Packages

### Your First Collection Task (Simulator mode, no hardware needed)

1. Launch `IndustrialDataCollector.exe`
2. In the device tree on the left, right-click → **Add Device** → choose the `Simulator` driver
3. Click **CSV Import** → use the simulator's default variables → Save
4. Click **▶ Start Collection**
5. Watch the data start rolling in

### Connecting Real Devices

| Device type | Key configuration |
|----------|---------|
| Modbus TCP PLC | IP + port 502 + register address + data type |
| Siemens S7-1200 | IP + Rack=0 Slot=1 + DB address (e.g. DB1.0.0) |
| Fanuc CNC | IP + port 8193 + macro variable number |
| MQTT Broker | Broker address + port + topic prefix |

---

## 📖 User Manual

### Device Management (4-Level Hierarchy)

The platform organizes devices in a **Company → Workshop → Process → Device** tree:

- **Right-click any tree node** → add/rename/delete folders or devices
- **Drag & drop** → move devices to another process/workshop, or migrate whole folders
- **Search box** → live filtering by device name or IP
- **Right-click "Move to..."** → precise relocation via a path-picker dialog

### Data Point Configuration (every single variable can be individually cleaned, configured, semantically tagged, alarmed... if a business scenario needs it, it should be here... and if something's missing, speak up and we'll figure it out together)


Basic info:

![Data Point - Basic Info](picture/%E5%8F%98%E9%87%8F%E5%9F%BA%E6%9C%AC%E4%BF%A1%E6%81%AF.png)

Data point alarms:

![Data Point - Alarms](picture/%E5%8F%98%E9%87%8F%E6%8A%A5%E8%AD%A6.png)

Custom scripts:

![Data Point - Custom Script](picture/%E5%8F%98%E9%87%8F%E8%87%AA%E5%AE%9A%E4%B9%89%E8%84%9A%E6%9C%AC.png)

Edge computing:

![Data Point - Edge Computing](picture/%E5%8F%98%E9%87%8F%E8%BE%B9%E7%BC%98%E8%AE%A1%E7%AE%97.png)

Advanced calculation:

![Data Point - Advanced Calculation](picture/%E5%8F%98%E9%87%8F%E9%AB%98%E7%BA%A7%E8%AE%A1%E7%AE%97-%E5%85%AC%E5%BC%8F%E5%A5%97%E7%94%A8%E8%BD%AC%E6%8D%A2.png)



Each device hosts data points that define what to collect and how to process it:

| Field | Description |
|------|------|
| Variable name | Chinese/English label (e.g. `Temperature Sensor`) |
| Address | Protocol-specific address (e.g. Modbus `40001`, S7 `DB1.0.0`) |
| Data type | int16 / uint16 / int32 / float32 / double / string... |
| Byte order | ABCD (big-endian) / DCBA (little-endian) / BADC / CDAB |
| Linear scaling | `y = kx + b`: raw value × k + b = engineering value |
| Rounding | Keep N decimal places |
| Alarm thresholds | HH / H / L / LL four-level settings |
| Unit | Engineering unit (℃, MPa, rpm, mm...) |

### MQTT Data Format

Each collection cycle sends one JSON packet to `{TopicPrefix}/{DeviceName}`:

```json
{
  "timestamp": "2026-07-16T14:30:00.123",
  "driver": "Simulator",
  "device": "Extruder #28",
  "values": [
    {"id": "temperature", "dt": "float32", "v": 245.6, "u": "℃"},
    {"id": "pressure", "dt": "float32", "v": 32.1, "u": "MPa"}
  ]
}
```

Meanwhile, each variable also gets its own subtopic `{TopicPrefix}/{DeviceName}/temperature` pushing the single value.

### Database Configuration


![Database Write Configuration](picture/%E6%95%B0%E6%8D%AE%E5%BA%93%E5%86%99%E5%85%A5%E9%85%8D%E7%BD%AE.png)


| Database | Connection string example | Auto table creation |
|--------|-----------|:---:|
| SQLite | Defaults to `Data/industrial.db` | ✅ |
| MySQL | `Server=192.168.1.100;Database=idc;User=root;Password=***` | ✅ |
| SQL Server | `Server=.;Database=idc;Integrated Security=True;` | ✅ |
| PostgreSQL | `Host=192.168.1.100;Database=idc;Username=postgres;Password=***` | ✅ |

All four databases share the same table schema: `industrial_data(id, db_type, device, variable, data_type, value, unit, timestamp)`

### MCP AI Integration

The platform ships with a built-in MCP Server exposing 50 AI-callable tools. Any MCP-capable AI client (Claude Desktop, etc.) can connect and then control devices in natural language:

```
User: Add a Mitsubishi FX5U PLC at IP 192.168.1.50, collecting D100 (temperature) and D101 (pressure)
AI:   calls add_device → add_variables → reload_config → device online
```

Tool coverage: device CRUD · variable management · collection start/stop · real-time queries · database queries · data source management

### CSV Bulk Import Template

CSV format (UTF-8, editable directly in Excel):

| Variable | Address | Data type | Unit | Rounding | Scale K | Scale B | HH | H | L | LL |
|--------|------|----------|------|------|-------|-------|----|---|---|----|
| Main Motor Current | 40001 | float32 | A | 2 | 0.1 | 0 | 500 | 450 | 50 | 10 |
| Temperature Sensor | 40003 | int16 | ℃ | 1 | 1 | 0 | 300 | 250 | -10 | -20 |

A template file ships with the desktop app: `变量点模板_ModbusTCP.csv` (Data Point Template - Modbus TCP)

### Logging & Troubleshooting

- Log directory: `Logs/log_YYYYMMDD.txt`
- Default level is **Info** (start/stop/errors/API requests) — no log spam
- To troubleshoot collection details, set `LogLevel.Debug` in `Logger.cs` to restore full logging
- Logs older than 30 days are cleaned up automatically

---

## 🆚 Enterprise Edition — Full Firepower for the Whole Factory

> **Don't measure the Enterprise Edition with the Community Edition's ruler — these are two entirely different weapon systems.**
>
> The Community Edition helps you **connect to devices**. The Enterprise Edition helps you **run the entire factory**. Every item below is a heavy-duty capability designed from the ground up for large industrial scenarios — none of them exist in the Community Edition:

### 🧬 Semantic Layer · Digital Twin Engine


![Dashboard](picture/%E7%9C%8B%E6%9D%BF.png)


![Semantic Management](picture/%E8%AF%AD%E4%B9%89%E7%AE%A1%E7%90%86.png)



> Turn "40001 = 245.6" into "Extrusion Workshop #3 ▶ Line 1 ▶ Main Machine ▶ barrel temperature running high — recommend checking cooling pump #3"

- **Automatic modeling** — the device tree and data source tree mirror each other; drag-to-reorganize instantly updates the digital twin topology
- **Tag system** — a unified variable namespace across devices and protocols; one TAG standard across the entire plant
- **Relationship graph** — device→variable→data source, three-way linkage; AI can trace any datum from "source device → collection driver → database table → MQTT topic"
- **State tracking** — online/offline/disabled/deleted four-state markers; AI knows what's alive, what's dead, and when it died
- **Variable events** — derived metrics such as rate, accumulation, and duration are computed automatically

### ⚡ Fabric Time-Series Analysis Engine

> 8 hot-pluggable operators — 10× faster than SQL, 100× less effort than writing Python scripts

| Operator | Function | Industrial use case |
|------|------|---------|
| **Aggregate** | Windowed aggregation (avg/max/min/sum/count/stddev) | Hourly average temperature, daily peak power |
| **Correlation** | Multi-variable Pearson correlation | Correlation diagnosis between vibration and temperature |
| **Anomaly** | 3-sigma / IQR outlier detection | Sudden spikes, sensor drift |
| **Digital Twin** | Deviation between theoretical and measured values | Energy benchmarking, efficiency decay |
| **Rate** | Rate of change d/dt | Temperature rise rate, pressure change rate |
| **Accumulate** | Cumulative totals (integration) | Total output, total energy, total runtime |
| **Threshold** | Dynamic upper/lower limit checks | Adaptive process-window alarms |
| **Prediction** | Linear regression trend forecasting | Predictive maintenance, spare-parts early warning |

Analysis results land in the database alongside the raw data, and can be queried in real time by the MCP AI tools.

### 🎛️ Dashboard

- Factory-level real-time monitoring wall, with device/production line/workshop three-level drill-down
- Trend curves + alarm panel + device status matrix
- Four-color alarm grading (HH red / H orange / L blue / LL purple) + 20-minute time window + incremental row updates with no screen flicker
- Supports independent multi-monitor wall display

### 📦 Templates · Cloning · Bulk Deployment

- **Configuration templates** — export a device's complete configuration (driver + variables + alarm parameters + MQTT topics) as a template
- **One-click cloning** — pick a template → enter the IP → 10 identical devices, configured
- **Differentiated overrides** — after applying a template in bulk, each device can still be tuned individually, with no interference
- Deploying a hundred devices: from 2 hours down to 2 minutes

### 🗄️ TDengine Time-Series Database

- Columnar storage optimized for industrial time series, 100K points/second writes
- Auto-partitioned super tables; 10 years of data queryable in seconds
- Compression ratio 10:1–20:1 — GBs of data shrink to MBs on disk
- 4 hard-won lessons are baked into MCP `introduce_platform("tdengine")`, so AI automatically avoids the pitfalls

### 🛡️ Industrial-Grade Reliability

| Capability | Description |
|------|------|
| **Offline caching** | When the network drops, data is written to local SQLite; after recovery, MQTT is re-sent and the database backfilled automatically |
| **Heartbeat monitoring** | Independent heartbeat per device; records state transitions only, no disk spam |
| **Exponential backoff reconnect** | Consecutive failures back off 1→2→4→8→16→32→60s, auto-reset on success |
| **Multi-generation config backups** | `config.json.bak.1~50` — up to 50 generations to roll back to |
| **Event rule engine** | Condition→action (MQTT/DB/alarm): IF `temperature>300` THEN `send MQTT + write DB + raise alarm` |
| **Authentication & security** | SHA256+SALT passwords + MAC hardware binding + Token authentication |

### 📊 Full Comparison

| Capability | Community Edition | Enterprise Edition |
|----------|:---:|:---:|
| 40 industrial protocol drivers | ✅ | ✅ |
| MQTT millisecond push | ✅ | ✅ |
| SQLite / MySQL / SQL Server / PostgreSQL | ✅ | ✅ |
| MCP AI 50 tools | ✅ | ✅ |
| REST API | ✅ | ✅ |
| CSV bulk import/export | ✅ | ✅ |
| Bilingual UI (Chinese/English) | ✅ | ✅ |
| **Semantic-layer digital twin modeling** | ❌ | ✅ |
| **Fabric 8-operator time-series engine** | ❌ | ✅ |
| **Dashboard monitoring wall** | ❌ | ✅ |
| **Config templates · device cloning** | ❌ | ✅ |
| **TDengine time-series database** | ❌ | ✅ |
| **Offline caching · re-send** | ❌ | ✅ |
| **Event rule engine** | ❌ | ✅ |
| **Auth · hardware binding · Token** | ❌ | ✅ |
| **Multi-generation rolling config backups** | ❌ | ✅ |
| **Commercial license · technical support** | ❌ | ✅ |

> 🔥 **The Enterprise Edition is not the Community Edition "with extras" — it is a redesigned, factory-grade data operating system.**


---

## 🧑‍💻 About the Author

**Zhang Chenglong** — art student turned full-stack developer, digital twin system architect.

A one-man army. From UI prototypes to low-level protocol stacks, from frontend dashboards to backend time-series engines, from database schema design to integrating 40 industrial protocols — all of it done alone.

Why open source? Because I know how hard it is, in China, for an engineer to install a usable data acquisition program on their own industrial PC for free. You either get scared off by resellers' sky-high license fees, hold your nose and use pirated software, or spend two months wrestling with a patchwork of open-source components that still can't connect to your devices.

**It shouldn't be this way.** Industrial data acquisition is the most fundamental bedrock of digital twins and smart manufacturing — bedrock shouldn't cost money, shouldn't be encrypted, and shouldn't be locked away. So I'm putting it out there, open-sourced, clean and simple. You don't pay a cent, you don't need to contact me for a license, you don't need anyone's approval — download, build, connect your devices. That's it.

This is the Neo Industrial Data Acquisition Platform. **The data foundation of the digital twin.**

---

## 🧠 Why It Kicks Ass

Others sell "protocol converters". We built an **industrial data operating system** — and the gap between those two phrases fits in one sentence:

> Others connect you to your devices. We let AI run your entire factory for you.

### Where It's Ahead of the Curve

| Dimension | Industry status quo (2026) | Neo Industrial Data Platform |
|------|-----------------|-----------------|
| **AI integration** | Still "discussing" AI + industry | **50 MCP atomic tools, AI-native control** — Claude directly adds devices, edits configuration, queries data, and diagnoses faults for you |
| **Protocol coverage** | Single category (PLC-only / CNC-only) | **40 protocols across all categories** — PLC + CNC + power + building + semiconductor + meters, one package |
| **Digital twin** | Buy an extra platform + model manually | **Semantic-layer automatic modeling** — drag the device tree and the twin updates; the tag system is AI-reasonable |
| **Time-series analysis** | Write Python scripts / SQL | **Fabric: 8 hot-pluggable operators** — anomaly detection, correlation analysis, trend prediction; configure and it works |
| **Data egress** | Locked into the vendor's own platform | **Everything open** — MQTT + REST API + MCP; your data goes wherever you want |
| **Deployment barrier** | Servers / clusters / K8s | **Single exe, unzip and run** — one industrial PC handles 100 devices, no ops team required |

### One Person vs. the Industry Giants

This system is the work of one person. Compare it with similar offerings in the industry:

- **Kepware** (PTC, $5000+/year) — comparable driver count, but no database writes, no MQTT two-layer topics, no AI integration, no dashboard
- **Ignition** (Inductive Automation, $20,000+ to start) — powerful but heavy as an elephant; needs Java + a database + a web server; not built for industrial PCs
- **KingView / ForceControl / ZijinBridge** (组态王 / 力控 / 紫金桥) — steep learning curves, aging protocol libraries, no MCP/AI; basically stuck in 2010
- **Node-RED / ThingsBoard** — a cobbled-together feel; industrial protocol support relies on community plugins left to fend for themselves; offline reconnection logic is essentially absent

**Neo isn't trying to be anyone's competitor. It fills the piece the digital twin world is missing — the data foundation for industrial devices.**

---

## 📋 Version History

### v1.0.0 | 2026-07-16 — First Community Edition release

- **40 industrial protocol drivers** — Modbus / Siemens S7 / OPC UA / BACnet / EtherNet/IP / Profinet / PROFIBUS / Beckhoff / CODESYS / Mitsubishi / Fanuc / IEC 104 61850 / DNP3 / KNX / DALI / SECS/GEM / MTConnect, and more
- **Parallel writes to 4 databases** — SQLite / MySQL / SQL Server / PostgreSQL
- **MQTT two-layer topics** — bulk JSON + per-variable subtopic push
- **50 tools over the MCP protocol** — AI can directly manage devices, query data, and control start/stop
- **REST API** — HTTP real-time data query interface
- **CSV bulk import/export** — Excel-compatible UTF-8
- **Bilingual Chinese/English** — runtime switching, no restart required
- **Simulator driver** — 20+ simulated variables, walk through the entire flow with zero hardware
- **Zero-barrier startup** — no authentication, no encryption, no license; unzip and run

> For Enterprise Edition version history (v1.0 – v2.6.1), see `docs/工业数采平台_版本记录.md` (Industrial Data Acquisition Platform Release Notes)

---

## 🤝 Contributing

One person can go fast, but a group of people can go far.

This project was written by one art student in under three months — it is definitely not perfect, definitely has bugs, and there are definitely industrial device protocols out there you've seen and I've never even heard of. **And that's fine.** The point of open source was never "hand over a flawless finished product" — it's "put something useful here so the people who need it can use it, improve it, and make it better together".

If you used it in your plant to connect to a PLC and solve a real problem — **come leave a comment and tell me**. That would make me happier than ten thousand stars.

If you found a bug in some protocol, or it's missing a feature you need — **open an Issue, submit a PR**, even if it's just fixing a typo. Open-source communities aren't great because one person is amazing; they're great because everyone is willing to share the small problems they fixed.

If you have industrial device protocols I haven't covered — **let's talk**. China's factories hide the most complex industrial device ecosystem in the world, and one person could never see it all in a lifetime. You contribute one driver, and it might help hundreds of other folks using the same device.

**Support each other. Treat each other with sincerity.** Open source isn't easy — late at night it's just you and the glowing screen, you have to climb out of pits no one has ever stepped in before all by yourself, and it can wear you down when people use your project without even dropping a star. But I believe: sincere work will, sooner or later, meet sincere people.

So no matter which city, which factory, or which school you come from — **this is your project, and everyone's.**

- 🐛 Report bugs → [Issues](https://gitee.com/jede_master/IndustrialDataCollector/issues)
- 💻 Contribute code → Fork → PR (make sure it builds)
- 🔌 Request a new driver → Issue tag `driver-request`
- 💬 Just want to chat → [Issues](https://gitee.com/jede_master/IndustrialDataCollector/issues) — open one anytime, no bug required

> If this project has helped you, **give it a Star ⭐**. It won't cost you a cent — that little star is the biggest encouragement you can give me.

## 📄 License

The Community Edition is open-sourced under the [Apache License 2.0](LICENSE), **free for both commercial and non-commercial use**.

To get the Enterprise Edition client: send your requirements to `751326339@qq.com`, or call `18354356258` / `18854344113` (Zhang Chenglong).

The Enterprise Edition requires a commercial license. To obtain one: send your hardware ID to `751326339@qq.com`, or call `18354356258` / `18854344113` (Zhang Chenglong).

---

<div align="center">

**IndustrialDataCollector — The Data Foundation of the Digital Twin**

© 2026 Zhang Chenglong · Community Edition Apache 2.0 · Enterprise Edition requires a commercial license, please contact the developer

</div>
