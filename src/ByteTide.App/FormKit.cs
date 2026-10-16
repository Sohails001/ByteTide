namespace ByteTide.App;

/// <summary>
/// Small helpers that give every dialog the same fixed, DPI-aware layout and
/// footer bar. Controls are placed at exact positions inside a fixed-size
/// window, so nothing can be clipped or overlap whatever the font or screen scale.
/// </summary>
internal static class FormKit
{
    public static void Setup(Form form, string title, int width, int height)
    {
        // Scale every position and size together with the screen's DPI setting.
        form.AutoScaleDimensions = new SizeF(96f, 96f);
        form.AutoScaleMode = AutoScaleMode.Dpi;

        form.Text = title;
        form.Font = Theme.Ui();
        form.BackColor = Theme.Surface;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.CenterParent;
        form.ClientSize = new Size(width, height);
    }

    public static Label Heading(string text, int x, int y) => new()
    {
        Text = text,
        Font = Theme.UiSemibold(15f),
        ForeColor = Theme.TextPrimary,
        AutoSize = true,
        Location = new Point(x, y)
    };

    public static Label Muted(string text, int x, int y, int width, int height) => new()
    {
        Text = text,
        ForeColor = Theme.TextMuted,
        AutoSize = false,
        Size = new Size(width, height),
        Location = new Point(x, y)
    };

    public static Label Caption(string text, int x, int y) => new()
    {
        Text = text,
        ForeColor = Theme.TextPrimary,
        AutoSize = true,
        Location = new Point(x, y)
    };

    public static void Place(Control control, int x, int y, int width)
    {
        control.Location = new Point(x, y);
        control.Width = width;
    }

    public static void StyleInputs(params Control[] controls)
    {
        foreach (var c in controls)
        {
            if (c is TextBox tb) tb.BorderStyle = BorderStyle.FixedSingle;
            if (c is ComboBox cb) cb.FlatStyle = FlatStyle.Flat;
        }
    }

    /// <summary>Adds a bottom bar. Buttons are listed rightmost first; "left" is an optional status label or checkbox.</summary>
    public static void AddFooter(Form form, Control? left, params Button[] rightmostFirst)
    {
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = Theme.SurfaceAlt };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 14, 14, 0),
            BackColor = Theme.SurfaceAlt
        };
        foreach (var b in rightmostFirst) buttons.Controls.Add(b);
        footer.Controls.Add(buttons);

        if (left is not null)
        {
            left.Location = new Point(20, 21);
            footer.Controls.Add(left);
        }

        form.Controls.Add(footer);
    }
}
