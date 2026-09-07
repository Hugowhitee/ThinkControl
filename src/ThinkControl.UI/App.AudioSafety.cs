using ThinkControl.Core.Audio;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    internal AudioSafetyService AudioSafety { get; } = new();

    internal async Task<AudioSafetyTransitionResult> SetAudioSafetyModeAsync(AudioSafetyMode mode)
    {
        AudioSafetyTransitionResult result = await AudioSafety.SetModeAsync(mode);
        if (result.Success && mode != AudioSafetyMode.Normal)
            _touchpadFeature?.CancelAudioActions();
        return result;
    }

    private void InitializeAudioSafetyLifecycle()
    {
        Exit += (_, _) =>
        {
            try { AudioSafety.Dispose(); }
            catch { }
        };
    }
}
