namespace ThinkControl.Core.Notifications;

public static class AttentionCooldownPolicy
{
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromHours(24);

    public static string HardwareKey(string? status)
    {
        string value = status?.Trim() ?? string.Empty;
        if (value.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("does not respond", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("service", StringComparison.OrdinalIgnoreCase) &&
            (value.Contains("stopped", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("not installed", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("offline", StringComparison.OrdinalIgnoreCase)))
        {
            return "hardware:service";
        }
        if (value.Contains("low-level", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("pawnio", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("component", StringComparison.OrdinalIgnoreCase))
        {
            return "hardware:low-level";
        }
        return "hardware:provider";
    }

    public static bool IsSuppressed(
        string key,
        string? acknowledgedKey,
        string? acknowledgedAtUtc,
        DateTimeOffset now,
        TimeSpan? cooldown = null)
    {
        if (!string.Equals(key, acknowledgedKey?.Trim(), StringComparison.Ordinal))
            return false;
        if (!DateTimeOffset.TryParse(acknowledgedAtUtc, out DateTimeOffset acknowledged))
            return false;
        TimeSpan elapsed = now - acknowledged.ToUniversalTime();
        return elapsed >= TimeSpan.Zero && elapsed < (cooldown ?? DefaultCooldown);
    }
}
