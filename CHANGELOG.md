# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

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
