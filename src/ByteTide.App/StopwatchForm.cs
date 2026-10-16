using System.Net.NetworkInformation;
using ByteTide.Core;

namespace ByteTide.App;

/// <summary>
/// "Traffic Stopwatch" -- a timed speed test: start it, let it run for a
/// while, and see total transferred plus max/min/average rate for the
/// chosen adapter over that window.
/// </summary>
public sealed class StopwatchForm : Form
{
    private readonly NetworkSampler _sampler;

    private readonly ComboBox _adapter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _alwaysOnTop = new() { Text = "Always on top", Checked = true, AutoSize = true };
    private readonly Button _toggle = new() { Text = "Start" };
    private readonly Button _close = new() { Text = "Close", DialogResult = DialogResult.Cancel };
    private readonly Label _elapsed = new() { Text = "00:00:00", Font = new Font("Consolas", 26f, FontStyle.Bold), ForeColor = Theme.TextPrimary, AutoSize = true };

    private readonly Label _totalDown = ValueLabel();
    private readonly Label _totalUp = ValueLabel();
    private readonly Label _maxDown = ValueLabel();
    private readonly Label _maxUp = ValueLabel();
    private readonly Label _minDown = ValueLabel();
    private readonly Label _minUp = ValueLabel();
    private readonly Label _avgDown = ValueLabel();
    private readonly Label _avgUp = ValueLabel();

    private readonly List<string> _adapterIds = new();

    private bool _running;
    private DateTime _startTime;
    private long _totalDownBytes;
    private long _totalUpBytes;
    private double _maxDownRate;
    private double _maxUpRate;
    private double _minDownRate = double.MaxValue;
    private double _minUpRate = double.MaxValue;

    public StopwatchForm(NetworkSampler sampler)
    {
        _sampler = sampler;

        FormKit.Setup(this, "Traffic Stopwatch", 480, 470);
        TopMost = true;

        Controls.Add(FormKit.Heading("Traffic Stopwatch", 24, 18));
        Controls.Add(FormKit.Muted("Press Start, run your download or upload, then press Stop to see how fast it was.", 24, 54, 432, 40));

        Controls.Add(FormKit.Caption("Adapter", 24, 108));
        FormKit.Place(_adapter, 24, 130, 432);
        Controls.Add(_adapter);
        FormKit.StyleInputs(_adapter);

        _elapsed.Location = new Point(22, 176);
        Controls.Add(_elapsed);

        _toggle.Location = new Point(342, 180);
        Theme.StyleButton(_toggle, primary: true);
        Controls.Add(_toggle);

        // Results card
        var card = new Panel { Location = new Point(24, 244), Size = new Size(432, 152), BackColor = Theme.SurfaceAlt };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        AddCardHeader(card, "Download", 170);
        AddCardHeader(card, "Upload", 300);
        AddCardRow(card, 34, "Total", _totalDown, _totalUp);
        AddCardRow(card, 62, "Maximum rate", _maxDown, _maxUp);
        AddCardRow(card, 90, "Minimum rate", _minDown, _minUp);
        AddCardRow(card, 118, "Average rate", _avgDown, _avgUp);
        Controls.Add(card);

        Theme.StyleButton(_close);
        FormKit.AddFooter(this, _alwaysOnTop, _close);
        CancelButton = _close;

        _alwaysOnTop.CheckedChanged += (_, _) => TopMost = _alwaysOnTop.Checked;
        _toggle.Click += (_, _) => ToggleRunning();

        PopulateAdapters();

        _sampler.SamplesReady += OnSamples;
        FormClosing += (_, _) => _sampler.SamplesReady -= OnSamples;
    }

    private static Label ValueLabel() => new() { Text = "--", AutoSize = true, ForeColor = Theme.TextPrimary };

    private static void AddCardHeader(Panel card, string text, int x)
    {
        card.Controls.Add(new Label
        {
            Text = text,
            Font = Theme.Ui(9f, FontStyle.Bold),
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Location = new Point(x, 10)
        });
    }

    private static void AddCardRow(Panel card, int y, string label, Label down, Label up)
    {
        card.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = Theme.TextMuted, Location = new Point(14, y) });
        down.Location = new Point(170, y);
        up.Location = new Point(300, y);
        card.Controls.Add(down);
        card.Controls.Add(up);
    }

    private void PopulateAdapters()
    {
        _adapter.Items.Add("All Connections");
        _adapterIds.Add("");

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            _adapter.Items.Add(nic.Description);
            _adapterIds.Add(nic.Id);
        }

        _adapter.SelectedIndex = 0;
    }

    private void ToggleRunning()
    {
        _running = !_running;

        if (_running)
        {
            _startTime = DateTime.Now;
            _totalDownBytes = 0;
            _totalUpBytes = 0;
            _maxDownRate = 0;
            _maxUpRate = 0;
            _minDownRate = double.MaxValue;
            _minUpRate = double.MaxValue;
            _toggle.Text = "Stop";
            _adapter.Enabled = false;
        }
        else
        {
            _toggle.Text = "Start";
            _adapter.Enabled = true;
        }
    }

    private void OnSamples(IReadOnlyList<LiveSample> samples)
    {
        if (!_running) return;

        string? adapterId = _adapter.SelectedIndex > 0 ? _adapterIds[_adapter.SelectedIndex] : null;
        var relevant = adapterId is null ? samples : samples.Where(s => s.AdapterId == adapterId).ToList();
        if (relevant.Count == 0) return;

        double down = relevant.Sum(s => s.DownloadBps);
        double up = relevant.Sum(s => s.UploadBps);

        // Exact bytes moved since the previous sample (reset-safe).
        _totalDownBytes += relevant.Sum(s => s.DeltaBytesReceived);
        _totalUpBytes += relevant.Sum(s => s.DeltaBytesSent);
        _maxDownRate = Math.Max(_maxDownRate, down);
        _maxUpRate = Math.Max(_maxUpRate, up);
        _minDownRate = Math.Min(_minDownRate, down);
        _minUpRate = Math.Min(_minUpRate, up);

        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(UpdateUi)); }
            catch (ObjectDisposedException) { /* closing race, ignore */ }
        }
        else
        {
            UpdateUi();
        }
    }

    private void UpdateUi()
    {
        var elapsed = DateTime.Now - _startTime;
        double seconds = Math.Max(1, elapsed.TotalSeconds);

        _elapsed.Text = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        _totalDown.Text = FormatBytes(_totalDownBytes);
        _totalUp.Text = FormatBytes(_totalUpBytes);
        _maxDown.Text = FormatRate(_maxDownRate);
        _maxUp.Text = FormatRate(_maxUpRate);
        _minDown.Text = FormatRate(_minDownRate == double.MaxValue ? 0 : _minDownRate);
        _minUp.Text = FormatRate(_minUpRate == double.MaxValue ? 0 : _minUpRate);
        _avgDown.Text = FormatRate(_totalDownBytes / seconds);
        _avgUp.Text = FormatRate(_totalUpBytes / seconds);
    }

    private static string FormatRate(double bytesPerSec)
    {
        if (bytesPerSec >= 1024 * 1024) return $"{bytesPerSec / (1024 * 1024):0.00} MB/s";
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
}
