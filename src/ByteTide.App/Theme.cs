using System.Drawing.Drawing2D;

namespace ByteTide.App;

/// <summary>Shared colours and helpers so every window has the same modern, flat look.</summary>
internal static class Theme
{
    public static readonly Color Accent = Color.FromArgb(0, 120, 212);
    public static readonly Color AccentHover = Color.FromArgb(0, 103, 184);
    public static readonly Color Danger = Color.FromArgb(196, 43, 28);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(247, 248, 250);
    public static readonly Color Border = Color.FromArgb(220, 223, 228);
    public static readonly Color TextPrimary = Color.FromArgb(32, 33, 36);
    public static readonly Color TextMuted = Color.FromArgb(96, 100, 108);
    public static readonly Color RowAlt = Color.FromArgb(246, 248, 251);
    public static readonly Color Selection = Color.FromArgb(222, 236, 249);

    public static readonly Color DarkSurface = Color.FromArgb(24, 24, 28);
    public static readonly Color DarkMenu = Color.FromArgb(32, 32, 38);
    public static readonly Color DarkHover = Color.FromArgb(62, 62, 72);
    public static readonly Color DarkBorder = Color.FromArgb(70, 70, 80);
    public static readonly Color DarkText = Color.FromArgb(235, 235, 240);

    /// <summary>True when the main window is currently using the dark scheme.</summary>
    public static bool IsDark { get; private set; } = true;

    public static MenuPalette Menu => IsDark ? MenuPalette.Dark : MenuPalette.Light;

    public static ToolStripProfessionalRenderer CreateMenuRenderer() => new ModernMenuRenderer(Menu);

    /// <summary>Resolves a theme setting to dark/light and remembers it for menus created elsewhere (the tray).</summary>
    public static bool Resolve(ThemeMode mode)
    {
        bool dark = mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => WindowsPrefersDark()
        };
        IsDark = dark;
        return dark;
    }

    private static bool WindowsPrefersDark()
    {
        try
        {
            var value = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int light && light == 0;
        }
        catch
        {
            return true;
        }
    }

    public static Font Ui(float size = 9f, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);

    public static Font UiSemibold(float size) => new("Segoe UI Semibold", size);

    /// <summary>Applies the shared look to a dialog: font, background and flat buttons.</summary>
    public static void ApplyDialog(Form form)
    {
        form.Font = Ui();
        form.BackColor = Surface;

        var stack = new Stack<Control>();
        foreach (Control c in form.Controls) stack.Push(c);

        while (stack.Count > 0)
        {
            var c = stack.Pop();

            if (c is Button b)
                StyleButton(b, primary: ReferenceEquals(form.AcceptButton, b));
            else if (c is Panel or FlowLayoutPanel or TableLayoutPanel or GroupBox or TabPage)
            {
                if (c.BackColor == SystemColors.Control) c.BackColor = Surface;
            }

            foreach (Control child in c.Controls) stack.Push(child);
        }
    }

    public static void StyleButton(Button b, bool primary = false, bool danger = false)
    {
        var fill = danger ? Danger : primary ? Accent : Color.White;

        b.FlatStyle = FlatStyle.Flat;
        b.UseVisualStyleBackColor = false;
        b.BackColor = fill;
        b.ForeColor = (primary || danger) ? Color.White : TextPrimary;
        b.FlatAppearance.BorderSize = (primary || danger) ? 0 : 1;
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.MouseOverBackColor = danger ? Color.FromArgb(170, 35, 22) : primary ? AccentHover : Color.FromArgb(240, 242, 245);
        b.FlatAppearance.MouseDownBackColor = danger ? Color.FromArgb(150, 30, 20) : primary ? Color.FromArgb(0, 90, 160) : Color.FromArgb(228, 231, 236);
        b.Cursor = Cursors.Hand;
        b.AutoSize = true;
        b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        b.MinimumSize = new Size(88, 32);
        b.Padding = new Padding(12, 3, 12, 3);
    }

    public static void StyleGrid(DataGridView g)
    {
        g.BorderStyle = BorderStyle.None;
        g.BackgroundColor = Surface;
        g.GridColor = Color.FromArgb(234, 236, 240);
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        g.EnableHeadersVisualStyles = false;
        g.RowHeadersVisible = false;
        g.AllowUserToResizeRows = false;
        g.AllowUserToResizeColumns = false;
        g.MultiSelect = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = 38;
        g.RowTemplate.Height = 32;
        g.Font = Ui(9.5f);

        var header = g.ColumnHeadersDefaultCellStyle;
        header.BackColor = SurfaceAlt;
        header.ForeColor = TextMuted;
        header.Font = Ui(9f, FontStyle.Bold);
        header.SelectionBackColor = SurfaceAlt;
        header.SelectionForeColor = TextMuted;
        header.Padding = new Padding(10, 0, 10, 0);

        var cell = g.DefaultCellStyle;
        cell.BackColor = Surface;
        cell.ForeColor = TextPrimary;
        cell.SelectionBackColor = Selection;
        cell.SelectionForeColor = TextPrimary;
        cell.Padding = new Padding(10, 0, 10, 0);

        g.AlternatingRowsDefaultCellStyle.BackColor = RowAlt;
        g.AlternatingRowsDefaultCellStyle.SelectionBackColor = Selection;
        g.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextPrimary;
    }
}

