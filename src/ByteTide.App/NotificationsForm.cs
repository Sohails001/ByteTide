using System.Text.Json;
using ByteTide.Core;

namespace ByteTide.App;

/// <summary>
/// Bandwidth usage alert configuration: "notify me when more than X GB were
/// downloaded this month", with sound/beep/email/run-program actions.
/// Settings are read by TrayContext's rollup timer, which does the actual
/// threshold checking and firing -- this form only edits the config.
/// </summary>
public sealed class NotificationsForm : Form
{
    private readonly Storage _storage;

    private readonly CheckBox _enable = new() { Text = "Enable notification" };

    private readonly NumericUpDown _amount = new() { Minimum = 0, Maximum = 999999, DecimalPlaces = 2, Value = 2, Width = 80 };
    private readonly ComboBox _unit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
    private readonly ComboBox _measure = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _period = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly ComboBox _adapter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };

    private readonly CheckBox _playSound = new() { Text = "play a sound" };
    private readonly TextBox _soundPath = new() { Width = 220 };
    private readonly Button _browseSound = new() { Text = "..." };

    private readonly CheckBox _playBeep = new() { Text = "play beep" };

    private readonly CheckBox _sendEmail = new() { Text = "send me an email" };
    private readonly Button _emailSettingsBtn = new() { Text = "Email Settings..." };
    private readonly TextBox _emailAddresses = new() { Width = 300 };

    private readonly CheckBox _runProgram = new() { Text = "run a program" };
    private readonly TextBox _programPath = new() { Width = 220 };
    private readonly Button _browseProgram = new() { Text = "..." };

    private EmailSettings _emailSettings = new();

    private const string NotificationSettingsKey = "notificationSettings";
    private const string EmailSettingsKey = "emailSettings";

    public NotificationsForm(Storage storage)
    {
        _storage = storage;

        Text = "Bandwidth Usage Notification";
        Width = 560;
        Height = 480;
        StartPosition = FormStartPosition.CenterParent;

        _unit.Items.AddRange(new object[] { "MB", "GB" });
        _measure.Items.AddRange(new object[] { "downloaded", "uploaded", "both" });
        _period.Items.AddRange(new object[] { "today", "week", "month", "year" });

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        top.Controls.Add(_enable);

        var thresholdLine = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 0, 12, 0) };
        thresholdLine.Controls.Add(new Label { Text = "When more than", AutoSize = true, Padding = new Padding(0, 6, 4, 0) });
        thresholdLine.Controls.Add(_amount);
        thresholdLine.Controls.Add(_unit);
        thresholdLine.Controls.Add(new Label { Text = "were", AutoSize = true, Padding = new Padding(8, 6, 4, 0) });
        thresholdLine.Controls.Add(_measure);

        var periodLine = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 0, 12, 0) };
        periodLine.Controls.Add(new Label { Text = "in the current", AutoSize = true, Padding = new Padding(0, 6, 4, 0) });
        periodLine.Controls.Add(_period);
        periodLine.Controls.Add(new Label { Text = "through", AutoSize = true, Padding = new Padding(8, 6, 4, 0) });
        periodLine.Controls.Add(_adapter);

        var soundLine = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 6, 12, 0) };
        soundLine.Controls.Add(_playSound);
        soundLine.Controls.Add(_soundPath);
        soundLine.Controls.Add(_browseSound);
        _browseSound.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Wave files (*.wav)|*.wav|All files (*.*)|*.*" };
            if (dlg.ShowDialog(this) == DialogResult.OK) _soundPath.Text = dlg.FileName;
        };

        var beepLine = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 6, 12, 0) };
        beepLine.Controls.Add(_playBeep);

        var emailLine = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 6, 12, 0) };
        emailLine.Controls.Add(_sendEmail);
        emailLine.Controls.Add(_emailSettingsBtn);
        _emailSettingsBtn.Click += (_, _) =>
        {
            using var dlg = new EmailSettingsForm(_emailSettings);
            if (dlg.ShowDialog(this) == DialogResult.OK) _emailSettings = dlg.Result;
        };

        var emailAddrLine = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 0, 12, 0) };
        emailAddrLine.Controls.Add(new Label { Text = "Address(es), comma-separated:", AutoSize = true, Padding = new Padding(0, 6, 4, 0) });
        emailAddrLine.Controls.Add(_emailAddresses);

        var programLine = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 6, 12, 0) };
        programLine.Controls.Add(_runProgram);
        programLine.Controls.Add(_programPath);
        programLine.Controls.Add(_browseProgram);
        _browseProgram.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*" };
            if (dlg.ShowDialog(this) == DialogResult.OK) _programPath.Text = dlg.FileName;
        };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Padding = new Padding(12), FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        bottom.Controls.Add(cancel);
        bottom.Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;
        ok.Click += (_, _) => SaveSettings();

        Controls.Add(bottom);
        Controls.Add(programLine);
        Controls.Add(emailAddrLine);
        Controls.Add(emailLine);
        Controls.Add(beepLine);
        Controls.Add(soundLine);
        Controls.Add(periodLine);
        Controls.Add(thresholdLine);
        Controls.Add(top);

        PopulateAdapters();
        LoadSettings();
    }

    private void PopulateAdapters()
    {
        _adapter.Items.Add("All Connections");
        foreach (var name in _storage.GetKnownAdapterNames())
            _adapter.Items.Add(name);
        _adapter.SelectedIndex = 0;
    }

    private void LoadSettings()
    {
        var settings = new NotificationSettings();
        var json = _storage.GetSetting(NotificationSettingsKey);
        if (json is not null)
        {
            try { settings = JsonSerializer.Deserialize<NotificationSettings>(json) ?? new NotificationSettings(); }
            catch { settings = new NotificationSettings(); }
        }

        var emailJson = _storage.GetSetting(EmailSettingsKey);
        if (emailJson is not null)
        {
            try { _emailSettings = JsonSerializer.Deserialize<EmailSettings>(emailJson) ?? new EmailSettings(); }
            catch { _emailSettings = new EmailSettings(); }
        }

        _enable.Checked = settings.Enabled;
        _amount.Value = (decimal)settings.ThresholdAmount;
        _unit.SelectedItem = settings.ThresholdUnit;
        _measure.SelectedItem = settings.Measure;
        _period.SelectedItem = settings.Period;
        _adapter.SelectedItem = settings.AdapterFilter ?? "All Connections";

        _playSound.Checked = settings.PlaySound;
        _soundPath.Text = settings.SoundFilePath ?? "";
        _playBeep.Checked = settings.PlayBeep;
        _sendEmail.Checked = settings.SendEmail;
        _emailAddresses.Text = settings.EmailAddresses ?? "";
        _runProgram.Checked = settings.RunProgram;
        _programPath.Text = settings.ProgramPath ?? "";

        if (_unit.SelectedIndex < 0) _unit.SelectedIndex = 1;
        if (_measure.SelectedIndex < 0) _measure.SelectedIndex = 0;
        if (_period.SelectedIndex < 0) _period.SelectedIndex = 2;
    }

    private void SaveSettings()
    {
        var settings = new NotificationSettings
        {
            Enabled = _enable.Checked,
            ThresholdAmount = (double)_amount.Value,
            ThresholdUnit = _unit.SelectedItem?.ToString() ?? "GB",
            Measure = _measure.SelectedItem?.ToString() ?? "downloaded",
            Period = _period.SelectedItem?.ToString() ?? "month",
            AdapterFilter = _adapter.SelectedIndex <= 0 ? null : _adapter.SelectedItem?.ToString(),
            PlaySound = _playSound.Checked,
            SoundFilePath = _soundPath.Text,
            PlayBeep = _playBeep.Checked,
            SendEmail = _sendEmail.Checked,
            EmailAddresses = _emailAddresses.Text,
            RunProgram = _runProgram.Checked,
            ProgramPath = _programPath.Text
        };

        _storage.SetSetting(NotificationSettingsKey, JsonSerializer.Serialize(settings));
        _storage.SetSetting(EmailSettingsKey, JsonSerializer.Serialize(_emailSettings));
    }
}
