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
        ServiceQuery pawnIo = lowLevelRelevant
            ? await QueryServiceAsync(PawnIoServiceName).ConfigureAwait(false)
            : default;

        // PawnIO is a demand-start kernel driver. Being installed but currently
        // STOPPED is not a failure: LHM/PawnIO can activate the device on demand.
        bool pawnIoInstalled = !lowLevelRelevant || pawnIo.Exists;

        string lowLevelDetail = !lowLevelRelevant
            ? "Not currently required by detected capabilities"
            : pawnIo.Running
                ? "Installed · driver active · provider access can be probed"
                : pawnIo.Exists
                    ? "Installed · demand-start driver idle until LibreHardwareMonitor or the verified EC provider opens it"
                    : verifiedWriteProfile
                        ? "Missing · required for X9 sensor discovery and the verified EC fan provider"
                        : "Missing · install it for additional LibreHardwareMonitor sensor discovery";

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
            LowLevelAccessInstalled: pawnIoInstalled,
            LowLevelAccessRunning: pawnIo.Running,
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
            return new(false, false, "The installed ThinkControl hardware service executable could not be found. Reinstall ThinkControl to restore the application payload.");

        string escapedExe = serviceExe.Replace("\"", "\"\"");
        string command =
            $"sc.exe stop {ServiceName} >nul 2>&1 & " +
            "timeout /t 1 /nobreak >nul 2>&1 & " +
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
                ? new(true, false, "ThinkControl hardware service was restarted. ThinkControl will verify the app connection and providers next.")
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
            if (after.Exists)
            {
                return new(true, restart,
                    restart
                        ? "PawnIO is installed. Windows requested a restart; after reboot ThinkControl will refresh LibreHardwareMonitor/PawnIO automatically."
                        : "PawnIO is installed. Its demand-start driver will activate when ThinkControl opens the LibreHardwareMonitor provider; refreshing providers now is usually enough.");
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
