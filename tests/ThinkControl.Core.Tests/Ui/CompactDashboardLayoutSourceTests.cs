using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class CompactDashboardLayoutSourceTests
{
    [Fact]
    public void FooterAudioSafetySelector_HasDedicatedGeometryAndDoesNotUseCardComboSizing()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.xaml.cs"));

        Assert.Contains("<RowDefinition Height=\"36\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("CompactAudioSafetyCombo", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"28\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Margin=\"0,-1,10,0\"", xaml, StringComparison.Ordinal);

        string geometry = code.Split("private void ConfigureQuickControlGeometry()", StringSplitOptions.None)[1]
            .Split("internal void Initialize(App app)", StringSplitOptions.None)[0];

        string cardLoop = geometry.Split("foreach (ComboBox combo in new[]", StringSplitOptions.None)[1]
            .Split("})", StringSplitOptions.None)[0];

        Assert.DoesNotContain("CompactAudioSafetyCombo", cardLoop, StringComparison.Ordinal);
        Assert.Contains("CompactAudioSafetyCombo.Margin = new Thickness(0, 0, 10, 0);", geometry, StringComparison.Ordinal);
        Assert.Contains("CompactAudioSafetyCombo.MinHeight = 32;", geometry, StringComparison.Ordinal);
        Assert.Contains("CompactAudioSafetyCombo.VerticalAlignment = VerticalAlignment.Center;", geometry, StringComparison.Ordinal);
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
