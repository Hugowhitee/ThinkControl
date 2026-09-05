using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class TouchpadReverseCloseSourceTests
{
    [Fact]
    public void HideToTray_UsesSynchronousCompactTransitionHide()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.TrayActivation.cs"));
        string method = Slice(
            source,
            "internal void HideThinkControlToTray()",
            "public void ShowThinkControlFromTray()");

        Assert.Contains("CompactWindow.HideForViewTransition();", method, StringComparison.Ordinal);
        Assert.DoesNotContain("CompactWindow.HideAnimated();", method, StringComparison.Ordinal);
        Assert.Contains(
            "VerifyPrimarySurfaceState(operation, expectCompact: false, expectAdvanced: false);",
            method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReverseCornerSnapshot_DoesNotComposeAnInwardLiveTrailFirst()
    {
        string root = FindRepositoryRoot();
        string diagnostics = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.Diagnostics.cs"));
        string route = Slice(
            diagnostics,
            "public void PrepareTouchpadCornerForSnapshot(TouchpadCorner corner, bool live)",
            "public void ValidateTouchpadCornerSymmetryForSnapshot()");

        Assert.Contains("if (corner == TouchpadCorner.TopRight)", route, StringComparison.Ordinal);
        Assert.Contains("TouchpadPanelControl.PrepareReverseCornerForSnapshot(corner, live);", route, StringComparison.Ordinal);
        Assert.Contains("TouchpadPanelControl.PrepareCornerForSnapshot(corner, live);", route, StringComparison.Ordinal);

        string fixtures = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ThinkControl.UI",
            "Controls",
            "TouchpadPanel.SnapshotFixtures.cs"));
        string reverse = Slice(
            fixtures,
            "internal void PrepareReverseCornerForSnapshot(TouchpadCorner corner, bool live)",
            "internal void ValidateCornerEditorLayoutForSnapshot(TouchpadCorner corner, bool live)");

        int cleanBaseline = reverse.IndexOf("PrepareCornerForSnapshot(corner, live: false);", StringComparison.Ordinal);
        int liveFrame = reverse.IndexOf("Visualizer.SetTestFrame([", StringComparison.Ordinal);
        Assert.True(cleanBaseline >= 0, "Reverse-close fixture must initialize a clean non-live corner baseline.");
        Assert.True(liveFrame < 0 || cleanBaseline < liveFrame, "The clean baseline must be established before any reverse live frame is appended.");
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find source marker: {startMarker}");
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find source marker after {startMarker}: {endMarker}");
        return source[start..end];
    }

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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for reverse-close source validation.");
    }
}
