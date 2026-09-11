# Industrial Data Collector (Enterprise Edition) — User Manual

| Item | Content |
|------|---------|
| Product | Industrial Data Collector (Enterprise Edition / Windows Desktop) |
| Applicable version | v2.6.x series (including all enterprise features up to v2.6.2) |
| Form factor | WinForms desktop application (`IndustrialDataCollection.exe`) |
| Audience | Field engineers / system administrators / end users deploying for the first time |
| Document version | v1.0 (2026-09-11) |
| Author | Zhang Chenglong |
| Technical support | Phone: **18854344113 / 18354356258**　Email: **751326339@qq.com** |

> This manual is written for **first-time** users: install the software, complete the configuration in the recommended order, then go through each module in depth, and finally use the troubleshooting FAQ. Every feature description has been verified against the current program code.

---

## 1. Platform Overview

### 1.1 What is it

Industrial Data Collector (Enterprise Edition) is an **industrial data acquisition and semantic modeling workstation** that runs on a Windows PC: downward, it connects to PLCs, CNC machines, power meters, building automation devices and more through **40 industrial communication drivers**; upward, it writes collected data into **5 types of databases** and publishes it via **MQTT**; in between, it provides a semantic tree modeler, edge-computing data cleansing, event alarming, a live dashboard, an AI assistant (MCP) and a REST API.

Capability overview:

| Capability | Description |
|------|------|
| Device acquisition | **40 production-ready drivers** (all real protocol implementations), device tree with groups, context menus and status lights |
| Data storage | SQLite / MySQL / SQL Server / PostgreSQL / TDengine — **each type has an independent on/off switch**, parallel writing supported |
| MQTT publishing | Two-tier topics (batch + per-variable sub-topics), offline caching while disconnected, automatic replay on recovery |
| Edge computing | 9 cleansing strategies + rounding + filtering + calculation formulas — quality control at the source |
| Semantic modeling | Factory semantic tree (workshop / line / equipment / variable), auto-synced from the device tree, 17 relation types |
| Events & alarms | HH/H/L/LL four-level thresholds, 12 handling methods (alarm / email / SMS / webhook / work order / AI analysis …) |
| Dashboard | Device status, live alarms, realtime data, trend curves, data-flow rates on one screen |
| AI assistant | Built-in MCP service (50 tools) — manage devices, query data and generate reports in natural language |
| REST API | Standalone HTTP service + Swagger docs for third-party integration |
| Config safety net | Auto-recovery from corrupted config + 10-generation history backups + Ctrl+Z rollback + templates + device cloning |
| Network tunnels | VPN / NAT tunnel management with IP mapping for cross-segment device access |

### 1.2 Enterprise Edition vs. Web Edition

This product (WinForms desktop) and NeoIndustrial Web Edition share the **same data kernel**:

