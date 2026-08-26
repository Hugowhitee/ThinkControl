namespace ThinkControl.Core.Battery;

/// <summary>
/// Retention rules for battery history. Detailed samples expire quickly while
/// compact session summaries remain available for trends and learned estimates.
/// </summary>
public static class BatteryHistoryRetentionPolicy
{
    public const int SummaryRetentionDays = 365;

    public static int NormalizeDetailedDays(int days) => days switch
    {
        <= 7 => 7,
        <= 14 => 14,
        _ => 30
    };

    public static bool KeepDetailedSamples(DateTimeOffset sessionEnd, DateTimeOffset now, int configuredDays) =>
        sessionEnd >= now - TimeSpan.FromDays(NormalizeDetailedDays(configuredDays));

    public static bool KeepSummary(DateTimeOffset sessionEnd, DateTimeOffset now) =>
        sessionEnd >= now - TimeSpan.FromDays(SummaryRetentionDays);
}
