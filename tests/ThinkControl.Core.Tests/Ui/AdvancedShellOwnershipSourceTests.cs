using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class AdvancedShellOwnershipSourceTests
{
    [Fact]
    public void AdvancedSidebar_ConstructsUtilitiesDirectlyInTheirFinalOwner()
    {
        string root = FindRepositoryRoot();
        string shell = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.xaml.cs"));
        string branding = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.Branding.cs"));
        string surface = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.Diagnostics.cs"));
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.xaml"));
        string appXaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.xaml"));
        string notificationSheet = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.NotificationSheet.cs"));

        Assert.Contains("AddShellUtilityRow();", shell, StringComparison.Ordinal);
        Assert.Contains("Tag = \"ThinkControl.UtilityRow\"", shell, StringComparison.Ordinal);
        Assert.Contains("ShellUtilityOrder.NotificationTag", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("AddDockControl", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("ThinkControl.NotificationSlot", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Text = \"Advanced\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfigureShellUtilitySizing", shell, StringComparison.Ordinal);
        Assert.Contains("Width = 38", shell, StringComparison.Ordinal);
        Assert.Contains("Height = 38", shell, StringComparison.Ordinal);
        Assert.Contains("Margin = new Thickness(0, 0, 4, 0)", shell, StringComparison.Ordinal);

        Assert.Contains("Tag = \"ThinkControl.BrandRow\"", branding, StringComparison.Ordinal);
        Assert.DoesNotContain("dockRow.Children.Remove", branding, StringComparison.Ordinal);
        Assert.DoesNotContain("var utilityRow = new Grid", branding, StringComparison.Ordinal);

        Assert.DoesNotContain("ConfigureShellUtilitySizing();", surface, StringComparison.Ordinal);

        Assert.Contains("WindowStyle=\"SingleBorderWindow\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowChrome", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TcCaptionButton", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Dock_Click", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Minimize_Click", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Maximize_Click", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Close_Click", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TcCaptionButton", appXaml, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"AdvancedBody\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid body = AdvancedBody;", notificationSheet, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.GetRow(grid) == 1", notificationSheet, StringComparison.Ordinal);
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