- Some config files under `%LOCALAPPDATA%\IndustrialDataCollection\` (device config, database config, etc.) are shared by both products;
- The desktop edition targets **on-site deployment, works out of the box on a single machine**; the Web edition targets browser access from multiple clients and platform-style operation;
- They can be used independently or together (desktop collects, Web browses and runs AI conversations).

### 1.3 Main window tour

After login the program opens the **Dashboard** (`DashboardForm`) as the main window:

- Click **Collection Management** on the dashboard to enter the **collection main window** (Neo Industrial Network Data Collection Platform) — device tree, toolbar and status bar live here;
- Dashboard and collection pages **coexist** — monitor and adjust configuration at the same time;
- Entry points in the collection main window:

| Entry | Location | Purpose |
|------|------|------|
| Data Source Manager | Toolbar / menu | Add devices, manage groups, database datasources, network tunnels |
| Add / Edit / Delete Device | Menu | Device lifecycle operations |
| Start / Stop Collection | Menu / context menu | Start or stop collection per device |
| Database Config | Toolbar / menu | Configure storage targets |
| MQTT Config | Toolbar / menu | Configure the MQTT broker and topic prefix |
| Semantic Management | Toolbar (between Database Config and language switch) | Open the semantic tree modeler |
| Dashboard | Menu | Return to the monitoring dashboard |
| Template Management | Tools menu | Browse / search / delete config templates |
| Config History | Tools menu | Roll back through 10 generations of backups (supports Ctrl+Z) |
| MCP Service / REST API | Menu (auto-start optional) | AI assistant interface / HTTP data interface |
| 中文 / English | Toolbar | Instant UI language switch |

---

## 2. Installation & First Start

### 2.1 System requirements

| Item | Minimum | Recommended |
|------|----------|----------|
| OS | Windows 7 SP1 / Windows Server 2008 R2 | Windows 10 / Windows Server 2019+ |
| Runtime | .NET Framework 4.8 | .NET Framework 4.8 |
| Memory | 4 GB | 8 GB+ |
| Disk | 500 MB (excluding data storage) | SSD 1 GB+ |
| Database | Built-in SQLite (zero config) | MySQL / SQL Server / PostgreSQL / TDengine |

### 2.2 Installation steps

1. **Get the package** — copy the program folder to the target machine, keeping `IndustrialDataCollection.exe` together with `Drivers/`, `Resources/` and other resources;
2. **Install .NET Framework 4.8** if missing (download from Microsoft);
3. **Run** — double-click `IndustrialDataCollection.exe`;
4. **Single instance** — a global mutex prevents double launch; a second start shows a notice and exits;
5. **First start** — the local database, auth database, semantic database and config directory are created automatically; no pre-configuration needed.

### 2.3 First login

| Item | Description |
|----|------|
| Default account | **admin / admin** (created automatically on first start, administrator role) |
| Password storage | SHA256 + salt |
| Login records | Successes and failures are logged |

> ⚠️ Change the default password before production use. The change-password capability is built into the auth service; if it is not convenient on site, contact technical support (18854344113) for remote assistance.

### 2.4 Software activation

After the first login, activate the license (`ActivationForm`):

1. Enter the **license key** obtained from the vendor;
2. The system reads the local **MAC address** for hardware binding (virtual adapters / VMware / Hyper-V / Docker are filtered automatically);
3. **Online activation** (automatic when online) or **offline activation** (export request file → obtain license file → import);
4. The license is stored as `license.dat` in the config directory and verified on every start.

> Note: contact technical support before changing network adapters or reinstalling the OS to avoid invalidating the license.

### 2.5 Where the data lives

**Program directory (next to the exe):**

| Directory / file | Description |
|-----------|------|
| `IndustrialDataCollection.exe` | Main program |
| `Data/offline_cache.db` | Offline cache database (dual MQTT/DB caches + device heartbeat table) |
| `Logs/` | Runtime logs (older than 30 days are purged automatically) |

**User config directory (`C:\Users\<name>\AppData\Local\IndustrialDataCollection\`):**

| File | Description |
|------|------|
| `devices.json` | All device and variable configuration (core file, auto-multi-backup) |
| `groups.json` | Device groups |
| `mqtt.json` | MQTT broker configuration |
| `dbConfig.json` | Database write configuration (5 DB types + retention days) |
| `datasources.json` | Database datasource connections from the Data Source Manager |
| `auth.db` | User accounts (username / password hash / role) |
| `semantic_v2.db` | Semantic tree database |
| `tunnels.json` | Network tunnel configuration |
| `license.dat` | License file |
| `log_YYYYMMDD.txt` | Daily runtime log |
| `crash.log` | Unhandled exceptions (check here first when the app crashes) |

> Every save of `devices.json` rotates multi-generation backups (`devices.json.bak.1` ~ `bak.N`) — you can always roll back (see chapter 12).

---

## 3. Five-Minute Quick Start: Recommended Configuration Order

**The most frequently asked first-time question: semantic tree first, MQTT first, or database first?**

> **Recommended main line: configure the database → then MQTT → create groups and add devices → configure variables → start collection and verify → build the semantic tree and event rules last.**

1. **Configure the database (storage target)** — Settings → Database Config. Decide "where the data goes" first: SQLite works out of the box; enable MySQL / SQL Server / PostgreSQL / TDengine as needed. Saving creates the `industrial_data` table.
2. **Configure MQTT (cloud target, optional)** — Settings → MQTT Config. If data must be published to EMQX / a cloud platform, set the broker and topic prefix now — every device added later simply references this global configuration.
3. **Create groups and add devices (data source)** — Data Source Manager → Add device: pick a driver (40 available), fill in IP / serial port / station number, and attach the device to a group (the group path determines the Chinese tag, e.g. `Workshop1/Extruder/Extruder-28`).
4. **Configure variables (points)** — double-click a device: add variables (address / type / interval / unit), optionally attach cleansing strategies and four-level alarms, then click **Apply** (effective immediately).
5. **Start collection and verify** — right-click a device → Start Collection; once the status light turns green, confirm realtime data, MQTT and database writes on the dashboard.
6. **Build the semantic tree (organization & relations)** — toolbar → Semantic Management. The device tree auto-syncs into the semantic hierarchy; then add upstream/downstream relations, datasource field bindings and event rules. The semantic tree does not block collection — doing it last never affects steps 1–5.
7. **Extensions (as needed)** — event notifications (email/webhook), MCP for AI assistants, REST API for third-party systems, templates and cloning for batch deployment.

**Why this order (dependencies):**

| Feature | Prerequisite | Notes |
|------|----------|------|
| Data storage | Database config | Without an enabled target, data only lands in the offline cache |
| MQTT publishing | MQTT config + per-device publish mode | One global broker, referenced by each device |
| Device collection | Device + variable config | **Does not depend** on database / MQTT / semantic tree |
| Chinese tags | Device group path | Changing groups changes tag paths |
| Semantic tree | Device tree (auto sync) | The collection-side hierarchy syncs **one-way** into semantics — devices come first |
| Event rules | Semantic nodes | Event rules attach to semantic tree nodes |
| Templates / cloning | At least one configured device | Extracted from an example device |

In one sentence: **devices are the main line; the database and MQTT are destinations (set them up first to avoid rework); the semantic tree is the organization layer (no rush — it can be added any time).**

---

## 4. Database Config (where data is stored)

### 4.1 Entry point

Main menu: **Settings → Database Config** (`DatabaseConfigForm`); configuration is saved to `dbConfig.json`.

### 4.2 Supported database types

| Database | Default port | Connection notes | Use case |
|--------|----------|----------|------|
| SQLite | — (file-based) | Just pick the .db file path — zero config | Single machine / quick trials |
| MySQL | 3306 | Server, port, user, password, database | Small & medium scale |
| SQL Server | 1433 | Instance name, auth mode, database | Windows enterprise environments |
| PostgreSQL | 5432 | Host, port, user, password, database | High-concurrency scenarios |
| TDengine | 6041 (REST) | Server, port, database, root password | High-frequency time-series (tens of millions of points per day) |

### 4.3 Configuration steps (MySQL example)

1. Pick **MySQL** in the type dropdown (port auto-fills 3306);
2. Fill in server, port, user, password, database name;
3. Tick **Enable database write** — **each of the five types has an independent switch**; enable one, or all of them in parallel;
4. In the device selection list, tick the devices to write into this database (**empty = all devices**);
5. Optional: tick **Fabric history** so the analytics engine can read history from this database;
6. Click **Test Connection** (5-second timeout);
7. Click **Save** — the `industrial_data` table is created automatically.

### 4.4 Data retention days (important)

- The database config has a **retention-days** setting (`RetentionDays`), **default 7 days**;
- A cleanup job runs **every 6 hours** and deletes `industrial_data` rows older than the retention in batches;
- **0 (or negative) = auto cleanup disabled — data is kept forever**;
- Actual behavior per database:

| Database | Does auto cleanup work | Recommendation |
|--------|------------------|------|
| SQLite / MySQL / SQL Server / PostgreSQL | ✅ batched deletes work normally | Set retention days according to disk capacity and compliance needs |
| TDengine | ❌ in the current version the cleanup statement is not compatible with TDengine's time column and fails silently — **data is not deleted** | Manage retention with TDengine's own `KEEP` parameter (e.g. `ALTER DATABASE mytsdb KEEP 3650d`) and set the platform retention to 0 |

### 4.5 Table structure (industrial_data)

One row = one collected value:

| Column | Content |
|----|--------|
| `db_type` | Target database type |
| `device` | Device name |
| `variable` | Variable name |
| `data_type` | Data type (float / int16 / bool…) |
| `value` | Collected value (up to 2000 chars, long JSON supported) |
| `unit` | Engineering unit |
| `tag` | English tag ID |
| `tag_zh` | **Chinese tag path** (e.g. `Workshop1/Extruder/Extruder-28/BarrelTemp` — use this when querying manually) |
| `timestamp` | Collection timestamp |

> When writing to TDengine, reserved words are mapped automatically: `value → val`, `tag → tag_id`, `timestamp → ts`; a super table is created per device.

### 4.6 Dual independent offline caches

MQTT and the database each maintain an **independent offline cache** (`Data/offline_cache.db` next to the exe):

| Table | Cached content |
|----|----------|
| `offline_mqtt_cache` | MQTT messages not yet published |
| `offline_db_cache` | Database rows not yet written |
| `device_heartbeat` | Device heartbeats |

- One broken link does not affect the other: if the database is down, MQTT keeps publishing, and vice versa;
- A background job **checks every 10 seconds** and replays each channel independently once its link recovers;
- Pending cached data is **never deleted by age** — no matter how long the outage lasts, no data is lost.

---

## 5. MQTT Config (where data is published)

### 5.1 Global broker configuration

Main menu: **Settings → MQTT Config** (`MqttConfigForm`), saved to `mqtt.json`:

| Parameter | Description | Example |
|------|------|------|
| Broker address | MQTT server IP / domain | `192.168.1.200` or `broker.emqx.io` |
| Port | 1883 plaintext, 8883 for TLS | `1883` |
| Client ID | Unique client identifier (auto-generated if empty) | `collector-01` |
| Username / password | Required when the broker authenticates | — |
| Topic prefix | Root of all published topics | `factory/workshop1/` |
| QoS | Quality of service level | 1 |
| Auto reconnect | Reconnect automatically after disconnects | Recommended on |

Click **Test Connection** to verify, then **Save**.

### 5.2 Two-tier topic publishing (per device)

Set `MqttPublishMode` on each device's configuration page:

| Mode | Topics | Description |
|------|------|------|
| **Original (batch only)** | `{prefix}/{DeviceName}` | One complete device packet (JSON array) per collection cycle |
| **Resolved (batch + sub-topics)** | `{prefix}/{DeviceName}` + `{prefix}/{DeviceName}/{VariableName}` | Publishes both the batch packet and per-variable sub-topics |

Batch message example:

```json
{
  "timestamp": 1783176200000,
  "driver": "SiemensS7",
  "device": "Extruder-28",
  "values": [
    {
      "id": "Extruder-28|BarrelTemp",
      "dt": "float",
      "v": "72.3",
      "u": "°C",
      "variable_id": "a1b2c3d4-...",
      "tag_cn": "Workshop1/Extruder/Extruder-28/BarrelTemp"
    }
  ]
}
```

Sub-topic example: topic `factory/workshop1/Extruder-28/BarrelTemp`, payload `{"timestamp":...,"variable":"BarrelTemp","value":72.3,"unit":"°C"}`.

### 5.3 MQTT Subscribe devices

If a device/gateway publishes to MQTT itself, create a device with the **MQTT Subscribe** driver. `TopicFilter` supports two forms:

| TopicFilter | What you receive |
|-------------|----------|
| `#` | All variables of the device (batch JSON messages) |
| `/VariableName` | Exactly one variable (sub-topic message) |

