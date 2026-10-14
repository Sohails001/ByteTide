using ByteTide.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ByteTide.Tests;

public sealed class DailyRollupTrackerTests : IDisposable
{
    private static readonly DateTime Noon = new(2026, 10, 13, 12, 0, 0);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");
    private readonly Storage _storage;

    public DailyRollupTrackerTests()
    {
        _storage = new Storage(_dbPath);
    }

    private static LiveSample Sample(long rx, long tx, double elapsed = 1.0) => new()
    {
        AdapterId = "adapter-1",
        AdapterName = "Test Ethernet",
        MacAddress = "AA-BB-CC-DD-EE-01",
        Timestamp = Noon,
        DeltaBytesReceived = rx,
        DeltaBytesSent = tx,
        ElapsedSeconds = elapsed
    };

    private List<DailyStat> Day() =>
        _storage.GetDailyStats(DateOnly.FromDateTime(Noon), DateOnly.FromDateTime(Noon));

    [Fact]
    public void Flush_AddsUpDeltasAndTime()
    {
        var tracker = new DailyRollupTracker();
        tracker.RecordSamples(new[] { Sample(1000, 10), Sample(500, 5) });
        tracker.Flush(_storage);

        var row = Assert.Single(Day());
        Assert.Equal(1500, row.BytesReceived);
        Assert.Equal(15, row.BytesSent);
        Assert.Equal(2, row.SecondsOnline);
    }

    [Fact]
    public void RestartingTheApp_DoesNotEraseWhatWasAlreadyRecorded()
    {
        var first = new DailyRollupTracker();
        first.RecordSamples(new[] { Sample(1000, 100) });
        first.Flush(_storage);

        // "Restart": a brand-new tracker, as when the app is relaunched mid-day.
        var second = new DailyRollupTracker();
        second.RecordSamples(new[] { Sample(500, 50) });
        second.Flush(_storage);

        var row = Assert.Single(Day());
        Assert.Equal(1500, row.BytesReceived);
        Assert.Equal(150, row.BytesSent);
    }

    [Fact]
    public void FlushingTwiceWithoutNewData_DoesNotDoubleCount()
    {
        var tracker = new DailyRollupTracker();
        tracker.RecordSamples(new[] { Sample(1000, 100) });
        tracker.Flush(_storage);
        tracker.Flush(_storage);

        Assert.Equal(1000, Assert.Single(Day()).BytesReceived);
    }

    [Fact]
    public void LongGap_IsNotCountedAsOnlineTime()
    {
        var tracker = new DailyRollupTracker();
        tracker.RecordSamples(new[] { Sample(0, 0, elapsed: 3 * 3600) }); // PC slept for three hours
        tracker.Flush(_storage);

        Assert.InRange(Assert.Single(Day()).SecondsOnline, 0, 2);
    }

    public void Dispose()
    {
        _storage.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { /* temp file; not worth failing a test over */ }
    }
}
