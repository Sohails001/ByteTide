using ByteTide.Core;

namespace ByteTide.App;

public sealed class EmailSettingsForm : Form
{
    private readonly TextBox _host = new() { Width = 220 };
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Value = 587, Width = 80 };
    private readonly CheckBox _useSsl = new() { Text = "Use SSL/TLS", Checked = true };
    private readonly TextBox _username = new() { Width = 220 };
    private readonly TextBox _password = new() { Width = 220, UseSystemPasswordChar = true };
    private readonly TextBox _fromAddress = new() { Width = 220 };

    public EmailSettings Result { get; private set; }

    public EmailSettingsForm(EmailSettings current)
    {
        Result = current;

        FormKit.Setup(this, "Email Settings", 470, 440);

        Controls.Add(FormKit.Heading("Email Settings", 24, 18));
        Controls.Add(FormKit.Muted("Your mail server's details, used to send usage alerts. Use an app-specific password.", 24, 54, 422, 40));

        AddRow("SMTP server", _host, 106);
        AddRow("Port", _port, 148, 90);
        _useSsl.Location = new Point(170, 188);
        _useSsl.AutoSize = true;
        Controls.Add(_useSsl);
        AddRow("User name", _username, 226);
        AddRow("Password", _password, 268);
        AddRow("From address", _fromAddress, 310);

        FormKit.StyleInputs(_host, _username, _password, _fromAddress);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        Theme.StyleButton(ok, primary: true);
        Theme.StyleButton(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
        FormKit.AddFooter(this, null, cancel, ok);

        ok.Click += (_, _) =>
        {
            Result = new EmailSettings
            {
                SmtpHost = _host.Text,
                SmtpPort = (int)_port.Value,
                UseSsl = _useSsl.Checked,
                Username = _username.Text,
                Password = _password.Text,
                FromAddress = _fromAddress.Text
            };
        };

        _host.Text = current.SmtpHost;
        _port.Value = Math.Clamp(current.SmtpPort, 1, 65535);
        _useSsl.Checked = current.UseSsl;
        _username.Text = current.Username;
        _password.Text = current.Password;
        _fromAddress.Text = current.FromAddress;
    }

    private void AddRow(string caption, Control input, int y, int width = 276)
    {
        Controls.Add(FormKit.Caption(caption, 24, y + 4));
        FormKit.Place(input, 170, y, width);
        Controls.Add(input);
    }
}