Both JSON shapes — "nested batch" (with a `values` array) and "flat single-variable" (with `variable` + `value`) — are parsed automatically. When a Subscribe device is selected, the acquisition page shows the raw payload as **pretty-printed dark-theme JSON**.

---

## 6. Adding Devices & Variables (where data comes from)

### 6.1 Groups first, then devices

1. Open the **Data Source Manager** (`DataSourceManagerForm`);
2. Right-click empty space in the device tree → **Add Group**; build the hierarchy (company / workshop / line) — nested sub-groups, drag-and-drop and "Move to" are supported;
3. Click **Add** to open the new-device dialog.

> The group path determines the device's Chinese tag: `Workshop1/Extruder/Extruder-28`. Changing groups later changes tag paths — the app will ask you to stop collection first.

### 6.2 Driver selection (40 drivers, all production-ready)

| Category | Driver | Typical devices / scenarios |
|------|------|--------------|
| **Industrial Ethernet** | ModbusTCP / ModbusRTU | PLCs, meters, sensors (Ethernet / serial) |
| | Siemens S7 | Siemens S7-1200/1500/300/400 |
| | Siemens 840D | Siemens 840D CNC |
| | OPC UA / OPC DA / OPC UA PubSub | OPC servers and classic architectures |
| | EtherNet/IP | Rockwell / Allen-Bradley |
| **CNC** | Fanuc FOCAS | Fanuc CNC |
| | Haas CNC (RS232) | Haas machines |
| | Mazak (RS232/ASCII) | Mazak machines |
| | Heidenhain | Heidenhain CNC |
| **Japanese / European PLCs** | Mitsubishi FX (RS232) | Mitsubishi FX programming port |
| | Mitsubishi MELSEC MC | Mitsubishi Q/L/iQ series |
| | Keyence KV | Keyence KV |
| | Panasonic Mewtocol | Panasonic FP series |
| | Omron FINS / HostLink | Omron CJ/CS series |
| | CODESYS (Modbus) | CODESYS controllers |
| | Beckhoff ADS (TwinCAT) | Beckhoff TwinCAT controllers |
| **Building / power / meters** | BACnet | Building automation (HVAC/lighting/security) |
| | IEC 61850 / IEC 104 | Substation automation / power telecontrol |
| | DNP3 | Power / water SCADA |
| | DLMS | Smart electricity meters |
| | M-Bus | Water / heat / gas meters |
| | KNX / DALI | Smart building lighting / blinds / HVAC |
| **Fieldbus** | PROFIBUS (gateway) | Siemens fieldbus |
| | DeviceNet (CIP) | Rockwell DeviceNet |
| | CC-Link | Mitsubishi CC-Link |
| | HART IP | HART instruments (IP gateway) |
| **IoT / relay** | MQTT Subscribe | Devices that publish to MQTT themselves |
| | Sparkplug B | Industrial IoT devices |
| | HTTP REST | REST API devices / gateways |
| | SECS/GEM / MTConnect | Semiconductor equipment / CNC standards |
| **Other** | Profinet / LonWorks | Industrial Ethernet / building control |
| | Simulator | Sine-wave simulated data (testing / demo / training) |

