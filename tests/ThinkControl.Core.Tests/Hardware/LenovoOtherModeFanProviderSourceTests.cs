using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class LenovoOtherModeFanProviderSourceTests
{
    [Fact]
    public void OtherModeProvider_RequiresCapabilityReportedConstrainedFanChannels()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");

        Assert.Contains("LENOVO_OTHER_METHOD", source, StringComparison.Ordinal);
        Assert.Contains("LENOVO_CAPABILITY_DATA_00", source, StringComparison.Ordinal);
        Assert.Contains("LENOVO_FAN_TEST_DATA", source, StringComparison.Ordinal);
        Assert.Contains("RequiredWriteSupport = SupportValid | SupportGet | SupportSet", source, StringComparison.Ordinal);
        Assert.Contains("channels.Length >= 2", source, StringComparison.Ordinal);
        Assert.Contains("IsSaneConstraint(channel.MinRpm, channel.MaxRpm)", source, StringComparison.Ordinal);
        Assert.Contains("BuildFanRpmAttributeId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherModeProvider_MapsPercentToOemMinMaxAndUsesZeroForAuto()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");

        Assert.Contains("ResolveTargetRpm(channel, percent)", source, StringComparison.Ordinal);
        Assert.Contains("channel.MaxRpm - channel.MinRpm", source, StringComparison.Ordinal);
        Assert.Contains("target / RpmDivisor * RpmDivisor", source, StringComparison.Ordinal);
        Assert.Contains("TrySetFeatureValue(method, channel.AttributeId, 0", source, StringComparison.Ordinal);
        Assert.Contains("BestEffortReturnToAuto(method, channels)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("0x831020C0", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CleanDust", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FanSupervisor_RoutesContinuousTargetsToOemAndKeepsEcCalibrationFallbackOnly()
    {
        string source = ReadSource("src", "ThinkControl.Service", "FanSupervisor.cs");

        Assert.Contains("LenovoFanControlKind.LenovoOtherModeTargetRpm", source, StringComparison.Ordinal);
        Assert.Contains("_hardware.SetFanPercent(requestedPercent", source, StringComparison.Ordinal);
        Assert.Contains("SetHardwarePercentSerialized(percent", source, StringComparison.Ordinal);
        Assert.Contains("preflight.FanControlKind != LenovoFanControlKind.ThinkPadEcDiscrete", source, StringComparison.Ordinal);
        Assert.Contains("seven-step EC calibration is not used", source, StringComparison.Ordinal);
        Assert.Contains("FanOutputMapping.State output = ResolveOutputState(percent)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceAndFansUi_ExposeProviderKindWithoutPretendingOemTargetsAreEcSteps()
    {
        string service = ReadSource("src", "ThinkControl.Service", "ServiceEngine.cs");
        string ui = ReadSource("src", "ThinkControl.UI", "Controls", "FansPanel.xaml.cs");

        Assert.Contains("ToFanControlKind(status.FanControlKind)", service, StringComparison.Ordinal);
        Assert.Contains("FanControlKinds.OemTargetRpm", service, StringComparison.Ordinal);
        Assert.Contains("FanControlKinds.DiscreteEc", service, StringComparison.Ordinal);

        Assert.Contains("100% requests each fan's Lenovo-reported maximum target RPM", ui, StringComparison.Ordinal);
        Assert.Contains("RawEcStepsExpander.Visibility = x9EcWriter", ui, StringComparison.Ordinal);
        Assert.Contains("CalibrationCard.Visibility = x9Calibration", ui, StringComparison.Ordinal);
        Assert.Contains("% OEM target", ui, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for Lenovo Other Mode validation.");
    }
}
