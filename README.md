# 🔬 BinOcular

<p align="center">
  <strong>Automated Static Binary, Firmware & Malware Triage Workbench</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/React-19.0-61DAFB?style=for-the-badge&logo=react&logoColor=black" alt="React 19" />
  <img src="https://img.shields.io/badge/TypeScript-5.7-3178C6?style=for-the-badge&logo=typescript&logoColor=white" alt="TypeScript" />
  <img src="https://img.shields.io/badge/Tailwind_CSS-v4-06B6D4?style=for-the-badge&logo=tailwindcss&logoColor=white" alt="Tailwind CSS" />
  <img src="https://img.shields.io/badge/SQLite-3-003B57?style=for-the-badge&logo=sqlite&logoColor=white" alt="SQLite" />
  <img src="https://img.shields.io/badge/Docker-Ready-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker" />
  <img src="https://img.shields.io/badge/License-MIT-green?style=for-the-badge" alt="MIT License" />
</p>

---

## 📌 Overview

**BinOcular** is a high-performance, automated static binary analysis and firmware triage workbench designed for security analysts, incident responders, malware researchers, and reverse engineers.

When evaluating suspicious files (`.exe`, `.dll`, `.bin`, `.elf`, `.so`, `.dylib`), detonating them in a sandbox can be time-consuming, evasion-prone, or risky. **BinOcular** instantly extracts static indicators of compromise (IOCs), calculates whole-file and per-section Shannon entropy, identifies packers and compilers, detects dangerous RWX memory permissions, maps adversary techniques to the **MITRE ATT&CK® matrix**, and scores overall risk—**all without executing the untrusted binary**.

---

## ✨ Key Capabilities

- **⚡ 15 Automated Inspection Engines**: Runs a battery of static analysis routines concurrently in an asynchronous background pipeline.
- **🛡️ Composite Threat Scoring**: Computes an actionable 0–100 heuristic risk score assessing high entropy, evasion APIs, injection stubs, and embedded payloads.
- **🗺️ MITRE ATT&CK® Alignment**: Automatically maps detected capabilities (e.g. process hollowing, keylogging primitives, anti-debugging) to MITRE Enterprise Tactics & Techniques.
- **🔍 Deep Binary Strings Explorer**:
  - Virtualized, paginated extraction for ASCII and UTF-16 strings.
  - Automated categorization into **Network IOCs** (IPs, URLs), **Windows Registry Keys**, **File & Debug Paths**, and **Suspicious Shell Commands**.
  - One-click copy and full `.txt` raw file export.
- **📑 Executive Incident Briefing Export**: Formats all triage findings, scores, and tables into a clean, markdown-formatted report copied straight to your clipboard for SOC tickets or Jira.
- **🖥️ Forensic-Grade UI**:
  - Dark-mode forensic cybersecurity styling.
  - 4 inspection view modes: **Grid** (side-by-side cards), **Split Inspector** (directory sidebar + focused pane), **All Strings**, and **Raw Terminal Streams**.
  - Built-in keyboard shortcuts (`1`, `2`, `3`, `4`, `F`, `Esc`, `?`).
- **🐳 Single-Container Architecture**: Includes a production multi-stage Dockerfile that compiles the React frontend into `.NET` static files (`wwwroot`), auto-migrates the database on boot, and runs out of the box.

---

## 🏗️ Architecture & Pipeline Flow

