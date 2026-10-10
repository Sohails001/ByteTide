using System.Net.NetworkInformation;

namespace ByteTide.Core;

/// <summary>
/// Accumulates per-adapter online-seconds and per-day byte deltas from a
/// stream of LiveSamples, and flushes them into Storage's daily_stats table.
/// Mirrors the rollup logic the GUI app's tray host uses, factored out here
/// so the headless Windows Service (which has no tray host) can share it.
/// </summary>
public sealed class DailyRollupTracker
{
    private sealed class Accumulator
    {
        public DateOnly Date;
        public long StartBytesReceived;
        public long StartBytesSent;
        public long LastBytesReceived;
        public long LastBytesSent;
        public long SecondsOnline;
    }

    private readonly Dictionary<string, Accumulator> _state = new();

    public void RecordSamples(IEnumerable<LiveSample> samples)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var s in samples)
        {
            if (!_state.TryGetValue(s.AdapterId, out var acc) || acc.Date != today)
            {
                acc = new Accumulator
                {
                    Date = today,
                    StartBytesReceived = s.BytesReceived,
                    StartBytesSent = s.BytesSent
                };
                _state[s.AdapterId] = acc;
            }

            acc.SecondsOnline += 1;
            acc.LastBytesReceived = s.BytesReceived;
            acc.LastBytesSent = s.BytesSent;
        }
    }

    public void Flush(Storage storage)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!_state.TryGetValue(nic.Id, out var acc) || acc.Date != today) continue;

            var info = NetworkSampler.GetAdapterInfo(nic);
            long rx = Math.Max(0, acc.LastBytesReceived - acc.StartBytesReceived);
            long tx = Math.Max(0, acc.LastBytesSent - acc.StartBytesSent);

            storage.UpsertDailyStat(new DailyStat
            {
                Date = today,
                AdapterName = info.Name,
                MacAddress = info.MacAddress,
                SecondsOnline = acc.SecondsOnline,
                BytesReceived = rx,
                BytesSent = tx
            });
        }
    }
}
