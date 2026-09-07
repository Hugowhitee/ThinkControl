using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TrackControlPolishSourceTests
{
    [Fact]
    public void TrackCenterBehavesLikeAButtonWithoutLoweringSkipThreshold()
    {
        string recognizer = ReadSource("src", "ThinkControl.Core", "Touchpad", "EdgeGestureRecognizer.cs");
        string router = ReadSource("src", "ThinkControl.UI", "Services", "Touchpad", "GestureActionRouter.cs");
        string policy = ReadSource("src", "ThinkControl.Core", "Touchpad", "TrackCenterGesturePolicy.cs");

        Assert.Contains("IsTrackCenterTapCandidate()", recognizer, StringComparison.Ordinal);
        Assert.Contains("radialTravel <= TrackCenterGesturePolicy.MovementToleranceMm", recognizer, StringComparison.Ordinal);
        Assert.Contains("TrackCenterGesturePolicy.SwipeThresholdMm", router, StringComparison.Ordinal);
        Assert.Contains("CommitTrackCenterAfterHoldAsync", router, StringComparison.Ordinal);
        Assert.Contains("Task.Delay(TrackCenterGesturePolicy.HoldCommitMs)", router, StringComparison.Ordinal);
        Assert.Contains("generation == _trackCandidateGeneration", router, StringComparison.Ordinal);
        Assert.Contains("_trackActionCommitted", router, StringComparison.Ordinal);
        Assert.Contains("ButtonTravelToleranceMm = 8.75", policy, StringComparison.Ordinal);
        Assert.Contains("SwipeThresholdMm = 9.0", policy, StringComparison.Ordinal);
        Assert.Contains("HoldCommitMs = 240", policy, StringComparison.Ordinal);
        Assert.Contains("CenterZoneStart = 0.36", policy, StringComparison.Ordinal);
        Assert.Contains("CenterZoneEnd = 0.64", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("MaximumTapMs", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopwatch.GetTimestamp() - _trackGestureStarted", router, StringComparison.Ordinal);
        Assert.Contains("ShouldCommit(_trackMaxTravelMm, _trackStartPosition01)", router, StringComparison.Ordinal);
    }

    [Fact]
    public void TrackCenterHoldAndReleaseCannotDoubleToggleOrAlsoSkip()
    {
        string router = ReadSource("src", "ThinkControl.UI", "Services", "Touchpad", "GestureActionRouter.cs");

        Assert.Contains("if (_trackSwipeFired || _trackActionCommitted)", router, StringComparison.Ordinal);
        Assert.Contains("commit = !_trackActionCommitted", router, StringComparison.Ordinal);
        Assert.Contains("_trackActionCommitted = true;", router, StringComparison.Ordinal);
        Assert.Contains("_trackCandidateGeneration++;", router, StringComparison.Ordinal);
        Assert.Contains("_trackStartedInCenter", router, StringComparison.Ordinal);
    }

    [Fact]
    public void StandalonePlayPause_IsRemovedFromMenuAndMigratesIntoTrackControl()
    {
        string panel = ReadSource("src", "ThinkControl.UI", "Controls", "TouchpadPanel.xaml.cs");
        string models = ReadSource("src", "ThinkControl.Core", "Touchpad", "GestureModels.cs");

        string actionOptions = Normalize(panel).Split("Visualizer.ZoneSelected += OnZoneSelected;", StringSplitOptions.None)[0];
        Assert.DoesNotContain("new ActionOption(GestureActionKind.PlayPause", actionOptions, StringComparison.Ordinal);
        Assert.Contains("new ActionOption(GestureActionKind.PreviousNextTrack", actionOptions, StringComparison.Ordinal);
        Assert.Contains("GestureActionKind.PlayPause => GestureActionKind.PreviousNextTrack", models, StringComparison.Ordinal);
    }

    [Fact]
    public void OccupiedEdgeAssignment_SwapsActionsInsteadOfClearingPreviousEdge()
    {
        string panel = ReadSource("src", "ThinkControl.UI", "Controls", "TouchpadPanel.xaml.cs");

        Assert.Contains("occupiedBinding with { Action = selectedBinding.Action }", panel, StringComparison.Ordinal);
        Assert.Contains("selectedBinding with { Action = option.Action }", panel, StringComparison.Ordinal);
        Assert.Contains("Swapped {ActionLabel(option.Action)}", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("movedFrom = edge", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void PlaybackOsd_ShowsNextAvailableActionLikeStandardMediaPlayers()
    {
        string osd = ReadSource("src", "ThinkControl.UI", "Services", "Touchpad", "GestureOsdService.cs");
        string normalized = Normalize(osd);

        Assert.Contains(
            "case MediaToggleResult.Playing:\n                ShowMediaCommand(\"Playing\", PauseStateGeometry);",
            normalized,
            StringComparison.Ordinal);
        Assert.Contains(
            "case MediaToggleResult.Paused:\n                ShowMediaCommand(\"Paused\", PlayStateGeometry);",
            normalized,
            StringComparison.Ordinal);
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
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.Core")) &&
                    Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.UI")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for Track control source validation.");
    }
}