```
   [ User File Upload ] (.exe, .elf, .dll, .bin)
           │
           ▼
┌────────────────────────────────────────────────────────┐
│  ASP.NET Core 10 Web API                              │
│  - Multi-part stream upload (100MB max limit)          │
│  - SHA-256 pre-calculation & deduplication check       │
│  - Enqueues job to Channel<AnalysisJob>                │
└───────────────────────┬────────────────────────────────┘
                        │
                        ▼
┌────────────────────────────────────────────────────────┐
│  Background Analysis Worker Service                    │
│  - Concurrently executes 15 Heuristic Tools            │
│  - Fault-isolated try/catch execution boundaries       │
│  - Transforms raw output via IToolResultFormatter      │
└───────────────────────┬────────────────────────────────┘
                        │
                        ▼
┌────────────────────────────────────────────────────────┐
│  Storage & Persistence Layer (SQLite + EF Core)       │
│  - Analyses, Status, Timestamps, Threat Verdicts       │
│  - Pre-computed JSON sections & raw logs               │
└───────────────────────┬────────────────────────────────┘
                        │ Real-Time Polling / REST API
                        ▼
┌────────────────────────────────────────────────────────┐
│  React 19 Forensic Dashboard                           │
│  - Smart queue monitor & dynamic status pills          │
│  - 4 Inspection View Modes + Strings Explorer          │
│  - Instant Markdown Incident Report Generation         │
└────────────────────────────────────────────────────────┘
```

---

## 🛠️ The 15 Analysis Engines

| # | Inspection Engine | Description | Key Indicators Detected |
|---|---|---|---|
| **1** | **File Format & Architecture** | Inspects magic bytes, headers, and target architectures. | PE32/PE32+, ELF64, Mach-O, ASLR, DEP, PIE, SafeSEH. |
| **2** | **Cryptographic Hashes & Entropy** | Calculates MD5, SHA-1, SHA-256, SHA-512, and Shannon entropy. | Whole-file entropy ($\ge 7.2$), crypto digests. |
| **3** | **SHA-256 Verifier** | Verifies file integrity against computed cryptographic hashes. | Checksum matching, integrity proof. |
| **4** | **Detect It Easy (DIE)** | Heuristic packer, protector, compiler, and linker detection. | UPX, Themida, VMProtect, InnoSetup, Nullsoft. |
| **5** | **Binary Sections & Memory Perms** | Analyzes PE/ELF sections, raw vs virtual sizes, and entropy. | **RWX sections** (in-memory injection primitives). |
| **6** | **Imports & DLL Dependencies** | Scans import tables against high-risk API signatures. | Process hollowing, remote thread injection, keyloggers. |
| **7** | **Printable Strings & IOCs** | Extracts ASCII & UTF-16 strings with regex classifiers. | Hardcoded IPs, C2 URLs, registry paths, GUIDs. |
| **8** | **Crypto Algorithm Scanner** | Scans for cryptographic constants and S-Boxes. | AES, DES, MD5, SHA-1/256 constants, RC4 S-Boxes. |
| **9** | **Certificates & Secrets** | Extracts digital signatures, Authenticode, and leaked keys. | Self-signed certs, embedded private keys, JWTs, tokens. |
| **10** | **Embedded Assets & Droppers** | Inspects resource headers and overlays for nested files. | Embedded secondary PEs, ZIPs, cab files, dropper stubs. |
| **11** | **Compiler & Timestamp Anomaly** | Extracts linker timestamps and compiler signatures. | Timestomping anomalies, future dates, Delphy/Go/Rust. |
| **12** | **Heuristic Risk Scorer** | Composite algorithm weighing multiple malicious indicators. | Threat score (0–100), verdict: Clean/Low/Suspicious/Malicious. |
| **13** | **MITRE ATT&CK® Mapper** | Translates detected capabilities into adversary techniques. | T1055 (Injection), T1056 (Keylogging), T1027 (Obfuscation). |
| **14** | **Network Endpoint & C2 Parser** | Identifies external communication endpoints and protocols. | IPv4/IPv6, FQDNs, WebSockets, HTTP User-Agents. |
| **15** | **Opcode & Shellcode Pattern Matcher** | Disassembles byte patterns for exploitation primitives. | NOP sleds, syscall stubs, anti-debug (`RDTSC`, `INT 3`). |

---

## 🚀 Quick Start

### Option 1: Docker (Recommended — 1 Command)

