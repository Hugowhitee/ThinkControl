using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class AudioPanelLifecycleSourceTests
{
    [Fact]
    public void AudioSliders_CommitOnceAndHiddenPageClearsPollingAndDragState()
    {
        string root = FindRepositoryRoot();
        string lifecyclePath = Path.Combine(root, "src", "ThinkControl.UI", "Controls", "AudioPanel.Lifecycle.cs");
        string source = File.ReadAllText(lifecyclePath);

        Assert.Contains("e.Property == IsVisibleProperty", source, StringComparison.Ordinal);
        Assert.Contains("_volumeRefreshTimer.Stop();", source, StringComparison.Ordinal);
        Assert.Contains("_volumeDragging = false;", source, StringComparison.Ordinal);
        Assert.Contains("_microphoneDragging = false;", source, StringComparison.Ordinal);

        string panel = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "AudioPanel.xaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "AudioPanel.xaml"));
        Assert.DoesNotContain("_volumeApplyTimer", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("_microphoneApplyTimer", panel, StringComparison.Ordinal);
        Assert.Contains("if (!_volumeDragging)", panel, StringComparison.Ordinal);
        Assert.Contains("if (!_microphoneDragging)", panel, StringComparison.Ordinal);
        Assert.Contains("PreviewMouseLeftButtonDown=\"VolumeSlider_MouseDown\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewKeyDown=\"VolumeSlider_KeyDown\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewKeyUp=\"VolumeSlider_KeyUp\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewMouseLeftButtonDown=\"MicrophoneSlider_MouseDown\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewKeyDown=\"MicrophoneSlider_KeyDown\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewKeyUp=\"MicrophoneSlider_KeyUp\"", xaml, StringComparison.Ordinal);
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
