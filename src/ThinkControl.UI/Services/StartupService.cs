using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace ThinkControl.UI.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ThinkControl";
    private const string StartupApprovedKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    // Windows Settings / Task Manager can disable a Run entry without deleting
    // it. This OS-owned binary value must only be read, never overwritten by us.
    public static bool IsDisabledByWindows()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupApprovedKey, writable: false);
            return key?.GetValue(ValueName) is byte[] bytes &&
                   bytes.Length >= 4 && bytes[0] == 0x03;
        }
        catch
        {
            return false;
        }
    }

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
                StringComparison.OrdinalIgnoreCase) &&
                !IsDisabledByWindows();
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
            // Respect a disable in Windows Startup Apps; never silently bypass
            // the OS-managed approval state or display this as enabled.
            return !IsDisabledByWindows();
        }
        catch
        {
            return false;
        }
    }
}
