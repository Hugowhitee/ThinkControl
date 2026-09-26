using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class AdvancedHeaderConsistencySourceTests
{
    [Fact]
    public void AdvancedPageHeaders_ShareOneTitleActionRail()
    {
        string consistency = Read("src", "ThinkControl.UI", "AdvancedWindow.UiConsistency.cs");
        string resets = Read("src", "ThinkControl.UI", "AdvancedWindow.ResetDefaults.cs");
        string battery = Read("src", "ThinkControl.UI", "AdvancedWindow.Battery.cs");
        string performance = Read("src", "ThinkControl.UI", "Controls", "PerformancePanel.xaml");
        string audio = Read("src", "ThinkControl.UI", "Controls", "AudioPanel.xaml");
        string touchpad = Read("src", "ThinkControl.UI", "Controls", "TouchpadPanel.xaml");
        string fans = Read("src", "ThinkControl.UI", "Controls", "FansPanel.xaml.cs");

        Assert.Contains("PageHeaderMinHeight = 38", consistency, StringComparison.Ordinal);

        Assert.Contains("MinHeight=\"38\"", performance, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"38\"", audio, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"38\"", touchpad, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"1\" Content=\"Defaults\"", audio, StringComparison.Ordinal);

        Assert.Contains("MinHeight = PageHeaderMinHeight", battery, StringComparison.Ordinal);
        Assert.Contains("MinHeight = PageHeaderMinHeight", resets, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment = VerticalAlignment.Center", resets, StringComparison.Ordinal);
        Assert.DoesNotContain("actions.VerticalAlignment = VerticalAlignment.Top", resets, StringComparison.Ordinal);

        Assert.Contains("MinHeight = AdvancedWindow.PageHeaderMinHeight", fans, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(reset, 1)", fans, StringComparison.Ordinal);

        // Touchpad is the page with a persistent switch in its header. Keep the
        // subtitle below the shared title/action row so the switch does not drift
        // vertically when switching between pages.
        Assert.Contains("Grid.Row=\"1\" Grid.ColumnSpan=\"2\"", touchpad, StringComparison.Ordinal);
        Assert.Contains("Orientation=\"Horizontal\" VerticalAlignment=\"Center\" MinHeight=\"38\"", touchpad, StringComparison.Ordinal);
    }

    private static string Read(params string[] path)
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
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.UI")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ThinkControl repository root for Advanced header validation.");
    }
}
