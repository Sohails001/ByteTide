using System.Linq;
using System.Net.NetworkInformation;

namespace ByteTide.Core;

/// <summary>
/// Polls every active, non-loopback network interface on a fixed interval
/// and turns the raw cumulative byte counters Windows exposes into
/// instantaneous download/upload rates.
///
/// No drivers, no packet capture -- just the same counters every other
/// bandwidth monitor (including the original RAS Graph &amp; Stats) reads
/// from the OS.
/// </summary>
public sealed class NetworkSampler : IDisposable
{
    private readonly Timer _timer;
    private readonly Dictionary<string, (long Rx, long Tx, DateTime Ts)> _last = new();
    private readonly object _lastLock = new();

    private readonly object _filterLock = new();
    private HashSet<string> _excludedIds = new();
    private HashSet<string> _forcedIncludedIds = new();
    private bool _includeVirtual;

    public event Action<IReadOnlyList<LiveSample>>? SamplesReady;

    /// <summary>
    /// When false (the default), virtual, VPN and tunnel adapters are ignored
    /// so their traffic isn't counted twice alongside the physical adapter.
    /// </summary>
    public bool IncludeVirtualAdapters
    {
        get { lock (_filterLock) return _includeVirtual; }
        set { lock (_filterLock) _includeVirtual = value; }
    }

    /// <summary>Adapter ids (NetworkInterface.Id) the user has chosen not to count.</summary>
    public IReadOnlyCollection<string> ExcludedAdapterIds
    {
        get { lock (_filterLock) return _excludedIds.ToList(); }
    }

    public void SetExcludedAdapterIds(IEnumerable<string> ids)
    {
        lock (_filterLock) _excludedIds = new HashSet<string>(ids);
    }

    /// <summary>
    /// Adapter ids the user explicitly wants counted even though they look
    /// virtual (overrides the automatic virtual/VPN exclusion).
    /// </summary>
    public IReadOnlyCollection<string> ForcedIncludedAdapterIds
    {
        get { lock (_filterLock) return _forcedIncludedIds.ToList(); }
    }

    public void SetForcedIncludedAdapterIds(IEnumerable<string> ids)
    {
        lock (_filterLock) _forcedIncludedIds = new HashSet<string>(ids);
    }

    /// <summary>Whether this adapter currently counts towards totals.</summary>
    public bool IsTracked(NetworkInterface nic)
    {
        if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) return false;

        lock (_filterLock)
        {
            if (_excludedIds.Contains(nic.Id)) return false;
            if (_forcedIncludedIds.Contains(nic.Id)) return true;
            if (!_includeVirtual && AdapterFilter.LooksVirtual(nic)) return false;
        }
        return true;
    }

    public NetworkSampler(TimeSpan interval)
    {
        _timer = new Timer(Tick, null, TimeSpan.Zero, interval);
    }

    private void Tick(object? state)
    {
        var now = DateTime.Now;
        var samples = new List<LiveSample>();

        lock (_lastLock)
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (!IsTracked(nic)) continue;

                IPInterfaceStatistics stats;
                try
                {
                    stats = nic.GetIPStatistics();
                }
                catch
                {
                    // Adapter can vanish between enumeration and query (e.g. USB NIC unplugged).
                    continue;
                }

                long rx = stats.BytesReceived;
                long tx = stats.BytesSent;

                if (_last.TryGetValue(nic.Id, out var prev))
                {
                    double elapsed = (now - prev.Ts).TotalSeconds;
                    if (elapsed > 0)
                    {
                        // Counters jump backwards when an adapter resets or reconnects;
                        // CounterMath treats that as "restarted from zero" so no bytes
                        // are lost and no negative rate is reported.
                        long drx = CounterMath.Delta(prev.Rx, rx);
                        long dtx = CounterMath.Delta(prev.Tx, tx);

                        var info = GetAdapterInfo(nic);

                        samples.Add(new LiveSample
                        {
                            Timestamp = now,
                            AdapterId = nic.Id,
                            BytesReceived = rx,
                            BytesSent = tx,
                            DownloadBps = drx / elapsed,
                            UploadBps = dtx / elapsed,
                            SpeedBps = nic.Speed,
                            AdapterName = info.Name,
                            MacAddress = info.MacAddress,
                            DeltaBytesReceived = drx,
                            DeltaBytesSent = dtx,
                            ElapsedSeconds = elapsed
                        });
                    }
                }

                _last[nic.Id] = (rx, tx, now);
            }
        }

        if (samples.Count > 0)
            SamplesReady?.Invoke(samples);
    }

    /// <summary>
    /// Builds the same "Name @ MAC" identity the legacy .dat format used,
    /// so live rollups and imported history describe adapters identically.
    /// </summary>
    public static AdapterInfo GetAdapterInfo(NetworkInterface nic)
    {
        return new AdapterInfo
        {
            Id = nic.Id,
            Name = nic.Description,
            MacAddress = FormatMac(nic.GetPhysicalAddress())
        };
    }

    private static string FormatMac(System.Net.NetworkInformation.PhysicalAddress addr)
    {
        var bytes = addr.GetAddressBytes();
        if (bytes.Length == 0) return "00-00-00-00-00-00";
        return string.Join("-", bytes.Select(b => b.ToString("X2")));
    }

    public void Dispose() => _timer.Dispose();
}
