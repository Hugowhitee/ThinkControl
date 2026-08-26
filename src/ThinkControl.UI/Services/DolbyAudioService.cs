using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

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
public sealed record DolbyLaunchResult(bool Success, string Detail);

/// <summary>
/// Installation/launcher and non-mutating provider detection. Direct DAX state is
/// owned by DolbyDirectControlService; modern Fusion profile selection is an
/// explicit user action owned by DolbyAccessProfileBridge.
/// </summary>
public sealed class DolbyAudioService
{
    private const string AppUserModelId = "DolbyLaboratories.DolbyAccess_rz1tebttyb220!App";
    private const string PackageFamilyName = "DolbyLaboratories.DolbyAccess_rz1tebttyb220";
    private const string StoreUri = "ms-windows-store://pdp/?ProductId=9N0866FS04W8";
    private const string DaxClsid = "{20532D01-15BE-4BB9-A727-CA34555D881C}";
    private const string FusionSwcEnumPath = @"SYSTEM\CurrentControlSet\Enum\SWC\VEN_DOLBY&PID_FUSIONAPOSVC";

    public DolbyAudioStatus Probe()
    {
        bool access = IsDolbyAccessInstalled();
        bool dax = IsDaxBackendPresent();
        bool fusion = IsDolbyFusionBackendPresent();

        string detail;
        if (fusion && dax)
        {
            detail = "Dolby Access/OEM audio is installed. This system exposes Dolby Fusion plus the legacy DAX3 API; ThinkControl prefers verified direct DAX control.";
        }
        else if (fusion)
        {
            detail = access
                ? "Dolby Fusion is active. Profile changes use the official Dolby Access controls on demand because this generation does not expose ThinkControl's verified DAX profile API."
                : "The OEM Dolby Fusion component is installed. Profile selection can try the official Dolby Access app on demand; install/repair remains available if Windows cannot open it.";
        }
        else
        {
            detail = (access, dax) switch
            {
                (true, true) => "Dolby Access and the OEM DAX3 backend are installed. ThinkControl enables only direct controls that the DAX provider exposes safely.",
                (false, true) => "Dolby processing is installed, but Dolby Access was not detected. Direct DAX controls remain available where the driver exposes them.",
                (true, false) => "Dolby Access is installed, but no supported OEM Dolby backend was detected. Lenovo's current audio driver may need repair; ThinkControl will not guess a profile API.",
                _ => "Dolby Access and a supported OEM Dolby backend were not detected. Lenovo's audio driver may also be required for Dolby processing."
            };
        }

        return new DolbyAudioStatus(access, dax, detail, FusionBackendDetected: fusion);
    }

    public bool OpenDolbyAccess()
        => OpenDolbyAccessWithResult().Success;

    public DolbyLaunchResult OpenDolbyAccessWithResult()
    {
        // Use Windows' documented packaged-app activation contract first. This
        // gives us a real HRESULT and process id instead of treating an explorer
        // process launch as proof that Dolby Access opened.
        try
        {
            var manager = (IApplicationActivationManager)(object)new ApplicationActivationManager();
            int result = manager.ActivateApplication(AppUserModelId, null, ActivateOptions.None, out uint processId);
            if (result >= 0)
                return new(true, processId > 0
                    ? $"Dolby Access opened (process {processId})."
                    : "Dolby Access activation completed.");
        }
        catch
        {
        }

        // Older package registration states can still resolve through AppsFolder.
        // Report this as a completed shell handoff, never as indefinite progress.
        try
        {
            string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            Process? process = Process.Start(new ProcessStartInfo(explorer, $"shell:AppsFolder\\{AppUserModelId}") { UseShellExecute = true });
            return process is null
                ? new(false, "Windows could not hand Dolby Access to the packaged-app shell.")
                : new(true, "Dolby Access launch request was handed to Windows.");
        }
        catch
        {
            return new(false, "Windows could not activate Dolby Access. Repair or install the app and try again.");
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
            if (packages?.GetSubKeyNames().Any(name =>
                    name.StartsWith("DolbyLaboratories.DolbyAccess_", StringComparison.OrdinalIgnoreCase)) == true)
            {
                return true;
            }
        }
        catch
        {
        }

        // The package family folder is a cheap second signal and avoids invoking a
        // package-manager process just to render the Audio page.
        try
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(local) && Directory.Exists(Path.Combine(local, "Packages", PackageFamilyName)))
                return true;
        }
        catch
        {
        }

        return false;
    }

    private static bool IsDaxBackendPresent()
    {
        // Current Lenovo packages can expose DAX3API only as a Windows service and
        // APO software component; the legacy automation COM class is not present on
        // the X9 generation. Backend detection must not confuse that with the much
        // narrower direct-control capability checked by DolbyDirectControlService.
        try
        {
            using RegistryKey? service = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\DolbyDAXAPI");
            if (service is not null)
                return true;
        }
        catch
        {
        }

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
        try
        {
            using RegistryKey? swc = Registry.LocalMachine.OpenSubKey(FusionSwcEnumPath);
            if (swc is not null && swc.GetSubKeyNames().Length > 0)
                return true;
        }
        catch
        {
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

    [Flags]
    private enum ActivateOptions
    {
        None = 0
    }

    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private sealed class ApplicationActivationManager
    {
    }

    [ComImport]
    [Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
            ActivateOptions options,
            out uint processId);

        [PreserveSig]
        int ActivateForFile(IntPtr appUserModelId, IntPtr itemArray, IntPtr verb, out uint processId);

        [PreserveSig]
        int ActivateForProtocol(IntPtr appUserModelId, IntPtr itemArray, out uint processId);
    }
}
