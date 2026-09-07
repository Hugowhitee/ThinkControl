using ThinkControl.Core.Audio;

namespace ThinkControl.UI.Controls;

public partial class AudioPanel
{
    internal void PrepareAudioSafetyForSnapshot(AudioSafetyMode mode)
    {
        if (mode != AudioSafetyMode.Silent)
            return;

        VolumeSlider.IsEnabled = false;
        MuteButton.IsEnabled = false;
        VolumeValueText.Text = "Silent";
        VolumeDeviceText.Text = "Silent · output locked by Audio safety · default Windows output";
    }
}
