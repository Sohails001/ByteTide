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
- **Click-through** — the window ignores mouse clicks so it can sit over other apps like a HUD. **Undo it from the tray icon:** double-click the tray icon, or right-click it → **Window → Click-through**. Click-through is never remembered, so ByteTide always starts with it off.
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

Right-click the tray icon for a menu with **everything the main window's menu bar has** (Adapters, View, Window, Tools, Reports, Import, About), plus **Tray Icon** options and Exit. Double-click the icon (or choose **Open ByteTide**) to bring the window forward; this also switches click-through off.

Hover over the icon to see a tooltip with the current download (▼) and upload (▲) speed.

### Tray Icon menu

- **Icon style**
  - **Mini graph**: a tiny live line graph (blue = download, red = upload).
  - **Speed numbers**: two short numbers, download on top, upload underneath (for example `391M` over `0`). Tray icons are only about 16 pixels, so for fully readable text use the floating mini meter.
  - **Classic dial-up monitors**: two little computers. The back screen lights up while downloading, the front one while uploading, like the old dial-up status icon.
- **Dial-up monitor colors**: Green / Yellow, Green, Yellow, Blue, XP Blue, ZoneAlarm, or **Custom** (choose your own download and upload colors).
- **Show speeds in bits (kbps / Mbps)**: switches the tooltip, speed numbers and mini meter from bytes to bits.
- **Floating mini meter**: a small always-on-top window with readable speeds and a mini graph. Drag it anywhere (it remembers where), right-click it to close. It never ignores the mouse.

## Where data is kept

`%AppData%\ByteTide\data.db`, a SQLite database. Live samples are pruned after 48 hours; daily totals are kept indefinitely. To back up your history, copy the file while the app is closed.
