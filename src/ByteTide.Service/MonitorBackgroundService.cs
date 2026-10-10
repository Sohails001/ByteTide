using ByteTide.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ByteTide.Service;

/// <summary>
/// Runs bandwidth monitoring headlessly: continuous sampling, daily history
/// rollups into the same SQLite schema the GUI app uses, and periodic
/// summary report generation to a file -- so usage can be tracked on a
/// machine without staying logged in, matching the original app's
/// "Run as a System Service" feature.
/// </summary>
public sealed class MonitorBackgroundService : BackgroundService
{
    private readonly ILogger<MonitorBackgroundService> _logger;
    private readonly IConfiguration _config;

    public MonitorBackgroundService(ILogger<MonitorBackgroundService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = _config.GetSection("ByteTide");
        var configuredDbPath = section["DataDbPath"];
        var reportIntervalSeconds = int.TryParse(section["ReportIntervalSeconds"], out var r) ? r : 300;
        var reportOutputPath = section["ReportOutputPath"];

        // Services typically run under a system account, not the interactive
        // user, so %AppData% would resolve somewhere unexpected -- use the
        // machine-wide ProgramData location by default instead. Set
        // DataDbPath in appsettings.json if you want this to share history
        // with a specific user's GUI app database.
        var defaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ByteTide");
        Directory.CreateDirectory(defaultDir);

        var dbPath = string.IsNullOrWhiteSpace(configuredDbPath)
            ? Path.Combine(defaultDir, "data.db")
            : configuredDbPath;

        _logger.LogInformation("ByteTide service starting. Database: {DbPath}", dbPath);

        using var storage = new Storage(dbPath);
        using var sampler = new NetworkSampler(TimeSpan.FromSeconds(1));
        var rollup = new DailyRollupTracker();

        sampler.SamplesReady += samples =>
        {
            foreach (var s in samples) storage.InsertLiveSample(s);
            rollup.RecordSamples(samples);
        };

        var lastRollup = DateTime.MinValue;
        var lastReport = DateTime.MinValue;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

                if ((DateTime.Now - lastRollup).TotalSeconds >= 60)
                {
                    rollup.Flush(storage);
                    storage.PruneLiveSamplesOlderThan(TimeSpan.FromHours(48));
                    lastRollup = DateTime.Now;
                }

                if (!string.IsNullOrWhiteSpace(reportOutputPath) &&
                    (DateTime.Now - lastReport).TotalSeconds >= reportIntervalSeconds)
                {
                    TryWriteReport(storage, reportOutputPath);
                    lastReport = DateTime.Now;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on service stop.
        }
        finally
        {
            rollup.Flush(storage);
            _logger.LogInformation("ByteTide service stopping.");
        }
    }

    private void TryWriteReport(Storage storage, string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var today = DateOnly.FromDateTime(DateTime.Today);
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);
            var stats = storage.GetDailyStats(startOfMonth, today);

            long down = stats.Sum(s => s.BytesReceived);
            long up = stats.Sum(s => s.BytesSent);

            var lines = new[]
            {
                $"ByteTide Service Report -- generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Month to date ({startOfMonth:yyyy-MM-dd} to {today:yyyy-MM-dd}):",
                $"  Downloaded: {FormatBytes(down)}",
                $"  Uploaded:   {FormatBytes(up)}",
                $"  Total:      {FormatBytes(down + up)}"
            };

            File.WriteAllLines(path, lines);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write scheduled report to {Path}", path);
        }
    }

    private static string FormatBytes(long bytes)
    {
        double b = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (b >= 1024 && i < units.Length - 1)
        {
            b /= 1024;
            i++;
        }
        return $"{b:0.##} {units[i]}";
    }
}
