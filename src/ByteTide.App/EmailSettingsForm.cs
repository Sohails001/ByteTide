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

        Text = "Email Settings";
        Width = 380;
        Height = 300;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(12) };
        layout.Controls.Add(new Label { Text = "SMTP Host:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(_host);
        layout.Controls.Add(new Label { Text = "Port:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(_port);
        layout.Controls.Add(new Label { Text = "", AutoSize = true });
        layout.Controls.Add(_useSsl);
        layout.Controls.Add(new Label { Text = "Username:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(_username);
        layout.Controls.Add(new Label { Text = "Password:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(_password);
        layout.Controls.Add(new Label { Text = "From address:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        layout.Controls.Add(_fromAddress);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(12), FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;

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

        Controls.Add(buttons);
        Controls.Add(layout);

        _host.Text = current.SmtpHost;
        _port.Value = Math.Clamp(current.SmtpPort, 1, 65535);
        _useSsl.Checked = current.UseSsl;
        _username.Text = current.Username;
        _password.Text = current.Password;
        _fromAddress.Text = current.FromAddress;
    }
}
