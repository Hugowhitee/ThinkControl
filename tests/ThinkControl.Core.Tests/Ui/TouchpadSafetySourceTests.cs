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
        Assert.Contains("_continuousDeltaPercent = 0;", router, StringComparison.Ordinal);
        Assert.Contains("_continuousRawTravelMm = 0;", router, StringComparison.Ordinal);
        Assert.DoesNotContain("BeginContinuous(signal", router, StringComparison.Ordinal);

        Assert.Contains("ContinuousVolumeLeadLimit = 8", host, StringComparison.Ordinal);
        Assert.Contains("ContinuousBrightnessLeadLimit = 10", host, StringComparison.Ordinal);
        Assert.Contains("QueueGestureVolume", host, StringComparison.Ordinal);
        Assert.Contains("QueueGestureBrightness", host, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _pendingVolume, -1)", host, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _pendingBrightness, -1)", host, StringComparison.Ordinal);

        Assert.Contains("TrySetVolume(int percent, out int applied)", native, StringComparison.Ordinal);
        Assert.Contains("MasterVolumeLevelScalar * 100", native, StringComparison.Ordinal);
        Assert.Contains("_showValue?.Invoke("Volume", applied)", native, StringComparison.Ordinal);
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
