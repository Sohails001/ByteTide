using System.Drawing.Drawing2D;

namespace ByteTide.App;

/// <summary>
/// A lightweight, dependency-free scrolling chart for live download and
/// upload speed, styled after the original app's black-background /
/// green-scale-text look. Pure GDI+, no charting library needed.
///
/// Supports the full View-menu option set: area vs. column style, optional
/// scale/time labels and gridlines (independently toggleable from the
/// gridlines themselves), average reference lines, and a text overlay
/// block (title / IP / max rate / average speeds).
/// </summary>
public sealed class GraphControl : Control
{
    private const int MaxSamples = 180; // 3 minutes of history at a 1s sampling interval

    private readonly List<(DateTime Time, double Down, double Up)> _samples = new();

    private static readonly double[] NiceSteps =
    {
        1024, 2048, 4096, 8192, 16384, 32768, 65536,
        131072, 262144, 524288, 1048576, 2097152, 4194304,
        8388608, 16777216, 33554432, 67108864, 134217728
    };

    // Shared, reused across every repaint instead of being allocated fresh each
    // time -- these colors/styles never change at runtime, so recreating them
    // every second (as the graph redraws live) was pure avoidable overhead.
    private static readonly Pen GridPen = new(Color.FromArgb(55, 55, 55)) { DashStyle = DashStyle.Dot };
    private static readonly Font AxisFont = new("Segoe UI", 7.5f);
    private static readonly Brush AxisTextBrush = new SolidBrush(Color.FromArgb(120, 220, 120));
    private static readonly Font OverlayFont = new("Segoe UI", 8f, FontStyle.Bold);
    private static readonly Brush OverlayTextBrush = new SolidBrush(Color.FromArgb(220, 220, 220));
    private static readonly Font OverlaySmallFont = new("Segoe UI", 7.5f);

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
        if (_samples.Count > MaxSamples) _samples.RemoveAt(0);
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
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        g.FillRectangle(Brushes.Black, rect);

        if (_samples.Count < 2)
        {
            DrawOverlayText(g, rect);
            return;
        }

        double maxVal = Math.Max(_samples.Max(s => s.Down), _samples.Max(s => s.Up));
        double scale = NiceCeiling(Math.Max(maxVal, 1024));

        var downs = _samples.Select(s => s.Down).ToList();
        var ups = _samples.Select(s => s.Up).ToList();

        // Chart drawn first...
        if (Options.ColumnStyle)
        {
            DrawColumns(g, rect, scale, downs, Color.FromArgb(240, 90, 160, 255));
            DrawColumns(g, rect, scale, ups, Color.FromArgb(220, 255, 70, 70));
        }
        else
        {
            DrawSeries(g, rect, scale, downs,
                lineColor: Color.FromArgb(255, 90, 160, 255),
                fillColor: Color.FromArgb(235, 30, 100, 220));
            DrawSeries(g, rect, scale, ups,
                lineColor: Color.FromArgb(255, 255, 70, 70),
                fillColor: Color.FromArgb(210, 220, 40, 40));
        }

        if (Options.ShowAverageDownloadLine)
            DrawAverageLine(g, rect, scale, downs.Average(), Color.FromArgb(200, 120, 180, 255));
        if (Options.ShowAverageUploadLine)
            DrawAverageLine(g, rect, scale, ups.Average(), Color.FromArgb(200, 255, 140, 140));

        // ...gridlines, axis labels, and overlay text drawn last, so they
        // always stay legible on top of the (now solid/opaque) chart fill
        // instead of getting painted over by it.
        if (Options.ShowScaleGrid || Options.ShowScale)
            DrawHorizontalGridLines(g, rect, scale);
        if (Options.ShowTimeGrid || Options.ShowTime)
            DrawVerticalTimeGridLines(g, rect);

