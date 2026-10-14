using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ByteTide.App;

/// <summary>
/// Live download/upload graph, drawn the way classic traffic monitors do it:
/// one sample per second, a fixed number of whole pixels per sample, no
/// anti-aliasing, so every edge is razor sharp. Gridlines sit on round clock
/// times and scroll with the data. The visible history grows with the window.
/// Pure GDI+, no charting library.
/// </summary>
public sealed class GraphControl : Control
{
    private const int MaxHistory = 7200; // two hours at one sample per second

    private readonly List<(DateTime Time, double Down, double Up)> _samples = new();

    private static readonly double[] NiceSteps =
    {
        1024, 2048, 4096, 8192, 16384, 32768, 65536,
        131072, 262144, 524288, 1048576, 2097152, 4194304,
        8388608, 16777216, 33554432, 67108864, 134217728,
        268435456, 536870912, 1073741824
    };

    private static readonly int[] TimeGridSeconds = { 10, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200 };

    private static readonly Color DownFill = Color.FromArgb(28, 92, 205);
    private static readonly Color DownLine = Color.FromArgb(110, 175, 255);
    private static readonly Color UpFill = Color.FromArgb(190, 35, 35);
    private static readonly Color UpLine = Color.FromArgb(255, 105, 105);

    // Created once and reused on every repaint.
    private static readonly Pen GridPen = new(Color.FromArgb(58, 58, 62)) { DashStyle = DashStyle.Dot };
    private static readonly Font AxisFont = new("Segoe UI", 8f);
    private static readonly Brush AxisTextBrush = new SolidBrush(Color.FromArgb(120, 220, 120));
    private static readonly Font OverlayFont = new("Segoe UI Semibold", 9f);
    private static readonly Brush OverlayTextBrush = new SolidBrush(Color.FromArgb(225, 225, 230));
    private static readonly Font OverlaySmallFont = new("Segoe UI", 8f);

    public GraphOptions Options { get; set; } = new();

    /// <summary>Adapter description or "All Connections", shown when ShowTitle is on.</summary>
    public string CurrentAdapterLabel { get; set; } = "All Connections";

    /// <summary>Shown next to the title when ShowIpAddress is on.</summary>
    public string CurrentIpAddress { get; set; } = "";

    public GraphControl()
    {
        DoubleBuffered = true;
        BackColor = Color.Black;
        ResizeRedraw = true;
    }

    public void AddSample(double downBytesPerSec, double upBytesPerSec)
    {
        _samples.Add((DateTime.Now, downBytesPerSec, upBytesPerSec));
        if (_samples.Count > MaxHistory) _samples.RemoveRange(0, _samples.Count - MaxHistory);
        Invalidate();
    }

    public void Clear()
    {
        _samples.Clear();
        Invalidate();
    }

    public void RefreshOptions() => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;

        // No smoothing anywhere: every line and edge lands exactly on a pixel.
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.None;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        int w = ClientSize.Width;
        int h = ClientSize.Height;
        g.Clear(Color.Black);

        if (w < 8 || h < 8) return;

        int pps = Math.Max(1, Math.Min(8, Options.PixelsPerSecond));
        int count = _samples.Count;

        if (count < 2)
        {
            DrawOverlayText(g, w, 0, 0);
            return;
        }

        int visible = Math.Min(count, Math.Max(2, w / pps + 1));
        int start = count - visible;

        double maxVal = 1024;
        for (int i = start; i < count; i++)
        {
            if (_samples[i].Down > maxVal) maxVal = _samples[i].Down;
            if (_samples[i].Up > maxVal) maxVal = _samples[i].Up;
        }
        double scale = NiceCeiling(maxVal);

        // Chart first, then grid/labels/text on top so they stay readable.
        switch (Options.Style)
        {
            case GraphStyle.Column:
                DrawColumns(g, w, h, pps, start, scale, true, DownFill);
                DrawColumns(g, w, h, pps, start, scale, false, UpFill);
                break;
            case GraphStyle.Line:
                DrawStepLine(g, w, h, pps, start, scale, true, DownLine);
                DrawStepLine(g, w, h, pps, start, scale, false, UpLine);
                break;
            default:
                DrawArea(g, w, h, pps, start, scale, true, DownFill, DownLine);
                DrawArea(g, w, h, pps, start, scale, false, UpFill, UpLine);
                break;
        }

