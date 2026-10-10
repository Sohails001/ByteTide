using System.Globalization;

namespace ByteTide.App;

/// <summary>Formats a transfer rate (given in bytes per second) for display.</summary>
public static class SpeedFormat
{
    /// <summary>Readable form, e.g. "391.0 Mbps" (bits) or "14.1 KB/s" (bytes).</summary>
    public static string Full(double bytesPerSec, bool bits)
    {
        var ci = CultureInfo.InvariantCulture;

        if (bits)
        {
            double b = bytesPerSec * 8;
            if (b >= 1_000_000_000) return (b / 1_000_000_000).ToString("0.0", ci) + " Gbps";
            if (b >= 1_000_000) return (b / 1_000_000).ToString("0.0", ci) + " Mbps";
            if (b >= 1_000) return (b / 1_000).ToString("0.0", ci) + " kbps";
            return b.ToString("0", ci) + " bps";
        }

        if (bytesPerSec >= 1024.0 * 1024 * 1024) return (bytesPerSec / (1024.0 * 1024 * 1024)).ToString("0.0", ci) + " GB/s";
        if (bytesPerSec >= 1024.0 * 1024) return (bytesPerSec / (1024.0 * 1024)).ToString("0.0", ci) + " MB/s";
        if (bytesPerSec >= 1024.0) return (bytesPerSec / 1024.0).ToString("0.0", ci) + " KB/s";
        return bytesPerSec.ToString("0", ci) + " B/s";
    }

    /// <summary>Very short form for the tiny tray icon, e.g. "391M", "1.2M", "14K", "0".</summary>
    public static string Compact(double bytesPerSec, bool bits)
    {
        double v;
        string unit;

        if (bits)
        {
            double b = bytesPerSec * 8;
            if (b >= 1e9) { v = b / 1e9; unit = "G"; }
            else if (b >= 1e6) { v = b / 1e6; unit = "M"; }
            else if (b >= 1e3) { v = b / 1e3; unit = "k"; }
            else { v = b; unit = ""; }
        }
        else
        {
            if (bytesPerSec >= 1024.0 * 1024 * 1024) { v = bytesPerSec / (1024.0 * 1024 * 1024); unit = "G"; }
            else if (bytesPerSec >= 1024.0 * 1024) { v = bytesPerSec / (1024.0 * 1024); unit = "M"; }
            else if (bytesPerSec >= 1024.0) { v = bytesPerSec / 1024.0; unit = "K"; }
            else { v = bytesPerSec; unit = ""; }
        }

        string num = (v >= 10 || unit.Length == 0)
            ? ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture)
            : v.ToString("0.0", CultureInfo.InvariantCulture);

        return num + unit;
    }
}