### 6.3 Device creation flow

1. **Pick the driver** — choose from the categorized dropdown (e.g. a Modbus TCP device needs IP + port 502 + station number);
2. **Configure connection parameters**:

| Parameter | Description |
|------|------|
| Device name | Something recognizable on site |
| Protocol type | The selected driver |
| IP / serial port | Network address or COM port |
| Port / station / unit ID | Communication port and slave address |
| Timeout / retries | Millisecond timeout and retry count |
| MQTT publish mode | Original / Resolved (see 5.2) |
| Network tunnel | Select a tunnel for cross-segment access (see chapter 6.4) |

3. **Edit channel-level advanced parameters** (optional);
4. **Save** — the device appears in the tree; the status light shows the connection test: 🟢 online / 🔴 offline / 🟡 fault;
5. Right-click the device → **Start Collection**.

### 6.4 Network tunnels (cross-segment access)

When devices live behind a VPN / NAT, you do not need to change their real IPs. In the datasource add/edit page, in the **Network Tunnel** section:

1. Click "New tunnel" to open `TunnelEditDialog`;
2. Choose type **VPN** (OpenVPN / L2TP / IPsec, TUN layer-3 or TAP layer-2) or **NAT** (Huawei AR / Moxa / PgYun / generic — fill management IP, port, account);
3. Maintain the **IP mapping table**: `original IP:port → mapped IP:port` (e.g. `192.168.1.50:502 → 10.8.0.5:1502`);
4. Save, then select the tunnel in the device config — collection goes through the mapped address.

