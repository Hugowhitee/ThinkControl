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
    string ServiceDetail,
    string LowLevelAccessDetail)
{
    internal bool NeedsAttention => !ServiceRunning || (LowLevelAccessRelevant && !LowLevelAccessInstalled);
}

internal sealed record HardwareSetupResult(bool Success, bool RestartRequired, string Message);

/// <summary>
/// Repairs ThinkControl's own service and installs optional device prerequisites.
/// The main installer stays generic; device-specific write access is still gated
/// by verified profiles, while PawnIO may also be offered as a read-only sensor
/// prerequisite when LibreHardwareMonitor cannot see useful telemetry.
/// </summary>
internal sealed class HardwareSetupService
{
    private const string ServiceName = "ThinkControlService";
    private const string PawnIoServiceName = "PawnIO";
    private const string PawnIoVersion = "2.2.0";
    private const string PawnIoUrl = "https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe";
    private const string PawnIoSha256 = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    internal async Task<HardwareSetupStatus> ReadStatusAsync(string? machineType, bool sensorProviderNeeded = false)
    {
        ServiceQuery service = await QueryServiceAsync(ServiceName).ConfigureAwait(false);
        bool verifiedWriteProfile = IsVerifiedEcProfile(machineType);
        bool lowLevelRelevant = verifiedWriteProfile || sensorProviderNeeded;
        ServiceQuery pawnIo = lowLevelRelevant
            ? await QueryServiceAsync(PawnIoServiceName).ConfigureAwait(false)
            : default;
        bool pawnIoReady = !lowLevelRelevant || (pawnIo.Exists && pawnIo.Running);

        string lowLevelDetail = !lowLevelRelevant
            ? "Not currently required by detected capabilities"
            : pawnIo.Running
                ? "PawnIO running · low-level sensor/EC provider ready"
                : pawnIo.Exists
                    ? "PawnIO is installed but its kernel driver is not running. Repair it or restart Windows before hardware access can be used."
                    : verifiedWriteProfile
                        ? "Recommended for X9 sensors and required by the verified EC fan provider"
                        : "Recommended for additional read-only sensor discovery; fan writes remain locked until a device profile is verified";

        return new HardwareSetupStatus(
            ServiceInstalled: service.Exists,
            ServiceRunning: service.Running,
            LowLevelAccessRelevant: lowLevelRelevant,
            // This property historically meant "installed" in the UI. Treat it as
            // ready here: a registered but stopped kernel driver cannot provide
            // sensors/EC and must stay visibly actionable instead of showing green.
            LowLevelAccessInstalled: pawnIoReady,
            ServiceDetail: service.Running
                ? "Running"
                : service.Exists ? "Installed but not running" : "Not registered",
            LowLevelAccessDetail: lowLevelDetail);
    }

    internal async Task<HardwareSetupResult> RepairServiceAsync()
    {
        string? uiPath = Environment.ProcessPath;
        string? uiDirectory = string.IsNullOrWhiteSpace(uiPath) ? null : Path.GetDirectoryName(uiPath);
        string? root = uiDirectory is null ? null : Directory.GetParent(uiDirectory)?.FullName;
        string? serviceExe = root is null ? null : Path.Combine(root, "service", "ThinkControl.Service.exe");

        if (string.IsNullOrWhiteSpace(serviceExe) || !File.Exists(serviceExe))
            return new(false, false, "The installed ThinkControl hardware service executable could not be found. Reinstall ThinkControl to restore the application payload.");

        string escapedExe = serviceExe.Replace("\"", "\"\"");
        string command =
            $"sc.exe create {ServiceName} binPath= \"\\\"{escapedExe}\\\"\" start= auto DisplayName= \"ThinkControl Hardware Service\" >nul 2>&1 & " +
            $"sc.exe config {ServiceName} binPath= \"\\\"{escapedExe}\\\"\" start= auto DisplayName= \"ThinkControl Hardware Service\" >nul 2>&1 & " +
            $"sc.exe failure {ServiceName} reset= 86400 actions= restart/5000 >nul 2>&1 & " +
            $"sc.exe start {ServiceName} >nul 2>&1";

        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /s /c \"" + command.Replace("\"", "\\\"") + "\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (process is null)
                return new(false, false, "Windows could not start the hardware service repair.");

            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.Delay(700).ConfigureAwait(false);
            ServiceQuery after = await QueryServiceAsync(ServiceName).ConfigureAwait(false);
            return after.Running
                ? new(true, false, "ThinkControl hardware service is running.")
                : new(false, false, "The hardware service is still not running. Reinstall ThinkControl or check Windows Services for ThinkControl Hardware Service.");
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
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = installer,
                Arguments = "-install -silent",
                UseShellExecute = true,
                Verb = "runas"
            });
            if (process is null)
                return new(false, false, "Windows could not start the hardware component installer.");

            await process.WaitForExitAsync().ConfigureAwait(false);
            bool restart = process.ExitCode == 3010;
            if (process.ExitCode is not 0 and not 3010)
                return new(false, false, $"The hardware component installer returned exit code {process.ExitCode}.");

            await Task.Delay(900).ConfigureAwait(false);
            ServiceQuery after = await QueryServiceAsync(PawnIoServiceName).ConfigureAwait(false);
            if (after.Running)
            {
                return new(true, restart, restart
                    ? "PawnIO is running, but Windows also requested a restart to complete the driver update."
                    : "PawnIO is running. ThinkControl will recycle sensor providers and retry discovery automatically.");
            }

            if (after.Exists)
            {
                return new(true, true,
                    "PawnIO is installed but the kernel driver is not active yet. Restart Windows, then ThinkControl will retry sensors and the verified X9 EC provider automatically.");
            }

            return new(false, restart,
                "The component installer finished, but Windows does not report PawnIO. Restart Windows or run Hardware setup again.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new(false, false, "Hardware component installation was cancelled.");
        }
        catch (Exception ex)
        {
            return new(false, false, $"Hardware component installation failed: {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(installer)) File.Delete(installer); } catch { }
        }
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
}
