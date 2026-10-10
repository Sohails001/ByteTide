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

    // Tracks cumulative bytes-at-selection-start per adapter, so the status
    // bar's D/L and U/L totals reflect "since this adapter/filter was chosen"
    // rather than since-boot NIC lifetime counters.
    private readonly Dictionary<string, (long RxBaseline, long TxBaseline, long RxLast, long TxLast)> _totals = new();

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
        }

        var windowJson = _storage.GetSetting(WindowOptionsKey);
        if (windowJson is not null)
        {
            try { _windowOptions = JsonSerializer.Deserialize<WindowOptions>(windowJson) ?? new WindowOptions(); }
            catch { _windowOptions = new WindowOptions(); }
        }
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

    private void BuildMenu()
    {
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Import Legacy Data...", null, (_, _) => OpenImport());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => Application.Exit());

        _adaptersMenu.DropDownOpening += (_, _) => RebuildAdaptersMenu();
        RebuildAdaptersMenu();

        BuildViewMenu();
        BuildWindowMenu();

        var tools = new ToolStripMenuItem("Tools");
        tools.DropDownItems.Add("Rates Recorder...", null, (_, _) => OpenRatesRecorder());
        tools.DropDownItems.Add("Traffic Stopwatch...", null, (_, _) => OpenStopwatch());
        tools.DropDownItems.Add(new ToolStripSeparator());
        tools.DropDownItems.Add("Bandwidth Usage Notifications...", null, (_, _) => OpenNotifications());

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

    private void BuildViewMenu()
    {
        AddToggle(_viewMenu, "Title", () => _graphOptions.ShowTitle, v => _graphOptions.ShowTitle = v);
        AddToggle(_viewMenu, "IP Address", () => _graphOptions.ShowIpAddress, v => _graphOptions.ShowIpAddress = v);
        _viewMenu.DropDownItems.Add(new ToolStripSeparator());

        var areaItem = new ToolStripMenuItem("Graph (Area)") { CheckOnClick = false };
        var columnItem = new ToolStripMenuItem("Column Graph") { CheckOnClick = false };
        areaItem.Click += (_, _) => { _graphOptions.ColumnStyle = false; RefreshStyleChecks(areaItem, columnItem, false); SaveGraphOptions(); };
        columnItem.Click += (_, _) => { _graphOptions.ColumnStyle = true; RefreshStyleChecks(areaItem, columnItem, true); SaveGraphOptions(); };
        RefreshStyleChecks(areaItem, columnItem, _graphOptions.ColumnStyle);
        _viewMenu.DropDownItems.Add(areaItem);
        _viewMenu.DropDownItems.Add(columnItem);
        _viewMenu.DropDownItems.Add(new ToolStripSeparator());

        AddToggle(_viewMenu, "Scale", () => _graphOptions.ShowScale, v => _graphOptions.ShowScale = v);
        AddToggle(_viewMenu, "Scale Grid", () => _graphOptions.ShowScaleGrid, v => _graphOptions.ShowScaleGrid = v);
        AddToggle(_viewMenu, "Time", () => _graphOptions.ShowTime, v => _graphOptions.ShowTime = v);
        AddToggle(_viewMenu, "Time Grid", () => _graphOptions.ShowTimeGrid, v => _graphOptions.ShowTimeGrid = v);
        _viewMenu.DropDownItems.Add(new ToolStripSeparator());

        AddToggle(_viewMenu, "Average Download Line", () => _graphOptions.ShowAverageDownloadLine, v => _graphOptions.ShowAverageDownloadLine = v);
        AddToggle(_viewMenu, "Average Upload Line", () => _graphOptions.ShowAverageUploadLine, v => _graphOptions.ShowAverageUploadLine = v);
        _viewMenu.DropDownItems.Add(new ToolStripSeparator());

        AddToggle(_viewMenu, "Text", () => _graphOptions.ShowText, v => _graphOptions.ShowText = v);
        AddToggle(_viewMenu, "Max Rate", () => _graphOptions.ShowMaxRate, v => _graphOptions.ShowMaxRate = v);
        AddToggle(_viewMenu, "Average Speeds", () => _graphOptions.ShowAverageSpeeds, v => _graphOptions.ShowAverageSpeeds = v);
        _viewMenu.DropDownItems.Add(new ToolStripSeparator());

        AddToggle(_viewMenu, "Use kbps (instead of KB/s)", () => _graphOptions.UseKbps, v => _graphOptions.UseKbps = v);
    }

    private static void RefreshStyleChecks(ToolStripMenuItem area, ToolStripMenuItem column, bool columnStyle)
    {
        area.Checked = !columnStyle;
        column.Checked = columnStyle;
    }

    private void AddToggle(ToolStripMenuItem parent, string text, Func<bool> getter, Action<bool> setter)
    {
        var item = new ToolStripMenuItem(text) { CheckOnClick = true, Checked = getter() };
        item.Click += (_, _) =>
        {
            setter(item.Checked);
            SaveGraphOptions();
        };
        parent.DropDownItems.Add(item);
    }

    private void BuildWindowMenu()
    {
        var alwaysOnTop = new ToolStripMenuItem("Always on Top") { CheckOnClick = true, Checked = _windowOptions.AlwaysOnTop };
        alwaysOnTop.Click += (_, _) =>
        {
            _windowOptions.AlwaysOnTop = alwaysOnTop.Checked;
            TopMost = _windowOptions.AlwaysOnTop;
            SaveWindowOptions();
        };

        var semiTransparent = new ToolStripMenuItem("Semi-transparent") { CheckOnClick = true, Checked = _windowOptions.SemiTransparent };
        semiTransparent.Click += (_, _) =>
        {
            _windowOptions.SemiTransparent = semiTransparent.Checked;
            Opacity = _windowOptions.SemiTransparent ? 0.85 : 1.0;
            SaveWindowOptions();
            ApplyClickThrough(); // alpha for click-through should track current opacity
        };

        var clickThrough = new ToolStripMenuItem("Click-through") { CheckOnClick = true, Checked = _windowOptions.ClickThrough };
        clickThrough.Click += (_, _) =>
        {
            _windowOptions.ClickThrough = clickThrough.Checked;
            SaveWindowOptions();
            ApplyClickThrough();
        };

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

        _windowMenu.DropDownItems.Add(alwaysOnTop);
        _windowMenu.DropDownItems.Add(semiTransparent);
        _windowMenu.DropDownItems.Add(clickThrough);
        _windowMenu.DropDownItems.Add(new ToolStripSeparator());
        _windowMenu.DropDownItems.Add(resetPosition);
    }

    private void ApplyClickThrough()
    {
        if (!IsHandleCreated) return;
        byte alpha = (byte)(Opacity * 255);
        NativeMethods.SetClickThrough(Handle, _windowOptions.ClickThrough, alpha);
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            this,
            "ByteTide\n\n" +
            "Created by Sohail Sher Ali.\n\n" +
            "An open-source network traffic monitor, built as a modern replacement\n" +
            "for classic Windows bandwidth monitors, with support for importing\n" +
            "legacy RAS Graph & Stats history.",
            "About ByteTide",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    /// <summary>
    /// Rebuilds the Adapters menu from the current live adapter list every
    /// time it's opened, so adapters that appear/disappear (USB NICs, VPNs
    /// connecting/disconnecting) are reflected without restarting the app.
    /// </summary>
    private void RebuildAdaptersMenu()
    {
        _adaptersMenu.DropDownItems.Clear();

        var allItem = new ToolStripMenuItem("All Connections") { Checked = _selectedAdapterId is null };
        allItem.Click += (_, _) => SelectAdapter(null);
        _adaptersMenu.DropDownItems.Add(allItem);
        _adaptersMenu.DropDownItems.Add(new ToolStripSeparator());

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var id = nic.Id;
            var item = new ToolStripMenuItem(nic.Description) { Checked = _selectedAdapterId == id };
            item.Click += (_, _) => SelectAdapter(id);
            _adaptersMenu.DropDownItems.Add(item);
        }
    }

    private void SelectAdapter(string? adapterId)
    {
        _selectedAdapterId = adapterId;
        _totals.Clear();
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
        _status.BackColor = Color.FromArgb(20, 20, 20);
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

        foreach (var s in included)
        {
            if (!_totals.TryGetValue(s.AdapterId, out var t))
            {
                t = (s.BytesReceived, s.BytesSent, s.BytesReceived, s.BytesSent);
            }
            else
            {
                t = (t.RxBaseline, t.TxBaseline, s.BytesReceived, s.BytesSent);
            }
            _totals[s.AdapterId] = t;
        }

        long sessionRx = _totals.Values.Sum(t => Math.Max(0, t.RxLast - t.RxBaseline));
        long sessionTx = _totals.Values.Sum(t => Math.Max(0, t.TxLast - t.TxBaseline));

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

    private void OpenImport()
    {
        using var dlg = new ImportForm(_storage);
        dlg.ShowDialog(this);
    }

    private void OpenReports()
    {
        using var dlg = new ReportsForm(_storage);
        dlg.ShowDialog(this);
    }

    private void OpenRatesRecorder()
    {
        using var dlg = new RatesRecorderForm(_sampler);
        dlg.ShowDialog(this);
    }

    private void OpenStopwatch()
    {
        using var dlg = new StopwatchForm(_sampler);
        dlg.ShowDialog(this);
    }

    private void OpenNotifications()
    {
        using var dlg = new NotificationsForm(_storage);
        dlg.ShowDialog(this);
    }
}