### 6.5 Variable configuration

Double-click a device to open **DeviceConfigForm**, then click "Add Variable":

| Field | Description |
|------|------|
| Variable name | Chinese label (e.g. "Barrel Temperature") |
| Address / register | Protocol address (Modbus 40001, S7 `DB1.DBD0`, …) |
| Data type | Int16 / Int32 / Float / Double / Bool / String |
| Collection interval | Milliseconds (short intervals increase load) |
| Access | Read-only / write-only / read-write |
| Unit | °C, MPa, rpm… |

Once the name is filled in, the **Chinese tag path is generated automatically** (`group path/device name/variable name`).

**"Apply" takes effect immediately**: after changing alarm thresholds or rounding, click **Apply** — the config is written to disk (devices.json) and running devices pick it up **on the next collection cycle** without stopping collection.

### 6.6 Edge computing (9 cleansing strategies + rounding + filtering + formulas)

Enable via `PointEditForm_Edge`. Strategies execute in pipeline order:

| Category | Strategy | Effect |
|------|------|------|
| Value correction (modifies value) | Null filter | Detects NaN / Inf / negatives; replaces with last valid or a fixed value |
| | Dead-zone suppression | Small fluctuations are not recorded until change exceeds the threshold |
| | Spike suppression | MAD-based single-point jump detection; replaced by the median |
| | Rate limiting | Clamps the maximum change between two consecutive samples |
| | Clamping | Truncates to max / min limits |
| | Outlier removal | 3σ statistical removal of outliers |
| Quality checks (log only) | Freeze detection | N consecutive identical values (sensor may be stuck) |
| | IQR | Inter-quartile-range outlier flagging |
| | Range sanity | Flags values beyond the sensor's physical range |

Also available: **rounding** (decimal places), **filtering** (first/second order), **calculation formulas** (e.g. `{Temp} * 1.8 + 32`), **four-level alarms** (HH/H/L/LL).

### 6.7 CSV bulk import

For many variables: click **Export Template** on the device config page → fill the CSV template (UTF-8) → **Import CSV** to create variables in bulk.

---

## 7. Running Collection & the Dashboard

### 7.1 Start / stop and status

- Right-click a device (or use the menu) → **Start / Stop Collection**; each device runs in its own collection thread; parameter changes are hot-applied;
- Status lights: 🟢 collecting / 🔴 stopped / 🟡 fault;
- The status bar shows collected variable count, MQTT status and REST API status.

### 7.2 Dashboard (DashboardForm)

The default main window after start; reopen any time from the menu:

