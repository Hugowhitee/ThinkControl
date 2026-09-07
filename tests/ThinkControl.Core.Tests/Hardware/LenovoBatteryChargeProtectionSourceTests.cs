using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class LenovoBatteryChargeProtectionSourceTests
{
    [Fact]
    public void X9BatteryCare_UsesOnlyDocumentedStandardAndLongLifeSemantic()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoBatteryChargeProtectionService.cs");

        Assert.Contains("ChargeTypeAttributeId = 0x03010001", source, StringComparison.Ordinal);
        Assert.Contains("ChargeTypeStandard = 0", source, StringComparison.Ordinal);
        Assert.Contains("ChargeTypeLongLife = 1", source, StringComparison.Ordinal);
        Assert.Contains("limitPercent is not 80 and not 100", source, StringComparison.Ordinal);
        Assert.Contains("if (!identity.IsVerifiedX9)", source, StringComparison.Ordinal);
        Assert.Contains("RequiredWriteSupport = SupportValid | SupportGet | SupportSet", source, StringComparison.Ordinal);
        Assert.Contains("capabilityPresent &&", source, StringComparison.Ordinal);
        Assert.Contains("TryGetFeatureValue(method, ChargeTypeAttributeId", source, StringComparison.Ordinal);
        Assert.Contains("verified != requested", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BatteryThreshold", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SetChargeLimit", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ServiceAndClient_ExposeSemanticBatteryCareWithoutRawWmiPassthrough()
    {
        string contract = ReadSource("src", "ThinkControl.Core", "Ipc", "TelemetryContracts.cs");
        string service = ReadSource("src", "ThinkControl.Service", "ServiceEngine.cs");
        string client = ReadSource("src", "ThinkControl.UI", "Services", "HardwareServiceClient.cs");

        Assert.Contains("BatteryChargeProtection = false", contract, StringComparison.Ordinal);
        Assert.Contains("BatteryChargeLimitPercent", contract, StringComparison.Ordinal);
        Assert.Contains("\"SetBatteryChargeLimit\" => SetBatteryChargeLimit(request.Value)", service, StringComparison.Ordinal);
        Assert.Contains("LenovoBatteryChargeProtectionService.TrySetLimit", service, StringComparison.Ordinal);
        Assert.Contains("BatteryChargeProtection: batteryProtection.Available && batteryProtection.Writable", service, StringComparison.Ordinal);
        Assert.Contains("SetBatteryChargeLimitAsync(int percent", client, StringComparison.Ordinal);
        Assert.Contains("SendTrackedAsync(\"SetBatteryChargeLimit\"", client, StringComparison.Ordinal);
        Assert.DoesNotContain("SetFeatureValue", client, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for battery charge protection validation.");
    }
}
