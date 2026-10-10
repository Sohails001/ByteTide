namespace ByteTide.Core;

/// <summary>
/// Pure threshold-checking logic for bandwidth usage alerts. Deciding *how*
/// to notify (sound/email/run program) is left to the caller -- this just
/// answers "has the configured limit been crossed for the current period?".
/// </summary>
public static class NotificationEngine
{
    public readonly record struct CheckResult(bool Triggered, long TotalBytes, long ThresholdBytes, string PeriodKey);

    public static CheckResult CheckThreshold(Storage storage, NotificationSettings settings)
    {
        if (!settings.Enabled)
            return new CheckResult(false, 0, 0, "");

        var today = DateOnly.FromDateTime(DateTime.Today);

        DateOnly from;
        string periodKey;
        switch (settings.Period)
        {
            case "today":
                from = today;
                periodKey = $"today:{today:yyyy-MM-dd}";
                break;
            case "week":
                from = today.AddDays(-(int)today.DayOfWeek);
                periodKey = $"week:{from:yyyy-MM-dd}";
                break;
            case "year":
                from = new DateOnly(today.Year, 1, 1);
                periodKey = $"year:{today.Year}";
                break;
            default: // "month"
                from = new DateOnly(today.Year, today.Month, 1);
                periodKey = $"month:{today:yyyy-MM}";
                break;
        }

        var stats = storage.GetDailyStats(from, today);
        if (settings.AdapterFilter is not null)
            stats = stats.Where(s => s.AdapterName == settings.AdapterFilter).ToList();

        long total = settings.Measure switch
        {
            "uploaded" => stats.Sum(s => s.BytesSent),
            "both" => stats.Sum(s => s.BytesReceived + s.BytesSent),
            _ => stats.Sum(s => s.BytesReceived) // "downloaded"
        };

        long thresholdBytes = (long)(settings.ThresholdAmount *
            (settings.ThresholdUnit == "GB" ? 1024L * 1024 * 1024 : 1024L * 1024));

        bool triggered = total >= thresholdBytes && settings.LastTriggeredPeriodKey != periodKey;
        return new CheckResult(triggered, total, thresholdBytes, periodKey);
    }
}