Ensure you have [Docker](https://www.docker.com/) installed:

```bash
# Clone the repository
git clone https://github.com/your-username/BinOcular.git
cd BinOcular

# Build and start the entire app (Frontend + Backend + DB)
docker compose up --build -d
```

Open your browser to: **`http://localhost:8080`**

---

### Option 2: Local Development

#### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+ / 22](https://nodejs.org/) & `npm`

#### 1. Start Backend API
```bash
cd backend/BinOcular.Api
dotnet run
# API listens on http://localhost:5279
```

#### 2. Start Frontend UI
```bash
cd frontend/binocular-ui
npm install
npm run dev
# Dashboard opens on http://localhost:5173 (proxies /api to 5279)
```

---

## 📡 REST API Reference

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/analyses` | Upload binary for triage (`multipart/form-data`, 100MB limit). |
| `GET` | `/api/analyses` | Get paginated list of analyses (`?page=1&pageSize=8&search=&status=`). |
| `GET` | `/api/analyses/{id}` | Get metadata and status for a single analysis. |
| `GET` | `/api/analyses/{id}/tool-results` | Get formatted output and JSON results for all 15 tools. |
| `GET` | `/api/analyses/{id}/strings` | Paginated binary strings with category filters and search. |
| `GET` | `/api/analyses/{id}/strings/download` | Download all extracted strings as a formatted `.txt` file. |
| `GET` | `/api/analyses/stats` | Aggregated dashboard stats (Total, Completed, Running, Queued, Failed). |
| `DELETE` | `/api/analyses/{id}` | Delete analysis record and remove binary file from disk. |
| `DELETE` | `/api/analyses` | Clear database and purge all uploaded binaries. |
| `GET` | `/healthz` | Health check probe for container orchestrators. |

---

## ⌨️ Keyboard Navigation

When inspecting an analysis, BinOcular supports fast keyboard shortcuts:

| Key | Action |
|:---:|---|
| <kbd>1</kbd> | Switch to **Grid View** (all 15 tool cards side-by-side) |
| <kbd>2</kbd> | Switch to **Split Inspector** (directory sidebar + focused view) |
| <kbd>3</kbd> | Switch to **Strings Explorer** (full searchable strings table) |
| <kbd>4</kbd> | Switch to **Raw Output** (direct CLI stdout / stderr streams) |
| <kbd>F</kbd> | Toggle **Fullscreen Mode** |
| <kbd>Esc</kbd> | Close inspector modal |
| <kbd>?</kbd> | Open Keyboard Shortcuts Cheat Sheet |

---

## 🌐 Free Hosting Guide

BinOcular can be deployed completely **free of charge** using several cloud providers:

### Option A: Fly.io (Zero-Config Container + Persistent Volume)
1. Install the Fly CLI: `brew install flyctl` (or `curl -L https://fly.io/install.sh | sh`)
2. Log in: `fly auth login`
3. Launch app: `fly launch` (it will auto-detect the root `Dockerfile`)
4. Create a persistent volume for the SQLite database and uploads:
   ```bash
   fly volumes create binocular_data --size 1
   ```
5. Deploy: `fly deploy`

### Option B: Render.com (Free Web Service)
1. Fork or push this repository to GitHub.
2. Sign in to [Render](https://render.com) and create a **New Web Service**.
3. Connect your GitHub repository.
4. Select **Docker** environment and set the instance type to **Free**.
5. Render will build the multi-stage Dockerfile and issue an automatic SSL certificate.

### Option C: Oracle Cloud Always Free VM (Production VPS)
Oracle Cloud provides **4 ARM CPU cores and 24 GB of RAM** 100% free forever:
1. Provision an Ubuntu 24.04 ARM Compute instance in Oracle Cloud Free Tier.
2. Install Docker & Docker Compose: `sudo apt update && sudo apt install -y docker.io docker-compose-v2`
3. Clone your repository: `git clone https://github.com/your-username/BinOcular.git && cd BinOcular`
4. Run: `sudo docker compose up -d`
5. Optional: Use [Caddy](https://caddyserver.com/) for automatic free Let's Encrypt HTTPS certificates.

---

## 🔒 Security Notice

BinOcular performs **pure static analysis** (parsing file structures, headers, opcodes, and strings without invoking dynamic execution). However, whenever handling untrusted malware samples or unknown binaries, it is strongly advised to deploy BinOcular inside an isolated container, VM, or dedicated analysis network.

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.
