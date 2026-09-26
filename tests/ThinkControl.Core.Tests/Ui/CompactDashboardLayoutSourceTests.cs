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

        Assert.Contains("<RowDefinition Height=\"122\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<RowDefinition Height=\"34\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Mode\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"2\" Grid.Column=\"1\" Grid.ColumnSpan=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"38\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"168\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Left\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"520\"", window, StringComparison.Ordinal);

        int footer = xaml.IndexOf("<Grid Grid.Row=\"4\"", StringComparison.Ordinal);
        Assert.True(footer >= 0);
        string footerBlock = xaml[footer..];
        Assert.DoesNotContain("CompactModeCombo", footerBlock, StringComparison.Ordinal);
        Assert.Contains("Content=\"Audio\"", footerBlock, StringComparison.Ordinal);
        Assert.Contains("Content=\"Settings  ›\"", footerBlock, StringComparison.Ordinal);

        string geometry = code.Split("private void ConfigureQuickControlGeometry()", StringSplitOptions.None)[1]
            .Split("internal void Initialize(App app)", StringSplitOptions.None)[0];
        Assert.Contains("CompactModeCombo.MinHeight = 38;", geometry, StringComparison.Ordinal);
        Assert.Contains("CompactModeCombo.Margin = new Thickness(0);", geometry, StringComparison.Ordinal);
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
