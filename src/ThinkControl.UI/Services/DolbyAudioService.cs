using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace ThinkControl.UI.Services;

public sealed record DolbyAudioStatus(
    bool DolbyAccessInstalled,
    bool DaxBackendDetected,
    string Detail,
    bool DirectApiAvailable = false,
    string? ActiveProfile = null,
    string? ActiveSubProfile = null);

public sealed record DolbyProfileResult(bool Success, string Detail);

/// <summary>
/// Installation/launcher helper only. Direct Dolby state and changes are owned by
/// DolbyDirectControlService. Keeping launch/install concerns separate prevents a
/// failed profile command from silently opening Dolby Access or introducing a
/// second, competing Dolby control path.
/// </summary>
public sealed class DolbyAudioService
{
    private const string AppUserModelId = "DolbyLaboratories.DolbyAccess_rz1tebttyb220!App";
    private const string StoreUri = "ms-windows-store://pdp/?ProductId=9N0866FS04W8";
    private const string DaxClsid = "{20532D01-15BE-4BB9-A727-CA34555D881C}";

    public DolbyAudioStatus Probe()
    {
        bool access = IsDolbyAccessInstalled();
        bool dax = IsKnownDaxBackendRegistered();
        string detail = (access, dax) switch
        {
            (true, true) => "Dolby Access and the OEM DAX backend are installed. ThinkControl enables only direct controls that the DAX provider can read back.",
            (false, true) => "Dolby processing is installed, but Dolby Access is not. Direct DAX controls remain available where the driver exposes verified state.",
            (true, false) => "Dolby Access is installed, but the expected OEM DAX backend is not registered. Use Dolby Access for settings this driver does not expose directly.",
            _ => "Dolby Access and the expected OEM DAX backend were not detected. Lenovo's audio driver may also be required for Dolby processing."
        };
        return new DolbyAudioStatus(access, dax, detail);
    }

    public bool OpenDolbyAccess()
    {
        if (!IsDolbyAccessInstalled())
            return false;

        try
        {
            string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            Process.Start(new ProcessStartInfo(explorer, $"shell:AppsFolder\\{AppUserModelId}") { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool OpenStore()
    {
        try
        {
            Process.Start(new ProcessStartInfo(StoreUri) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDolbyAccessInstalled()
    {
        try
        {
            using RegistryKey? packages = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
            return packages?.GetSubKeyNames().Any(name =>
                name.StartsWith("DolbyLaboratories.DolbyAccess_", StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsKnownDaxBackendRegistered()
    {
        try
        {
            using RegistryKey? key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{DaxClsid}");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}
