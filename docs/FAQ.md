# FAQ & Troubleshooting

**Does ByteTide capture packets or need a driver?**
No. It reads the interface byte counters Windows already maintains.

**Does it show per-application traffic?**
No. It tracks per-adapter totals. Per-process accounting needs packet capture or ETW and isn't a goal right now.

**Does it send my data anywhere?**
No. Everything stays in a local SQLite file. The only outbound traffic is email alerts you configure.

**Why is memory use around 20 MB?**
It's a managed .NET app (GC + JIT). That's normal for WinForms.

**My totals look too low or too high compared with another monitor.**
Make sure you are on version 1.0.2 or later. Earlier builds replaced the day's total on every restart and lost bytes when an adapter reconnected. Then check **Adapters > Count in All Connections**: if two adapters carry the same traffic (for example a PPPoE connection over Ethernet, or a VPN), count only one of them. Small differences of a percent or two between programs are normal because each reads the network counters at slightly different moments.

**The window is dark and I want it light (or the other way round).**
Open **Window → Appearance** (also in the tray icon's right-click menu) and choose **Match Windows**, **Dark** or **Light**.

**The link speed in the status bar shows a strange number such as 100000 Mbps.**
That comes from a virtual adapter. Open **Adapters → Count in All Connections**, find the adapter with that speed, and uncheck it.

**A day shows more than 24 hours online.**
That came from adding up every adapter's time. Version 1.0.2 shows the longest connected time among adapters for each day instead.

**The window disappeared / is off-screen.**
Tray icon → Open, then **Window → Reset Window Position**.

**I can't click the window anymore.**
Click-through is on. Double-click the ByteTide tray icon, or right-click it → **Window → Click-through**. From version 1.0.1 ByteTide always starts with click-through off. On version 1.0.0 you can also click the ByteTide taskbar button, press **Alt**, use the arrow keys to reach **Window → Click-through**, and press **Enter**, or exit from the tray menu and relaunch.

**The tray icon text is too small to read.**
Windows tray icons are only about 16 pixels wide. Turn on **Tray Icon → Floating mini meter** for large readable speeds, and hover over the tray icon for a tooltip.

**Where do I find the database?**
`%AppData%\ByteTide\data.db` (GUI) or `%ProgramData%\ByteTide\data.db` (service).

**I used an earlier "Bandwidth Monitor" build.**
On first launch ByteTide moves `%AppData%\BandwidthMonitor` to `%AppData%\ByteTide` automatically. The service uses a new default location; point `DataDbPath` at your old file to keep using it.

**Windows SmartScreen warns about the exe.**
The release isn't code-signed, so Windows may show an "unrecognised app" prompt. You can build from source to verify it yourself.

**Exit crashed with "connection is not open".**
Fixed in internal v3.4; make sure you're on the latest release.
