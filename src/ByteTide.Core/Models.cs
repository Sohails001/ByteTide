namespace ByteTide.Core;

/// <summary>
/// Identifies a network adapter. Name/MacAddress mirror what the legacy
/// RAS Graph &amp; Stats file format used, so imported and live data line up.
/// </summary>
public sealed class AdapterInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string MacAddress { get; init; } = "";
}

/// <summary>
/// One instantaneous measurement for a single adapter, taken every sampling
/// interval (default 1s). Rates are derived from the delta against the
/// previous sample.
/// </summary>
public sealed class LiveSample
{
    public DateTime Timestamp { get; init; }
    public string AdapterId { get; init; } = "";
    public long BytesReceived { get; init; }
    public long BytesSent { get; init; }
    public double DownloadBps { get; init; }
    public double UploadBps { get; init; }

    /// <summary>Adapter link speed in bits/sec (NetworkInterface.Speed), captured
    /// alongside the byte counters so callers don't need a separate, relatively
    /// expensive NetworkInterface.GetAllNetworkInterfaces() call just for this.</summary>
    public long SpeedBps { get; init; }

    /// <summary>Adapter description + MAC, so rollups never need to re-enumerate adapters.</summary>
    public string AdapterName { get; init; } = "";
    public string MacAddress { get; init; } = "";

    /// <summary>Bytes transferred since the previous sample, reset-safe (see CounterMath).
    /// Accumulate these for totals -- never subtract raw counters.</summary>
    public long DeltaBytesReceived { get; init; }
    public long DeltaBytesSent { get; init; }

    /// <summary>Seconds since the previous sample (about 1; larger after sleep or a stall).</summary>
    public double ElapsedSeconds { get; init; }
}

/// <summary>
/// Aggregated totals for one adapter on one calendar day. This is the
/// shared shape used both by live rollups and by the legacy importer, so
/// old and new history sit in the same table.
/// </summary>
public sealed class DailyStat
{
    public DateOnly Date { get; init; }
    public string AdapterName { get; init; } = "";
    public string MacAddress { get; init; } = "";
    public long SecondsOnline { get; init; }
    public long BytesReceived { get; init; }
    public long BytesSent { get; init; }
}

public readonly record struct ImportSummary(int FilesProcessed, int RecordsImported, int FilesFailed);
