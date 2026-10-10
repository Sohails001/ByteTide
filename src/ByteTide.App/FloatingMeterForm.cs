using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ByteTide.App;

/// <summary>
/// Small always-on-top window showing live download/upload speed as text plus
/// a mini graph. Drag it anywhere (it remembers where); right-click to close.
/// It never ignores the mouse, so it can't trap you the way click-through can.
/// </summary>
public sealed class FloatingMeterForm : Form
{
    private const int MaxPoints = 60;
    private const int MeterWidth = 190;
    private const int MeterHeight = 44;

    private static readonly Color DownColor = Color.FromArgb(90, 160, 255);
    private static readonly Color UpColor = Color.FromArgb(255, 100, 100);
    private static readonly Font TextFont = new("Segoe UI", 8.5f, FontStyle.Bold);

    private readonly List<(double Down, double Up)> _history = new();
    private double _down;
    private double _up;

    private bool _dragging;
    private bool _dragMoved;
    private Point _dragCursorStart;
    private Point _dragFormStart;

    /// <summary>Show speeds as bits instead of bytes.</summary>
    public bool UseBits { get; set; }

    public event Action? CloseRequested;
    public event Action<Point>? Moved;

    public FloatingMeterForm(Point? location)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(MeterWidth, MeterHeight);
        BackColor = Color.FromArgb(18, 18, 22);
        DoubleBuffered = true;
        Text = "ByteTide meter";

        if (location is { } p)
        {
            Location = p;
        }
        else
        {
            var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
            Location = new Point(wa.Right - MeterWidth - 12, wa.Bottom - MeterHeight - 12);
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("Close mini meter", null, (_, _) => CloseRequested?.Invoke());
        ContextMenuStrip = menu;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW: keep it out of Alt+Tab
            return cp;
        }
    }

    public void Push(double down, double up)
    {
        _down = down;
        _up = up;
        _history.Add((down, up));
        if (_history.Count > MaxPoints) _history.RemoveAt(0);
        Invalidate();
    }

    // ----- Dragging --------------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        _dragMoved = false;
        _dragCursorStart = Cursor.Position;
        _dragFormStart = Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        var now = Cursor.Position;
        var dx = now.X - _dragCursorStart.X;
        var dy = now.Y - _dragCursorStart.Y;
        if (dx != 0 || dy != 0) _dragMoved = true;
        Location = new Point(_dragFormStart.X + dx, _dragFormStart.Y + dy);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging) return;
        _dragging = false;
        if (_dragMoved) Moved?.Invoke(Location);
    }

    // ----- Painting --------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (var border = new Pen(Color.FromArgb(75, 75, 88)))
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        using (var downBrush = new SolidBrush(DownColor))
            g.DrawString("D: " + SpeedFormat.Full(_down, UseBits), TextFont, downBrush, 6, 5);
        using (var upBrush = new SolidBrush(UpColor))
            g.DrawString("U: " + SpeedFormat.Full(_up, UseBits), TextFont, upBrush, 6, 22);

        var graphRect = new Rectangle(Width - 74, 5, 68, Height - 10);
        using (var graphBack = new SolidBrush(Color.FromArgb(8, 8, 10)))
            g.FillRectangle(graphBack, graphRect);

        if (_history.Count >= 2)
        {
            double max = 1024;
            foreach (var s in _history)
            {
                if (s.Down > max) max = s.Down;
                if (s.Up > max) max = s.Up;
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawSeries(g, graphRect, true, max, DownColor);
            DrawSeries(g, graphRect, false, max, UpColor);
        }
    }

    private void DrawSeries(Graphics g, Rectangle r, bool down, double max, Color color)
    {
        int n = _history.Count;
        float step = r.Width / (float)(MaxPoints - 1);
        float x0 = r.Right - (n - 1) * step;

        var points = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            double value = down ? _history[i].Down : _history[i].Up;
            float y = r.Bottom - (float)(value / max * r.Height);
            points[i] = new PointF(x0 + i * step, Math.Max(r.Top, Math.Min(r.Bottom, y)));
        }

        var area = new PointF[n + 2];
        area[0] = new PointF(points[0].X, r.Bottom);
        Array.Copy(points, 0, area, 1, n);
        area[n + 1] = new PointF(points[n - 1].X, r.Bottom);

        using (var fill = new SolidBrush(Color.FromArgb(70, color)))
            g.FillPolygon(fill, area);
        using (var pen = new Pen(color, 1.2f))
            g.DrawLines(pen, points);
    }
}
