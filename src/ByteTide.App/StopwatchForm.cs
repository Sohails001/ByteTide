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

    private readonly ComboBox _adapter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly CheckBox _alwaysOnTop = new() { Text = "Always on top", Checked = true };
    private readonly Button _toggle = new() { Text = "Start" };
    private readonly Label _elapsed = new() { Text = "00:00:00", Font = new Font("Consolas", 14f, FontStyle.Bold), AutoSize = true };

    private readonly Label _totalDown = new() { Text = "--" };
    private readonly Label _totalUp = new() { Text = "--" };
    private readonly Label _maxDown = new() { Text = "--" };
    private readonly Label _maxUp = new() { Text = "--" };
    private readonly Label _minDown = new() { Text = "--" };
    private readonly Label _minUp = new() { Text = "--" };
    private readonly Label _avgDown = new() { Text = "--" };
    private readonly Label _avgUp = new() { Text = "--" };

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

        Text = "Traffic Stopwatch";
        Width = 420;
        Height = 300;
        StartPosition = FormStartPosition.CenterParent;
        TopMost = true;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(10) };
        top.Controls.Add(new Label { Text = "Adapter:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
        top.Controls.Add(_adapter);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 5,
            Padding = new Padding(10),
            AutoSize = true
        };
        grid.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 0);
        grid.Controls.Add(new Label { Text = "Download", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 1, 0);
        grid.Controls.Add(new Label { Text = "Upload", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 2, 0);

        AddRow(grid, 1, "Total:", _totalDown, _totalUp);
        AddRow(grid, 2, "Maximum Rate:", _maxDown, _maxUp);
        AddRow(grid, 3, "Minimum Rate:", _minDown, _minUp);
        AddRow(grid, 4, "Average Rate:", _avgDown, _avgUp);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(10) };
        bottom.Controls.Add(_toggle);
        bottom.Controls.Add(new Panel { Width = 20 });
        bottom.Controls.Add(_elapsed);

        var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(10) };
        optionsPanel.Controls.Add(_alwaysOnTop);
        _alwaysOnTop.CheckedChanged += (_, _) => TopMost = _alwaysOnTop.Checked;

        Controls.Add(bottom);
        Controls.Add(grid);
        Controls.Add(optionsPanel);
        Controls.Add(top);

        _toggle.Click += (_, _) => ToggleRunning();

        PopulateAdapters();

        _sampler.SamplesReady += OnSamples;
        FormClosing += (_, _) => _sampler.SamplesReady -= OnSamples;
    }

    private static void AddRow(TableLayoutPanel grid, int row, string label, Label down, Label up)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 4, 10, 0) }, 0, row);
        down.AutoSize = true;
        down.Padding = new Padding(0, 4, 20, 0);
        up.AutoSize = true;
        up.Padding = new Padding(0, 4, 0, 0);
        grid.Controls.Add(down, 1, row);
        grid.Controls.Add(up, 2, row);
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

        // Sampler ticks at ~1Hz, so a rate in bytes/sec is a reasonable
        // stand-in for "bytes transferred this tick".
        _totalDownBytes += (long)down;
        _totalUpBytes += (long)up;
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
