namespace ByteTide.App;

/// <summary>
/// Every toggle in the View menu, in one serializable bag so it can be
/// saved/restored as JSON via Storage's settings table.
/// </summary>
public sealed class GraphOptions
{
    public bool ShowTitle { get; set; } = true;
    public bool ShowIpAddress { get; set; }
    public bool ColumnStyle { get; set; } // false = filled area graph, true = column/bar graph
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
