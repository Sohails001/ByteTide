using System.Net.NetworkInformation;
using System.Text.Json;
using ByteTide.Core;

namespace ByteTide.App;

public sealed class MainForm : Form
{
    private readonly GraphControl _graph = new();
    private readonly MenuStrip _menu = new() { Dock = DockStyle.Top };
    private readonly StatusStrip _status = new() { Dock = DockStyle.Bottom };

    private readonly ToolStripStatusLabel _lblLinkSpeed = new("-- Mbps");
    private readonly ToolStripStatusLabel _lblUptime = new("00:00:00");
    private readonly ToolStripStatusLabel _lblTime = new("--:--:--");
    private readonly ToolStripStatusLabel _lblDown = new("D/L: -- @ --");
    private readonly ToolStripStatusLabel _lblUp = new("U/L: -- @ --");

    private readonly ToolStripMenuItem _adaptersMenu = new("Adapters");
    private readonly ToolStripMenuItem _viewMenu = new("View");
    private readonly ToolStripMenuItem _windowMenu = new("Window");

    private readonly NetworkSampler _sampler;
    private readonly Storage _storage;
    private readonly DateTime _appStarted = DateTime.Now;

    private GraphOptions _graphOptions = new();
    private WindowOptions _windowOptions = new();

    /// <summary>Null means "All Connections" (combine every adapter).</summary>
    private string? _selectedAdapterId;

    // Running totals for the status bar, built by adding each sample's
    // reset-safe byte deltas ("since this adapter/filter was chosen").
    private long _sessionRx;
    private long _sessionTx;
    private readonly object _sessionLock = new();

    private const string GraphOptionsKey = "graphOptions";
    private const string WindowOptionsKey = "windowOptions";

    public MainForm(NetworkSampler sampler, Storage storage)
    {
        _sampler = sampler;
        _storage = storage;

        LoadOptions();

        Text = "ByteTide";
        BackColor = Color.Black;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(_windowOptions.X, _windowOptions.Y);
        Size = new Size(_windowOptions.Width, _windowOptions.Height);
        TopMost = _windowOptions.AlwaysOnTop;
        Opacity = _windowOptions.SemiTransparent ? 0.85 : 1.0;

        BuildMenu();
        BuildStatusBar();

        _graph.Dock = DockStyle.Fill;
        _graph.Options = _graphOptions;

        // Fill control added first, edge-docked controls after -- WinForms
        // allocates docked space in reverse insertion order, so this gives
        // the menu/status bars their strip and leaves the remainder to the graph.
        Controls.Add(_graph);
        Controls.Add(_status);
        Controls.Add(_menu);
        MainMenuStrip = _menu;

        // Defensive: guarantees the menu/status bar are always painted on top
        // of the graph, even if anything about the dock layout order above
        // doesn't behave the way it's expected to on a given machine/theme.
        _status.BringToFront();
        _menu.BringToFront();

        UpdateGraphAdapterLabel();

        Shown += (_, _) => ApplyClickThrough();
        FormClosing += (_, _) => SaveWindowOptions();

        _sampler.SamplesReady += OnSamples;
    }

    // ----- Options persistence -----------------------------------------

    private void LoadOptions()
    {
        var graphJson = _storage.GetSetting(GraphOptionsKey);
        if (graphJson is not null)
        {
            try { _graphOptions = JsonSerializer.Deserialize<GraphOptions>(graphJson) ?? new GraphOptions(); }
            catch { _graphOptions = new GraphOptions(); }

            // Older builds stored the graph style as a single true/false flag.
            if (_graphOptions.ColumnStyle)
            {
                _graphOptions.Style = GraphStyle.Column;
                _graphOptions.ColumnStyle = false;
            }
        }

        var windowJson = _storage.GetSetting(WindowOptionsKey);
        if (windowJson is not null)
        {
            try { _windowOptions = JsonSerializer.Deserialize<WindowOptions>(windowJson) ?? new WindowOptions(); }
            catch { _windowOptions = new WindowOptions(); }
        }

        // Safety: click-through is deliberately NOT remembered between runs.
        // If it were, enabling it and then closing the app would leave the
        // window permanently unclickable on the next launch.
        _windowOptions.ClickThrough = false;
    }

    private void SaveGraphOptions()
    {
        _storage.SetSetting(GraphOptionsKey, JsonSerializer.Serialize(_graphOptions));
        _graph.RefreshOptions();
    }

