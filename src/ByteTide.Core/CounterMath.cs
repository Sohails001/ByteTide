namespace ByteTide.Core;

/// <summary>Arithmetic on the cumulative byte counters Windows exposes.</summary>
public static class CounterMath
{
    /// <summary>
    /// Bytes transferred between two readings of a cumulative counter. If the
    /// counter went backwards (adapter reset, Wi-Fi reconnect, PPP redial) it
    /// restarted from zero, so the new reading is the amount since the reset.
    /// Totals built from these deltas survive reconnects; totals built from
    /// "last minus first" do not.
    /// </summary>
    public static long Delta(long previous, long current)
        => current >= previous ? current - previous : current;
}
