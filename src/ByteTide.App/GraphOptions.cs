namespace ByteTide.App;

/// <summary>How the live graph is drawn.</summary>
public enum GraphStyle
{
    /// <summary>Filled area with a crisp outline (default).</summary>
    Area = 0,
    /// <summary>Separate vertical bars, one per second.</summary>
    Column = 1,
    /// <summary>Outline only.</summary>
    Line = 2
}

/// <summary>
/// Every toggle in the View menu, in one serializable bag so it can be
/// saved/restored as JSON via Storage's settings table.
/// </summary>
public sealed class GraphOptions
{
    public bool ShowTitle { get; set; } = true;
    public bool ShowIpAddress { get; set; }

    /// <summary>Legacy flag from earlier builds; migrated into Style when settings load.</summary>
    public bool ColumnStyle { get; set; }

    public GraphStyle Style { get; set; } = GraphStyle.Area;

    /// <summary>Horizontal pixels per second of history (1 = dense, 4 = zoomed in).</summary>
    public int PixelsPerSecond { get; set; } = 2;

    public bool ShowScale { get; set; } = true;        // y-axis value labels
    public bool ShowScaleGrid { get; set; } = true;    // horizontal gridlines
    public bool ShowTime { get; set; } = true;         // x-axis time labels
    public bool ShowTimeGrid { get; set; } = true;     // vertical gridlines
    public bool ShowAverageDownloadLine { get; set; }
    public bool ShowAverageUploadLine { get; set; }
    public bool ShowText { get; set; } = true;         // master switch for title/IP/max-rate/avg-speed overlay text
    public bool ShowMaxRate { get; set; }
    public bool ShowAverageSpeeds { get; set; }
    public bool UseKbps { get; set; } // false = KB/s (bytes), true = kbps (bits)
}