/// <summary>Colours for menus, drop-downs and the status bar.</summary>
internal sealed class MenuPalette
{
    public Color Bar, Dropdown, Hover, Border, Text, DisabledText, Arrow;

    public static readonly MenuPalette Dark = new()
    {
        Bar = Color.FromArgb(24, 24, 28), Dropdown = Color.FromArgb(32, 32, 38), Hover = Color.FromArgb(62, 62, 72),
        Border = Color.FromArgb(70, 70, 80), Text = Color.FromArgb(235, 235, 240),
        DisabledText = Color.FromArgb(130, 130, 140), Arrow = Color.FromArgb(200, 200, 208)
    };

    public static readonly MenuPalette Light = new()
    {
        Bar = Color.FromArgb(243, 244, 246), Dropdown = Color.White, Hover = Color.FromArgb(229, 239, 250),
        Border = Color.FromArgb(218, 221, 226), Text = Color.FromArgb(32, 33, 36),
        DisabledText = Color.FromArgb(150, 153, 160), Arrow = Color.FromArgb(90, 94, 102)
    };
}

/// <summary>Colours for the live graph.</summary>
internal sealed class GraphPalette
{
    public Color Background, Grid, AxisText, OverlayText, DownFill, DownLine, UpFill, UpLine;

    public static readonly GraphPalette Dark = new()
    {
        Background = Color.Black, Grid = Color.FromArgb(58, 58, 62), AxisText = Color.FromArgb(120, 220, 120),
        OverlayText = Color.FromArgb(225, 225, 230),
        DownFill = Color.FromArgb(28, 92, 205), DownLine = Color.FromArgb(110, 175, 255),
        UpFill = Color.FromArgb(190, 35, 35), UpLine = Color.FromArgb(255, 105, 105)
    };

    public static readonly GraphPalette Light = new()
    {
        Background = Color.White, Grid = Color.FromArgb(208, 212, 218), AxisText = Color.FromArgb(30, 120, 60),
        OverlayText = Color.FromArgb(40, 42, 48),
        DownFill = Color.FromArgb(120, 170, 245), DownLine = Color.FromArgb(25, 90, 205),
        UpFill = Color.FromArgb(245, 150, 140), UpLine = Color.FromArgb(185, 35, 25)
    };
}

/// <summary>Flat menu / status-bar renderer in either the dark or the light palette.</summary>
internal sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly MenuPalette _p;

    public ModernMenuRenderer(MenuPalette palette) : base(new Colors(palette))
    {
        _p = palette;
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _p.Text : _p.DisabledText;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = _p.Arrow;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        var r = e.ImageRectangle;

        using (var back = new SolidBrush(Theme.Accent))
            g.FillRectangle(back, r);

        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pen = new Pen(Color.White, 1.8f))
        {
            g.DrawLines(pen, new[]
            {
                new PointF(r.Left + r.Width * 0.24f, r.Top + r.Height * 0.52f),
                new PointF(r.Left + r.Width * 0.43f, r.Top + r.Height * 0.72f),
                new PointF(r.Left + r.Width * 0.77f, r.Top + r.Height * 0.30f)
            });
        }
        g.SmoothingMode = old;
    }

    private sealed class Colors : ProfessionalColorTable
    {
        private readonly MenuPalette _c;

        public Colors(MenuPalette c)
        {
            _c = c;
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground => _c.Dropdown;
        public override Color ImageMarginGradientBegin => _c.Dropdown;
        public override Color ImageMarginGradientMiddle => _c.Dropdown;
        public override Color ImageMarginGradientEnd => _c.Dropdown;
        public override Color MenuBorder => _c.Border;
        public override Color MenuItemBorder => _c.Hover;
        public override Color MenuItemSelected => _c.Hover;
        public override Color MenuItemSelectedGradientBegin => _c.Hover;
        public override Color MenuItemSelectedGradientEnd => _c.Hover;
        public override Color MenuItemPressedGradientBegin => _c.Hover;
        public override Color MenuItemPressedGradientMiddle => _c.Hover;
        public override Color MenuItemPressedGradientEnd => _c.Hover;
        public override Color MenuStripGradientBegin => _c.Bar;
        public override Color MenuStripGradientEnd => _c.Bar;
        public override Color SeparatorDark => _c.Border;
        public override Color SeparatorLight => _c.Border;
        public override Color ToolStripBorder => _c.Bar;
        public override Color StatusStripGradientBegin => _c.Bar;
        public override Color StatusStripGradientEnd => _c.Bar;
        public override Color CheckBackground => Theme.Accent;
        public override Color CheckSelectedBackground => Theme.Accent;
        public override Color CheckPressedBackground => Theme.Accent;
    }
}