| Panel | Content |
|------|------|
| Device status | Per-device collection / MQTT / REST API status (fixed header, scrollable rows) |
| Alarm panel | White-background live alarm list in the format `[ALARM HH] [Extruder-28] BarrelTemp = 92.5 °C`; 20-minute window with auto-clear; duplicate alarms merged with counts (e.g. "Temp over limit (×3)") |
| Realtime data | Key metrics as numbers / gauges |
| Trend curves | Time-series trends of selected variables |
| Data-flow monitoring | Collection rate, write rate, network throughput |

Dashboard and collection pages **coexist**: "Collection Management" opens the collection page without hiding the dashboard; closing the dashboard returns to the collection page without exiting the process.

---

## 8. Semantic Tree Modeling (factory semantic layer)

### 8.1 Entry and layout

Toolbar → **Semantic Management** (`SemanticManagementForm`, between Database Config and the language switch). Semantic tree on the left; three tabs on the right: **Node Info / Node Relations / Node Events**.

### 8.2 Where the tree comes from (three-way sync)

- **Device config → semantic tree**: device group hierarchy syncs automatically into semantic nodes (workshop / line / equipment / variable) — no manual rebuild;
- **Datasources → semantic tree**: database datasources can be included as well;
- **Manual adjustments are preserved**: drag-and-drop / "Move to" changes are recorded as `parent_override` so later syncs will not revert them;
- Note: hierarchy edits on the semantic side are **not** written back to the device tree — change device groups in the Data Source Manager.

### 8.3 Common operations

| Operation | How |
|------|------|
| Build hierarchy | Right-click empty space → Add folder (company / workshop / line) |
| Move nodes | Drag-and-drop, or right-click → "Move to" and pick the target parent |
| Big trees stay fast | Lazy loading kicks in automatically beyond 200 nodes; search uses an in-memory cache |
| Variable relations | "Node Relations" tab: bind variables to datasource fields / constants / expressions, plus upstream/downstream relations (17 relation types) |
| Node events | "Node Events" tab: configure event rules (see chapter 9) |
| Node status | Synced nodes are driven by devices and read-only; manual nodes are freely editable |

---

## 9. Events & Alarms (12 handling methods)

### 9.1 Where to configure

Event rules attach to **semantic tree nodes**: Semantic Management → select a node → "Node Events" tab → add a rule: pick the trigger (threshold / state change / device offline) → pick one or more handling methods → fill in parameters (recipients / webhook URL …).

### 9.2 The 12 handling methods

| Method | Scenario |
|----------|------|
| Log only | Keep a record, no response |
| Alarm | On-site acknowledgement (HH/H/L/LL thresholds) |
| Message / in-site notification | Notify operators |
| Email / SMS | Remote notification |
| Webhook / Call API | Integrate ERP / MES and other systems |
| Trigger workflow / generate work order | Automated ops |
| Trigger MCP task / trigger AI analysis | AI-assisted analysis and response |

### 9.3 Alarm message format

```
[ALARM HH] [Extruder-28] BarrelTemp = 92.5 °C
[ALARM H]  [Compressor] OutletPressure = 0.85 MPa
[ALARM L]  [InverterA] Frequency = 12.3 Hz
```

The `[device name]` prefix pinpoints the source in multi-device environments; the dashboard alarm panel uses a 20-minute window, auto-clears and merges duplicates.

---

## 10. AI Assistant & MCP Service

### 10.1 Enabling MCP

1. Menu: **Tools → MCP Service Config**;
2. Set the listening port (**default 5101**; if occupied it increments automatically up to 10 tries — the effective port is in the log line "MCP service started");
3. Configure token authentication (recommended; one-click generation available);
4. Enable the service;
5. If you see "access denied": run as administrator, or execute
   `netsh http add urlacl url=http://+:5101/ user=Everyone`.

### 10.2 Connecting an AI assistant

- Endpoint: `http://[machine-IP]:[port]/mcp?token=***`
- Transport: Streamable HTTP
- Once connected, the assistant automatically discovers **50 MCP tools** and manages devices in natural language.

### 10.3 What it can do (example dialogue)

```
User: Add a Siemens S7-1200 PLC, IP 192.168.1.100
AI  : Device created (protocol S7). Now let's configure variables — provide the register addresses.

User: Temperature DB1.DBD0 (float), pressure DB1.DBD4 (float), status M0.0 (bool)
AI  : Added 3 variables (°C / MPa / bool). Configure alarms?

User: Alarm above 85 for temperature, below 0.3 for pressure
AI  : Alarm rules configured. Start collection now?

User: Start, and give me a report in 30 minutes
AI  : Collection started. (30 minutes later) Daily report generated: avg outlet temp 72.3°C … 0 alarms.
```

