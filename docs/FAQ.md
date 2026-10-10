# FAQ & Troubleshooting

**Does ByteTide capture packets or need a driver?**
No. It reads the interface byte counters Windows already maintains.

**Does it show per-application traffic?**
No. It tracks per-adapter totals. Per-process accounting needs packet capture or ETW and isn't a goal right now.

**Does it send my data anywhere?**
No. Everything stays in a local SQLite file. The only outbound traffic is email alerts you configure.

**Why is memory use around 20 MB?**
It's a managed .NET app (GC + JIT). That's normal for WinForms.

**My totals look too low after a restart.**
Known limitation: the day's counter baseline resets when the app or service restarts mid-day. See the README.

**The window disappeared / is off-screen.**
Tray icon → Open, then **Window → Reset Window Position**.

**I can't click the window anymore.**
Click-through is on. Turn it off from the **Window** menu or by right-clicking the tray icon.

**Where do I find the database?**
`%AppData%\ByteTide\data.db` (GUI) or `%ProgramData%\ByteTide\data.db` (service).

**I used an earlier "Bandwidth Monitor" build.**
On first launch ByteTide moves `%AppData%\BandwidthMonitor` to `%AppData%\ByteTide` automatically. The service uses a new default location; point `DataDbPath` at your old file to keep using it.

**Windows SmartScreen warns about the exe.**
The release isn't code-signed, so Windows may show an "unrecognised app" prompt. You can build from source to verify it yourself.

**Exit crashed with "connection is not open".**
Fixed in internal v3.4; make sure you're on the latest release.
