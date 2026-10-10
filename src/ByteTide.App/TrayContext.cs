using System.Diagnostics;
using System.Media;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Text.Json;
using ByteTide.Core;

namespace ByteTide.App;

/// <summary>
/// Owns the app's lifetime: runs from the system tray, keeps sampling and
/// rolling up daily stats even while the main window is hidden, and closing
/// the window just hides it (matches the original app's "runs in tray"
/// behavior). Also renders a small live traffic graph directly into the
/// tray icon, and checks bandwidth usage notification thresholds.
/// </summary>
public sealed class TrayContext : ApplicationContext
{
    private const string NotificationSettingsKey = "notificationSettings";
    private const string EmailSettingsKey = "emailSettings";

    private readonly NotifyIcon _trayIcon;
    private readonly NetworkSampler _sampler;
    private readonly Storage _storage;
    private readonly System.Windows.Forms.Timer _rollupTimer;
    private readonly Dictionary<string, DailyAccumulator> _today = new();
    private MainForm? _mainForm;

    private readonly List<(double Down, double Up)> _trayHistory = new();
    private Icon? _currentTrayIcon;
    private DateTime _lastTrayIconUpdate = DateTime.MinValue;

    private const string TrayOptionsKey = "trayOptions";

    // Used to hop from the sampler's background timer thread onto the UI
    // thread, since the tray icon and floating meter are UI objects.
    private readonly Control _marshal = new();

    private TrayOptions _trayOptions = new();
    private FloatingMeterForm? _meter;
    private double _lastDown;
    private double _lastUp;
    private string? _lastIconKey;
    private string? _lastTooltip;

    // Below this rate (bytes/sec) the dial-up monitors count as "idle", so
    // background chatter doesn't make them flicker constantly.
    private const double ActivityThresholdBps = 512;

    /// <summary>
    /// Pre-release builds stored data under "BandwidthMonitor". If that folder
    /// exists and the new "ByteTide" one doesn't, move it over so existing
    /// history carries across the rename (falls back to the old folder if the
    /// move fails, e.g. a file is locked).
    /// </summary>
    private static void MigrateLegacyDataDir(ref string dataDir)
    {
        var legacyDir = Path.Combine(Path.GetDirectoryName(dataDir)!, "BandwidthMonitor");
        if (Directory.Exists(dataDir) || !Directory.Exists(legacyDir)) return;
        try { Directory.Move(legacyDir, dataDir); }
        catch { dataDir = legacyDir; }
    }

    public TrayContext()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ByteTide");
        MigrateLegacyDataDir(ref dataDir);
        Directory.CreateDirectory(dataDir);

        _storage = new Storage(Path.Combine(dataDir, "data.db"));
        _ = _marshal.Handle; // force handle creation here, on the UI thread
        LoadTrayOptions();

        _sampler = new NetworkSampler(TimeSpan.FromSeconds(1));
        _sampler.SamplesReady += OnSamples;

        // The tray menu mirrors the main window's menu bar (see BuildTrayMenu)
        // and is rebuilt each time it opens so check marks are always current.
        var menu = new ContextMenuStrip();
        BuildTrayMenu(menu);
        menu.Opening += (_, _) => BuildTrayMenu(menu);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "ByteTide",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowMain();

        _rollupTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _rollupTimer.Tick += (_, _) =>
        {
            FlushDailyStats();
            CheckNotifications();
        };
        _rollupTimer.Start();

