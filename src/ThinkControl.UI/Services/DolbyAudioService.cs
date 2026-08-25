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
    string? ActiveSubProfile = null,
    bool FusionBackendDetected = false)
{
    public bool OemBackendDetected => DaxBackendDetected || FusionBackendDetected;
}

public sealed record DolbyProfileResult(bool Success, string Detail);

/// <summary>
/// Installation/launcher helper only. Direct Dolby state and changes are owned by
/// DolbyDirectControlService. Modern ThinkPad generations can use Dolby Fusion SWC
/// rather than the older DAX3 COM class, so provider detection must not report that
/// the OEM audio stack is missing merely because the legacy CLSID is absent.
/// </summary>
public sealed class DolbyAudioService
{
    private const string AppUserModelId = "DolbyLaboratories.DolbyAccess_rz1tebttyb220!App";
    private const string StoreUri = "ms-windows-store://pdp/?ProductId=9N0866FS04W8";
    private const string DaxClsid = "{20532D01-15BE-4BB9-A727-CA34555D881C}";
    private const string FusionSwcEnumPath = @"SYSTEM\CurrentControlSet\Enum\SWC\VEN_DOLBY&PID_FUSIONAPOSVC";

    public DolbyAudioStatus Probe()
    {
        bool access = IsDolbyAccessInstalled();
        bool dax = IsKnownDaxBackendRegistered();
        bool fusion = IsDolbyFusionBackendPresent();

        string detail;
        if (fusion && dax)
        {
            detail = "Dolby Access/OEM audio is installed. This system exposes Dolby Fusion plus the legacy DAX3 API; ThinkControl enables only direct controls that pass a real readback.";
        }
        else if (fusion)
        {
            detail = access
                ? "Dolby Access and the OEM Dolby Fusion audio component are installed. This generation does not expose ThinkControl's verified legacy DAX3 profile API; use Dolby Access for Atmos profiles while Fusion remains active."
                : "The OEM Dolby Fusion audio component is installed, but Dolby Access was not detected for this user. Audio processing can remain active; install/open Dolby Access for profile controls not exposed through a verified API.";
        }
        else
        {
            detail = (access, dax) switch
            {
                (true, true) => "Dolby Access and the OEM DAX3 backend are installed. ThinkControl enables only direct controls that the DAX provider can read back.",
                (false, true) => "Dolby processing is installed, but Dolby Access is not. Direct DAX controls remain available where the driver exposes verified state.",
                (true, false) => "Dolby Access is installed, but no supported OEM Dolby backend was detected. Lenovo's current audio driver may need repair; ThinkControl will not guess a profile API.",
                _ => "Dolby Access and a supported OEM Dolby backend were not detected. Lenovo's audio driver may also be required for Dolby processing."
            };
        }

        return new DolbyAudioStatus(access, dax, detail, FusionBackendDetected: fusion);
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

    private static bool IsDolbyFusionBackendPresent()
    {
        // Current Lenovo X9 audio packages identify the modern software component as
        // SWC\VEN_DOLBY&PID_FUSIONAPOSVC. Check that exact PnP component first; it is
        // much cheaper and less ambiguous than broad WMI/device enumeration.
        try
        {
            using RegistryKey? swc = Registry.LocalMachine.OpenSubKey(FusionSwcEnumPath);
            if (swc is not null && swc.GetSubKeyNames().Length > 0)
                return true;
        }
        catch
        {
            // Enum permissions vary. The service registry fallback below is read-only.
        }

        try
        {
            using RegistryKey? services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (services is null)
                return false;

            foreach (string serviceName in services.GetSubKeyNames())
            {
                if (ContainsDolbyFusion(serviceName))
                    return true;

                try
                {
                    using RegistryKey? service = services.OpenSubKey(serviceName);
                    string display = Convert.ToString(service?.GetValue("DisplayName")) ?? string.Empty;
                    string image = Convert.ToString(service?.GetValue("ImagePath")) ?? string.Empty;
                    if (ContainsDolbyFusion(display) || ContainsDolbyFusion(image))
                        return true;
                }
                catch
                {
                    // One inaccessible service must not turn an Audio page probe into
                    // a failure or trigger an elevated/background discovery path.
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool ContainsDolbyFusion(string value) =>
        value.Contains("Dolby", StringComparison.OrdinalIgnoreCase) &&
        (value.Contains("Fusion", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("FUSIONAPOSVC", StringComparison.OrdinalIgnoreCase));
}
