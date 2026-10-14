namespace ByteTide.Core;

/// <summary>
/// Accumulates per-adapter, per-day usage from a stream of LiveSamples and
/// flushes it into Storage's daily_stats table.
///
/// Design notes (these fix the "totals reset when the app restarts" and
/// "bytes vanish after a reconnect" problems of the original rollup):
///  * It sums each sample's reset-safe DELTAS instead of subtracting raw
///    counters, so adapter reconnects don't lose data.
///  * Every flush ADDS the pending amounts to the stored day (see
///    Storage.AddToDailyStat) instead of overwriting it, so restarting the app
///    mid-day keeps what was already recorded.
///  * Online time is the real elapsed time of each sample, not "1 per tick".
/// Shared by the GUI tray host and the headless Windows Service.
/// </summary>
public sealed class DailyRollupTracker
{
    private sealed class Pending
    {
        public DateOnly Date;
        public string AdapterName = "";
        public string MacAddress = "";
        public double Seconds;
        public long Received;
        public long Sent;
    }

    // A single sample never contributes more than this much online time, so a
    // sleep/hibernate gap isn't counted as time spent online.
    private const double MaxSecondsPerSample = 2.0;

    private readonly object _lock = new();
    private readonly Dictionary<string, Pending> _pending = new();
    private readonly List<Pending> _finished = new(); // earlier days / failed writes waiting to be saved

    public void RecordSamples(IEnumerable<LiveSample> samples)
    {
        lock (_lock)
        {
            foreach (var s in samples)
            {
                var date = DateOnly.FromDateTime(s.Timestamp);

                if (!_pending.TryGetValue(s.AdapterId, out var p))
                {
                    p = new Pending { Date = date };
                    _pending[s.AdapterId] = p;
                }
                else if (p.Date != date)
                {
                    // Midnight passed: keep yesterday's amounts for yesterday
                    // (but don't bother saving an empty record).
                    if (p.Seconds >= 0.5 || p.Received > 0 || p.Sent > 0) _finished.Add(p);
                    p = new Pending { Date = date };
                    _pending[s.AdapterId] = p;
                }

                p.AdapterName = s.AdapterName;
                p.MacAddress = s.MacAddress;
                p.Seconds += Math.Min(Math.Max(s.ElapsedSeconds, 0), MaxSecondsPerSample);
                p.Received += s.DeltaBytesReceived;
                p.Sent += s.DeltaBytesSent;
            }
        }
    }

    /// <summary>Writes everything accumulated so far into storage (additively).</summary>
    public void Flush(Storage storage)
    {
        var toWrite = new List<Pending>();

        lock (_lock)
        {
            toWrite.AddRange(_finished);
            _finished.Clear();

            foreach (var p in _pending.Values)
            {
                long wholeSeconds = (long)p.Seconds;
                if (wholeSeconds == 0 && p.Received == 0 && p.Sent == 0) continue;

                toWrite.Add(new Pending
                {
                    Date = p.Date,
                    AdapterName = p.AdapterName,
                    MacAddress = p.MacAddress,
                    Seconds = wholeSeconds,
                    Received = p.Received,
                    Sent = p.Sent
                });

                p.Seconds -= wholeSeconds; // keep the fractional remainder for next time
                p.Received = 0;
                p.Sent = 0;
            }
        }

        foreach (var item in toWrite)
        {
            if (string.IsNullOrEmpty(item.AdapterName)) continue;

            try
            {
                storage.AddToDailyStat(new DailyStat
                {
                    Date = item.Date,
                    AdapterName = item.AdapterName,
                    MacAddress = item.MacAddress,
                    SecondsOnline = (long)Math.Round(item.Seconds),
                    BytesReceived = item.Received,
                    BytesSent = item.Sent
                });
            }
            catch
            {
                // Don't lose the data if the database was briefly unavailable; retry next flush.
                lock (_lock) _finished.Add(item);
            }
        }
    }
}
