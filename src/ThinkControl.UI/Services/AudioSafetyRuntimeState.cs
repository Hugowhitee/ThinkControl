using ThinkControl.Core.Audio;

namespace ThinkControl.UI.Services;

/// <summary>
/// Process-local command gate used by all Windows output helpers. AudioSafetyService
/// is the only writer; consumers may read it to fail closed during Silent.
/// </summary>
internal static class AudioSafetyRuntimeState
{
    private static int _mode = (int)AudioSafetyMode.Normal;

    internal static AudioSafetyMode Mode => (AudioSafetyMode)Volatile.Read(ref _mode);

    internal static void SetMode(AudioSafetyMode mode) =>
        Volatile.Write(ref _mode, (int)mode);
}
