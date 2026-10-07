using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class AdvancedShellOwnershipSourceTests
{
    [Fact]
    public void AdvancedSidebar_ConstructsUtilitiesDirectlyInTheirFinalOwner()
    {
        string root = FindRepositoryRoot();
        string shell = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.xaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.xaml"));
        string consistency = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.UiConsistency.cs"));
        string notificationSheet = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.NotificationSheet.cs"));
        Assert.Contains("WindowStyle=\"SingleBorderWindow\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SidebarNotificationsButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"SidebarCompact_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"2\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AddShellUtilityRow", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfigureSharedPageRails", consistency, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowChrome", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid body = AdvancedBody;", notificationSheet, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for Advanced shell validation.");
    }
}
