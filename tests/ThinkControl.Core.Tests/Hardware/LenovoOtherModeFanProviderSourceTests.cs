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
    public void OtherModeProvider_AllowsNarrowDirectIdFallbackButNeverOverridesExplicitRejection()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");
        string controller = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs");

        Assert.Contains("bool CapabilityPresent", source, StringComparison.Ordinal);
        Assert.Contains("allowExactModelDirectIdFallback = true", source, StringComparison.Ordinal);
        Assert.Contains("capabilities = [];", source, StringComparison.Ordinal);
        Assert.Contains("!IsSaneConstraint(range.MinRpm, range.MaxRpm)", source, StringComparison.Ordinal);
        Assert.Contains("if ((capability & SupportValid) == 0)", source, StringComparison.Ordinal);
        Assert.Contains("only fills an omitted record; it never overrides an invalid one", source, StringComparison.Ordinal);
        Assert.Contains("TryGetFeatureValue(method, channel.AttributeId", source, StringComparison.Ordinal);
        Assert.Contains("cap=missing(direct-ID)", source, StringComparison.Ordinal);

        // The tolerant read/discovery path does not weaken the production write
        // boundary: only the exact X9 controller can call SetFanPercent.
        Assert.Contains("if (!_identity.IsVerifiedX9)", controller, StringComparison.Ordinal);
        Assert.Contains("OEM target-RPM fan control is not enabled for this device identity", controller, StringComparison.Ordinal);
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
    public void OtherModeProvider_VerifiesLiveResponseAndRetainsFailedAutoOwnership()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");
        string controller = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs");

        Assert.Contains("private LenovoOtherModeFanChannel[] _ownedChannels = [];", source, StringComparison.Ordinal);
        Assert.Contains("internal bool HasOwnedChannels", source, StringComparison.Ordinal);
        Assert.Contains("_ownedChannels = liveWritable.ToArray();", source, StringComparison.Ordinal);
        Assert.Contains("owned = _ownedChannels;", source, StringComparison.Ordinal);
        Assert.Contains("if (owned.Length == 0)", source, StringComparison.Ordinal);
        Assert.Contains("VerifyTargetResponse(method, liveWritable, before, targets", source, StringComparison.Ordinal);
        Assert.Contains("HasVerifiedTargetProgress", source, StringComparison.Ordinal);
        Assert.Contains("ReturnChannelsToAuto(method, touched)", source, StringComparison.Ordinal);
        Assert.Contains("PreserveFailedAutoOwnership(failedAuto)", source, StringComparison.Ordinal);
        Assert.Contains("failed ownership was retained for a later cleanup retry", source, StringComparison.Ordinal);
        Assert.Contains("Ownership is intentionally preserved through capability refresh", source, StringComparison.Ordinal);
        Assert.Contains("Only channels that actually reached the write phase", source, StringComparison.Ordinal);
        Assert.Contains("if (_otherModeFans.HasOwnedChannels)", controller, StringComparison.Ordinal);
        Assert.Contains("potentially live manual target", controller, StringComparison.Ordinal);

        // Discovery/readback must never infer ownership, and a metadata-only channel
        // must not be included in exception cleanup merely because it advertised SET.
        Assert.DoesNotContain("_ownedChannels = channels", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecoverAuto(candidates)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherModeProvider_BacksOffFailedLiveAndDiscoveryWmiProbesUntilRefreshOrDeadline()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");

        Assert.Contains("LiveProbeFailureBackoff = TimeSpan.FromSeconds(10)", source, StringComparison.Ordinal);
        Assert.Contains("DiscoveryFailureBackoff = TimeSpan.FromSeconds(10)", source, StringComparison.Ordinal);
        Assert.Contains("ShouldBackOffLiveProbe", source, StringComparison.Ordinal);
        Assert.Contains("RecordLiveProbeFailure", source, StringComparison.Ordinal);
        Assert.Contains("if (now < _discoveryRetryAfter)", source, StringComparison.Ordinal);
        Assert.Contains("_discoveryComplete = false;", source, StringComparison.Ordinal);
        Assert.Contains("_discoveryRetryAfter = DateTimeOffset.UtcNow + DiscoveryFailureBackoff", source, StringComparison.Ordinal);
        Assert.Contains("retry after bounded backoff", source, StringComparison.Ordinal);
        Assert.Contains("_liveProbeRetryAfter = DateTimeOffset.MinValue", source, StringComparison.Ordinal);
        Assert.Contains("_discoveryRetryAfter = DateTimeOffset.MinValue", source, StringComparison.Ordinal);
        Assert.Contains("_energyDrv.Refresh();", source, StringComparison.Ordinal);
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
        Assert.Contains("ownsDiscreteEc = _activeFanControlKind == LenovoFanControlKind.ThinkPadEcDiscrete", source, StringComparison.Ordinal);
        Assert.Contains("An explicitly owned EC state must be released through 0x2F/0x80 before", source, StringComparison.Ordinal);
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
        string appState = ReadSource("src", "ThinkControl.UI", "ViewModels", "AppState.cs");
        string editor = ReadSource("src", "ThinkControl.UI", "FanCurveEditorWindow.cs");
        string cooling = ReadSource("src", "ThinkControl.UI", "App.Cooling.cs");

        Assert.Contains("ToFanControlKind(status.FanControlKind)", service, StringComparison.Ordinal);
        Assert.Contains("FanControlKinds.OemTargetRpm", service, StringComparison.Ordinal);
        Assert.Contains("FanControlKinds.DiscreteEc", service, StringComparison.Ordinal);
        Assert.Contains("FanCalibrationSupported: fanCalibrationSupported", service, StringComparison.Ordinal);
        Assert.Contains("FanCalibrationRequired: fanCalibrationRequired", service, StringComparison.Ordinal);
        Assert.Contains("public string FanControlKind", appState, StringComparison.Ordinal);

        Assert.Contains("provider-reported maximum target RPM", ui, StringComparison.Ordinal);
        Assert.Contains("RawEcStepsExpander.Visibility = discreteEcWriter", ui, StringComparison.Ordinal);
        Assert.Contains("CalibrationCard.Visibility = calibration.Relevant", ui, StringComparison.Ordinal);
        Assert.Contains("Calibration visibility is owned solely by provider capability state", ui, StringComparison.Ordinal);
        Assert.Contains("capabilities.FanCalibrationSupported", cooling, StringComparison.Ordinal);
        Assert.DoesNotContain("IsVerifiedX9(State.MachineType) &&\n                        capabilities.FanControl", cooling, StringComparison.Ordinal);
        Assert.DoesNotContain("DeviceCapabilityExpectations.IsVerifiedX9", ui, StringComparison.Ordinal);
        Assert.DoesNotContain("HardwareAccess", ui.Split("private static string DescribeUnavailable", StringSplitOptions.None)[0].Split("private void ApplyProviderCopy", StringSplitOptions.None)[1], StringComparison.Ordinal);
        Assert.Contains("% OEM target", ui, StringComparison.Ordinal);
        Assert.DoesNotContain("100% means the highest verified standard X9 EC step", xaml, StringComparison.Ordinal);
        Assert.Contains("FanControlKinds.OemTargetRpm", editor, StringComparison.Ordinal);
        Assert.Contains("Lenovo OEM target-RPM", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualQa_ExercisesOemProviderWithoutIssuingHardwareRequestsOrReplacingBalancedFixture()
    {
        string snapshot = ReadSource("src", "ThinkControl.UI", "Controls", "FansPanel.ManualTestSnapshot.cs");
        string advancedSnapshot = ReadSource("src", "ThinkControl.UI", "AdvancedWindow.Diagnostics.cs");
        string renderer = ReadSource("tools", "ThinkControl.Snapshots", "Program.cs");

        Assert.Contains("PrepareOemTargetRpmForSnapshot(72)", snapshot, StringComparison.Ordinal);
        Assert.Contains("_fanControlKind = FanControlKinds.OemTargetRpm", snapshot, StringComparison.Ordinal);
        Assert.Contains("oem-target-rpm-1", snapshot, StringComparison.Ordinal);
        Assert.Contains("oem-target-rpm-2", snapshot, StringComparison.Ordinal);
        Assert.Contains("ApplyProviderCopy(true, FanControlKinds.OemTargetRpm)", snapshot, StringComparison.Ordinal);
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
