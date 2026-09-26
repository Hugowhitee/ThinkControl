using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class AudioSafetySourceTests
{
    [Fact]
    public void AudioSafety_RemainsOneSubsystemOwnerUnderModes()
    {
        string root = FindRepositoryRoot();
        string app = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "App.AudioSafety.cs"));
        string modes = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "ThinkControlModeCoordinator.cs"));
        string compact = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.QuickControls.cs"));
        string home = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.HomeQuickControls.cs"));
        string touchpad = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "Touchpad", "GestureActionRouter.cs"));

        Assert.Contains("internal AudioSafetyService AudioSafety { get; } = new();", app, StringComparison.Ordinal);
        Assert.Contains("ApplyAudioSafetyModeFromModeAsync", modes, StringComparison.Ordinal);
        Assert.Contains("_app.Modes.ActivateAsync(mode.Id)", compact, StringComparison.Ordinal);
        Assert.Contains("_app.Modes.ActivateAsync(mode.Id)", home, StringComparison.Ordinal);
        Assert.DoesNotContain("_app.SetAudioSafetyModeAsync", compact, StringComparison.Ordinal);
        Assert.DoesNotContain("_app.SetAudioSafetyModeAsync", home, StringComparison.Ordinal);
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
        Assert.Contains("OnVolumeNotification += _ => EnsureSilentOutput()", service, StringComparison.Ordinal);
        Assert.Contains("RegisterEndpointNotificationCallback", service, StringComparison.Ordinal);
        Assert.Contains("OnDefaultDeviceChanged", service, StringComparison.Ordinal);
        Assert.Contains("MMDevice? _silentObservedDevice", service, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _enforcementPending, 1)", service, StringComparison.Ordinal);
        Assert.Contains("Volatile.Read(ref _enforcementPending)", service, StringComparison.Ordinal);
        Assert.Contains("new SilentVolumeKeyBlocker()", service, StringComparison.Ordinal);
        Assert.Contains("HardwareClient.StatusObserved += AudioSafety_StatusObserved", app, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherTimer", service, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Threading.Timer", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Silent_VolumeKeyGuardSuppressesOnlyWindowsVolumeKeys()
    {
        string root = FindRepositoryRoot();
        string blocker = File.ReadAllText(Path.Combine(root, "src", "ThinkControl.UI", "Services", "SilentVolumeKeyBlocker.cs"));

        Assert.Contains("WhKeyboardLl = 13", blocker, StringComparison.Ordinal);
        Assert.Contains("VkVolumeMute = 0xAD", blocker, StringComparison.Ordinal);
        Assert.Contains("VkVolumeDown = 0xAE", blocker, StringComparison.Ordinal);
        Assert.Contains("VkVolumeUp = 0xAF", blocker, StringComparison.Ordinal);
        Assert.Contains("WmKeyDown = 0x0100", blocker, StringComparison.Ordinal);
        Assert.Contains("WmSysKeyDown = 0x0104", blocker, StringComparison.Ordinal);
        Assert.Contains("volumeKey && keyDown", blocker, StringComparison.Ordinal);
        Assert.DoesNotContain("WmKeyUp", blocker, StringComparison.Ordinal);
        Assert.Contains("return (IntPtr)1;", blocker, StringComparison.Ordinal);
        Assert.Contains("CallNextHookEx", blocker, StringComparison.Ordinal);
        Assert.DoesNotContain("VkMediaPlay", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VkMediaNext", blocker, StringComparison.OrdinalIgnoreCase);
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
