using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class AudioSafetySourceTests
{
    [Fact]
    public void AudioSafety_IsOneSessionOwnerAcrossCompactSettingsAndTouchpad()
    {
        string root = FindRepositoryRoot();
        string app = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.AudioSafety.cs"));
        string compact = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.QuickControls.cs"));
        string settings = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.AppPreferences.cs"));
        string touchpad = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "Touchpad", "GestureActionRouter.cs"));

        Assert.Contains("internal AudioSafetyService AudioSafety { get; } = new();", app, StringComparison.Ordinal);
        Assert.Contains("_app.AudioSafety.Mode", compact, StringComparison.Ordinal);
        Assert.Contains("_app.SetAudioSafetyModeAsync", compact, StringComparison.Ordinal);
        Assert.Contains("_app.AudioSafety.Mode", settings, StringComparison.Ordinal);
        Assert.Contains("_app.SetAudioSafetyModeAsync", settings, StringComparison.Ordinal);
        Assert.Contains("AudioSafetyPolicy.BlocksTouchpadAudio", touchpad, StringComparison.Ordinal);
    }

    [Fact]
    public void Silent_GuardsRenderWritesButDoesNotCaptureMicrophone()
    {
        string root = FindRepositoryRoot();
        string volume = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "WindowsVolumeService.cs"));
        string audioPanel = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "AudioPanel.xaml.cs"));

        Assert.Contains("flow == DataFlow.Render", volume, StringComparison.Ordinal);
        Assert.Contains("AudioSafetyPolicy.BlocksExplicitOutputChanges", volume, StringComparison.Ordinal);
        Assert.Contains("DataFlow.Capture", audioPanel, StringComparison.Ordinal);
        Assert.Contains("_volume.SetMuted(!muted, DataFlow.Capture)", audioPanel, StringComparison.Ordinal);
    }

    [Fact]
    public void Silent_RestoresOnlyRememberedEndpointMuteStatesAndCreatesNoTimer()
    {
        string root = FindRepositoryRoot();
        string service = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "AudioSafetyService.cs"));
        string app = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.AudioSafety.cs"));

        Assert.Contains("Dictionary<string, bool> _priorMuteByEndpoint", service, StringComparison.Ordinal);
        Assert.Contains("_priorMuteByEndpoint[device.ID] = device.AudioEndpointVolume.Mute", service, StringComparison.Ordinal);
        Assert.Contains("device.AudioEndpointVolume.Mute = priorMuted", service, StringComparison.Ordinal);
        Assert.Contains("HardwareClient.StatusObserved += AudioSafety_StatusObserved", app, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherTimer", service, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Threading.Timer", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Mode_IsDeliberatelyNotPersistedInUserSettings()
    {
        string root = FindRepositoryRoot();
        string settings = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "UserSettingsService.cs"));
        string service = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "AudioSafetyService.cs"));

        Assert.DoesNotContain("AudioSafetyMode", settings, StringComparison.Ordinal);
        Assert.Contains("mode is intentionally not persisted", service, StringComparison.OrdinalIgnoreCase);
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

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for Audio safety validation.");
    }
}
