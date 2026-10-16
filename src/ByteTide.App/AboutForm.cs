using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace ByteTide.App;

/// <summary>Proper About window: big app name, version, credits, link.</summary>
public sealed class AboutForm : Form
{
    private const string RepoUrl = "https://github.com/Sohails001/ByteTide";

    public AboutForm()
    {
        Text = "About ByteTide";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 340);
        BackColor = Theme.Surface;
        Font = Theme.Ui();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;

        var logo = new LogoControl { Location = new Point(32, 32), Size = new Size(84, 84) };

        var title = new Label
        {
            Text = "ByteTide",
            Font = Theme.UiSemibold(32f),
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Location = new Point(132, 22)
        };

        var tagline = new Label
        {
            Text = "Network bandwidth monitor for Windows",
            Font = Theme.Ui(11f),
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Location = new Point(136, 78)
        };

        var version = new Label
        {
            Text = "Version " + GetVersion(),
            Font = Theme.Ui(9.5f),
            ForeColor = Theme.Accent,
            AutoSize = true,
            Location = new Point(136, 104)
        };

        var divider = new Panel { BackColor = Theme.Border, Location = new Point(32, 142), Size = new Size(496, 1) };

        var author = new Label
        {
            Text = "Created by Sohail Sher Ali",
            Font = Theme.Ui(10.5f, FontStyle.Bold),
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Location = new Point(32, 158)
        };

        var license = new Label
        {
            Text = "Free and open source, released under the MIT License.",
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Location = new Point(32, 184)
        };

        var link = new LinkLabel
        {
            Text = RepoUrl.Replace("https://", ""),
            AutoSize = true,
            LinkColor = Theme.Accent,
            ActiveLinkColor = Theme.AccentHover,
            Location = new Point(32, 208)
        };
        link.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true }); }
            catch { /* no default browser configured */ }
        };

        var credits = new Label
        {
            Text = "Inspired by RAS Graph & Stats by Charles DeWeese, and able to import its history. " +
                   "ByteTide is an independent project and is not affiliated with it.",
            ForeColor = Theme.TextMuted,
            Font = Theme.Ui(8.5f),
            AutoSize = false,
            Size = new Size(496, 40),
            Location = new Point(32, 240)
        };

        var close = new Button { Text = "Close", DialogResult = DialogResult.OK, Location = new Point(440, 292) };
        Theme.StyleButton(close, primary: true);

        Controls.AddRange(new Control[] { logo, title, tagline, version, divider, author, license, link, credits, close });
        AcceptButton = close;
        CancelButton = close;
    }

    private static string GetVersion()
    {
        var v = Assembly.GetEntryAssembly()?.GetName().Version ?? Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "1.0" : $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
    }

    /// <summary>The ByteTide mark: a blue rounded tile with two white waves.</summary>
    private sealed class LogoControl : Control
    {
        public LogoControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Theme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Surface);

            var tile = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedRect(tile, 18))
            using (var brush = new LinearGradientBrush(tile, Color.FromArgb(0, 140, 235), Color.FromArgb(0, 78, 160), 60f))
                g.FillPath(brush, path);

            using var pen = new Pen(Color.White, 4.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            DrawWave(g, pen, Width * 0.18f, Height * 0.40f, Width * 0.64f, Height * 0.11f);
            using var pen2 = new Pen(Color.FromArgb(210, 235, 255), 4.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            DrawWave(g, pen2, Width * 0.18f, Height * 0.66f, Width * 0.64f, Height * 0.11f);
        }

        private static void DrawWave(Graphics g, Pen pen, float x, float y, float width, float amplitude)
        {
            var pts = new PointF[25];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = i / (float)(pts.Length - 1);
                pts[i] = new PointF(x + t * width, y + (float)Math.Sin(t * Math.PI * 2) * amplitude * 1.4f);
            }
            g.DrawLines(pen, pts);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
