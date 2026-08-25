using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace ThinkControl.UI.Services;

internal sealed record HardwareSetupStatus(
    bool ServiceInstalled,
    bool ServiceRunning,
    bool LowLevelAccessRelevant,
    bool LowLevelAccessInstalled,
    bool LowLevelAccessRunning,
    string ServiceDetail,
    string LowLevelAccessDetail,
    bool ServiceReachable = false)
{
    internal bool NeedsAttention =>
        !ServiceRunning ||
        !ServiceReachable ||
        (LowLevelAccessRelevant && !LowLevelAccessInstalled);
}

internal sealed record HardwareSetupResult(bool Success, bool RestartRequired, string Message);

internal sealed class HardwareSetupService
{
    private const string ServiceName = "ThinkControlService";
    private const string PawnIoServiceName = "PawnIO";
    private const string PawnIoVersion = "2.2.0";
    private const int ServiceRepairIpcFailure = 26;
    private static readonly Version MinimumPawnIoVersion = new(2, 2, 0);
    private const string PawnIoUrl = "https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe";
    private const string PawnIoSha256 = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    internal async Task<HardwareSetupStatus> ReadStatusAsync(
        string? machineType,
        bool sensorProviderNeeded = false,
        bool serviceReachable = false)
    {
        ServiceQuery service = await QueryServiceAsync(ServiceName).ConfigureAwait(false);
        bool verifiedWriteProfile = IsVerifiedEcProfile(machineType);
        bool lowLevelRelevant = verifiedWriteProfile || sensorProviderNeeded;

        PawnIoInstallState pawnIoInstall = lowLevelRelevant ? ReadPawnIoInstallState() : default;
        ServiceQuery pawnIoDriver = lowLevelRelevant
            ? await QueryServiceAsync(PawnIoServiceName).ConfigureAwait(false)
            : default;

        // Match LibreHardwareMonitor's own readiness contract: PawnIO installation is
        // identified by its uninstall registration and DisplayVersion, not by whether
        // the demand-start kernel driver happens to be RUNNING at this instant.
        bool pawnIoCompatible = !lowLevelRelevant ||
                                (pawnIoInstall.Installed && pawnIoInstall.Version is not null && pawnIoInstall.Version >= MinimumPawnIoVersion);

        string lowLevelDetail = !lowLevelRelevant
            ? "Not currently required by detected capabilities"
            : !pawnIoInstall.Installed
                ? verifiedWriteProfile
                    ? "Missing · required for X9 sensor discovery and the verified EC fan provider"
                    : "Missing · install it for additional LibreHardwareMonitor sensor discovery"
                : pawnIoInstall.Version is null
                    ? "Installed · version could not be verified · repair recommended"
                    : pawnIoInstall.Version < MinimumPawnIoVersion
                        ? $"Installed {pawnIoInstall.Version} · PawnIO {PawnIoVersion} or newer is required"
                        : pawnIoDriver.Running
                            ? $"Installed {pawnIoInstall.Version} · driver active"
                            : $"Installed {pawnIoInstall.Version} · demand-start driver idle until a provider opens it";

        string serviceDetail = service.Running
            ? serviceReachable
                ? "Running · ThinkControl app connection ready"
                : "Running in Windows · app connection is not responding"
            : service.Exists
                ? "Installed but not running"
                : "Not registered";

        return new HardwareSetupStatus(
            ServiceInstalled: service.Exists,
            ServiceRunning: service.Running,
            LowLevelAccessRelevant: lowLevelRelevant,
            LowLevelAccessInstalled: pawnIoCompatible,
            LowLevelAccessRunning: pawnIoDriver.Running,
            ServiceDetail: serviceDetail,
            LowLevelAccessDetail: lowLevelDetail,
            ServiceReachable: service.Running && serviceReachable);
    }

    internal async Task<HardwareSetupResult> RepairServiceAsync()
    {
        string? uiPath = Environment.ProcessPath;
        string? uiDirectory = string.IsNullOrWhiteSpace(uiPath) ? null : Path.GetDirectoryName(uiPath);
        string? root = uiDirectory is null ? null : Directory.GetParent(uiDirectory)?.FullName;
        string? serviceExe = root is null ? null : Path.Combine(root, "service", "ThinkControl.Service.exe");

        if (string.IsNullOrWhiteSpace(serviceExe) || !File.Exists(serviceExe))
        {
            return new(false, false,
                "The installed ThinkControl hardware service executable could not be found. Reinstall ThinkControl to restore the missing application payload.");
        }

        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = serviceExe,
                Arguments = "--repair-service",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (process is null)
                return new(false, false, "Windows could not start the ThinkControl hardware service repair.");

            await process.WaitForExitAsync().ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                if (process.ExitCode == ServiceRepairIpcFailure)
                {
                    return new(false, false,
                        "Windows started the hardware service, but its local ThinkControl IPC handshake never became responsive. The installed files were not replaced. Retry once; if it repeats, review the hardware-service log in Diagnostics.");
                }

                return new(false, false,
                    $"Hardware service repair returned code {process.ExitCode}. The installed application payload was left unchanged; review Diagnostics before reinstalling.");
            }

