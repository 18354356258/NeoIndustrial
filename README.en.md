<div align="center">

# 18354356258

**Translating shop-floor machines into knowledge that AI can actually read.**

Industrial devices are a zoo of incompatible protocols. I do exactly one thing: **bridge 39 industrial protocol drivers, then shape the data into a semantic ontology that machines can interpret and humans can trust.**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Blazor](https://img.shields.io/badge/Blazor_Server-InteractiveServer-512BD4?logo=blazor&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![SQLite](https://img.shields.io/badge/SQLite-003B57?logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![MCP](https://img.shields.io/badge/MCP-Model_Context_Protocol-3B82F6)](https://modelcontextprotocol.io/)
[![Linux](https://img.shields.io/badge/Linux-x64%20%7C%20arm64-FCC624?logo=linux&logoColor=black)](https://www.kernel.org/)

</div>

---

## 🏭 Flagship Project · NeoIndustrial

> **An industrial data acquisition and semantic governance platform** — one unbroken loop from device protocol onboarding and time-series storage to semantic ontology and knowledge graph.
> Closed-source, commercially delivered; shipped as both a Windows pilot package and a Xinchuang (domestic IT stack) self-test package.

| Capability | What is actually shipped |
| --- | --- |
| **Protocols** | **39 built-in industrial protocol drivers across 7 categories**: general industrial / PLC / CNC / building automation / power & energy / semiconductor / general purpose. Covers Modbus TCP·RTU, Siemens S7, OPC UA·UA PubSub·OPC DA, Mitsubishi MC·FX, Omron FINS·HostLink, Keyence KV, IEC 104, IEC 61850, DNP3, BACnet, EtherNet/IP, PROFIBUS, DeviceNet, CC-Link, HART-IP, MQTT·Sparkplug B, MTConnect, SECS/GEM, Fanuc FOCAS, CODESYS, Beckhoff ADS, Heidenhain, KNX, DALI, M-Bus, DLMS, SNMP, Haas·Mazak machine tools, HttpRest, and a simulator |
| **AI · MCP** | **150 MCP tools** built in (140 visible to the AI chat side, 59 of them write-capable). The AI can maintain the semantic layer and the ontology on its own — including **searching the web and then installing a driver by itself**; write access is gated by three layers |
| **Databases** | **Five families of database drivers** — relational / time-series / domestic Xinchuang / embedded-file-based / other — including MySQL, SQL Server, PostgreSQL, **Oracle (with schema selection)**, DM (Dameng), KingbaseES, SQLite and TDengine |
| **Semantics & graph** | Semantic ontology plus a 3D spherical-layout graph preview; relation types organised as a three-level catalogue of **8 categories / 48 families / 268 subtypes**, with custom types and a full change log |
| **Multi-agent** | A built-in orchestrator ("Hermes") plus virtual sub-agents that take dispatched work, splitting long pipelines across specialised roles |
| **Xinchuang ready** | Kylin OS / UnionTech UOS; **dual architecture x64 (Zhaoxin · Hygon · Intel) and arm64 (Phytium · Kunpeng)**, self-contained Linux release |
| **Delivery** | Windows pilot package + Xinchuang self-test package, with checksums, delivery notes and a third-party component licence list; UI in three languages (简体中文 / English / Tiếng Việt) |
| **Architecture** | Plugin-based drivers (upload / AI install / shadow-copy loading / start-stop / removal), plugin-based database drivers, three-layer write-tool gating, Service Worker cache versioning |

**📦 Project repositories (same project, two hosts)**
[![GitHub](https://img.shields.io/badge/GitHub-NeoIndustrial-181717?logo=github&logoColor=white)](https://github.com/18354356258/NeoIndustrial)
[![Gitee](https://img.shields.io/badge/Gitee-JEDI__MASTER-C71D23?logo=gitee&logoColor=white)](https://gitee.com/JEDI_MASTER/neoIndustrial)

---

## 🧩 Tech Stack

- **Industrial protocols** — PLC / CNC / power / building / semiconductor / general purpose; 39 drivers, mixing open-source implementations with in-house ones
- **Backend & frontend** — C# / .NET 8, Blazor Server (per-page `InteractiveServer` render mode), plugin-based driver loading
- **Data** — relational + time-series + Xinchuang + embedded storage; SQLite holds the metadata layer
- **AI & tooling** — MCP server (150 tools), multi-agent collaboration, AI-driven semantic/ontology self-maintenance
- **Xinchuang & delivery** — Kylin / UOS, x64 + arm64, self-contained Linux release, dual packages with verifiable manifests
- **Engineering method** — AI pairing / cluster-style development; a self-built acceptance pipeline: isolated instance → browser end-to-end CDP verification → geometric assertions → read-only production re-verification

---

## 📊 GitHub

<div align="center">

![Stats](https://github-readme-stats.vercel.app/api?username=18354356258&show_icons=true&theme=ocean_dark&hide_border=true&count_private=true)
![Top Langs](https://github-readme-stats.vercel.app/api/top-langs/?username=18354356258&layout=compact&theme=ocean_dark&hide_border=true)

</div>

---

## 🎯 What I'm working on

- **Lowering the cost of onboarding** on the factory floor — the more plugin-based drivers get, the closer a new device is to "just upload it"
- **Making the semantic layer genuinely useful** — moving from "we collected data" to "the data carries its own meaning", so AI works from an ontology instead of guessing
- **Keeping delivery equally complete on the Xinchuang stack** — dual architecture, tri-lingual UI, verifiable manifests

> Industrial software doesn't lack feature lists. It lacks the guarantee that **every device, every protocol and every delivery can be reproduced and verified.**

<div align="center">

**It connects. It understands. It ships.**

</div>

<!--
==================== FACT-CHECK LIST (verify before publishing, do not delete) ====================
1) GitHub account 18354356258 / Gitee account JEDI_MASTER
   → Source: user-provided (same project mirrored to both hosts)
2) Project name NeoIndustrial; positioned as "industrial data acquisition and semantic governance
   platform"; closed-source commercial delivery
   → Source: user-provided
3) 39 industrial protocol drivers / 7 categories (general industrial, PLC, CNC, building automation,
   power & energy, semiconductor, general purpose)
   → Source: user-provided (measured figures)
4) Protocol list is the user-provided example list; no protocol name was invented or added.
   Note: the list is illustrative and shorter than 39 — do not infer the total from the list.
   → Source: user-provided
5) 150 MCP tools / 140 visible on the AI chat side / 59 write-capable
   → Source: user-provided (measured figures)
6) Database "five families": relational / time-series / domestic Xinchuang / embedded-file-based / other
   → Source: user-provided; examples: MySQL, SQL Server, PostgreSQL, Oracle (with schema selection),
     DM (Dameng), KingbaseES, SQLite, TDengine
   * Check: the text says "five families" but names four families + "etc.". Align with source if the
     official naming differs.
7) Knowledge graph: semantic ontology + 3D spherical layout preview; relation-type three-level
   catalogue of 8 categories / 48 families / 268 subtypes
   → Source: user-provided (measured figures)
8) Multi-agent: built-in orchestrator Hermes + virtual sub-agent dispatch
   → Source: user-provided
9) Xinchuang: Kylin / UnionTech UOS; x64 (Zhaoxin, Hygon, Intel) + arm64 (Phytium, Kunpeng);
   self-contained Linux release
   → Source: user-provided
10) UI in three languages: Simplified Chinese / English / Vietnamese
   → Source: user-provided
11) Delivery: Windows pilot package + Xinchuang self-test package; includes checksums, delivery
    notes, third-party component licence list
   → Source: user-provided
12) Architecture: plugin-based drivers (upload / AI install / shadow-copy load / start-stop / remove),
    plugin-based DB drivers, three-layer write-tool gating, Service Worker cache versioning
   → Source: user-provided
13) Development method: AI pairing / cluster-style development; acceptance pipeline = isolated
    instance + browser end-to-end CDP verification + geometric assertions + read-only production
    re-verification
   → Source: user-provided (real facts)
14) Stack: .NET 8 / C# / Blazor Server (InteractiveServer) / SQLite metadata
   → Source: user-provided
15) Badges are static shields.io badges (no external data); stats cards use github-readme-stats with
    username hardcoded as 18354356258
16) Prohibited-content self-check: no real name, no employer/title/degree/years/city, no awards,
    papers, patents or customer names, no email or phone, no inflated claims ("world-leading", etc.),
    no unverified numbers (users, installs, stars)
=============================================================================================
-->
