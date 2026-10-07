using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class CompactDashboardLayoutSourceTests
{
    [Fact]
    public void Mode_LivesWithVolumeAndUsesCompactGeometry()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.xaml.cs"));
        string window = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "MainWindow.xaml"));

        Assert.Contains("Text=\"Mode\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"8\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"40\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"420\" Height=\"565\"", window, StringComparison.Ordinal);
        Assert.Contains("CompactSelect", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Power profile\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Cooling\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Keyboard light\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfigureQuickControlGeometry", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Settings  ›\"", xaml, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for compact layout validation.");
    }
}
