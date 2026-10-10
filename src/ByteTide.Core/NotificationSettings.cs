namespace ByteTide.Core;

/// <summary>
/// Bandwidth usage alert configuration: notify when usage crosses a
/// threshold over a given period, optionally filtered to one adapter.
/// </summary>
public sealed class NotificationSettings
{
    public bool Enabled { get; set; }
    public double ThresholdAmount { get; set; } = 2;
    public string ThresholdUnit { get; set; } = "GB"; // "MB" or "GB"
    public string Measure { get; set; } = "downloaded"; // "downloaded" | "uploaded" | "both"
    public string Period { get; set; } = "month"; // "today" | "week" | "month" | "year"
    public string? AdapterFilter { get; set; } // null = All Connections

    public bool PlaySound { get; set; }
    public string? SoundFilePath { get; set; }
    public bool PlayBeep { get; set; }
    public bool SendEmail { get; set; }
    public string? EmailAddresses { get; set; } // comma-separated
    public bool RunProgram { get; set; }
    public string? ProgramPath { get; set; }

    /// <summary>Set once an alert has fired for a given period key (e.g. "month:2026-08"),
    /// so the same threshold doesn't re-fire every minute for the rest of that period.</summary>
    public string? LastTriggeredPeriodKey { get; set; }
}

/// <summary>
/// SMTP configuration for the "send me an email" notification action.
/// Note: stored as plain text in the local SQLite settings table -- fine
/// for a personal machine, but don't reuse a sensitive account password
/// here without being aware of that.
/// </summary>
public sealed class EmailSettings
{
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
}
