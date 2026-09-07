using ThinkControl.Core.Audio;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private bool _audioSafetyRuntimeHooked;

    internal AudioSafetyService AudioSafety { get; } = new();

    internal async Task<AudioSafetyTransitionResult> SetAudioSafetyModeAsync(AudioSafetyMode mode)
    {
        EnsureAudioSafetyRuntimeHook();
        AudioSafetyTransitionResult result = await AudioSafety.SetModeAsync(mode);
        if (result.Success && mode != AudioSafetyMode.Normal)
            _touchpadFeature?.CancelAudioActions();
        return result;
    }

    private void EnsureAudioSafetyRuntimeHook()
    {
        if (_audioSafetyRuntimeHooked)
            return;
        _audioSafetyRuntimeHooked = true;
        HardwareClient.StatusObserved += AudioSafety_StatusObserved;
    }

    private void AudioSafety_StatusObserved(object? sender, ServiceResponse? response) =>
        AudioSafety.EnsureSilentOutput();

    private void DisposeAudioSafety()
    {
        if (_audioSafetyRuntimeHooked)
            HardwareClient.StatusObserved -= AudioSafety_StatusObserved;
        _audioSafetyRuntimeHooked = false;
        AudioSafety.Dispose();
    }
}