### 10.4 Tool groups

| Category | Count | Common tools |
|------|:--:|----------|
| Device CRUD + collection control | 7 | `add_device` `list_devices` `start_device` `stop_device` … |
| Variable management | 3 | `add_variables` `update_variables` `reload_config` |
| Datasource analytics | 4 | `datasource_list_all` `datasource_table_info` `datasource_latest_data` `datasource_query_timerange` |
| Data queries | 4 | `query_realtime_data` `query_history_data` `get_database_status` … |
| Semantic queries | 11 | `semantic_get_full_tree` `semantic_get_alarm_summary` `semantic_get_upstream` … |
| Semantic writes | 5 | `semantic_create_variable_relation` `semantic_batch_update_nodes` … |
| Fabric analytics | 2 | `fabric_list_operators` `fabric_execute` (window aggregation / trend detection / anomaly detection / correlation / root-cause / forecast / daily report) |
| Platform self-description | 1 | `introduce_platform` (overview / drivers / tools / semantic / fabric / best_practices) |
| Direct database queries | dynamic | Registered automatically per configured datasource |

> **Safety design**: destructive tools (delete device, delete variable relation) have been removed from MCP — perform them in the UI with the double-confirmation gate.
> **Analytics loop**: through the 4 datasource tools the AI completes "discover sources → inspect schema → sample data → time-range queries" without writing any SQL.

---

## 11. REST API (data for third-party systems)

1. Menu: **API Service Config** (`ApiServiceConfigForm`);
2. Default port **5000** (listens on all interfaces `http://+:5000/`); Swagger docs can be enabled;
3. Generate / configure the API token;
4. On "access denied": run as administrator or execute `netsh http add urlacl url=http://+:5000/ user=Everyone`.

Example calls (token in the header):

```
Authorization: Bearer <your-token>

GET /api/devices                      # device list
GET /api/devices/{id}/realtime        # realtime data
GET /api/variables/{id}/history?from=&to=   # history
GET /api/status                       # system status
```

---

## 12. Config Safety Net (afraid of breaking things?)

| Mechanism | Description |
|------|------|
| Auto-recovery | If `devices.json` fails to parse, it is restored automatically from `.bak`; the corrupt file is kept as `.corrupted`; logs mark `[CRITICAL]` / `[RECOVERY]` |
| 10-generation backups | Every save rotates `devices.json.bak.1 ~ bak.10`; Tools → Config History → "Roll back to this version" (current config is first saved as `bak.0`) |
| Ctrl+Z quick rollback | Outside text fields, Ctrl+Z rolls back to bak.1 (with confirmation) |
| Config templates | Right-click a device → Config Template → create / apply / overwrite; moves semantic relations, Fabric, events and cleansing to new devices |
| Device cloning | Right-click a device → Clone: full config inheritance for identical devices in ~5 minutes (adjust IP and register addresses afterwards) |
| Single instance | A global mutex blocks double launch |

**Clone vs. template**: identical models → **clone** (same variable names); different models → **apply template** (matched by variables); reusable best practices → **create template**.

---

## 13. Language, Logs and Shutdown

- **Language switch**: Settings → Language (中文 / English) — about 650 UI strings switch instantly; child windows follow automatically;
- **Logs**: `Logs/` and `log_YYYYMMDD.txt` in the config directory; logs older than 30 days are purged; crashes go to `crash.log`;
- **Shutdown**: exiting from the tray runs the ApplicationLifecycle **5-step reverse-order shutdown** (last started closes first, 5-second timeout per step) so MQTT / databases / collection threads are released cleanly.

---

## 14. FAQ

**Q1: The device shows offline but it is actually online?**
① ping / telnet for connectivity; ② open the port in the firewall (Modbus 502, S7 102, …); ③ verify station number / baud rate against the device; ④ for cross-segment devices confirm a tunnel is selected (chapter 6.4); ⑤ check `Logs/` for the actual error.

**Q2: Data is not written to the database?**
① Click "Test Connection" in Database Config (5-second timeout); ② make sure that database's **Enable database write** switch is ticked; ③ the DB user needs CREATE TABLE / INSERT rights; ④ for SQLite check the file path; ⑤ while disconnected, rows accumulate in `offline_db_cache` and are replayed on recovery; ⑥ check the device is ticked into that database's device list (empty = all).

**Q3: Data older than 7 days disappears / I want permanent retention?**
"Retention days" defaults to 7 with cleanup every 6 hours. **Set it to 0 for permanent retention.** Note: for TDengine the current version's auto cleanup does not take effect (data is not deleted) — manage retention with TDengine's `KEEP` parameter instead.

