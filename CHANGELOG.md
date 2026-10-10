# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [1.0.1]

### Fixed
- **Click-through could trap you.** With Click-through on, the window ignored all mouse clicks, and the tray menu only had Open and Exit, so there was no way to turn it off. Now:
  - Click-through is **never remembered** between runs; ByteTide always starts with it off (this also rescues anyone who was already stuck).
  - **Double-clicking the tray icon, or choosing Open**, always switches click-through off.
  - The tray menu now has **Window > Click-through**, and a balloon tip explains how to undo it whenever it is switched on.
- **File > Exit** now shuts down cleanly through the same path as the tray's Exit (no leftover ghost tray icon, daily totals flushed).

### Added
- The tray icon's right-click menu now contains **every option from the main window**: Adapters, View, Window, Tools, Reports, Import and About.
- **Tray icon styles:** mini graph, speed numbers (download over upload), and classic dial-up monitors whose screens light up on activity, with colour sets Green/Yellow, Green, Yellow, Blue, XP Blue, ZoneAlarm, and custom colours.
- **Hover tooltip** on the tray icon showing current download and upload speed.
- **Floating mini meter:** small always-on-top window with readable speeds and a mini graph; drag it anywhere, right-click to close.
- Option to show tray and meter speeds in **bits (kbps / Mbps)** instead of bytes.

## [1.0.0] - First public release

The project was developed privately as "Bandwidth Monitor" (internal versions v1 to v3.4) and is released publicly under the name **ByteTide**.

### Changed
- Renamed to **ByteTide**: namespaces, assemblies (`ByteTide.exe`, `ByteTideService.exe`), solution and project names.
- Application data folder is now `%AppData%\ByteTide`. An existing `%AppData%\BandwidthMonitor` folder is moved automatically on first launch, so history is preserved.
- Service configuration section in `appsettings.json` is now `ByteTide`; the default service name is `ByteTideService` and its default data folder is `%ProgramData%\ByteTide`.

### Included (from internal v3.x)
- Live graph with filled-area and column styles; per-adapter selection; live tray icon graph.
- Daily, Weekly, Monthly and Yearly reports with sorting, adapter filter and CSV export.
- View and Window options: overlays, gridlines, average lines, always-on-top, semi-transparent and click-through modes.
- Tools: Rates Recorder, Traffic Stopwatch, Usage Notifications (sound, beep, email, run program).
- Legacy RAS Graph & Stats `.dat` importer.
- Headless Windows Service with scheduled text report.
- Fixes from internal v3.1 to v3.4: status bar layering, solid graph fill, overlays drawn above the chart, crash on Exit, clipped Browse button, reduced CPU use and smaller build output.
