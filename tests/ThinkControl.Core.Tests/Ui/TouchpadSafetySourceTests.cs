using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class TouchpadSafetySourceTests
{
    [Fact]
    public void ContinuousControls_DoNotCommitAtClaimAndCannotBuildAnUnboundedHiddenLead()
    {
        string root = FindRepositoryRoot();
        string router = Read(root, "src", "ThinkControl.UI", "Services", "Touchpad", "GestureActionRouter.cs");
        string host = Read(root, "src", "ThinkControl.UI", "Services", "Touchpad", "TouchpadFeatureHost.cs");
        string native = Read(root, "src", "ThinkControl.UI", "Services", "Touchpad", "NativeInputService.cs");

        Assert.Contains("ContinuousCommitTravelMm = 1.5", router, StringComparison.Ordinal);
        Assert.Contains("ContinuousMaxFramePercent = 6.0", router, StringComparison.Ordinal);
        Assert.Contains("PhysicalDeltaMm", router, StringComparison.Ordinal);
        Assert.Contains("if (AdvanceContinuous(signal, VolumeBaseGain))", router, StringComparison.Ordinal);
        Assert.Contains("if (AdvanceContinuous(signal, BrightnessBaseGain))", router, StringComparison.Ordinal);
        Assert.Contains("_continuousDeltaPercent = 0;", router, StringComparison.Ordinal);
        Assert.Contains("_continuousRawTravelMm = 0;", router, StringComparison.Ordinal);
        Assert.DoesNotContain("BeginContinuous(signal", router, StringComparison.Ordinal);

        Assert.Contains("ContinuousVolumeLeadLimit = 8", host, StringComparison.Ordinal);
        Assert.Contains("ContinuousBrightnessLeadLimit = 10", host, StringComparison.Ordinal);
        Assert.Contains("_nativeInput.TryGetVolumePercent", host, StringComparison.Ordinal);
        Assert.Contains("QueueGestureVolume", host, StringComparison.Ordinal);
        Assert.Contains("QueueGestureBrightness", host, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _pendingVolume, -1)", host, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _pendingBrightness, -1)", host, StringComparison.Ordinal);
        Assert.Contains("_volumeWriteGate", host, StringComparison.Ordinal);
        Assert.Contains("_brightnessWriteGate", host, StringComparison.Ordinal);
        Assert.Contains("_volumeGestureGeneration", host, StringComparison.Ordinal);
        Assert.Contains("_brightnessGestureGeneration", host, StringComparison.Ordinal);
        Assert.Contains("_pendingVolumeGeneration", host, StringComparison.Ordinal);
        Assert.Contains("_pendingBrightnessGeneration", host, StringComparison.Ordinal);
        Assert.Contains("lock (_volumeWriteGate)", host, StringComparison.Ordinal);
        Assert.Contains("lock (_brightnessWriteGate)", host, StringComparison.Ordinal);
        Assert.Contains("_app.DisplayService.GetBrightness()", host, StringComparison.Ordinal);
        Assert.Contains("ReadGestureBrightnessBaseline", host, StringComparison.Ordinal);
        Assert.Contains("_app.DisplayService.GetBrightness()", host, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _confirmedBrightness, live)", host, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _confirmedBrightness, -1)", host, StringComparison.Ordinal);
        Assert.Contains("Func<int?> _getBrightness", router, StringComparison.Ordinal);
        Assert.Contains("_getBrightness() is not int brightnessAtStart", router, StringComparison.Ordinal);
        Assert.DoesNotContain("() => app.State.Brightness,\n            QueueGestureBrightness", host, StringComparison.Ordinal);

        Assert.Contains("TryGetVolumePercent()", native, StringComparison.Ordinal);
        Assert.Contains("internal int? GetVolumePercent()", native, StringComparison.Ordinal);
        Assert.Contains("return cached >= 0 ? cached : null;", native, StringComparison.Ordinal);
        Assert.Contains("return null;", native, StringComparison.Ordinal);
        Assert.DoesNotContain("return 50;", native, StringComparison.Ordinal);
        Assert.DoesNotContain("return cached >= 0 ? cached : 0;", native, StringComparison.Ordinal);
        Assert.Contains("TrySetVolume(int percent, out int applied)", native, StringComparison.Ordinal);
        Assert.Contains("MasterVolumeLevelScalar * 100", native, StringComparison.Ordinal);
        Assert.Contains("_showValue?.Invoke(\"Volume\", applied)", native, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownVolume_RemainsUnknownInTouchpadFeedbackAndBlockedOsd()
    {
        string root = FindRepositoryRoot();
        string host = Read(root, "src", "ThinkControl.UI", "Services", "Touchpad", "TouchpadFeatureHost.cs");
        string osd = Read(root, "src", "ThinkControl.UI", "Services", "Touchpad", "GestureOsdService.cs");
        string feedback = Read(root, "src", "ThinkControl.UI", "Controls", "TouchpadPanel.ValueFeedback.cs");

        Assert.Contains("internal int? ReadVolumePercent()", host, StringComparison.Ordinal);
        Assert.Contains("if (ReadVolumePercent() is int volume)", host, StringComparison.Ordinal);
        Assert.Contains("_osd.ShowStatus(label);", host, StringComparison.Ordinal);
        Assert.Contains("internal void ShowStatus(string label)", osd, StringComparison.Ordinal);
        Assert.Contains("FormatCurrentPercent(_host!.CurrentVolumeTarget, _host.ReadVolumePercent())", feedback, StringComparison.Ordinal);
        Assert.Contains("return value is int known", feedback, StringComparison.Ordinal);
        Assert.Contains(": \"—\";", feedback, StringComparison.Ordinal);
    }

    [Fact]
    public void ReverseCloseEditor_UsesSharedSwitchInsteadOfSquareCheckbox()
    {
        string root = FindRepositoryRoot();
        string source = Read(root, "src", "ThinkControl.UI", "Controls", "TouchpadPanel.CornerLaunches.cs");

        Assert.Contains("Text = \"Reverse swipe closes ThinkControl\"", source, StringComparison.Ordinal);
        Assert.Contains("Style = TryFindResource(\"TcSwitch\") as Style", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Content = \"Reverse swipe closes ThinkControl\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TrackSkip_CommitsOnlyOnReleaseAfterTheDeliberateThreshold()
    {
        string root = FindRepositoryRoot();
        string router = Normalize(Read(root, "src", "ThinkControl.UI", "Services", "Touchpad", "GestureActionRouter.cs"));
        string policy = Read(root, "src", "ThinkControl.Core", "Touchpad", "TrackCenterGesturePolicy.cs");

        string begin = router.Split("private void Begin(GestureSignal signal)", StringSplitOptions.None)[1]
            .Split("private void Update(GestureSignal signal)", StringSplitOptions.None)[0];
        string update = router.Split("private void Update(GestureSignal signal)", StringSplitOptions.None)[1]
            .Split("private void Release(GestureSignal signal)", StringSplitOptions.None)[0];
        string release = router.Split("private void Release(GestureSignal signal)", StringSplitOptions.None)[1]
            .Split("private void TryFireTrackSwipe", StringSplitOptions.None)[0];

        Assert.DoesNotContain("TryFireTrackSwipe", begin, StringComparison.Ordinal);
        Assert.DoesNotContain("TryFireTrackSwipe", update, StringComparison.Ordinal);
        Assert.Contains("TryFireTrackSwipe(signal)", release, StringComparison.Ordinal);
        Assert.Contains("_trackPeakTravelMm", router, StringComparison.Ordinal);
        Assert.Contains("double signed = _trackPeakTravelMm;", router, StringComparison.Ordinal);
        Assert.Contains("ObserveTrackTravel(signal);", release, StringComparison.Ordinal);
        Assert.Contains("SwipeThresholdMm = 12.0", policy, StringComparison.Ordinal);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string Read(string root, params string[] path) =>
        File.ReadAllText(Path.Combine([root, .. path]));

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new(start);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.UI")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for touchpad safety validation.");
    }
}
