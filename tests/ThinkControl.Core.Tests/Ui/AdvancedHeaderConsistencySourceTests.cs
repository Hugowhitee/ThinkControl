using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class AdvancedHeaderConsistencySourceTests
{
    [Fact]
    public void AdvancedPages_UseOneSharedHeaderPrimitive()
    {
        string root = FindRepositoryRoot();
        string headerXaml = Read(root, "src", "ThinkControl.UI", "Controls", "AdvancedPageHeader.xaml");
        string headerCode = Read(root, "src", "ThinkControl.UI", "Controls", "AdvancedPageHeader.xaml.cs");
        string shell = Read(root, "src", "ThinkControl.UI", "AdvancedWindow.xaml");
        string consistency = Read(root, "src", "ThinkControl.UI", "AdvancedWindow.UiConsistency.cs");
        string resets = Read(root, "src", "ThinkControl.UI", "AdvancedWindow.ResetDefaults.cs");
        string windowsLinks = Read(root, "src", "ThinkControl.UI", "AdvancedWindow.WindowsSettingsLinks.cs");

        Assert.Contains("MinHeight=\"38\"", headerXaml, StringComparison.Ordinal);
        Assert.Contains("TcText.PageTitle", headerXaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding Actions", headerXaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"1\"", headerXaml, StringComparison.Ordinal);
        Assert.Contains("UpdateSubtitleVisibility", headerCode, StringComparison.Ordinal);

        foreach (string title in new[]
                 {
                     "Overview", "Battery", "Display", "Keyboard",
                     "System", "Updates", "Settings"
                 })
        {
            Assert.Contains($"<controls:AdvancedPageHeader Title=\"{title}\"", shell, StringComparison.Ordinal);
        }

        Assert.Contains("x:Name=\"PageModes\"", shell, StringComparison.Ordinal);
        Assert.Contains("<controls:ModesPanel x:Name=\"ModesPanelControl\"", shell, StringComparison.Ordinal);
        Assert.Contains("\"PageModes\"", consistency, StringComparison.Ordinal);
        Assert.Contains("PageHeaderMinHeight = 38", consistency, StringComparison.Ordinal);

        foreach (string panel in new[]
                 {
                     "ModesPanel.xaml", "PerformancePanel.xaml", "FansPanel.xaml",
                     "AudioPanel.xaml", "TouchpadPanel.xaml"
                 })
        {
            string source = Read(root, "src", "ThinkControl.UI", "Controls", panel);
            Assert.Contains("AdvancedPageHeader", source, StringComparison.Ordinal);
        }

        Assert.Contains("OfType<AdvancedPageHeader>()", resets, StringComparison.Ordinal);
        Assert.Contains("OfType<AdvancedPageHeader>()", windowsLinks, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.Battery.cs")));

        Assert.DoesNotContain(
            "<TextBlock Text=\"Battery\" Style=\"{StaticResource TcText.PageTitle}\"",
            shell,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<TextBlock Text=\"Display\" Style=\"{StaticResource TcText.PageTitle}\"",
            shell,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FullVisualMatrix_CoversEveryAdvancedPageInBothThemes()
    {
        string root = FindRepositoryRoot();
        string snapshots = Read(root, "tools", "ThinkControl.Snapshots", "Program.cs");
        string workflow = Read(root, ".github", "workflows", "ci.yml");

        Assert.Contains("\"Home\", \"Modes\", \"Performance\"", snapshots, StringComparison.Ordinal);
        Assert.Contains("$\"advanced-{page.ToLowerInvariant()}-light.png\"", snapshots, StringComparison.Ordinal);
        Assert.Contains("$\"advanced-{page.ToLowerInvariant()}-min-light.png\"", snapshots, StringComparison.Ordinal);
        Assert.Contains("$\"advanced-{page.ToLowerInvariant()}-wide-light.png\"", snapshots, StringComparison.Ordinal);

        Assert.Contains("'modes'", workflow, StringComparison.Ordinal);
        Assert.Contains("\"advanced-$page-light.png\"", workflow, StringComparison.Ordinal);
        Assert.Contains("\"advanced-$page-min-light.png\"", workflow, StringComparison.Ordinal);
        Assert.Contains("\"advanced-$page-wide-light.png\"", workflow, StringComparison.Ordinal);
        Assert.Contains("advanced-modes-editor.png", workflow, StringComparison.Ordinal);
        Assert.Contains("advanced-modes-editor-light.png", workflow, StringComparison.Ordinal);
    }

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

        throw new DirectoryNotFoundException("Could not locate ThinkControl repository root for Advanced header validation.");
    }
}
