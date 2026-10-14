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
- **Graph (Area)**, **Column Graph** or **Line Graph** style
- **Graph width (pixels per second)**: 1 to 6. Smaller values show more history; larger values zoom in
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

**Count in All Connections** lists every active adapter with a check mark. Only checked adapters are recorded. Virtual, VPN and tunnel adapters (Hyper-V, VMware, WireGuard and similar) start unchecked because their traffic is already counted on the physical adapter beneath them, so counting both would double the totals. If you use a PPPoE connection and see inflated numbers, uncheck either the PPPoE adapter or the physical adapter it runs over.

### Tools
- **Rates Recorder** — logs download/upload rates for one adapter (or all combined) to a text or CSV file at an interval you choose. Handy for spotting patterns or feeding a spreadsheet.
- **Traffic Stopwatch** — start it, do your transfer, stop it. You get total data moved plus max, min and average rate for that window. Good for quick speed tests.
- **Bandwidth Usage Notifications** — raise an alert when usage crosses a threshold, e.g. "more than 2 GB downloaded this month". Actions: sound, beep, email via your SMTP server, and/or run a program. See [Email alerts](#email-alerts).

### Reports
Tabs for **Summary**, **Daily**, **Weekly**, **Monthly** and **Yearly**.

- Columns: DL, UL, DL & UL, Time.
- Click a column header to sort; click again to reverse. The default is newest first.
- **Time** is cumulative time online for the period and can exceed 24 hours for a week, month or year (for example `2270:59:17`). For a single day it is the longest connected time among your adapters, so it never exceeds 24 hours.
- Filter by adapter and export to **CSV**.
- **Delete days...** removes the recorded usage for a date range (all adapters). Use it to remove days that were measured wrongly.
- **Clear all...** deletes everything for the chosen adapter (or all adapters).

### About
Shows the version, author and a link to the project page.

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

## Correcting history you imported or measured imperfectly

1. **Reports > Delete days...** and choose the days that look wrong.
2. **File > Import Legacy Data...**, choose your RAS Graph & Stats folder, tick **Replace days that already have data**, and import. Every day found in those files is wiped first and replaced by the imported numbers.

From version 1.0.2 onward, restarting the app or reconnecting an adapter no longer affects the day's totals.
