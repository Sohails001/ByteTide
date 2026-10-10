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

    public event Action<IReadOnlyList<LiveSample>>? SamplesReady;

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
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

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
                        long drx = rx - prev.Rx;
                        long dtx = tx - prev.Tx;

                        // Counters can jump backwards on adapter reset/reconnect; treat as no traffic
                        // rather than reporting a bogus negative rate.
                        double downBps = drx >= 0 ? drx / elapsed : 0;
                        double upBps = dtx >= 0 ? dtx / elapsed : 0;

                        samples.Add(new LiveSample
                        {
                            Timestamp = now,
                            AdapterId = nic.Id,
                            BytesReceived = rx,
                            BytesSent = tx,
                            DownloadBps = downBps,
                            UploadBps = upBps,
                            SpeedBps = nic.Speed
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
