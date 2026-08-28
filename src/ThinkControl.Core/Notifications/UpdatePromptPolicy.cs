namespace ThinkControl.Core.Notifications;

/// <summary>
/// Shared semantics for update prompts. Dismissing a version suppresses only the
/// proactive prompt for that exact version; the update itself remains available in
/// the notification center and Updates page, and a newer version prompts again.
/// </summary>
public static class UpdatePromptPolicy
{
    public static bool IsDismissed(string? availableVersion, string? dismissedVersion)
    {
        string available = availableVersion?.Trim() ?? string.Empty;
        if (available.Length == 0)
            return false;
        return string.Equals(available, dismissedVersion?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string DisplayVersion(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        normalized = normalized.TrimStart('v', 'V');
        return normalized.Length == 0 ? "new version" : normalized;
    }

    public static string Transition(string? currentVersion, string? availableVersion) =>
        $"{DisplayVersion(currentVersion)}  →  {DisplayVersion(availableVersion)}";
}
