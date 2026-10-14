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

/// <summary>Dark, flat menu and status-bar renderer matching the black graph window.</summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColors())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.DarkText : Color.FromArgb(130, 130, 140);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Color.FromArgb(200, 200, 208);
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        var r = e.ImageRectangle;

        using (var back = new SolidBrush(Color.FromArgb(0, 95, 175)))
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

    private sealed class DarkColors : ProfessionalColorTable
    {
        public DarkColors() { UseSystemColors = false; }

        public override Color ToolStripDropDownBackground => Theme.DarkMenu;
        public override Color ImageMarginGradientBegin => Theme.DarkMenu;
        public override Color ImageMarginGradientMiddle => Theme.DarkMenu;
        public override Color ImageMarginGradientEnd => Theme.DarkMenu;
        public override Color MenuBorder => Theme.DarkBorder;
        public override Color MenuItemBorder => Theme.DarkHover;
        public override Color MenuItemSelected => Theme.DarkHover;
        public override Color MenuItemSelectedGradientBegin => Theme.DarkHover;
        public override Color MenuItemSelectedGradientEnd => Theme.DarkHover;
        public override Color MenuItemPressedGradientBegin => Theme.DarkHover;
        public override Color MenuItemPressedGradientMiddle => Theme.DarkHover;
        public override Color MenuItemPressedGradientEnd => Theme.DarkHover;
        public override Color MenuStripGradientBegin => Theme.DarkSurface;
        public override Color MenuStripGradientEnd => Theme.DarkSurface;
        public override Color SeparatorDark => Theme.DarkBorder;
        public override Color SeparatorLight => Theme.DarkBorder;
        public override Color ToolStripBorder => Theme.DarkSurface;
        public override Color StatusStripGradientBegin => Color.FromArgb(20, 20, 24);
        public override Color StatusStripGradientEnd => Color.FromArgb(20, 20, 24);
        public override Color CheckBackground => Color.FromArgb(0, 95, 175);
        public override Color CheckSelectedBackground => Color.FromArgb(0, 95, 175);
        public override Color CheckPressedBackground => Color.FromArgb(0, 95, 175);
    }
}
