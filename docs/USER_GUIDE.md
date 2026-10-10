# User Guide

## The main window

The main window shows a live graph of download and upload speed, with a status bar underneath (link speed, uptime, clock, and download/upload totals and live speed).

Closing the window **hides it to the tray**: monitoring continues. Double-click the tray icon (or right-click → **Open**) to bring it back. Right-click → **Exit** to quit completely.

## Menus

### File
- **Import Legacy Data...** — import RAS Graph & Stats archives. See [Legacy Import](LEGACY_IMPORT.md).
- **Exit** — quit the application.

### View
Controls what the graph draws. Each option is a toggle and is remembered.

- Window title / IP address overlay
- **Graph (Area)** or **Column Graph** style
- Scale labels and scale gridlines
- Time labels and time gridlines
- Average download/upload reference lines
- Max rate and average speeds text
- Master **Text** toggle
- **KB/s ↔ kbps** unit switch

### Window
- **Always on Top**
- **Semi-transparent** — about 85% opacity
- **Click-through** — the window ignores mouse clicks so it can sit over other apps like a HUD. Turn it off from the menu (or the tray icon) if you need to click the window again.
- **Reset Window Position** — useful if the window ends up off-screen after a monitor change.

### Adapters
Choose **All Connections** (combined) or a single adapter such as Ethernet, Wi-Fi or a VPN. With "All Connections" the status bar shows the busiest adapter's link speed.

### Tools
- **Rates Recorder** — logs download/upload rates for one adapter (or all combined) to a text or CSV file at an interval you choose. Handy for spotting patterns or feeding a spreadsheet.
- **Traffic Stopwatch** — start it, do your transfer, stop it. You get total data moved plus max, min and average rate for that window. Good for quick speed tests.
- **Bandwidth Usage Notifications** — raise an alert when usage crosses a threshold, e.g. "more than 2 GB downloaded this month". Actions: sound, beep, email via your SMTP server, and/or run a program. See [Email alerts](#email-alerts).

### Reports
Tabs for **Summary**, **Daily**, **Weekly**, **Monthly** and **Yearly**.

- Columns: DL, UL, DL & UL, Time.
- Click a column header to sort; click again to reverse. The default is newest first.
- **Time** is cumulative time online for the period and can exceed 24 hours (for example `2270:59:17` for a year).
- Filter by adapter, export to **CSV**, or clear history.

### About
Shows version and credits.

## Email alerts

Configure SMTP details in the notifications dialog (server, port, credentials, from/to). Use an app-specific password, not your main account password, since it is stored as plain text locally. See [SECURITY.md](../SECURITY.md).

## Tray icon

The tray icon draws a tiny live graph of recent traffic and refreshes every couple of seconds to keep CPU use low.

## Where data is kept

`%AppData%\ByteTide\data.db`, a SQLite database. Live samples are pruned after 48 hours; daily totals are kept indefinitely. To back up your history, copy the file while the app is closed.
