using Microsoft.Win32;
using System.Diagnostics;

namespace ThinkControl.UI.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ThinkControl";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            if (key?.GetValue(ValueName) is not string value)
                return false;

            // A stale Run value can survive a move, repair or installer handoff.
            // Settings must not display "on" merely because any string exists.
            string executable = Environment.ProcessPath ??
                Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                return false;

            return string.Equals(
                value.Trim(),
                $"\"{executable}\" --tray",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null)
                return false;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            string executable = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                return false;

            key.SetValue(ValueName, $"\"{executable}\" --tray", RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
