using ThinkControl.Core.Hardware;
using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class PawnIoBootstrapPolicyTests
{
    private static readonly Version Minimum = new(2, 2, 0);

    [Fact]
    public void CompatibleRegistryEntryWithoutKernelService_RequiresRepair()
    {
        PawnIoBootstrapReadiness readiness = PawnIoBootstrapPolicy.Evaluate(
            required: true,
            uninstallRegistered: true,
            installedVersion: new Version(2, 2, 0),
            minimumVersion: Minimum,
            kernelServiceRegistered: false,
            kernelServiceRunning: false);

        Assert.Equal(PawnIoBootstrapState.MissingKernelService, readiness.State);
        Assert.True(readiness.CompatibleRegistration);
        Assert.True(readiness.NeedsInstallOrRepair);
        Assert.False(readiness.Ready);
    }

    [Fact]
    public void DemandStartKernelServiceMayBeStoppedBeforeProviderOpensDevice()
    {
        PawnIoBootstrapReadiness readiness = PawnIoBootstrapPolicy.Evaluate(
            required: true,
            uninstallRegistered: true,
            installedVersion: new Version(2, 2, 0),
            minimumVersion: Minimum,
            kernelServiceRegistered: true,
            kernelServiceRunning: false);

        Assert.Equal(PawnIoBootstrapState.ReadyForProviderProbe, readiness.State);
        Assert.True(readiness.Ready);
        Assert.False(readiness.NeedsInstallOrRepair);
    }

    [Theory]
    [InlineData(false, null, PawnIoBootstrapState.MissingRegistration)]
    [InlineData(true, null, PawnIoBootstrapState.UnknownVersion)]
    public void MissingOrUnverifiableRegistration_IsNotReady(
        bool registered,
        string? version,
        PawnIoBootstrapState expected)
    {
        PawnIoBootstrapReadiness readiness = PawnIoBootstrapPolicy.Evaluate(
            required: true,
            uninstallRegistered: registered,
            installedVersion: version is null ? null : Version.Parse(version),
            minimumVersion: Minimum,
            kernelServiceRegistered: true,
            kernelServiceRunning: false);

        Assert.Equal(expected, readiness.State);
        Assert.True(readiness.NeedsInstallOrRepair);
    }

    [Fact]
    public void OldVersion_IsNotReadyEvenWhenKernelServiceExists()
    {
        PawnIoBootstrapReadiness readiness = PawnIoBootstrapPolicy.Evaluate(
            required: true,
            uninstallRegistered: true,
            installedVersion: new Version(2, 1, 0),
            minimumVersion: Minimum,
            kernelServiceRegistered: true,
            kernelServiceRunning: true);

        Assert.Equal(PawnIoBootstrapState.IncompatibleVersion, readiness.State);
        Assert.True(readiness.NeedsInstallOrRepair);
    }

    [Fact]
    public void IrrelevantCapability_DoesNotDemandPawnIo()
    {
        PawnIoBootstrapReadiness readiness = PawnIoBootstrapPolicy.Evaluate(
            required: false,
            uninstallRegistered: false,
            installedVersion: null,
            minimumVersion: Minimum,
            kernelServiceRegistered: false,
            kernelServiceRunning: false);

        Assert.Equal(PawnIoBootstrapState.NotRequired, readiness.State);
        Assert.True(readiness.Ready);
        Assert.False(readiness.NeedsInstallOrRepair);
    }
}