    private void SaveWindowOptions()
    {
        if (WindowState == FormWindowState.Normal)
        {
            _windowOptions.X = Location.X;
            _windowOptions.Y = Location.Y;
            _windowOptions.Width = Size.Width;
            _windowOptions.Height = Size.Height;
        }
        _storage.SetSetting(WindowOptionsKey, JsonSerializer.Serialize(_windowOptions));
    }

    // ----- Menu construction ---------------------------------------------
    //
    // Every dropdown is (re)built from scratch each time it opens, by a
    // Populate*Menu method that fills ANY item collection. That lets the exact
    // same options appear both in this window's menu bar and in the system
    // tray icon's right-click menu -- which matters because with Click-through
    // on, this window's menu bar can't be clicked at all.

    /// <summary>Raised when click-through is switched on or off.</summary>
    public event Action<bool>? ClickThroughChanged;

    /// <summary>Set by the tray host so File &gt; Exit shuts everything down cleanly.</summary>
    public Action? ExitRequested { get; set; }

    /// <summary>Whether the window ignores mouse clicks (HUD mode). Never persisted across runs.</summary>
    public bool ClickThrough
    {
        get => _windowOptions.ClickThrough;
        set
        {
            if (_windowOptions.ClickThrough == value) return;
            _windowOptions.ClickThrough = value;
            SaveWindowOptions();
            ApplyClickThrough();
            ClickThroughChanged?.Invoke(value);
        }
    }

    private void RequestExit()
    {
        if (ExitRequested is not null) ExitRequested();
        else Application.Exit();
    }

    private void BuildMenu()
    {
        _menu.Renderer = new DarkMenuRenderer();
        _menu.BackColor = Theme.DarkSurface;
        _menu.ForeColor = Theme.DarkText;
        _menu.Font = Theme.Ui(9f);
        _menu.Padding = new Padding(6, 3, 0, 3);

        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Import Legacy Data...", null, (_, _) => OpenImport());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => RequestExit());

        HookDynamicMenu(_adaptersMenu, PopulateAdaptersMenu);
        HookDynamicMenu(_viewMenu, PopulateViewMenu);
        HookDynamicMenu(_windowMenu, PopulateWindowMenu);

        var tools = new ToolStripMenuItem("Tools");
        HookDynamicMenu(tools, items => PopulateToolsMenu(items, null));

        var reports = new ToolStripMenuItem("Reports");
        reports.DropDownItems.Add("Daily / Weekly / Monthly / Yearly...", null, (_, _) => OpenReports());

        var about = new ToolStripMenuItem("About");
        about.Click += (_, _) => ShowAbout();

        _menu.Items.Add(file);
        _menu.Items.Add(_adaptersMenu);
        _menu.Items.Add(_viewMenu);
        _menu.Items.Add(_windowMenu);
        _menu.Items.Add(tools);
        _menu.Items.Add(reports);
        _menu.Items.Add(about);
    }

    private static void HookDynamicMenu(ToolStripMenuItem menu, Action<ToolStripItemCollection> populate)
    {
        populate(menu.DropDownItems);
        menu.DropDownOpening += (_, _) =>
        {
            MenuHelper.Clear(menu.DropDownItems);
            populate(menu.DropDownItems);
        };
    }

    /// <summary>
    /// Rebuilt every time it's opened, so adapters that appear/disappear
    /// (USB NICs, VPNs connecting/disconnecting) are reflected without restarting.
    /// </summary>
    public void PopulateAdaptersMenu(ToolStripItemCollection items)
    {
        var allItem = new ToolStripMenuItem("All Connections") { Checked = _selectedAdapterId is null };
        allItem.Click += (_, _) => SelectAdapter(null);
        items.Add(allItem);
        items.Add(new ToolStripSeparator());

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var id = nic.Id;
            var item = new ToolStripMenuItem(nic.Description) { Checked = _selectedAdapterId == id };
            item.Click += (_, _) => SelectAdapter(id);
            items.Add(item);
        }

