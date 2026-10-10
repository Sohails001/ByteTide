<div align="center">

# 🌊 ByteTide

**A fast, free, open-source network bandwidth monitor for Windows.**
Live tray graph · per-adapter tracking · daily / weekly / monthly / yearly reports · no drivers, no packet capture.

[![Build](https://github.com/Sohails001/ByteTide/actions/workflows/build.yml/badge.svg)](https://github.com/Sohails001/ByteTide/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/Sohails001/ByteTide?display_name=tag)](https://github.com/Sohails001/ByteTide/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)

[Download](#-download) · [Features](#-features) · [Documentation](#-documentation) · [Contributing](CONTRIBUTING.md)

</div>

---

## Why ByteTide?

Classic freeware traffic monitors such as *RAS Graph & Stats* did one job well: show
what your connection is doing right now, and remember how much you used. Many of them are
abandoned, closed-source, or no longer run properly on modern Windows.

ByteTide is a modern, open-source take on that idea:

- **Lightweight.** It reads the same interface counters Windows already exposes. No packet capture, no kernel driver, no Npcap.
- **Private.** Everything is stored locally in a single SQLite file. No account, no telemetry, no network calls (other than the optional email alerts *you* configure).
- **Keeps your history.** Imports old RAS Graph & Stats `.dat` archives, so years of data sit side by side with new data.
- **Runs your way.** A tray app for desktops, or a headless Windows Service for always-on machines.

> 📸 *Screenshots go here.* Add images to `docs/images/` and reference them, e.g. `![Main window](docs/images/main-window.png)`.

## ⬇ Download

Grab the latest `ByteTide-win-x64.zip` from the [**Releases**](https://github.com/Sohails001/ByteTide/releases) page, extract it anywhere, and run `ByteTide.exe`.

Requirements: Windows 10 / 11 (x64). The release build is self-contained, so no separate .NET install is needed.

Closing the window just hides it to the system tray. Sampling and daily totals keep running. Use **Exit** from the tray icon to quit.

## ✨ Features

**Live monitoring**
- Real-time download/upload graph (filled-area or column style)
- Tray icon that is itself a live mini-graph
- Status bar with link speed, uptime, clock, and session totals
- Watch one adapter (Ethernet, Wi-Fi, VPN…) or all combined

**Reports**
- Summary, Daily, Weekly, Monthly and Yearly tabs
- Sort by any column, filter by adapter
- Cumulative time online per period
- CSV export

**Customisation**
- Toggle title/IP overlay, scale and time labels, gridlines, average lines, max-rate text
- KB/s ↔ kbps switch
- Always-on-top, semi-transparent, and click-through (HUD) window modes
- Window position and all preferences are remembered

**Tools**
- **Rates Recorder**: log download/upload rates to a text or CSV file at a set interval
- **Traffic Stopwatch**: timed transfer test with total, max, min and average rate
- **Usage Notifications**: alert when usage crosses a threshold (e.g. "2 GB this month") by sound, beep, email (SMTP) or by running a program

**Windows Service**
- Headless background monitoring without a logged-in user
- Writes the same database schema as the GUI app
- Scheduled plain-text usage report

## 🚀 Quick start

1. Download and extract the release zip.
2. Run `ByteTide.exe`. A graph window opens and a tray icon appears.
3. Open **Adapters** to choose which network adapter to watch.
4. Open **Reports** after a day of use to see your history.

Importing old RAS Graph & Stats history? See the [Legacy Import guide](docs/LEGACY_IMPORT.md).

## 📚 Documentation

| Guide | What's in it |
|---|---|
| [User Guide](docs/USER_GUIDE.md) | Every menu, option and tool explained |
| [Windows Service](docs/SERVICE.md) | Install, configure, and remove the background service |
| [Legacy Import](docs/LEGACY_IMPORT.md) | Bring over RAS Graph & Stats history |
| [Architecture](docs/ARCHITECTURE.md) | How the code and database are organised |
| [FAQ & Troubleshooting](docs/FAQ.md) | Common questions and fixes |
| [Changelog](CHANGELOG.md) | What changed in each release |

## 🛠 Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Windows (WinForms and the network APIs are Windows-only).

```powershell
git clone https://github.com/Sohails001/ByteTide.git
cd ByteTide
dotnet build ByteTide.sln
dotnet test tests/ByteTide.Tests/ByteTide.Tests.csproj
dotnet run --project src/ByteTide.App
```

Or open `ByteTide.sln` in Visual Studio 2022+.

To produce a single-file executable like the official releases:

```powershell
dotnet publish src/ByteTide.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## 🗂 Project layout

```
src/
  ByteTide.Core/      Sampling, storage, legacy importer, rollups, notifications (no UI)
  ByteTide.App/       WinForms tray app: graph, reports, tools, settings
  ByteTide.Service/   Headless Windows Service
tests/
  ByteTide.Tests/     xUnit tests
docs/                 User and developer documentation
```

## 💾 Where is my data?

| Component | Location |
|---|---|
| GUI app | `%AppData%\ByteTide\data.db` |
| Service | `%ProgramData%\ByteTide\data.db` (or `DataDbPath` in its `appsettings.json`) |

It's a standard SQLite file; open it with any SQLite browser. Back it up by copying it.

## ⚠ Known limitations

- If the app or service restarts mid-day, that day's byte-counter baseline resets, so the rest of that day can be under-counted. Persisting baselines would fix this.
- SMTP passwords for email alerts are stored as plain text in the local settings table. Don't reuse a sensitive account's password.
- The GUI database path isn't configurable yet.
- Reports column headers don't show a ▲/▼ sort indicator yet.
- Memory use is typical for a .NET WinForms app (roughly 20 MB), higher than a tiny native Win32 tool.

These are good first contributions. See [CONTRIBUTING.md](CONTRIBUTING.md).

## 🤝 Contributing

Bug reports, feature ideas and pull requests are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md) first. Security issues: see [SECURITY.md](SECURITY.md).

## 📄 License

[MIT](LICENSE) © Sohail Sher Ali and ByteTide contributors. Free to use, modify and redistribute.

ByteTide is an independent project and is not affiliated with or endorsed by the authors of RAS Graph & Stats or any other product mentioned here. Product names are used only to describe compatibility.

---

<div align="center">
Created by <b>Sohail Sher Ali</b> · If ByteTide is useful to you, a ⭐ helps others find it.
</div>