        double sumDown = 0, sumUp = 0, maxDown = 0, maxUp = 0;
        for (int i = start; i < count; i++)
        {
            sumDown += _samples[i].Down;
            sumUp += _samples[i].Up;
            if (_samples[i].Down > maxDown) maxDown = _samples[i].Down;
            if (_samples[i].Up > maxUp) maxUp = _samples[i].Up;
        }

        if (Options.ShowAverageDownloadLine)
            DrawAverageLine(g, w, h, scale, sumDown / visible, DownLine);
        if (Options.ShowAverageUploadLine)
            DrawAverageLine(g, w, h, scale, sumUp / visible, UpLine);

        if (Options.ShowScaleGrid || Options.ShowScale)
            DrawHorizontalGrid(g, w, h, scale);
        if (Options.ShowTimeGrid || Options.ShowTime)
            DrawTimeGrid(g, w, h, pps, start);

        DrawOverlayText(g, w, maxDown, maxUp, sumDown / visible, sumUp / visible);
    }

    // x of the left edge of sample i; the newest sample sits flush against the right edge.
    private int SampleX(int w, int pps, int i) => w - (_samples.Count - i) * pps;

    private int ValueToHeight(double value, double scale, int h)
    {
        int ht = (int)Math.Round(value / scale * (h - 1));
        return Math.Max(0, Math.Min(h - 1, ht));
    }

    private double ValueOf(int i, bool down) => down ? _samples[i].Down : _samples[i].Up;

    private void DrawArea(Graphics g, int w, int h, int pps, int start, double scale, bool down, Color fill, Color line)
    {
        using var fillBrush = new SolidBrush(fill);
        using var linePen = new Pen(line, 1f);

        var outline = new List<Point>((_samples.Count - start) * 2);

        for (int i = start; i < _samples.Count; i++)
        {
            int x = SampleX(w, pps, i);
            int ht = ValueToHeight(ValueOf(i, down), scale, h);
            if (ht > 0) g.FillRectangle(fillBrush, x, h - ht, pps, ht);

            int y = h - 1 - ht;
            outline.Add(new Point(x, y));
            outline.Add(new Point(x + pps - 1, y));
        }

        if (outline.Count >= 2) g.DrawLines(linePen, outline.ToArray());
    }

    private void DrawStepLine(Graphics g, int w, int h, int pps, int start, double scale, bool down, Color line)
    {
        using var linePen = new Pen(line, 1f);
        var pts = new List<Point>((_samples.Count - start) * 2);

        for (int i = start; i < _samples.Count; i++)
        {
            int x = SampleX(w, pps, i);
            int y = h - 1 - ValueToHeight(ValueOf(i, down), scale, h);
            pts.Add(new Point(x, y));
            pts.Add(new Point(x + pps - 1, y));
        }

        if (pts.Count >= 2) g.DrawLines(linePen, pts.ToArray());
    }

    private void DrawColumns(Graphics g, int w, int h, int pps, int start, double scale, bool down, Color fill)
    {
        using var brush = new SolidBrush(fill);
        int barWidth = pps >= 3 ? pps - 1 : pps; // leave a 1px gap only when bars are wide enough

        for (int i = start; i < _samples.Count; i++)
        {
            int ht = ValueToHeight(ValueOf(i, down), scale, h);
            if (ht <= 0) continue;
            g.FillRectangle(brush, SampleX(w, pps, i), h - ht, barWidth, ht);
        }
    }

    private void DrawAverageLine(Graphics g, int w, int h, double scale, double average, Color color)
    {
        int y = h - 1 - ValueToHeight(average, scale, h);
        using var pen = new Pen(color, 1f) { DashStyle = DashStyle.Dash };
        g.DrawLine(pen, 0, y, w, y);
    }

    private void DrawHorizontalGrid(Graphics g, int w, int h, double scale)
    {
        const int divisions = 4;
        for (int i = 1; i <= divisions; i++)
        {
            int y = h - 1 - (h - 1) * i / divisions;

            if (Options.ShowScaleGrid)
                g.DrawLine(GridPen, 0, y, w, y);

            if (Options.ShowScale)
            {
                var label = FormatRate(scale * i / divisions);
                g.DrawString(label, AxisFont, AxisTextBrush, 3, Math.Max(0, y - 1));
            }
        }
    }

    /// <summary>
    /// Vertical gridlines on round clock times (e.g. every minute or five
    /// minutes depending on how much history fits), so they scroll with the
    /// data instead of jumping around.
    /// </summary>
    private void DrawTimeGrid(Graphics g, int w, int h, int pps, int start)
    {
        // Pick the smallest interval that keeps lines at least ~96 px apart.
        int step = TimeGridSeconds[^1];
        foreach (var s in TimeGridSeconds)
        {
            if (s * pps >= 96) { step = s; break; }
        }

        long ticksPerStep = step * TimeSpan.TicksPerSecond;
        string format = step < 60 ? "h:mm:ss tt" : "h:mm tt";

        for (int i = Math.Max(1, start); i < _samples.Count; i++)
        {
            long bucket = _samples[i].Time.Ticks / ticksPerStep;
            long previous = _samples[i - 1].Time.Ticks / ticksPerStep;
            if (bucket == previous) continue;

            int x = SampleX(w, pps, i);
            if (x < 0 || x >= w) continue;

            if (Options.ShowTimeGrid)
                g.DrawLine(GridPen, x, 0, x, h);

            if (Options.ShowTime)
            {
                var label = new DateTime(bucket * ticksPerStep).ToString(format);
                var size = g.MeasureString(label, AxisFont);
                float lx = Math.Min(w - size.Width - 2, x + 3);
                g.DrawString(label, AxisFont, AxisTextBrush, Math.Max(2, lx), h - size.Height - 1);
            }
        }
    }

    /// <summary>Title / IP / max-rate / average-speed text block, top-right of the graph.</summary>
    private void DrawOverlayText(Graphics g, int w, double maxDown, double maxUp, double avgDown = 0, double avgUp = 0)
    {
        if (!Options.ShowText) return;

        float y = 4;
        if (Options.ShowTitle)
        {
            var label = Options.ShowIpAddress && CurrentIpAddress.Length > 0
                ? $"{CurrentAdapterLabel}  [{CurrentIpAddress}]"
                : CurrentAdapterLabel;
            g.DrawString(label, OverlayFont, OverlayTextBrush, w - g.MeasureString(label, OverlayFont).Width - 6, y);
            y += 18;
        }

        if (_samples.Count == 0) return;

        if (Options.ShowMaxRate)
        {
            var text = $"Max  Down {FormatRate(maxDown)}   Up {FormatRate(maxUp)}";
            g.DrawString(text, OverlaySmallFont, OverlayTextBrush, w - g.MeasureString(text, OverlaySmallFont).Width - 6, y);
            y += 15;
        }

        if (Options.ShowAverageSpeeds)
        {
            var text = $"Avg  Down {FormatRate(avgDown)}   Up {FormatRate(avgUp)}";
            g.DrawString(text, OverlaySmallFont, OverlayTextBrush, w - g.MeasureString(text, OverlaySmallFont).Width - 6, y);
        }
    }

    private static double NiceCeiling(double value)
    {
        foreach (var step in NiceSteps)
            if (value <= step) return step;
        return value * 1.2;
    }

    private string FormatRate(double bytesPerSec)
    {
        if (Options.UseKbps)
        {
            double kbps = bytesPerSec * 8 / 1000;
            return kbps >= 1000 ? $"{kbps / 1000:0.00} Mbps" : $"{kbps:0} kbps";
        }

        if (bytesPerSec >= 1024 * 1024) return $"{bytesPerSec / (1024 * 1024):0.00} MB/s";
        if (bytesPerSec >= 1024) return $"{bytesPerSec / 1024:0} KB/s";
        return $"{bytesPerSec:0} B/s";
    }
}