        ShowMain();
        ApplyMeterVisibility();
        RenderTrayIcon(force: true);
    }

    private void OnSamples(IReadOnlyList<LiveSample> samples)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var sample in samples)
        {
            _storage.InsertLiveSample(sample);

            if (!_today.TryGetValue(sample.AdapterId, out var acc) || acc.Date != today)
            {
                acc = new DailyAccumulator
                {
                    Date = today,
                    StartBytesReceived = sample.BytesReceived,
                    StartBytesSent = sample.BytesSent
                };
                _today[sample.AdapterId] = acc;
            }

            acc.SecondsOnline += 1;
            acc.LastBytesReceived = sample.BytesReceived;
            acc.LastBytesSent = sample.BytesSent;
        }

        double down = samples.Sum(s => s.DownloadBps);
        double up = samples.Sum(s => s.UploadBps);

        // This runs on the sampler's background timer thread; the tray icon
        // and meter window must be updated on the UI thread.
        try
        {
            if (!_marshal.IsDisposed)
                _marshal.BeginInvoke(new Action(() => UpdateTray(down, up)));
        }
        catch (InvalidOperationException) { /* shutting down (also covers ObjectDisposedException) */ }
    }

    // ----- Tray icon, tooltip and floating meter ----------------------------

    private void UpdateTray(double down, double up)
    {
        _lastDown = down;
        _lastUp = up;

        _trayHistory.Add((down, up));
        if (_trayHistory.Count > 16) _trayHistory.RemoveAt(0);

        _meter?.Push(down, up);
        UpdateTooltip(down, up);
        RenderTrayIcon(force: false);
    }

    private void UpdateTooltip(double down, double up)
    {
        bool bits = _trayOptions.ShowSpeedsInBits;
        var text = "ByteTide - current transfer rate\n" +
                   "\u25BC " + SpeedFormat.Full(down, bits) + "    \u25B2 " + SpeedFormat.Full(up, bits);

        if (text == _lastTooltip) return;
        _lastTooltip = text;

        try { _trayIcon.Text = text.Length > 127 ? text.Substring(0, 127) : text; }
        catch { /* tooltip is cosmetic */ }
    }

    /// <summary>
    /// Redraws the tray icon for the current style. Regenerating an icon costs
    /// a Bitmap, a GDI+ draw pass and an unmanaged handle round trip, so each
    /// style skips redraws when nothing visible has changed.
    /// </summary>
    private void RenderTrayIcon(bool force)
    {
        try
        {
            bool bits = _trayOptions.ShowSpeedsInBits;
            Icon icon;

            switch (_trayOptions.Style)
            {
                case TrayIconStyle.SpeedNumbers:
                {
                    var key = "N|" + bits + "|" + SpeedFormat.Compact(_lastDown, bits) + "|" + SpeedFormat.Compact(_lastUp, bits);
                    if (!force && key == _lastIconKey) return;
                    _lastIconKey = key;
                    icon = TrayIconRenderer.RenderSpeedNumbers(_lastDown, _lastUp, bits);
                    break;
                }

                case TrayIconStyle.DialupMonitors:
                {
                    bool receiving = _lastDown > ActivityThresholdBps;
                    bool sending = _lastUp > ActivityThresholdBps;
                    var (downColor, upColor) = TrayIconRenderer.PaletteColors(_trayOptions);
                    var key = "D|" + receiving + "|" + sending + "|" + downColor.ToArgb() + "|" + upColor.ToArgb();
                    if (!force && key == _lastIconKey) return;
                    _lastIconKey = key;
                    icon = TrayIconRenderer.RenderDialup(receiving, sending, downColor, upColor);
                    break;
                }

                default:
                    if (!force && (DateTime.Now - _lastTrayIconUpdate).TotalSeconds < 2) return;
                    _lastIconKey = null;
                    icon = TrayIconRenderer.RenderGraph(_trayHistory);
                    break;
            }

            _lastTrayIconUpdate = DateTime.Now;

            var old = _currentTrayIcon;
            _currentTrayIcon = icon;
            _trayIcon.Icon = icon;
            old?.Dispose();
        }
        catch
        {
            // Icon rendering is cosmetic; never let a drawing failure take down monitoring.
        }
    }

    private void LoadTrayOptions()
    {
        try
        {
            var json = _storage.GetSetting(TrayOptionsKey);
            if (json is not null)
                _trayOptions = JsonSerializer.Deserialize<TrayOptions>(json) ?? new TrayOptions();
        }
        catch
        {
            _trayOptions = new TrayOptions();
        }
    }

    private void SaveTrayOptions()
    {
        try { _storage.SetSetting(TrayOptionsKey, JsonSerializer.Serialize(_trayOptions)); }
        catch { /* settings are best-effort */ }
    }

    private void TrayOptionsChanged()
    {
        SaveTrayOptions();
        _lastTooltip = null;
        if (_meter is { IsDisposed: false }) _meter.UseBits = _trayOptions.ShowSpeedsInBits;
        UpdateTooltip(_lastDown, _lastUp);
        RenderTrayIcon(force: true);
    }

    private void ApplyMeterVisibility()
    {
        if (_trayOptions.ShowFloatingMeter)
        {
            if (_meter is { IsDisposed: false })
            {
                _meter.Show();
                return;
            }

            _meter = new FloatingMeterForm(SavedMeterLocation()) { UseBits = _trayOptions.ShowSpeedsInBits };
            _meter.CloseRequested += () =>
            {
                _trayOptions.ShowFloatingMeter = false;
                SaveTrayOptions();
                ApplyMeterVisibility();
            };
            _meter.Moved += p =>
            {
                _trayOptions.MeterX = p.X;
                _trayOptions.MeterY = p.Y;
                SaveTrayOptions();
            };
            foreach (var h in _trayHistory) _meter.Push(h.Down, h.Up);
            _meter.Show();
        }
        else if (_meter is not null)
        {
            _meter.Close();
            _meter = null;
        }
    }

    private Point? SavedMeterLocation()
    {
        if (_trayOptions.MeterX == int.MinValue || _trayOptions.MeterY == int.MinValue) return null;

        var p = new Point(_trayOptions.MeterX, _trayOptions.MeterY);
        var bounds = new Rectangle(p, new Size(190, 44));

        // Ignore a saved position that is no longer on any screen (monitor unplugged).
        foreach (var screen in Screen.AllScreens)
            if (screen.WorkingArea.IntersectsWith(bounds)) return p;
        return null;
    }

    // ----- Tray menu --------------------------------------------------------

    /// <summary>
    /// Builds the right-click menu. It offers every option the main window's
    /// menu bar has, so everything is reachable even when the window is hidden
    /// or in click-through mode.
    /// </summary>
    private void BuildTrayMenu(ContextMenuStrip menu)
    {
        MenuHelper.Clear(menu.Items);

        var open = new ToolStripMenuItem("Open ByteTide", null, (_, _) => ShowMain());
        open.Font = new Font(open.Font, FontStyle.Bold);
        menu.Items.Add(open);

        var form = _mainForm is { IsDisposed: false } ? _mainForm : null;
        if (form is not null)
        {
            menu.Items.Add(new ToolStripSeparator());

            var adapters = new ToolStripMenuItem("Adapters");
            form.PopulateAdaptersMenu(adapters.DropDownItems);
            menu.Items.Add(adapters);

            var view = new ToolStripMenuItem("View");
            form.PopulateViewMenu(view.DropDownItems);
            menu.Items.Add(view);

            var window = new ToolStripMenuItem("Window");
            form.PopulateWindowMenu(window.DropDownItems);
            menu.Items.Add(window);

            menu.Items.Add(BuildTrayIconMenu());

            var tools = new ToolStripMenuItem("Tools");
            form.PopulateToolsMenu(tools.DropDownItems, () => ShowMain(makeInteractive: false));
            menu.Items.Add(tools);

            menu.Items.Add(new ToolStripMenuItem("Reports...", null, (_, _) => { ShowMain(false); form.OpenReports(); }));
            menu.Items.Add(new ToolStripMenuItem("Import Legacy Data...", null, (_, _) => { ShowMain(false); form.OpenImport(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("About ByteTide", null, (_, _) => { ShowMain(false); form.ShowAbout(); }));
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()));
    }

    private ToolStripMenuItem BuildTrayIconMenu()
    {
        var root = new ToolStripMenuItem("Tray Icon");

        var style = new ToolStripMenuItem("Icon style");
        AddStyleItem(style, "Mini graph", TrayIconStyle.MiniGraph);
        AddStyleItem(style, "Speed numbers (download over upload)", TrayIconStyle.SpeedNumbers);
        AddStyleItem(style, "Classic dial-up monitors", TrayIconStyle.DialupMonitors);
        root.DropDownItems.Add(style);

        var colors = new ToolStripMenuItem("Dial-up monitor colors");
        AddPaletteItem(colors, "Green / Yellow", DialupPalette.GreenYellow);
        AddPaletteItem(colors, "Green", DialupPalette.Green);
        AddPaletteItem(colors, "Yellow", DialupPalette.Yellow);
        AddPaletteItem(colors, "Blue", DialupPalette.Blue);
        AddPaletteItem(colors, "XP Blue", DialupPalette.XpBlue);
        AddPaletteItem(colors, "ZoneAlarm", DialupPalette.ZoneAlarm);
        colors.DropDownItems.Add(new ToolStripSeparator());
        AddPaletteItem(colors, "Custom", DialupPalette.Custom);
        colors.DropDownItems.Add(new ToolStripMenuItem("Choose custom download color...", null, (_, _) => PickCustomColor(true)));
        colors.DropDownItems.Add(new ToolStripMenuItem("Choose custom upload color...", null, (_, _) => PickCustomColor(false)));
        root.DropDownItems.Add(colors);

        root.DropDownItems.Add(new ToolStripSeparator());

        var bits = new ToolStripMenuItem("Show speeds in bits (kbps / Mbps)") { Checked = _trayOptions.ShowSpeedsInBits };
        bits.Click += (_, _) =>
        {
            _trayOptions.ShowSpeedsInBits = !_trayOptions.ShowSpeedsInBits;
            TrayOptionsChanged();
        };
        root.DropDownItems.Add(bits);

        var meter = new ToolStripMenuItem("Floating mini meter") { Checked = _trayOptions.ShowFloatingMeter };
        meter.Click += (_, _) =>
        {
            _trayOptions.ShowFloatingMeter = !_trayOptions.ShowFloatingMeter;
            SaveTrayOptions();
            ApplyMeterVisibility();
        };
        root.DropDownItems.Add(meter);

        return root;
    }

    private void AddStyleItem(ToolStripMenuItem parent, string text, TrayIconStyle value)
    {
        var item = new ToolStripMenuItem(text) { Checked = _trayOptions.Style == value };
        item.Click += (_, _) =>
        {
            _trayOptions.Style = value;
            TrayOptionsChanged();
        };
        parent.DropDownItems.Add(item);
    }

    private void AddPaletteItem(ToolStripMenuItem parent, string text, DialupPalette value)
    {
        var item = new ToolStripMenuItem(text) { Checked = _trayOptions.Palette == value };
        item.Click += (_, _) =>
        {
            _trayOptions.Palette = value;
            _trayOptions.Style = TrayIconStyle.DialupMonitors;
            TrayOptionsChanged();
        };
        parent.DropDownItems.Add(item);
    }

    private void PickCustomColor(bool download)
    {
        using var dlg = new ColorDialog
        {
            FullOpen = true,
            Color = Color.FromArgb(download ? _trayOptions.CustomDownloadArgb : _trayOptions.CustomUploadArgb)
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        if (download) _trayOptions.CustomDownloadArgb = dlg.Color.ToArgb();
        else _trayOptions.CustomUploadArgb = dlg.Color.ToArgb();

        _trayOptions.Palette = DialupPalette.Custom;
        _trayOptions.Style = TrayIconStyle.DialupMonitors;
        TrayOptionsChanged();
    }

    // ----- Daily rollups ----------------------------------------------------

    private void FlushDailyStats()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!_today.TryGetValue(nic.Id, out var acc) || acc.Date != today) continue;

            var info = NetworkSampler.GetAdapterInfo(nic);
            long rx = Math.Max(0, acc.LastBytesReceived - acc.StartBytesReceived);
            long tx = Math.Max(0, acc.LastBytesSent - acc.StartBytesSent);

            _storage.UpsertDailyStat(new DailyStat
            {
                Date = today,
                AdapterName = info.Name,
                MacAddress = info.MacAddress,
                SecondsOnline = acc.SecondsOnline,
                BytesReceived = rx,
                BytesSent = tx
            });
        }

        _storage.PruneLiveSamplesOlderThan(TimeSpan.FromHours(48));
    }

    // ----- Bandwidth usage notifications -----------------------------------

    private void CheckNotifications()
    {
        try
        {
            var json = _storage.GetSetting(NotificationSettingsKey);
            if (json is null) return;

            var settings = JsonSerializer.Deserialize<NotificationSettings>(json);
            if (settings is null || !settings.Enabled) return;

            var result = NotificationEngine.CheckThreshold(_storage, settings);
            if (!result.Triggered) return;

            FireNotification(settings, result);

            settings.LastTriggeredPeriodKey = result.PeriodKey;
            _storage.SetSetting(NotificationSettingsKey, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // Notification config/delivery problems shouldn't affect core monitoring.
        }
    }

    private void FireNotification(NotificationSettings settings, NotificationEngine.CheckResult result)
    {
        var message =
            $"Bandwidth threshold exceeded: {FormatBytes(result.TotalBytes)} {settings.Measure} " +
            $"(limit was {FormatBytes(result.ThresholdBytes)}) for the current {settings.Period}.";

        _trayIcon.ShowBalloonTip(8000, "ByteTide", message, ToolTipIcon.Warning);

        if (settings.PlayBeep)
        {
            try { SystemSounds.Beep.Play(); } catch { /* ignore */ }
        }

        if (settings.PlaySound && !string.IsNullOrWhiteSpace(settings.SoundFilePath) && File.Exists(settings.SoundFilePath))
        {
            try
            {
                using var player = new SoundPlayer(settings.SoundFilePath);
                player.Play();
            }
            catch { /* ignore */ }
        }

        if (settings.RunProgram && !string.IsNullOrWhiteSpace(settings.ProgramPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(settings.ProgramPath) { UseShellExecute = true });
            }
            catch { /* ignore */ }
        }

        if (settings.SendEmail && !string.IsNullOrWhiteSpace(settings.EmailAddresses))
        {
            try { SendAlertEmail(settings, message); } catch { /* ignore -- best effort */ }
        }
    }

    private void SendAlertEmail(NotificationSettings settings, string body)
    {
        var emailJson = _storage.GetSetting(EmailSettingsKey);
        if (emailJson is null) return;

        var email = JsonSerializer.Deserialize<EmailSettings>(emailJson);
        if (email is null || string.IsNullOrWhiteSpace(email.SmtpHost)) return;

        using var client = new SmtpClient(email.SmtpHost, email.SmtpPort)
        {
            Credentials = new NetworkCredential(email.Username, email.Password),
            EnableSsl = email.UseSsl
        };

        using var message = new MailMessage
        {
            From = new MailAddress(email.FromAddress),
            Subject = "ByteTide Alert",
            Body = body
        };

        var addresses = (settings.EmailAddresses ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var addr in addresses)
            message.To.Add(addr);

        if (message.To.Count > 0)
            client.Send(message);
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

    // ----- Window lifecycle -------------------------------------------------

    /// <summary>
    /// Brings the main window to the front. With makeInteractive (the default,
    /// used by "Open" and double-clicking the tray icon) it also switches
    /// click-through off -- that is the guaranteed way out of click-through mode.
    /// </summary>
    private void ShowMain(bool makeInteractive = true)
    {
        if (_mainForm is { IsDisposed: false })
        {
            if (makeInteractive && _mainForm.ClickThrough) _mainForm.ClickThrough = false;
            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
            return;
        }

        _mainForm = new MainForm(_sampler, _storage);
        _mainForm.ExitRequested = ExitApp;
        _mainForm.ClickThroughChanged += on =>
        {
            if (on)
            {
                _trayIcon.ShowBalloonTip(
                    8000,
                    "Click-through is ON",
                    "The graph window now ignores mouse clicks. To turn it off, double-click the " +
                    "ByteTide tray icon, or right-click it and choose Window > Click-through.",
                    ToolTipIcon.Info);
            }
        };
        _mainForm.FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // Keep monitoring in the tray instead of exiting.
                e.Cancel = true;
                _mainForm.Hide();
            }
        };
        _mainForm.Show();
    }

    private void ExitApp()
    {
        FlushDailyStats();
        _trayIcon.Visible = false;
        _currentTrayIcon?.Dispose();

        if (_meter is { IsDisposed: false }) _meter.Close();
        _meter = null;
        _marshal.Dispose();

        // Application.Exit() synchronously raises FormClosing on MainForm,
        // which saves the window position via _storage.SetSetting() -- so
        // Storage (and the sampler feeding it) must stay alive until after
        // this call returns, not be disposed beforehand.
        Application.Exit();

        _sampler.Dispose();
        _storage.Dispose();
    }

    private sealed class DailyAccumulator
    {
        public DateOnly Date;
        public long StartBytesReceived;
        public long StartBytesSent;
        public long LastBytesReceived;
        public long LastBytesSent;
        public long SecondsOnline;
    }
}
