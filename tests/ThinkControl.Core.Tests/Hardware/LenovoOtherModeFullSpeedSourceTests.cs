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
