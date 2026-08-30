using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class LenovoOtherModeFanProviderSourceTests
{
    [Fact]
    public void OtherModeProvider_UsesLenovoDirectTargetRpmContractWithPerChannelLiveGates()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");

        Assert.Contains("LENOVO_OTHER_METHOD", source, StringComparison.Ordinal);
        Assert.Contains("LENOVO_CAPABILITY_DATA_00", source, StringComparison.Ordinal);
        Assert.Contains("LENOVO_FAN_TEST_DATA", source, StringComparison.Ordinal);
        Assert.Contains("RequiredWriteSupport = SupportValid | SupportGet | SupportSet", source, StringComparison.Ordinal);
        Assert.Contains("FanDeviceId = 0x04", source, StringComparison.Ordinal);
        Assert.Contains("FanRpmFeatureId = 0x03", source, StringComparison.Ordinal);
        Assert.Contains("return FanDeviceId << 24 | FanRpmFeatureId << 16 | typeId;", source, StringComparison.Ordinal);
        Assert.Contains("writableLive.Length >= 2", source, StringComparison.Ordinal);
        Assert.Contains("liveWritable.Length < 2", source, StringComparison.Ordinal);
        Assert.Contains("IsWritableChannel(channel)", source, StringComparison.Ordinal);
        Assert.Contains("Lenovo WMI · Other Mode direct target-RPM", source, StringComparison.Ordinal);

        // Do not make every firmware-advertised capability record a global all-or-nothing
        // gate. Two independently live, constrained X9 fan channels are sufficient even
        // if firmware also exposes a phantom/unused record.
        Assert.DoesNotContain("fans.Count == channels.Length", source, StringComparison.Ordinal);
        Assert.DoesNotContain("channels.All(channel =>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherModeProvider_MapsPercentToOemRpmAndUsesZeroForAuto()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");

        Assert.Contains("ResolveTargetRpm(channel, percent)", source, StringComparison.Ordinal);
        Assert.Contains("channel.MaxRpm - channel.MinRpm", source, StringComparison.Ordinal);
        Assert.Contains("target / RpmDivisor * RpmDivisor", source, StringComparison.Ordinal);
        Assert.Contains("RpmDivisor = 100", source, StringComparison.Ordinal);
        Assert.Contains("TrySetFeatureValue(method, channel.AttributeId, 0", source, StringComparison.Ordinal);
        Assert.Contains("Lenovo OEM direct target-RPM", source, StringComparison.Ordinal);
        Assert.DoesNotContain("0x831020C0", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CleanDust", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OtherModeProvider_ReturnsOnlyOwnedChannelsToAutoAndPreservesFailedHandoffEvidence()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");

        Assert.Contains("private LenovoOtherModeFanChannel[] _ownedChannels = [];", source, StringComparison.Ordinal);
        Assert.Contains("_ownedChannels = liveWritable.ToArray();", source, StringComparison.Ordinal);
        Assert.Contains("owned = _ownedChannels;", source, StringComparison.Ordinal);
        Assert.Contains("if (owned.Length == 0)", source, StringComparison.Ordinal);
        Assert.Contains("foreach (LenovoOtherModeFanChannel channel in owned)", source, StringComparison.Ordinal);
        Assert.Contains("One or more ThinkControl-owned Lenovo fan channels did not accept the Auto target", source, StringComparison.Ordinal);
        Assert.Contains("Ownership is intentionally preserved through capability refresh", source, StringComparison.Ordinal);
        Assert.Contains("BestEffortRecoverAuto(touched)", source, StringComparison.Ordinal);
        Assert.Contains("Only channels that actually reached the write phase", source, StringComparison.Ordinal);

        // Discovery/readback must never infer ownership, and a metadata-only channel
        // must not be included in exception cleanup merely because it advertised SET.
        Assert.DoesNotContain("_ownedChannels = channels", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BestEffortRecoverAuto(candidates)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherModeProvider_ReportsExactCapabilityEvidenceWhenDirectWriterDoesNotActivate()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");

        Assert.Contains("DescribeChannelCapabilities", source, StringComparison.Ordinal);
        Assert.Contains("id=0x{channel.AttributeId:X8}", source, StringComparison.Ordinal);
        Assert.Contains("cap=0x{channel.Capability:X}", source, StringComparison.Ordinal);
        Assert.Contains("no-safe-range", source, StringComparison.Ordinal);
        Assert.Contains("checked 0x{BuildFanRpmAttributeId(0):X8}", source, StringComparison.Ordinal);
    }

    [Fact]
    public void X9Controller_GatesOemWritesByExactIdentityAndPrefersThemOverEcFallback()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs");

        Assert.Contains("bool oemFanControl = _identity.IsVerifiedX9 && oemFanStatus.CanControl;", source, StringComparison.Ordinal);
        Assert.Contains("if (!_identity.IsVerifiedX9)", source, StringComparison.Ordinal);
        Assert.Contains("if (oem.CanControl)", source, StringComparison.Ordinal);
        Assert.Contains("Raw EC steps are disabled while the X9 exposes Lenovo's constrained OEM target-RPM provider", source, StringComparison.Ordinal);
        Assert.Contains("_activeFanControlKind = LenovoFanControlKind.LenovoOtherModeTargetRpm", source, StringComparison.Ordinal);
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
        string xaml = ReadSource("src", "ThinkControl.UI", "Controls", "FansPanel.xaml");

        Assert.Contains("ToFanControlKind(status.FanControlKind)", service, StringComparison.Ordinal);
        Assert.Contains("FanControlKinds.OemTargetRpm", service, StringComparison.Ordinal);
        Assert.Contains("FanControlKinds.DiscreteEc", service, StringComparison.Ordinal);

        Assert.Contains("100% requests each fan's Lenovo-reported maximum target RPM", ui, StringComparison.Ordinal);
        Assert.Contains("RawEcStepsExpander.Visibility = x9EcWriter", ui, StringComparison.Ordinal);
        Assert.Contains("CalibrationCard.Visibility = x9Calibration", ui, StringComparison.Ordinal);
        Assert.Contains("% OEM target", ui, StringComparison.Ordinal);
        Assert.DoesNotContain("100% means the highest verified standard X9 EC step", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualQa_ExercisesOemProviderWithoutIssuingHardwareRequestsOrReplacingBalancedFixture()
    {
        string snapshot = ReadSource("src", "ThinkControl.UI", "Controls", "FansPanel.ManualTestSnapshot.cs");
        string advancedSnapshot = ReadSource("src", "ThinkControl.UI", "AdvancedWindow.Diagnostics.cs");
        string renderer = ReadSource("tools", "ThinkControl.Snapshots", "Program.cs");

        Assert.Contains("PrepareOemTargetRpmForSnapshot(72)", snapshot, StringComparison.Ordinal);
        Assert.Contains("_fanControlKind = FanControlKinds.OemTargetRpm", snapshot, StringComparison.Ordinal);
        Assert.Contains("lenovo-other-mode-1", snapshot, StringComparison.Ordinal);
        Assert.Contains("lenovo-other-mode-2", snapshot, StringComparison.Ordinal);
        Assert.Contains("ApplyProviderCopy(state, true, FanControlKinds.OemTargetRpm)", snapshot, StringComparison.Ordinal);
        Assert.Contains("% OEM target", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStatusAsync", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("SetFanPercent", snapshot, StringComparison.Ordinal);

        Assert.DoesNotContain("PrepareManualFanTestForSnapshot", advancedSnapshot, StringComparison.Ordinal);
        Assert.Contains("fansPanel.PrepareManualFanTestForSnapshot();", renderer, StringComparison.Ordinal);
        Assert.Contains("fanManualTest: true", renderer, StringComparison.Ordinal);
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
