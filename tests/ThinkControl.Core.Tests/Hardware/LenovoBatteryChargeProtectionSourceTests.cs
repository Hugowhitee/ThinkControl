using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class LenovoBatteryChargeProtectionSourceTests
{
    [Fact]
    public void X9BatteryPreservation_UsesBoundedLenovoPmThresholdProtocol()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoBatteryChargeProtectionService.cs");

        Assert.Contains("RegistryRoot = @\"SOFTWARE\\WOW6432Node\\Lenovo\\PWRMGRV\\ConfKeys\\Data\"", source, StringComparison.Ordinal);
        Assert.Contains("DriverPath = @\"\\\\.\\IBMPmDrv\"", source, StringComparison.Ordinal);
        Assert.Contains("IoctlSetChargeMode = 0x0022261C", source, StringComparison.Ordinal);
        Assert.Contains("IoctlSetStart = 0x00222630", source, StringComparison.Ordinal);
        Assert.Contains("IoctlSetStop = 0x00222638", source, StringComparison.Ordinal);
        Assert.Contains("PrimaryBattery = 0x00000100", source, StringComparison.Ordinal);
        Assert.Contains("ThresholdMode = 0x00000101", source, StringComparison.Ordinal);
        Assert.Contains("AutomaticMode = 0x00000000", source, StringComparison.Ordinal);
        Assert.Contains("DriverRejected = 0x80000000", source, StringComparison.Ordinal);
        Assert.Contains("if (!identity.IsVerifiedX9)", source, StringComparison.Ordinal);
        Assert.Contains("MinimumStartPercent = 40", source, StringComparison.Ordinal);
        Assert.Contains("MaximumStopPercent = 95", source, StringComparison.Ordinal);
        Assert.Contains("ThresholdStepPercent = 5", source, StringComparison.Ordinal);
        Assert.Contains("startPercent >= stopPercent", source, StringComparison.Ordinal);
        Assert.Contains("SendDriverCommand(handle, IoctlSetStop, PrimaryBattery", source, StringComparison.Ordinal);
        Assert.Contains("SendDriverCommand(handle, IoctlSetStart, PrimaryBattery", source, StringComparison.Ordinal);
        Assert.Contains("SendDriverCommand(handle, IoctlSetChargeMode, AutomaticMode", source, StringComparison.Ordinal);
        Assert.Contains("RestorePreviousState(before)", source, StringComparison.Ordinal);
        Assert.Contains("PWRMGRV readback verified", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetFeatureValue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LENOVO_OTHER_METHOD", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceAndClient_ExposeOnlySemanticThresholdPairs()
    {
        string contract = ReadSource("src", "ThinkControl.Core", "Ipc", "TelemetryContracts.cs");
        string service = ReadSource("src", "ThinkControl.Service", "ServiceEngine.cs");
        string client = ReadSource("src", "ThinkControl.UI", "Services", "HardwareServiceClient.cs");

        Assert.Contains("BatteryChargeProtection = false", contract, StringComparison.Ordinal);
        Assert.Contains("BatteryCustomChargeThresholds = false", contract, StringComparison.Ordinal);
        Assert.Contains("BatteryChargeProtectionEnabled", contract, StringComparison.Ordinal);
        Assert.Contains("BatteryChargeStartPercent", contract, StringComparison.Ordinal);
        Assert.Contains("BatteryChargeStopPercent", contract, StringComparison.Ordinal);
        Assert.Contains("\"SetBatteryChargeLimit\" => SetBatteryChargeLimit(request.Value)", service, StringComparison.Ordinal);
        Assert.Contains("LenovoBatteryChargeProtectionService.TrySetThresholds", service, StringComparison.Ordinal);
        Assert.Contains("LenovoBatteryChargeProtectionService.TryDisable", service, StringComparison.Ordinal);
        Assert.Contains("BatteryCustomChargeThresholds:", service, StringComparison.Ordinal);
        Assert.Contains("SetBatteryChargeThresholdsAsync", client, StringComparison.Ordinal);
        Assert.Contains("DisableBatteryChargeThresholdsAsync", client, StringComparison.Ordinal);
        Assert.Contains("$\"{startPercent},{stopPercent}\"", client, StringComparison.Ordinal);
        Assert.DoesNotContain("DeviceIoControl", client, StringComparison.Ordinal);
        Assert.DoesNotContain("IBMPmDrv", client, StringComparison.Ordinal);
    }

    [Fact]
    public void BatteryUi_UsesSmallPresetsAndDoesNotInventCycleSavings()
    {
        string xaml = ReadSource("src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.xaml");
        string panel = ReadSource("src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.ProtectionAndHistory.cs");
        string mainPanel = ReadSource("src", "ThinkControl.UI", "Controls", "BatteryTelemetryPanel.xaml.cs");

        Assert.Contains("Daily · 75–85% (recommended)", xaml, StringComparison.Ordinal);
        Assert.Contains("Desk · 55–80%", xaml, StringComparison.Ordinal);
        Assert.Contains("Maximum care · 40–60%", xaml, StringComparison.Ordinal);
        Assert.Contains("Full charge · 100%", xaml, StringComparison.Ordinal);
        Assert.Contains("does not claim a fixed cycle-life multiplier", xaml, StringComparison.Ordinal);
        Assert.Contains("Avoids routine charging in the top", panel, StringComparison.Ordinal);
        Assert.Contains("charging resumes below", panel, StringComparison.Ordinal);
        Assert.Contains("_historyVisibleDays = 7", panel, StringComparison.Ordinal);
        Assert.Contains("GetRecentDays(14)", mainPanel, StringComparison.Ordinal);
        Assert.Contains("Take(_historyVisibleDays)", mainPanel, StringComparison.Ordinal);
        Assert.Contains("Reset all history…", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Clear history\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("fewer cycles", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("×", xaml, StringComparison.Ordinal);
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
