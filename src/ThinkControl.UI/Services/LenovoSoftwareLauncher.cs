using Microsoft.Win32;
using System.Diagnostics;

namespace ThinkControl.UI.Services;

public static class LenovoSoftwareLauncher
{
    private static readonly string[] VantageProtocols =
    [
        "lenovo-metro-settings",
        "lenovo-vantage"
    ];

    public static bool TryOpenVantage()
    {
        // Commercial Vantage registers lenovo-metro-settings. Consumer Vantage
        // commonly registers lenovo-vantage. Only invoke a protocol when Windows
        // says it is registered, so a missing app cannot bounce the user into Store.
        foreach (string protocol in VantageProtocols)
        {
            if (IsRegisteredProtocol(protocol) && TryOpen($"{protocol}:"))
                return true;
        }

        // Packaged-app registrations can differ between Lenovo versions. Resolve
        // the installed Start-menu AUMID as a second, local-only fallback.
        return TryOpenInstalledStartApp();
    }

    private static bool IsRegisteredProtocol(string protocol)
    {
        try
        {
            using RegistryKey? key = Registry.ClassesRoot.OpenSubKey(protocol);
            return key is not null && key.GetValue("URL Protocol") is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryOpen(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryOpenInstalledStartApp()
    {
        try
        {
            const string script =
                "$app = Get-StartApps | Where-Object { $_.Name -match 'Commercial Vantage|Lenovo Vantage' } | Select-Object -First 1; " +
                "if ($null -eq $app) { exit 1 }; " +
                "Start-Process explorer.exe ('shell:AppsFolder\\' + $app.AppID); exit 0";

            var info = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-Command");
            info.ArgumentList.Add(script);

            using Process? process = Process.Start(info);
            if (process is null)
                return false;

            return process.WaitForExit(3000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
