using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ThinkControl.UI.Services;

internal sealed record UpdateHandoff(string TargetVersion, DateTimeOffset StartedAtUtc, string LogPath);

internal sealed record UpdateHandoffOutcome(bool Completed, string Status, string? LogPath = null);

internal static class UpdateHandoffService
{
    private static readonly string UpdateFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ThinkControl",
        "updates");
    private static readonly string HandoffPath = Path.Combine(UpdateFolder, "pending-update.json");

    internal static void Record(string targetVersion, string logPath)
    {
        Directory.CreateDirectory(UpdateFolder);
        string json = JsonSerializer.Serialize(new UpdateHandoff(
            Normalize(targetVersion),
            DateTimeOffset.UtcNow,
            logPath));
        File.WriteAllText(HandoffPath, json);
    }

    internal static void Clear()
    {
        try { File.Delete(HandoffPath); } catch { }
    }

    internal static UpdateHandoffOutcome? Evaluate(string currentVersion)
    {
        UpdateHandoff? handoff = Read();
        if (handoff is null)
            return null;

        if (string.Equals(Normalize(currentVersion), Normalize(handoff.TargetVersion), StringComparison.OrdinalIgnoreCase))
        {
            Clear();
            return new(true, $"Updated successfully to {handoff.TargetVersion}", handoff.LogPath);
        }

        TimeSpan age = DateTimeOffset.UtcNow - handoff.StartedAtUtc;
        if (age < TimeSpan.FromMinutes(2))
            return new(false, $"Update to {handoff.TargetVersion} is still waiting for Windows Setup", handoff.LogPath);

        return new(false,
            $"Update to {handoff.TargetVersion} did not finish. ThinkControl stayed on v{Normalize(currentVersion)}; the installer log is available.",
            handoff.LogPath);
    }

    internal static bool TryOpenLog(string? path = null)
    {
        string candidate = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(UpdateFolder, "last-update.log")
            : path;
        if (!File.Exists(candidate))
            return false;
        Process.Start(new ProcessStartInfo(candidate) { UseShellExecute = true });
        return true;
    }

    private static UpdateHandoff? Read()
    {
        try
        {
            if (!File.Exists(HandoffPath))
                return null;
            return JsonSerializer.Deserialize<UpdateHandoff>(File.ReadAllText(HandoffPath));
        }
        catch
        {
            Clear();
            return null;
        }
    }

    private static string Normalize(string value) => value.Trim().TrimStart('v', 'V').Split('+')[0];
}