        DrawOverlayText(g, rect);
    }

    /// <summary>Title / IP / max-rate / average-speed text block, top-left of the graph.</summary>
    private void DrawOverlayText(Graphics g, Rectangle rect)
    {
        if (!Options.ShowText) return;

        var font = OverlayFont;
        var brush = OverlayTextBrush;

        float y = 4;
        if (Options.ShowTitle)
        {
            var label = Options.ShowIpAddress && CurrentIpAddress.Length > 0
                ? $"{CurrentAdapterLabel}  [{CurrentIpAddress}]"
                : CurrentAdapterLabel;
            g.DrawString(label, font, brush, rect.Right - g.MeasureString(label, font).Width - 6, y);
            y += 16;
        }

        if (_samples.Count == 0) return;

        var smallFont = OverlaySmallFont;
        if (Options.ShowMaxRate)
        {
            double maxDown = _samples.Max(s => s.Down);
            double maxUp = _samples.Max(s => s.Up);
            var text = $"Max -- Down: {FormatRate(maxDown)}  Up: {FormatRate(maxUp)}";
            g.DrawString(text, smallFont, brush, rect.Right - g.MeasureString(text, smallFont).Width - 6, y);
            y += 14;
        }

        if (Options.ShowAverageSpeeds)
        {
            double avgDown = _samples.Average(s => s.Down);
            double avgUp = _samples.Average(s => s.Up);
            var text = $"Avg -- Down: {FormatRate(avgDown)}  Up: {FormatRate(avgUp)}";
            g.DrawString(text, smallFont, brush, rect.Right - g.MeasureString(text, smallFont).Width - 6, y);
        }
    }

    private void DrawHorizontalGridLines(Graphics g, Rectangle rect, double scale)
    {
        const int hLines = 4;
        for (int i = 0; i <= hLines; i++)
        {
            float y = rect.Height - rect.Height * i / (float)hLines;

            if (Options.ShowScaleGrid)
                g.DrawLine(GridPen, rect.Left, y, rect.Right, y);

            if (Options.ShowScale)
            {
                double val = scale * i / hLines;
                var label = FormatRate(val);
                var textY = Math.Max(0, y - 14);
                g.DrawString(label, AxisFont, AxisTextBrush, rect.Left + 3, textY);
            }
        }
    }

    private void DrawVerticalTimeGridLines(Graphics g, Rectangle rect)
    {
        int n = _samples.Count;
        if (n < 2) return;

        float xStep = rect.Width / (float)(MaxSamples - 1);
        float xOffset = (MaxSamples - n) * xStep;

        const int vLines = 3;
        for (int i = 1; i <= vLines; i++)
        {
            int sampleIndex = i * (n - 1) / (vLines + 1);
            if (sampleIndex <= 0 || sampleIndex >= n) continue;

            float x = xOffset + sampleIndex * xStep;

            if (Options.ShowTimeGrid)
                g.DrawLine(GridPen, x, rect.Top, x, rect.Bottom);

            if (Options.ShowTime)
            {
                var label = _samples[sampleIndex].Time.ToString("h:mm tt");
                var size = g.MeasureString(label, AxisFont);
                g.DrawString(label, AxisFont, AxisTextBrush, x - size.Width / 2, rect.Bottom - size.Height - 2);
            }
        }
    }

    private void DrawSeries(
        Graphics g, Rectangle rect, double scale, List<double> values,
        Color lineColor, Color? fillColor)
    {
        int n = values.Count;
        if (n < 2) return;

        var points = BuildPoints(rect, scale, values);

        if (fillColor is { } fc)
        {
            var poly = new List<PointF> { new(points[0].X, rect.Height) };
            poly.AddRange(points);
            poly.Add(new PointF(points[^1].X, rect.Height));

            using var fillBrush = new SolidBrush(fc);
            g.FillPolygon(fillBrush, poly.ToArray());
        }

        using var pen = new Pen(lineColor, 1.5f);
        g.DrawLines(pen, points);
    }

    private void DrawColumns(Graphics g, Rectangle rect, double scale, List<double> values, Color color)
    {
        int n = values.Count;
        if (n == 0) return;

        float xStep = rect.Width / (float)(MaxSamples - 1);
        float xOffset = (MaxSamples - n) * xStep;
        float barWidth = Math.Max(1f, xStep * 0.7f);

        using var brush = new SolidBrush(color);
        for (int i = 0; i < n; i++)
        {
            float x = xOffset + i * xStep - barWidth / 2;
            float height = (float)(values[i] / scale * rect.Height);
            height = Math.Max(0, Math.Min(rect.Height, height));
            g.FillRectangle(brush, x, rect.Height - height, barWidth, height);
        }
    }

    private void DrawAverageLine(Graphics g, Rectangle rect, double scale, double average, Color color)
    {
        float y = rect.Height - (float)(average / scale * rect.Height);
        y = Math.Max(0, Math.Min(rect.Height, y));

        using var pen = new Pen(color, 1f) { DashStyle = DashStyle.Dash };
        g.DrawLine(pen, rect.Left, y, rect.Right, y);
    }

    private PointF[] BuildPoints(Rectangle rect, double scale, List<double> values)
    {
        int n = values.Count;
        var points = new PointF[n];
        float xStep = rect.Width / (float)(MaxSamples - 1);
        float xOffset = (MaxSamples - n) * xStep;

        for (int i = 0; i < n; i++)
        {
            float x = xOffset + i * xStep;
            float y = rect.Height - (float)(values[i] / scale * rect.Height);
            y = Math.Max(0, Math.Min(rect.Height, y));
            points[i] = new PointF(x, y);
        }
        return points;
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