        // Which adapters add up to "All Connections". Virtual/VPN adapters are
        // off by default because their traffic is already counted on the
        // physical adapter underneath them.
        var counted = new ToolStripMenuItem("Count in All Connections");
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var id = nic.Id;
            bool virt = AdapterFilter.LooksVirtual(nic);
            var item = new ToolStripMenuItem(nic.Description + (virt ? "  (virtual / VPN)" : ""))
            {
                Checked = _sampler.IsTracked(nic)
            };
            item.Click += (_, _) => ToggleCounted(id, virt);
            counted.DropDownItems.Add(item);
        }

        if (counted.DropDownItems.Count > 0)
        {
            items.Add(new ToolStripSeparator());
            items.Add(counted);
        }
    }

    private void ToggleCounted(string adapterId, bool looksVirtual)
    {
        var excluded = new HashSet<string>(_sampler.ExcludedAdapterIds);
        var forced = new HashSet<string>(_sampler.ForcedIncludedAdapterIds);

        bool trackedNow = excluded.Contains(adapterId) ? false : forced.Contains(adapterId) ? true : !looksVirtual;

        if (trackedNow)
        {
            excluded.Add(adapterId);
            forced.Remove(adapterId);
        }
        else
        {
            excluded.Remove(adapterId);
            if (looksVirtual) forced.Add(adapterId);
        }

        _sampler.SetExcludedAdapterIds(excluded);
        _sampler.SetForcedIncludedAdapterIds(forced);
        AdapterSettings.Save(_storage, _sampler);
        ResetSessionTotals();
    }

    public void PopulateViewMenu(ToolStripItemCollection items)
    {
        AddToggle(items, "Title", () => _graphOptions.ShowTitle, v => _graphOptions.ShowTitle = v);
        AddToggle(items, "IP Address", () => _graphOptions.ShowIpAddress, v => _graphOptions.ShowIpAddress = v);
        items.Add(new ToolStripSeparator());

        AddStyleItem(items, "Graph (Area)", GraphStyle.Area);
        AddStyleItem(items, "Column Graph", GraphStyle.Column);
        AddStyleItem(items, "Line Graph", GraphStyle.Line);

        var widthMenu = new ToolStripMenuItem("Graph width (pixels per second)");
        foreach (var px in new[] { 1, 2, 3, 4, 6 })
        {
            int value = px;
            var item = new ToolStripMenuItem(px == 1 ? "1 pixel" : px + " pixels") { Checked = _graphOptions.PixelsPerSecond == value };
            item.Click += (_, _) =>
            {
                _graphOptions.PixelsPerSecond = value;
                SaveGraphOptions();
            };
            widthMenu.DropDownItems.Add(item);
        }
        items.Add(widthMenu);
        items.Add(new ToolStripSeparator());

        AddToggle(items, "Scale", () => _graphOptions.ShowScale, v => _graphOptions.ShowScale = v);
        AddToggle(items, "Scale Grid", () => _graphOptions.ShowScaleGrid, v => _graphOptions.ShowScaleGrid = v);
        AddToggle(items, "Time", () => _graphOptions.ShowTime, v => _graphOptions.ShowTime = v);
        AddToggle(items, "Time Grid", () => _graphOptions.ShowTimeGrid, v => _graphOptions.ShowTimeGrid = v);
        items.Add(new ToolStripSeparator());

        AddToggle(items, "Average Download Line", () => _graphOptions.ShowAverageDownloadLine, v => _graphOptions.ShowAverageDownloadLine = v);
        AddToggle(items, "Average Upload Line", () => _graphOptions.ShowAverageUploadLine, v => _graphOptions.ShowAverageUploadLine = v);
        items.Add(new ToolStripSeparator());

        AddToggle(items, "Text", () => _graphOptions.ShowText, v => _graphOptions.ShowText = v);
        AddToggle(items, "Max Rate", () => _graphOptions.ShowMaxRate, v => _graphOptions.ShowMaxRate = v);
        AddToggle(items, "Average Speeds", () => _graphOptions.ShowAverageSpeeds, v => _graphOptions.ShowAverageSpeeds = v);
        items.Add(new ToolStripSeparator());

        AddToggle(items, "Use kbps (instead of KB/s)", () => _graphOptions.UseKbps, v => _graphOptions.UseKbps = v);
    }

    private void AddStyleItem(ToolStripItemCollection items, string text, GraphStyle style)
    {
        var item = new ToolStripMenuItem(text) { Checked = _graphOptions.Style == style };
        item.Click += (_, _) =>
        {
            _graphOptions.Style = style;
            SaveGraphOptions();
        };
        items.Add(item);
    }

    private void AddToggle(ToolStripItemCollection parent, string text, Func<bool> getter, Action<bool> setter)
    {
        var item = new ToolStripMenuItem(text) { Checked = getter() };
        item.Click += (_, _) =>
        {
            setter(!getter());
            SaveGraphOptions();
        };
        parent.Add(item);
    }

    public void PopulateWindowMenu(ToolStripItemCollection items)
    {
        var alwaysOnTop = new ToolStripMenuItem("Always on Top") { Checked = _windowOptions.AlwaysOnTop };
        alwaysOnTop.Click += (_, _) =>
        {
            _windowOptions.AlwaysOnTop = !_windowOptions.AlwaysOnTop;
            TopMost = _windowOptions.AlwaysOnTop;
            SaveWindowOptions();
        };

        var semiTransparent = new ToolStripMenuItem("Semi-transparent") { Checked = _windowOptions.SemiTransparent };
        semiTransparent.Click += (_, _) =>
        {
            _windowOptions.SemiTransparent = !_windowOptions.SemiTransparent;
            Opacity = _windowOptions.SemiTransparent ? 0.85 : 1.0;
            SaveWindowOptions();
            ApplyClickThrough(); // alpha for click-through should track current opacity
        };

        var clickThrough = new ToolStripMenuItem("Click-through (undo from the tray icon)") { Checked = _windowOptions.ClickThrough };
        clickThrough.Click += (_, _) => ClickThrough = !ClickThrough;

        var resetPosition = new ToolStripMenuItem("Reset Window Position");
        resetPosition.Click += (_, _) =>
        {
            _windowOptions.X = 100;
            _windowOptions.Y = 100;
            _windowOptions.Width = 640;
            _windowOptions.Height = 400;
            Location = new Point(_windowOptions.X, _windowOptions.Y);
            Size = new Size(_windowOptions.Width, _windowOptions.Height);
            SaveWindowOptions();
        };

        items.Add(alwaysOnTop);
        items.Add(semiTransparent);
        items.Add(clickThrough);
        items.Add(new ToolStripSeparator());
        items.Add(resetPosition);
    }

    /// <param name="beforeOpen">Optional action run before a dialog opens (the tray uses it to show this window first).</param>
    public void PopulateToolsMenu(ToolStripItemCollection items, Action? beforeOpen)
    {
        items.Add("Rates Recorder...", null, (_, _) => { beforeOpen?.Invoke(); OpenRatesRecorder(); });
        items.Add("Traffic Stopwatch...", null, (_, _) => { beforeOpen?.Invoke(); OpenStopwatch(); });
        items.Add(new ToolStripSeparator());
        items.Add("Bandwidth Usage Notifications...", null, (_, _) => { beforeOpen?.Invoke(); OpenNotifications(); });
    }

    private void ResetSessionTotals()
    {
        lock (_sessionLock)
        {
            _sessionRx = 0;
            _sessionTx = 0;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.UseDarkTitleBar(Handle, true);
    }

    private void ApplyClickThrough()
    {
        if (!IsHandleCreated) return;
        byte alpha = (byte)(Opacity * 255);
        NativeMethods.SetClickThrough(Handle, _windowOptions.ClickThrough, alpha);
    }

    public void ShowAbout()
    {
        using var dlg = new AboutForm();
        dlg.ShowDialog(this);
    }

    private void SelectAdapter(string? adapterId)
    {
        _selectedAdapterId = adapterId;
        ResetSessionTotals();
        _graph.Clear();
        UpdateGraphAdapterLabel();
    }

    private void UpdateGraphAdapterLabel()
    {
        if (_selectedAdapterId is null)
        {
            _graph.CurrentAdapterLabel = "All Connections";
            _graph.CurrentIpAddress = "";
            return;
        }

        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == _selectedAdapterId);
            if (nic is null) return;

            _graph.CurrentAdapterLabel = nic.Description;
            var ipv4 = nic.GetIPProperties().UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            _graph.CurrentIpAddress = ipv4?.Address.ToString() ?? "";
        }
        catch
        {
            // Adapter may have vanished between selection and lookup; leave label as-is.
        }
    }

    private void BuildStatusBar()
    {
        // Explicit colors instead of leaving StatusStrip on its default light-gray
        // theme -- keeps it visually consistent with the black graph background
        // and, more importantly, guarantees the text renders in a color that's
        // never accidentally invisible against it.
        _status.Renderer = new DarkMenuRenderer();
        _status.Font = Theme.Ui(9f);
        _status.BackColor = Color.FromArgb(20, 20, 24);
        _status.SizingGrip = false;

        _lblLinkSpeed.ForeColor = StatusTextColor;
        _lblUptime.ForeColor = StatusTextColor;
        _lblTime.ForeColor = StatusTextColor;
        _lblDown.ForeColor = StatusTextColor;
        _lblUp.ForeColor = StatusTextColor;

        _status.Items.Add(_lblLinkSpeed);
        _status.Items.Add(Spacer());
        _status.Items.Add(_lblUptime);
        _status.Items.Add(Spacer());
        _status.Items.Add(_lblTime);
        _status.Items.Add(Spacer());
        _status.Items.Add(_lblDown);
        _status.Items.Add(Spacer());
        _status.Items.Add(_lblUp);
    }

    private static readonly Color StatusTextColor = Color.FromArgb(140, 230, 140);

    private static ToolStripStatusLabel Spacer() => new() { Spring = false, Text = "   " };

    private void OnSamples(IReadOnlyList<LiveSample> samples)
    {
        if (samples.Count == 0) return;

        var included = _selectedAdapterId is null
            ? samples
            : samples.Where(s => s.AdapterId == _selectedAdapterId).ToList();

        double totalDown = included.Sum(s => s.DownloadBps);
        double totalUp = included.Sum(s => s.UploadBps);

        long deltaRx = included.Sum(s => s.DeltaBytesReceived);
        long deltaTx = included.Sum(s => s.DeltaBytesSent);

        long sessionRx, sessionTx;
        lock (_sessionLock)
        {
            _sessionRx += deltaRx;
            _sessionTx += deltaTx;
            sessionRx = _sessionRx;
            sessionTx = _sessionTx;
        }

        double? linkSpeedMbps = null;
        string? adapterForSpeed = _selectedAdapterId;

        // In "All Connections" mode there's no single adapter to report a
        // link speed for -- fall back to whichever adapter is carrying the
        // most traffic this tick, rather than leaving the field blank.
        if (adapterForSpeed is null && included.Count > 0)
        {
            adapterForSpeed = included
                .OrderByDescending(s => s.DownloadBps + s.UploadBps)
                .First().AdapterId;
        }

        if (adapterForSpeed is not null)
        {
            // NetworkSampler already enumerated every adapter this tick and
            // captured each one's Speed alongside its byte counters -- reuse
            // that instead of calling the relatively expensive
            // GetAllNetworkInterfaces() a second time per second just for this.
            var match = included.FirstOrDefault(s => s.AdapterId == adapterForSpeed);
            if (match is not null && match.SpeedBps > 0)
                linkSpeedMbps = match.SpeedBps / 1_000_000.0;
        }

        if (IsDisposed) return;

        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => UpdateUi(totalDown, totalUp, sessionRx, sessionTx, linkSpeedMbps))); }
            catch (ObjectDisposedException) { /* window closing race, ignore */ }
        }
        else
        {
            UpdateUi(totalDown, totalUp, sessionRx, sessionTx, linkSpeedMbps);
        }
    }

    private void UpdateUi(double down, double up, long sessionRx, long sessionTx, double? linkSpeedMbps)
    {
        _graph.AddSample(down, up);

        var uptime = DateTime.Now - _appStarted;
        _lblLinkSpeed.Text = linkSpeedMbps is { } mbps ? $"{mbps:0.#} Mbps" : "-- Mbps";
        // Built from TotalHours rather than the "hh" custom format, which wraps
        // at 24h -- this app runs continuously from the tray, so uptime needs
        // to keep counting past a day rather than resetting to 00.
        _lblUptime.Text = $"{(int)uptime.TotalHours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
        _lblTime.Text = DateTime.Now.ToString("HH:mm:ss");
        _lblDown.Text = $"D/L: {FormatBytes(sessionRx)} @ {FormatRate(down)}";
        _lblUp.Text = $"U/L: {FormatBytes(sessionTx)} @ {FormatRate(up)}";
    }

    private static string FormatRate(double bytesPerSec)
    {
        if (bytesPerSec >= 1024 * 1024) return $"{bytesPerSec / (1024 * 1024):0.0} MB/s";
        if (bytesPerSec >= 1024) return $"{bytesPerSec / 1024:0.0} KB/s";
        return $"{bytesPerSec:0} B/s";
    }

    private static string FormatBytes(long bytes)
    {
        double b = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (b >= 1024 && i < units.Length - 1)
        {
            b /= 1024;
            i++;
        }
        return $"{b:0.##} {units[i]}";
    }

    public void OpenImport()
    {
        using var dlg = new ImportForm(_storage);
        dlg.ShowDialog(this);
    }

    public void OpenReports()
    {
        using var dlg = new ReportsForm(_storage);
        dlg.ShowDialog(this);
    }

    public void OpenRatesRecorder()
    {
        using var dlg = new RatesRecorderForm(_sampler);
        Theme.ApplyDialog(dlg);
        dlg.ShowDialog(this);
    }

    public void OpenStopwatch()
    {
        using var dlg = new StopwatchForm(_sampler);
        Theme.ApplyDialog(dlg);
        dlg.ShowDialog(this);
    }

    public void OpenNotifications()
    {
        using var dlg = new NotificationsForm(_storage);
        Theme.ApplyDialog(dlg);
        dlg.ShowDialog(this);
    }
}