            await Task.Delay(500).ConfigureAwait(false);
            ServiceQuery after = await QueryServiceAsync(ServiceName).ConfigureAwait(false);
            return after.Running
                ? new(true, false, "ThinkControl hardware service and local app connection were verified. Checking hardware providers now…")
                : new(false, false, "The hardware service repair completed, but Windows still reports the service as stopped.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new(false, false, "Hardware service repair was cancelled.");
        }
        catch (Exception ex)
        {
            return new(false, false, $"Hardware service repair failed: {ex.Message}");
        }
    }

    internal async Task<HardwareSetupResult> InstallLowLevelAccessAsync()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "ThinkControl", "hardware-setup");
        string installer = Path.Combine(tempDirectory, $"PawnIO-{PawnIoVersion}.exe");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            byte[] payload = await Http.GetByteArrayAsync(PawnIoUrl).ConfigureAwait(false);
            string hash = Convert.ToHexString(SHA256.HashData(payload));
            if (!hash.Equals(PawnIoSha256, StringComparison.OrdinalIgnoreCase))
                return new(false, false, "The downloaded hardware component failed SHA-256 verification and was not started.");

            await File.WriteAllBytesAsync(installer, payload).ConfigureAwait(false);

            // Use the exact public PawnIO installer mode used by LibreHardwareMonitor.
            // The previous extra -silent switch hid the only useful installation UX
            // and made a failed UAC/driver install look like a button that did nothing.
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = installer,
                Arguments = "-install",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = tempDirectory
            });
            if (process is null)
                return new(false, false, "Windows could not start the PawnIO installer.");

            await process.WaitForExitAsync().ConfigureAwait(false);
            bool restart = process.ExitCode == 3010;
            if (process.ExitCode is not 0 and not 3010)
                return new(false, false, $"PawnIO installation returned exit code {process.ExitCode}.");

            await Task.Delay(700).ConfigureAwait(false);
            PawnIoInstallState after = ReadPawnIoInstallState();
            if (after.Installed && after.Version is not null && after.Version >= MinimumPawnIoVersion)
            {
                return new(true, restart,
                    restart
                        ? $"PawnIO {after.Version} is installed. Windows requested a restart; ThinkControl will refresh providers after reboot."
                        : $"PawnIO {after.Version} is installed and verified. ThinkControl can refresh hardware providers now.");
            }

            return new(false, restart,
                after.Installed
                    ? "PawnIO is registered, but its installed version could not be verified as compatible. Run the repair again or restart Windows."
                    : "The PawnIO installer finished, but Windows does not report the installation. Restart Windows or run the repair again.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new(false, false, "PawnIO installation was cancelled.");
        }
        catch (Exception ex)
        {
            return new(false, false, $"PawnIO installation failed: {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(installer)) File.Delete(installer); } catch { }
        }
    }

    private static PawnIoInstallState ReadPawnIoInstallState()
    {
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using RegistryKey? key = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO", writable: false);
                if (key is null)
                    continue;

                string? rawVersion = key.GetValue("DisplayVersion") as string;
                Version? version = Version.TryParse(rawVersion, out Version? parsed) ? parsed : null;
                return new PawnIoInstallState(true, version);
            }
            catch
            {
                // Try the other registry view before declaring it unavailable.
            }
        }

        return new PawnIoInstallState(false, null);
    }

    private static bool IsVerifiedEcProfile(string? machineType) =>
        string.Equals(machineType, "21Q6", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(machineType, "21Q7", StringComparison.OrdinalIgnoreCase);

    private static async Task<ServiceQuery> QueryServiceAsync(string serviceName)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
                    Arguments = $"query {serviceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (process.ExitCode != 0)
                return new(false, false);
            return new(true, output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return new(false, false);
        }
    }

    private readonly record struct ServiceQuery(bool Exists, bool Running);
    private readonly record struct PawnIoInstallState(bool Installed, Version? Version);
}
