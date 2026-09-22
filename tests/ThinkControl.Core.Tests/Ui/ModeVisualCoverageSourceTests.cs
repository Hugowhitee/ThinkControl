using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class ModeVisualCoverageSourceTests
{
    [Fact]
    public void OpeningMode_UsesOneVisibleVocabulary()
    {
        string root = FindRepositoryRoot();
        string preferences = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.AppPreferences.cs"));
        string shellSmoke = File.ReadAllText(Path.Combine(root, "tools", "ThinkControl.ShellSmoke", "Program.cs"));

        Assert.Contains("Content = \"Advanced\"", preferences, StringComparison.Ordinal);
        Assert.DoesNotContain("Content = \"Full\"", preferences, StringComparison.Ordinal);
        Assert.Contains("preferred app-icon Advanced", shellSmoke, StringComparison.Ordinal);
        Assert.DoesNotContain("preferred app-icon Full", shellSmoke, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualQa_CoversOpeningAndAudioSafetyModes()
    {
        string root = FindRepositoryRoot();
        string snapshots = File.ReadAllText(Path.Combine(root, "tools", "ThinkControl.Snapshots", "Program.cs"));

        Assert.Contains("compact-media-lock.png", snapshots, StringComparison.Ordinal);
        Assert.Contains("compact-silent.png", snapshots, StringComparison.Ordinal);
        Assert.Contains("compact-silent-light.png", snapshots, StringComparison.Ordinal);
        Assert.Contains("advanced-home-audio-media-lock.png", snapshots, StringComparison.Ordinal);
        Assert.Contains("advanced-home-audio-silent-min.png", snapshots, StringComparison.Ordinal);
        Assert.Contains("advanced-home-audio-silent-light.png", snapshots, StringComparison.Ordinal);
        Assert.Contains("advanced-settings-opening-advanced.png", snapshots, StringComparison.Ordinal);
        Assert.Contains("advanced-settings-audio-silent.png", snapshots, StringComparison.Ordinal);
        Assert.Contains("advanced-settings-light.png", snapshots, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for UI mode validation.");
    }
}
