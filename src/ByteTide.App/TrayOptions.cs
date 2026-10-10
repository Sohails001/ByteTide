namespace ByteTide.App;

/// <summary>How the system tray icon is drawn.</summary>
public enum TrayIconStyle
{
    /// <summary>Tiny live line graph of recent download/upload.</summary>
    MiniGraph = 0,
    /// <summary>Two short numbers: download on top, upload underneath.</summary>
    SpeedNumbers = 1,
    /// <summary>Classic two-monitor "dial-up" icon whose screens light up on activity.</summary>
    DialupMonitors = 2
}

/// <summary>Colour sets for the dial-up monitor icon.</summary>
public enum DialupPalette
{
    GreenYellow = 0,
    Green = 1,
    Yellow = 2,
    Blue = 3,
    XpBlue = 4,
    ZoneAlarm = 5,
    Custom = 6
}

/// <summary>
/// Everything about the tray icon / floating meter that can be customised,
/// persisted as JSON in the settings table.
/// </summary>
public sealed class TrayOptions
{
    public TrayIconStyle Style { get; set; } = TrayIconStyle.MiniGraph;
    public DialupPalette Palette { get; set; } = DialupPalette.GreenYellow;

    public int CustomDownloadArgb { get; set; } = Color.Lime.ToArgb();
    public int CustomUploadArgb { get; set; } = Color.Yellow.ToArgb();

    /// <summary>Show speeds as bits (kbps / Mbps) instead of bytes (KB/s / MB/s).</summary>
    public bool ShowSpeedsInBits { get; set; }

    /// <summary>Small always-on-top window with live speeds and a graph.</summary>
    public bool ShowFloatingMeter { get; set; }

    /// <summary>Saved meter position; int.MinValue means "not set yet" (use default corner).</summary>
    public int MeterX { get; set; } = int.MinValue;
    public int MeterY { get; set; } = int.MinValue;
}
