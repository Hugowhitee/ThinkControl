using Xunit;

namespace ThinkControl.Core.Tests.Touchpad;

public sealed class TrackControlPolishSourceTests
{
    [Fact]
    public void TrackCenterRelease_AllowsTapAfterSmallRecognizerClaimWithoutLoweringSkipThreshold()
    {
        string router = ReadSource("src", "ThinkControl.UI", "Services", "Touchpad", "GestureActionRouter.cs");
        string policy = ReadSource("src", "ThinkControl.Core", "Touchpad", "TrackCenterGesturePolicy.cs");

        Assert.Contains("TrackSwipeThresholdMm = 9.0", router, StringComparison.Ordinal);
        Assert.Contains("TryFireTrackSwipe(signal, allowReleaseFallback: true);", router, StringComparison.Ordinal);
        Assert.Contains("if (!_trackSwipeFired)\n                TryFireTrackCenter();", Normalize(router), StringComparison.Ordinal);
        Assert.DoesNotContain("_trackStayedCandidate", router, StringComparison.Ordinal);
        Assert.Contains("MovementToleranceMm = 4.5", policy, StringComparison.Ordinal);
        Assert.Contains("MaximumTapMs = 700", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void StandalonePlayPause_IsRemovedFromMenuAndMigratesIntoTrackControl()
    {
        string layout = ReadSource("src", "ThinkControl.UI", "Controls", "TouchpadPanel.Layout.cs");
        string models = ReadSource("src", "ThinkControl.Core", "Touchpad", "GestureModels.cs");

        Assert.Contains("option.Action != GestureActionKind.PlayPause", layout, StringComparison.Ordinal);
        Assert.Contains("GestureActionKind.PlayPause => GestureActionKind.PreviousNextTrack", models, StringComparison.Ordinal);
    }

    [Fact]
    public void OccupiedEdgeAssignment_SwapsActionsInsteadOfClearingPreviousEdge()
    {
        string layout = ReadSource("src", "ThinkControl.UI", "Controls", "TouchpadPanel.Layout.cs");

        Assert.Contains("ActionCombo.SelectionChanged -= ActionCombo_SelectionChanged", layout, StringComparison.Ordinal);
        Assert.Contains("ActionCombo.SelectionChanged += ActionCombo_SwapSelectionChanged", layout, StringComparison.Ordinal);
        Assert.Contains("occupiedBinding with { Action = selectedBinding.Action }", layout, StringComparison.Ordinal);
        Assert.Contains("selectedBinding with { Action = option.Action }", layout, StringComparison.Ordinal);
        Assert.Contains("Swapped {ActionLabel(option.Action)}", layout, StringComparison.Ordinal);
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
