using System.Net.NetworkInformation;
using ByteTide.Core;

namespace ByteTide.App;

/// <summary>
/// "Rates Recorder" -- logs periodic download/upload rates for one adapter
/// (or all combined) to a plain text or CSV file.
/// </summary>
public sealed class RatesRecorderForm : Form
{
    private readonly NetworkSampler _sampler;

    private readonly ComboBox _adapter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly RadioButton _plainFormat = new() { Text = "Plain text", Checked = true, AutoSize = true };
    private readonly RadioButton _csvFormat = new() { Text = "CSV", AutoSize = true };
    private readonly NumericUpDown _interval = new() { Minimum = 1, Maximum = 3600, Value = 1 };
    private readonly TextBox _filePath = new();
    private readonly Button _browse = new() { Text = "Browse..." };
    private readonly Button _toggle = new() { Text = "Start recording" };
    private readonly Button _close = new() { Text = "Close", DialogResult = DialogResult.Cancel };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Theme.TextMuted, Text = "Not recording." };

    private RatesRecorder? _recorder;
    private readonly List<string> _adapterIds = new();

    public RatesRecorderForm(NetworkSampler sampler)
    {
        _sampler = sampler;

        FormKit.Setup(this, "Rates Recorder", 560, 360);

        Controls.Add(FormKit.Heading("Rates Recorder", 24, 18));
        Controls.Add(FormKit.Muted("Log download and upload rates to a text or CSV file at a regular interval.", 24, 54, 512, 22));

        Controls.Add(FormKit.Caption("Adapter", 24, 108));
        FormKit.Place(_adapter, 160, 104, 376);
        Controls.Add(_adapter);

        Controls.Add(FormKit.Caption("Format", 24, 150));
        _plainFormat.Location = new Point(160, 148);
        _csvFormat.Location = new Point(280, 148);
        Controls.Add(_plainFormat);
        Controls.Add(_csvFormat);

        Controls.Add(FormKit.Caption("Record every", 24, 192));
        FormKit.Place(_interval, 160, 188, 72);
        Controls.Add(_interval);
        Controls.Add(FormKit.Caption("second(s)", 242, 192));

        Controls.Add(FormKit.Caption("Save to", 24, 234));
        FormKit.Place(_filePath, 160, 230, 292);
        Controls.Add(_filePath);
        _browse.Location = new Point(462, 227);
        Theme.StyleButton(_browse);
        Controls.Add(_browse);

        Theme.StyleButton(_toggle, primary: true);
        Theme.StyleButton(_close);
        FormKit.AddFooter(this, _status, _close, _toggle);
        CancelButton = _close;

        FormKit.StyleInputs(_adapter, _filePath);

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
            _toggle.Text = "Start recording";
            _status.Text = "Stopped.";
            _status.ForeColor = Theme.TextMuted;
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
        _toggle.Text = "Stop recording";
        _status.Text = $"Recording to {Path.GetFileName(_filePath.Text)}...";
        _status.ForeColor = Color.FromArgb(24, 115, 52);
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
