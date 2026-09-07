namespace ThinkControl.Core.Audio;

/// <summary>
/// Session-level policy for preventing accidental ThinkControl audio/media actions.
/// This is deliberately separate from Windows/OEM audio implementation details.
/// </summary>
public enum AudioSafetyMode
{
    Normal,
    MediaLock,
    Silent
}

public static class AudioSafetyPolicy
{
    public static bool BlocksTouchpadAudio(AudioSafetyMode mode) =>
        mode is AudioSafetyMode.MediaLock or AudioSafetyMode.Silent;

    public static bool BlocksExplicitOutputChanges(AudioSafetyMode mode) =>
        mode == AudioSafetyMode.Silent;

    public static bool ForcesOutputMute(AudioSafetyMode mode) =>
        mode == AudioSafetyMode.Silent;

    public static string DisplayName(AudioSafetyMode mode) => mode switch
    {
        AudioSafetyMode.MediaLock => "Media lock",
        AudioSafetyMode.Silent => "Silent",
        _ => "Normal"
    };
}
