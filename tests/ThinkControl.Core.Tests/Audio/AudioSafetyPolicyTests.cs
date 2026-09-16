using ThinkControl.Core.Audio;
using Xunit;

namespace ThinkControl.Core.Tests.Audio;

public sealed class AudioSafetyPolicyTests
{
    [Theory]
    [InlineData(AudioSafetyMode.Normal, false, false, false)]
    [InlineData(AudioSafetyMode.MediaLock, true, false, false)]
    [InlineData(AudioSafetyMode.Silent, true, true, true)]
    public void Mode_HasExpectedAudioBoundaries(
        AudioSafetyMode mode,
        bool blocksTouchpadAudio,
        bool blocksExplicitOutput,
        bool forcesMute)
    {
        Assert.Equal(blocksTouchpadAudio, AudioSafetyPolicy.BlocksTouchpadAudio(mode));
        Assert.Equal(blocksExplicitOutput, AudioSafetyPolicy.BlocksExplicitOutputChanges(mode));
        Assert.Equal(forcesMute, AudioSafetyPolicy.ForcesOutputMute(mode));
    }

    [Theory]
    [InlineData(AudioSafetyMode.Normal, "Normal")]
    [InlineData(AudioSafetyMode.MediaLock, "Media lock")]
    [InlineData(AudioSafetyMode.Silent, "Silent")]
    public void DisplayName_IsStable(AudioSafetyMode mode, string expected) =>
        Assert.Equal(expected, AudioSafetyPolicy.DisplayName(mode));
}
