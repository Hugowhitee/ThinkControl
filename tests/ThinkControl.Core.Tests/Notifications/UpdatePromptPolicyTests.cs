using ThinkControl.Core.Notifications;
using Xunit;

namespace ThinkControl.Core.Tests.Notifications;

public sealed class UpdatePromptPolicyTests
{
    [Fact]
    public void DismissalSuppressesOnlyTheExactVersion()
    {
        Assert.True(UpdatePromptPolicy.IsDismissed("v0.1.0-alpha.29", "v0.1.0-alpha.29"));
        Assert.True(UpdatePromptPolicy.IsDismissed(" V0.1.0-ALPHA.29 ", "v0.1.0-alpha.29"));
        Assert.False(UpdatePromptPolicy.IsDismissed("v0.1.0-alpha.30", "v0.1.0-alpha.29"));
        Assert.False(UpdatePromptPolicy.IsDismissed("v0.1.0-alpha.29", ""));
        Assert.False(UpdatePromptPolicy.IsDismissed("", "v0.1.0-alpha.29"));
    }

    [Theory]
    [InlineData("v0.1.0-alpha.28", "v0.1.0-alpha.29", "0.1.0-alpha.28  →  0.1.0-alpha.29")]
    [InlineData("", "v0.1.0-alpha.29", "new version  →  0.1.0-alpha.29")]
    public void TransitionFormatsCurrentAndAvailableVersions(string current, string available, string expected) =>
        Assert.Equal(expected, UpdatePromptPolicy.Transition(current, available));
}
