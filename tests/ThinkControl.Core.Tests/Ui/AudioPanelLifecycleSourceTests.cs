using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class AudioPanelLifecycleSourceTests
{
    [Fact]
    public void HiddenAudioPage_ClearsPendingWritesAndDragState()
    {
        string root = FindRepositoryRoot();
        string lifecyclePath = Path.Combine(root, "src", "ThinkControl.UI", "Controls", "AudioPanel.Lifecycle.cs");
        string source = File.ReadAllText(lifecyclePath);

        Assert.Contains("e.Property == IsVisibleProperty", source, StringComparison.Ordinal);
        Assert.Contains("_volumeApplyTimer.Stop();", source, StringComparison.Ordinal);
        Assert.Contains("_microphoneApplyTimer.Stop();", source, StringComparison.Ordinal);
        Assert.Contains("_volumeRefreshTimer.Stop();", source, StringComparison.Ordinal);
        Assert.Contains("_volumeDragging = false;", source, StringComparison.Ordinal);
        Assert.Contains("_microphoneDragging = false;", source, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for Audio lifecycle validation.");
    }
}
