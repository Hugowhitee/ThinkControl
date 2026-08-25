using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Principal;

namespace ThinkControl.UI.Services;

/// <summary>
/// The UI is intentionally unprivileged; all administrator-only hardware work lives
/// in ThinkControl.Service or a dedicated installer/repair process. Older alpha
/// updaters pre-elevated Inno Setup before launching it, which means Inno's
/// runasoriginaluser flag cannot recover the original desktop token. If such an old
/// updater relaunches the new UI elevated, hand the executable back to Explorer once
/// and exit before tray hooks/windows are created.
/// </summary>
internal static class DesktopElevationGuard
{
    private const string UiExecutable = "ThinkControl.UI.exe";
    private const string GuardFile = "elevation-relaunch.guard";
    private static readonly TimeSpan LoopGuardWindow = TimeSpan.FromSeconds(12);

    [ModuleInitializer]
    internal static void EnsureNormalDesktopProcess()
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath) ||
            !Path.GetFileName(processPath).Equals(UiExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string marker = GetMarkerPath();
        if (!IsElevated())
        {
            TryDelete(marker);
            return;
        }

        // Prevent a relaunch loop on unusual systems where Explorer itself is elevated
        // (for example UAC disabled). In that configuration there is no medium token to
        // recover, so continuing is safer than repeatedly spawning the same process.
        if (IsRecentMarker(marker))
        {
            TryDelete(marker);
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            File.WriteAllText(marker, DateTimeOffset.UtcNow.ToString("O"));

            string explorer = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "explorer.exe");
            if (!File.Exists(explorer))
            {
                TryDelete(marker);
                return;
            }

            // Explorer is the normal medium-integrity desktop shell on a standard UAC
            // session. Passing a single executable path asks that shell to open it,
            // avoiding inheritance of the elevated installer's token. No arguments are
            // required for installer relaunches; normal startup/tray launches are never
            // elevated and therefore never take this path.
            using Process? relaunch = Process.Start(new ProcessStartInfo
            {
                FileName = explorer,
                Arguments = $"\"{processPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (relaunch is null)
            {
                TryDelete(marker);
                return;
            }

            Environment.Exit(0);
        }
        catch
        {
            TryDelete(marker);
            // Never make an elevation-recovery convenience path a startup blocker.
            // The app still remains safe because privileged hardware writes are behind
            // the service IPC boundary rather than performed directly by the UI.
        }
    }

    private static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string GetMarkerPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ThinkControl",
        GuardFile);

    private static bool IsRecentMarker(string marker)
    {
        try
        {
            return File.Exists(marker) &&
                   DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(marker) <= LoopGuardWindow;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
