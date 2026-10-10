# Architecture

## Projects

```
ByteTide.Core     (net8.0)           no UI dependency, shared
ByteTide.App      (net8.0-windows)   WinForms tray app  -> Core
ByteTide.Service  (net8.0-windows)   Worker / Windows Service -> Core
ByteTide.Tests    (net8.0)           xUnit -> Core
```

## Core building blocks

| Type | Role |
|---|---|
| `NetworkSampler` | Every second reads Windows interface counters and raises `SamplesReady` with a `LiveSample` per adapter (bytes, rates, link speed). |
| `Storage` | SQLite wrapper (Microsoft.Data.Sqlite): daily stats, live samples, settings. |
| `DailyRollupTracker` | Accumulates samples into per-day, per-adapter totals and flushes them to `daily_stats` roughly once a minute. |
| `LegacyImporter` | Parses RAS Graph & Stats `.dat` files into the same schema. |
| `RatesRecorder` | Periodic rate logging to text/CSV. |
| `NotificationEngine` / `NotificationSettings` | Threshold alerts and their configuration. |

## Data flow

```
Windows interface counters
        │  (1 Hz)
   NetworkSampler ──► LiveSample[]
        │                 │
        │                 ├─► Storage.live_samples  (pruned after 48 h)  ─► live graph
        │                 └─► DailyRollupTracker ─► Storage.daily_stats ─► Reports
        └─► NotificationEngine ─► sound / beep / email / run program
```

The GUI (`TrayContext` → `MainForm`) and the Service (`MonitorBackgroundService`) wire these same pieces together; only the host differs.

## Database schema

```sql
daily_stats(date, adapter_name, mac, seconds_online, bytes_received, bytes_sent)
  PRIMARY KEY (date, adapter_name, mac)
live_samples(ts, adapter_id, bytes_received, bytes_sent, down_bps, up_bps)
app_settings(key, value)          -- JSON blobs for view/window/notification prefs
```

## Design notes

- Windows-only by design (WinForms + native interface counters). Both executables target `win-x64` so only one platform's native SQLite binaries ship.
- `GraphControl` caches fonts, pens and brushes; avoid per-repaint allocations.
- The tray icon is regenerated at most every 2 seconds.
- Shutdown order matters: storage must stay open until after `Application.Exit()` returns, because the main form saves its position on close.
