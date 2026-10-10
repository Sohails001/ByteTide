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
        _sampler = new NetworkSampler(TimeSpan.FromSeconds(1));
        _sampler.SamplesReady += OnSamples;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMain());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

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

        UpdateTrayIcon(samples);
    }

    // ----- Tray mini-graph ------------------------------------------------

    private void UpdateTrayIcon(IReadOnlyList<LiveSample> samples)
    {
        double down = samples.Sum(s => s.DownloadBps);
        double up = samples.Sum(s => s.UploadBps);

        _trayHistory.Add((down, up));
        if (_trayHistory.Count > 16) _trayHistory.RemoveAt(0);

        // Regenerating the icon involves a Bitmap allocation, a GDI+ draw pass,
        // and an unmanaged icon-handle round trip (GetHicon/DestroyIcon) --
        // real, measurable cost if repeated every single second forever for as
        // long as the app runs. Every 2s is still visually "live" in the tray
        // and roughly halves that overhead.
        if ((DateTime.Now - _lastTrayIconUpdate).TotalSeconds < 2) return;
        _lastTrayIconUpdate = DateTime.Now;

        try
        {
            RenderTrayIcon();
        }
        catch
        {
            // Icon rendering is cosmetic; never let a drawing failure take down monitoring.
        }
    }

    private void RenderTrayIcon()
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);

            if (_trayHistory.Count >= 2)
            {
                double max = Math.Max(_trayHistory.Max(s => s.Down), _trayHistory.Max(s => s.Up));
                max = Math.Max(max, 1024);

                DrawMiniSeries(g, size, _trayHistory.Select(s => s.Down).ToList(), max, Color.FromArgb(90, 160, 255));
                DrawMiniSeries(g, size, _trayHistory.Select(s => s.Up).ToList(), max, Color.FromArgb(255, 100, 100));
            }
        }

        var hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            var cloned = (Icon)temp.Clone(); // independent GDI+ copy, safe to keep after we destroy hIcon below

            var old = _currentTrayIcon;
            _currentTrayIcon = cloned;
            _trayIcon.Icon = cloned;
            old?.Dispose();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    private static void DrawMiniSeries(Graphics g, int size, List<double> values, double max, Color color)
    {
        int n = values.Count;
        if (n < 2) return;

        using var pen = new Pen(color, 1.5f);
        float xStep = size / (float)(16 - 1);
        float xOffset = (16 - n) * xStep;

        var points = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            float x = xOffset + i * xStep;
            float y = size - (float)(values[i] / max * size);
            y = Math.Max(0, Math.Min(size, y));
            points[i] = new PointF(x, y);
        }
        g.DrawLines(pen, points);
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

    private void ShowMain()
    {
        if (_mainForm is { IsDisposed: false })
        {
            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
            return;
        }

        _mainForm = new MainForm(_sampler, _storage);
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
