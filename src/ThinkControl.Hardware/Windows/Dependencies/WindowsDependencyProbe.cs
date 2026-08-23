using Microsoft.Win32;
using ThinkControl.Core.Dependencies;

namespace ThinkControl.Hardware.Windows.Dependencies;

/// <summary>
/// Read-only probe for dependency/service presence. This class intentionally does
/// not install, start, stop or repair software. Mutation belongs to the elevated
/// installer/service flow and must be explicit.
/// </summary>
public sealed class WindowsDependencyProbe
{
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

    public IReadOnlyList<DependencyStatus> ProbeKnownComponents()
    {
        return
        [
            ProbeDotNetRuntime(),
            ProbeService(DependencyId.ThinkControlService, "ThinkControl.Service"),
            ProbeService(DependencyId.PawnIo, "PawnIO"),
            ProbeService(DependencyId.LenovoIntelligentThermalSolution, "LITSSVC"),
            ProbeService(DependencyId.LenovoPowerManagement, "IBMPMSVC"),
            ProbeService(DependencyId.LenovoVantage, "LenovoVantageService"),
            ProbeIntelInnovationPlatformFramework(),
            Unknown(DependencyId.LenovoServiceBridge,
                "Detection is not yet verified; ThinkControl does not assume a Lenovo Service Bridge service name.")
        ];
    }

    public static HardwareReadiness EvaluateReadiness(
        IEnumerable<DependencyStatus> statuses,
        bool pawnIoRequiredForVerifiedDevice)
    {
        var byId = statuses.ToDictionary(status => status.Definition.Id);

        if (IsMissingOrUnhealthy(byId, DependencyId.DotNetDesktopRuntime) ||
            IsMissingOrUnhealthy(byId, DependencyId.ThinkControlService))
        {
            return HardwareReadiness.NeedsAttention;
        }

        // OEM components only elevate the whole-device state when the probe knows
        // they are expected and positively reports a problem. Unknown means that
        // the backend has not yet been verified and should simply stay disabled.
        var oemProblem = byId.Values.Any(status =>
            status.Definition.Requirement == DependencyRequirement.OemPlatform &&
            status.State is DependencyState.Missing or DependencyState.Outdated or DependencyState.Unhealthy);

        if (oemProblem)
        {
            return HardwareReadiness.NeedsAttention;
        }

        if (pawnIoRequiredForVerifiedDevice &&
            (!byId.TryGetValue(DependencyId.PawnIo, out var pawnIo) || !pawnIo.IsReady))
        {
            return HardwareReadiness.Limited;
        }

        return HardwareReadiness.Full;
    }

    private static DependencyStatus ProbeDotNetRuntime()
    {
        var definition = DependencyCatalog.Get(DependencyId.DotNetDesktopRuntime);
        var version = Environment.Version;

        return new DependencyStatus(
            definition,
            version.Major >= 10 ? DependencyState.Present : DependencyState.Outdated,
            version.ToString(),
            version.Major >= 10
                ? "Current process is running on a supported .NET major version."
                : "ThinkControl requires the .NET 10 Desktop Runtime.");
    }

    private static DependencyStatus ProbeService(DependencyId id, string serviceName)
    {
        var definition = DependencyCatalog.Get(id);

        try
        {
            using var services = Registry.LocalMachine.OpenSubKey(ServicesKey, writable: false);
            using var service = services?.OpenSubKey(serviceName, writable: false);

            if (service is null)
            {
                return new DependencyStatus(
                    definition,
                    DependencyState.Missing,
                    Detail: $"Windows service '{serviceName}' was not found.");
            }

            var imagePath = service.GetValue("ImagePath") as string;
            var displayName = service.GetValue("DisplayName") as string;

            return new DependencyStatus(
                definition,
                DependencyState.Present,
                Detail: BuildServiceDetail(serviceName, displayName, imagePath));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new DependencyStatus(
                definition,
                DependencyState.Unknown,
                Detail: $"Service presence could not be read: {ex.GetType().Name}.");
        }
    }

    private static DependencyStatus ProbeIntelInnovationPlatformFramework()
    {
        var definition = DependencyCatalog.Get(DependencyId.IntelInnovationPlatformFramework);

        try
        {
            using var services = Registry.LocalMachine.OpenSubKey(ServicesKey, writable: false);
            if (services is null)
            {
                return Unknown(DependencyId.IntelInnovationPlatformFramework, "Windows service registry was unavailable.");
            }

            foreach (var serviceName in services.GetSubKeyNames())
            {
                using var service = services.OpenSubKey(serviceName, writable: false);
                var displayName = service?.GetValue("DisplayName") as string;

                if (displayName?.Contains("Innovation Platform Framework", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return new DependencyStatus(
                        definition,
                        DependencyState.Present,
                        Detail: $"Detected Windows service '{serviceName}' ({displayName}).");
                }
            }

            // IPF consists of more than a single stable service name across OEM
            // packages, so absence of this heuristic is not proof that IPF is missing.
            return Unknown(
                DependencyId.IntelInnovationPlatformFramework,
                "No service display name matched Intel Innovation Platform Framework; driver-level detection is still pending.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Unknown(
                DependencyId.IntelInnovationPlatformFramework,
                $"Platform service detection could not be read: {ex.GetType().Name}.");
        }
    }

    private static bool IsMissingOrUnhealthy(
        IReadOnlyDictionary<DependencyId, DependencyStatus> statuses,
        DependencyId id)
    {
        return !statuses.TryGetValue(id, out var status) ||
               status.State is DependencyState.Missing or DependencyState.Outdated or DependencyState.Unhealthy;
    }

    private static DependencyStatus Unknown(DependencyId id, string detail) =>
        new(DependencyCatalog.Get(id), DependencyState.Unknown, Detail: detail);

    private static string BuildServiceDetail(string serviceName, string? displayName, string? imagePath)
    {
        var label = string.IsNullOrWhiteSpace(displayName) ? serviceName : displayName;
        return string.IsNullOrWhiteSpace(imagePath)
            ? $"Detected Windows service '{serviceName}' ({label})."
            : $"Detected Windows service '{serviceName}' ({label}); image path is registered.";
    }
}