**Q4: Will an MQTT outage lose data?**
No. MQTT and the database use independent caches (`offline_mqtt_cache` / `offline_db_cache`) replayed every 10 seconds — no data loss no matter how long the outage.

**Q5: How do I subscribe to a single variable on an MQTT Subscribe device?**
Set TopicFilter to `/VariableName` (leading slash) for the per-variable sub-topic; `#` receives everything. Both JSON shapes are recognized automatically.

**Q6: The MCP / AI assistant cannot connect?**
① Make sure the MCP service is enabled; ② use the effective port from the log (default 5101, auto-increments when busy); ③ run as administrator or `netsh http add urlacl`; ④ the token must match the configuration; ⑤ transport must be Streamable HTTP.

**Q7: The Chinese tag is not generated automatically?**
① The device must belong to a group (tag = group path/device name/variable name); ② new variables auto-generate after the name is typed; ③ for old variables click "Auto-generate tag".

**Q8: I broke the configuration — how do I roll back?**
Tools → Config History → pick a version → "Roll back to this version"; or press **Ctrl+Z** outside text fields. The auto-recovery mechanism (`bak` → automatic rollback) is the last safety net.

**Q9: Can the dashboard and the collection page be open at the same time?**
Yes — coexistence since v2.2. Closing the dashboard returns to the collection page without exiting the process.

**Q10: Which column identifies a variable in industrial_data?**
For humans: `tag_zh` (Chinese tag path, e.g. `Workshop1/Extruder/Extruder-28/BarrelTemp`); `device` is the device name and `variable` the variable name; on TDengine the mapped names are `tag_id / val / ts`.

**Q11: Deploying 10 identical devices one by one is slow?**
Configure one → right-click **Clone Device** (tick everything) → change the new device's IP and register addresses → Apply → Start. Use **config templates** for different models.

**Q12: Datasource analysis freezes with many tables (e.g. TDengine with thousands)?**
The Data Source Manager supports **selective analysis**: after Test Connection only table names are loaded → tick the tables you need → "Analyze selected tables" fetches columns and row counts in bulk; results can be copied.

**Q13: "The platform is already running"?**
Single-instance protection. To restart, close the current instance (tray exit); if a stale lock remains after a crash, wait a few seconds or kill the old process in Task Manager.

**Q14: Forgot the admin password?**
The account store is `auth.db` in the config directory. Contact technical support (18854344113 / 751326339@qq.com) for a reset — do not delete auth.db, or other settings may be affected.

**Q15: MCP / REST port shows "access denied"?**
Both services listen on all interfaces (`http://+:port/`). Run as administrator, or authorize once: `netsh http add urlacl url=http://+:5101/ user=Everyone` (REST uses 5000).

**Q16: Anything to watch out for when upgrading / migrating machines?**
Back up the whole config directory (especially `devices.json`, `groups.json`, `mqtt.json`, `dbConfig.json`, `semantic_v2.db`, `license.dat`); `license.dat` is bound to the MAC address — contact technical support for re-licensing before changing hardware.

---

## 15. Notes & Best Practices

1. **Change the default password first** (admin/admin) — mandatory for production;
2. **Back up the config directory and SQLite files externally on a schedule** (the 10-generation automatic backup is good, a second copy is better);
3. **Set collection intervals realistically** — overly short intervals overload devices and networks;
4. **Cleanse at the source** (the 9 edge strategies) to reduce useless traffic and storage;
5. **Prefer wired networks** on the factory floor;
6. **Manage TDengine retention with the database-side KEEP**, and set the platform retention to 0 (see Q3);
7. **Stop collection before changing group hierarchies** (tag paths change; the app will ask for confirmation);
8. **Batch deployment**: clone (identical models) + templates (different models);
9. **High-risk operations in the UI only**: deleting devices or variable relations happens in the interface with double confirmation, not via MCP;
10. **Logs are purged after 30 days** — export `crash.log` and the day's log promptly when issues occur.

---

## 16. Technical Support & Contact

| Item | Information |
|----|------|
| Phone | **18854344113**　**18354356258** |
| Email | **751326339@qq.com** |
| Scope | Installation & deployment / licensing / driver integration / database & MQTT integration / AI assistant onboarding / troubleshooting / upgrades |

> When reporting an issue, please attach: today's log from `Logs/`, `crash.log` (if any), the device driver type and a screenshot of the connection parameters (sanitized) — it speeds up diagnosis considerably.

---

*Document version: v1.0 (2026-09-11) · Applicable product: v2.6.x Enterprise Edition · Author: Zhang Chenglong*
