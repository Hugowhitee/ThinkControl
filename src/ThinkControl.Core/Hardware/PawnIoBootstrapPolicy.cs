namespace ThinkControl.Core.Hardware;

public enum PawnIoBootstrapState
{
    NotRequired,
    MissingRegistration,
    UnknownVersion,
    IncompatibleVersion,
    MissingKernelService,
    ReadyForProviderProbe
}

public readonly record struct PawnIoBootstrapReadiness(
    PawnIoBootstrapState State,
    bool Required,
    bool CompatibleRegistration,
    bool KernelServiceRegistered,
    bool KernelServiceRunning)
{
    public bool NeedsInstallOrRepair => Required && State != PawnIoBootstrapState.ReadyForProviderProbe;
    public bool Ready => !Required || State == PawnIoBootstrapState.ReadyForProviderProbe;
}

/// <summary>
/// Pure bootstrap policy for PawnIO. A compatible uninstall registration alone is
/// never treated as a usable installation: the kernel service must also exist.
/// The service may legitimately be stopped because PawnIO is demand-started; the
/// real provider/device open remains the final capability gate in Hardware.
/// </summary>
public static class PawnIoBootstrapPolicy
{
    public static PawnIoBootstrapReadiness Evaluate(
        bool required,
        bool uninstallRegistered,
        Version? installedVersion,
        Version minimumVersion,
        bool kernelServiceRegistered,
        bool kernelServiceRunning)
    {
        if (!required)
        {
            return new PawnIoBootstrapReadiness(
                PawnIoBootstrapState.NotRequired,
                Required: false,
                CompatibleRegistration: true,
                KernelServiceRegistered: kernelServiceRegistered,
                KernelServiceRunning: kernelServiceRunning);
        }

        if (!uninstallRegistered)
        {
            return new PawnIoBootstrapReadiness(
                PawnIoBootstrapState.MissingRegistration,
                Required: true,
                CompatibleRegistration: false,
                KernelServiceRegistered: kernelServiceRegistered,
                KernelServiceRunning: kernelServiceRunning);
        }

        if (installedVersion is null)
        {
            return new PawnIoBootstrapReadiness(
                PawnIoBootstrapState.UnknownVersion,
                Required: true,
                CompatibleRegistration: false,
                KernelServiceRegistered: kernelServiceRegistered,
                KernelServiceRunning: kernelServiceRunning);
        }

        if (installedVersion < minimumVersion)
        {
            return new PawnIoBootstrapReadiness(
                PawnIoBootstrapState.IncompatibleVersion,
                Required: true,
                CompatibleRegistration: false,
                KernelServiceRegistered: kernelServiceRegistered,
                KernelServiceRunning: kernelServiceRunning);
        }

        if (!kernelServiceRegistered)
        {
            return new PawnIoBootstrapReadiness(
                PawnIoBootstrapState.MissingKernelService,
                Required: true,
                CompatibleRegistration: true,
                KernelServiceRegistered: false,
                KernelServiceRunning: false);
        }

        return new PawnIoBootstrapReadiness(
            PawnIoBootstrapState.ReadyForProviderProbe,
            Required: true,
            CompatibleRegistration: true,
            KernelServiceRegistered: true,
            KernelServiceRunning: kernelServiceRunning);
    }
}
