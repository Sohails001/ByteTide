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

        FormKit.Setup(this, "Usage Notifications", 620, 540);

        _unit.Items.AddRange(new object[] { "MB", "GB" });
        _measure.Items.AddRange(new object[] { "downloaded", "uploaded", "both" });
        _period.Items.AddRange(new object[] { "today", "week", "month", "year" });

        Controls.Add(FormKit.Heading("Usage Notifications", 24, 18));
        Controls.Add(FormKit.Muted("Get alerted when your usage crosses a limit, for example more than 2 GB downloaded this month.", 24, 54, 572, 22));

        _enable.Location = new Point(24, 90);
        _enable.AutoSize = true;
        Controls.Add(_enable);

        // "When more than [2] [GB] were [downloaded]"
        Controls.Add(FormKit.Caption("When more than", 24, 134));
        FormKit.Place(_amount, 150, 130, 84);
        FormKit.Place(_unit, 242, 130, 64);
        Controls.Add(FormKit.Caption("were", 316, 134));
        FormKit.Place(_measure, 360, 130, 130);
        Controls.Add(_amount);
        Controls.Add(_unit);
        Controls.Add(_measure);

        // "in the current [month] through [All Connections]"
        Controls.Add(FormKit.Caption("in the current", 24, 176));
        FormKit.Place(_period, 150, 172, 100);
        Controls.Add(FormKit.Caption("through", 262, 176));
        FormKit.Place(_adapter, 330, 172, 266);
        Controls.Add(_period);
        Controls.Add(_adapter);

        var divider = new Panel { BackColor = Theme.Border, Location = new Point(24, 220), Size = new Size(572, 1) };
        Controls.Add(divider);
        Controls.Add(new Label { Text = "When that happens", Font = Theme.Ui(9f, FontStyle.Bold), ForeColor = Theme.TextMuted, AutoSize = true, Location = new Point(24, 232) });

        // Sound
        _playSound.Location = new Point(24, 266);
        _playSound.AutoSize = true;
        FormKit.Place(_soundPath, 190, 264, 340);
        _browseSound.Location = new Point(538, 262);
        Theme.StyleButton(_browseSound);
        _browseSound.MinimumSize = new Size(58, 32);
        _browseSound.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Wave files (*.wav)|*.wav|All files (*.*)|*.*" };
            if (dlg.ShowDialog(this) == DialogResult.OK) _soundPath.Text = dlg.FileName;
        };

        // Beep
        _playBeep.Location = new Point(24, 308);
        _playBeep.AutoSize = true;

        // Email
        _sendEmail.Location = new Point(24, 350);
        _sendEmail.AutoSize = true;
        _emailSettingsBtn.Location = new Point(190, 345);
        Theme.StyleButton(_emailSettingsBtn);
        _emailSettingsBtn.Click += (_, _) =>
        {
            using var dlg = new EmailSettingsForm(_emailSettings);
            if (dlg.ShowDialog(this) == DialogResult.OK) _emailSettings = dlg.Result;
        };
        Controls.Add(FormKit.Caption("Send to", 44, 394));
        FormKit.Place(_emailAddresses, 190, 390, 406);

        // Program
        _runProgram.Location = new Point(24, 436);
        _runProgram.AutoSize = true;
        FormKit.Place(_programPath, 190, 434, 340);
        _browseProgram.Location = new Point(538, 432);
        Theme.StyleButton(_browseProgram);
        _browseProgram.MinimumSize = new Size(58, 32);
        _browseProgram.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*" };
            if (dlg.ShowDialog(this) == DialogResult.OK) _programPath.Text = dlg.FileName;
        };

        Controls.AddRange(new Control[]
        {
            _playSound, _soundPath, _browseSound,
            _playBeep,
            _sendEmail, _emailSettingsBtn, _emailAddresses,
            _runProgram, _programPath, _browseProgram
        });

        FormKit.StyleInputs(_unit, _measure, _period, _adapter, _soundPath, _emailAddresses, _programPath);

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        Theme.StyleButton(ok, primary: true);
        Theme.StyleButton(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
        ok.Click += (_, _) => SaveSettings();
        FormKit.AddFooter(this, null, cancel, ok);

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
