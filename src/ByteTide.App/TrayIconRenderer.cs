using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ByteTide.App;

/// <summary>Draws the three tray icon styles.</summary>
internal static class TrayIconRenderer
{
    private static readonly Color DownColor = Color.FromArgb(90, 160, 255);
    private static readonly Color UpColor = Color.FromArgb(255, 100, 100);

    /// <summary>Size Windows wants for a tray icon at the current DPI (16 at 100%).</summary>
    public static int IconSize => Math.Max(16, SystemInformation.SmallIconSize.Width);

    // ----- Style 1: mini graph -------------------------------------------

    public static Icon RenderGraph(IReadOnlyList<(double Down, double Up)> history)
    {
        int size = IconSize;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);

            if (history.Count >= 2)
            {
                double max = 1024;
                foreach (var s in history)
                {
                    if (s.Down > max) max = s.Down;
                    if (s.Up > max) max = s.Up;
                }

                DrawSeries(g, size, history, true, max, DownColor);
                DrawSeries(g, size, history, false, max, UpColor);
            }
        }
        return ToIcon(bmp);
    }

    private static void DrawSeries(Graphics g, int size, IReadOnlyList<(double Down, double Up)> history, bool down, double max, Color color)
    {
        int n = history.Count;
        const int slots = 16;

        using var pen = new Pen(color, Math.Max(1.2f, size / 11f));
        float xStep = size / (float)(slots - 1);
        float xOffset = (slots - n) * xStep;

        var points = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            double value = down ? history[i].Down : history[i].Up;
            float x = xOffset + i * xStep;
            float y = size - (float)(value / max * size);
            points[i] = new PointF(x, Math.Max(0, Math.Min(size, y)));
        }
        g.DrawLines(pen, points);
    }

    // ----- Style 2: speed numbers ----------------------------------------

    public static Icon RenderSpeedNumbers(double downBytesPerSec, double upBytesPerSec, bool bits)
    {
        int size = IconSize;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

            using var font = new Font("Segoe UI", size * 0.41f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center
            };
            using var downBrush = new SolidBrush(DownColor);
            using var upBrush = new SolidBrush(UpColor);

            float half = size / 2f;
            g.DrawString(SpeedFormat.Compact(downBytesPerSec, bits), font, downBrush, new RectangleF(0, 0, size, half), format);
            g.DrawString(SpeedFormat.Compact(upBytesPerSec, bits), font, upBrush, new RectangleF(0, half, size, half), format);
        }
        return ToIcon(bmp);
    }

    // ----- Style 3: classic dial-up monitors -----------------------------

    public static (Color Download, Color Upload) PaletteColors(TrayOptions o)
    {
        return o.Palette switch
        {
            DialupPalette.GreenYellow => (Color.FromArgb(0, 230, 0), Color.FromArgb(255, 220, 0)),
            DialupPalette.Green => (Color.FromArgb(0, 230, 0), Color.FromArgb(0, 230, 0)),
            DialupPalette.Yellow => (Color.FromArgb(255, 220, 0), Color.FromArgb(255, 220, 0)),
            DialupPalette.Blue => (Color.FromArgb(70, 140, 255), Color.FromArgb(70, 140, 255)),
            DialupPalette.XpBlue => (Color.FromArgb(60, 120, 230), Color.FromArgb(150, 205, 255)),
            DialupPalette.ZoneAlarm => (Color.FromArgb(0, 200, 0), Color.FromArgb(230, 40, 40)),
            _ => (Color.FromArgb(o.CustomDownloadArgb), Color.FromArgb(o.CustomUploadArgb))
        };
    }

    /// <summary>
    /// Two overlapping computer monitors. The back (upper-left) screen lights
    /// up while downloading, the front (lower-right) screen while uploading.
    /// </summary>
    public static Icon RenderDialup(bool receiving, bool sending, Color downColor, Color upColor)
    {
        int size = IconSize;
        float k = size / 16f;

        var body = Color.FromArgb(125, 130, 145);
        var idleScreen = Color.FromArgb(25, 30, 45);

        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            // Back monitor (receive)
            Rect(g, body, k, 0, 0, 10, 8);
            Rect(g, receiving ? downColor : idleScreen, k, 1, 1, 8, 5);
            Rect(g, body, k, 4, 8, 2, 1);
            Rect(g, body, k, 3, 9, 4, 1);

            // Front monitor (send)
            Rect(g, body, k, 6, 6, 10, 8);
            Rect(g, sending ? upColor : idleScreen, k, 7, 7, 8, 5);
            Rect(g, body, k, 10, 14, 2, 1);
            Rect(g, body, k, 9, 15, 4, 1);
        }
        return ToIcon(bmp);
    }

    private static void Rect(Graphics g, Color c, float k, float x, float y, float w, float h)
    {
        using var brush = new SolidBrush(c);
        g.FillRectangle(brush, x * k, y * k, w * k, h * k);
    }

    // ----- Shared ---------------------------------------------------------

    private static Icon ToIcon(Bitmap bmp)
    {
        var hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone(); // independent copy, safe after DestroyIcon below
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
