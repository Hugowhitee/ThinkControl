using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class LenovoOtherModeFullSpeedSourceTests
{
    [Fact]
    public void FullSpeedSemantic_IsExactX9BooleanAndReadbackGated()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFullSpeedService.cs");

        Assert.Contains("FullSpeedAttributeId = 0x04020000", source, StringComparison.Ordinal);
        Assert.Contains("identity.IsVerifiedX9", source, StringComparison.Ordinal);
        Assert.Contains("LENOVO_OTHER_METHOD", source, StringComparison.Ordinal);
        Assert.Contains("GetFeatureValue", source, StringComparison.Ordinal);
        Assert.Contains("SetFeatureValue", source, StringComparison.Ordinal);
        Assert.Contains("raw > 1", source, StringComparison.Ordinal);
        Assert.Contains("verified != (enabled ? 1u : 0u)", source, StringComparison.Ordinal);
        Assert.Contains("RequiredWriteSupport", source, StringComparison.Ordinal);
        Assert.Contains("capabilityPresent", source, StringComparison.Ordinal);
        Assert.Contains("live direct-ID fallback; capability row omitted", source, StringComparison.Ordinal);
        Assert.DoesNotContain("0x8310257C", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0x831020C0", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MaxCooling_UsesFullSpeedWithoutReenablingRejectedTargetRpmWriter()
    {
        string coordinator = ReadSource("src", "ThinkControl.Service", "LenovoCoolingPolicyCoordinator.cs");
        string targetWriter = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");

        Assert.Contains("LenovoOtherModeFullSpeedService.Read", coordinator, StringComparison.Ordinal);
        Assert.Contains("LenovoOtherModeFullSpeedService.TrySet", coordinator, StringComparison.Ordinal);
        Assert.Contains("profileId == FanCurveDefaults.MaxCoolingId", coordinator, StringComparison.Ordinal);
        Assert.Contains("Performance", coordinator, StringComparison.Ordinal);
        Assert.Contains("_fullSpeedOwned", coordinator, StringComparison.Ordinal);
        Assert.Contains("ClearProfileOverride", coordinator, StringComparison.Ordinal);
        Assert.Contains("RequestFirmwareAuto", coordinator, StringComparison.Ordinal);
        Assert.Contains("Max cooling · Lenovo Other Mode full-speed", coordinator, StringComparison.Ordinal);

        Assert.Contains("DirectTargetRpmWritesPhysicallyAccepted = false", targetWriter, StringComparison.Ordinal);
        Assert.Contains("target-RPM writes held read-only", targetWriter, StringComparison.Ordinal);
    }

    [Fact]
    public void LowerProfiles_DoNotSilentlyStealExternalFullSpeedOwnership()
    {
        string coordinator = ReadSource("src", "ThinkControl.Service", "LenovoCoolingPolicyCoordinator.cs");

        Assert.Contains("fullSpeed.Enabled && !fullSpeedOwned", coordinator, StringComparison.Ordinal);
        Assert.Contains("will not silently disable another utility's fan ownership", coordinator, StringComparison.Ordinal);
        Assert.Contains("if (!wantsFullSpeed && fullSpeedOwned)", coordinator, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitAuto_CanRecoverVerifiedFullSpeedAfterServiceRestartWithoutChangingDisposeSemantics()
    {
        string coordinator = ReadSource("src", "ThinkControl.Service", "LenovoCoolingPolicyCoordinator.cs");
        string service = ReadSource("src", "ThinkControl.Service", "ServiceEngine.cs");

        Assert.Contains("internal bool RequestFirmwareAuto", coordinator, StringComparison.Ordinal);
        Assert.Contains("fullSpeed.Available && fullSpeed.Enabled", coordinator, StringComparison.Ordinal);
        Assert.Contains("LenovoOtherModeFullSpeedService.TrySet(_hardware.Identity, enabled: false", coordinator, StringComparison.Ordinal);

        string autoMethod = Normalize(service)
            .Split("private ServiceResponse ReturnFanToAuto()", StringSplitOptions.None)[1]
            .Split("private ServiceResponse SetCoolingProfile", StringSplitOptions.None)[0];
        Assert.Contains("_coolingPolicy.RequestFirmwareAuto", autoMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("_coolingPolicy.ClearProfileOverride", autoMethod, StringComparison.Ordinal);

        // Automatic service disposal remains ownership-aware and does not use the
        // wider explicit-user recovery operation that may clear a stale prior-instance bit.
        string disposeMethod = Normalize(service)
            .Split("public void Dispose()", StringSplitOptions.None)[1];
        Assert.Contains("_coolingPolicy.ClearProfileOverride", disposeMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("RequestFirmwareAuto", disposeMethod, StringComparison.Ordinal);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string ReadSource(params string[] path)
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([root, .. path]));
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new(start);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.Hardware")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for Lenovo full-speed validation.");
    }
}
