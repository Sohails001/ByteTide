using System.Net.NetworkInformation;
using ByteTide.Core;

namespace ByteTide.App;

/// <summary>
/// "Traffic Rates Recorder" -- logs periodic download/upload rates for one
/// adapter (or all combined) to a plain text or CSV file.
/// </summary>
public sealed class RatesRecorderForm : Form
{
    private readonly NetworkSampler _sampler;

    private readonly ComboBox _adapter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly RadioButton _plainFormat = new() { Text = "Plain text", Checked = true };
    private readonly RadioButton _csvFormat = new() { Text = "CSV" };
    private readonly NumericUpDown _interval = new() { Minimum = 1, Maximum = 3600, Value = 1, Width = 60 };
    private readonly TextBox _filePath = new() { Width = 320 };
    private readonly Button _browse = new() { Text = "Browse..." };
    private readonly Button _toggle = new() { Text = "Start Recording" };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.DarkGreen };

    private RatesRecorder? _recorder;
    private readonly List<string> _adapterIds = new();

    public RatesRecorderForm(NetworkSampler sampler)
    {
        _sampler = sampler;

        Text = "Traffic Rates Recorder";
        Width = 460;
        Height = 260;
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(12), AutoSize = true };
        layout.Controls.Add(new Label { Text = "Adapter:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(_adapter);

        var formatPanel = new FlowLayoutPanel { AutoSize = true };
        formatPanel.Controls.Add(_plainFormat);
        formatPanel.Controls.Add(_csvFormat);
        layout.Controls.Add(new Label { Text = "Format:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(formatPanel);

        var intervalPanel = new FlowLayoutPanel { AutoSize = true };
        intervalPanel.Controls.Add(_interval);
        intervalPanel.Controls.Add(new Label { Text = "second(s)", AutoSize = true, Padding = new Padding(4, 6, 0, 0) });
        layout.Controls.Add(new Label { Text = "Recording interval:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(intervalPanel);

        var filePanel = new FlowLayoutPanel { AutoSize = true };
        filePanel.Controls.Add(_filePath);
        filePanel.Controls.Add(_browse);
        layout.Controls.Add(new Label { Text = "Save rates to:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(filePanel);

        _browse.Click += (_, _) =>
        {
            using var dlg = new SaveFileDialog
            {
                Filter = _csvFormat.Checked ? "CSV file (*.csv)|*.csv" : "Text file (*.txt)|*.txt",
                FileName = _csvFormat.Checked ? "rates.csv" : "rates.txt"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK) _filePath.Text = dlg.FileName;
        };

        _toggle.Click += (_, _) => ToggleRecording();

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(12) };
        bottom.Controls.Add(_toggle);
        bottom.Controls.Add(new Panel { Width = 12 });
        bottom.Controls.Add(_status);

        Controls.Add(bottom);
        Controls.Add(layout);

        PopulateAdapters();

        FormClosing += (_, _) => _recorder?.Dispose();
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

    private void ToggleRecording()
    {
        if (_recorder is not null)
        {
            _recorder.Dispose();
            _recorder = null;
            _toggle.Text = "Start Recording";
            _status.Text = "Stopped.";
            SetInputsEnabled(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_filePath.Text))
        {
            MessageBox.Show(this, "Choose a file to save rates to first.", "Rates Recorder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string? adapterId = _adapter.SelectedIndex > 0 ? _adapterIds[_adapter.SelectedIndex] : null;
        var interval = TimeSpan.FromSeconds((double)_interval.Value);

        _recorder = new RatesRecorder(_sampler, adapterId, interval, _filePath.Text, _csvFormat.Checked);
        _toggle.Text = "Stop Recording";
        _status.Text = $"Recording to {Path.GetFileName(_filePath.Text)}...";
        SetInputsEnabled(false);
    }

    private void SetInputsEnabled(bool enabled)
    {
        _adapter.Enabled = enabled;
        _plainFormat.Enabled = enabled;
        _csvFormat.Enabled = enabled;
        _interval.Enabled = enabled;
        _filePath.Enabled = enabled;
        _browse.Enabled = enabled;
    }
}
