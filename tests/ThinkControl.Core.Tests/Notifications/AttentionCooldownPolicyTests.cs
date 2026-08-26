using ThinkControl.Core.Notifications;
using Xunit;

namespace ThinkControl.Core.Tests.Notifications;

public sealed class AttentionCooldownPolicyTests
{
    [Theory]
    [InlineData("Hardware service stopped", "hardware:service")]
    [InlineData("PawnIO device needs repair", "hardware:low-level")]
    [InlineData("One or more providers need attention", "hardware:provider")]
    public void HardwareKey_GroupsChangingDetailsIntoStableRootCause(string status, string expected) =>
        Assert.Equal(expected, AttentionCooldownPolicy.HardwareKey(status));

    [Fact]
    public void SameAcknowledgedIssue_IsSuppressedForTwentyFourHours()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.True(AttentionCooldownPolicy.IsSuppressed(
            "hardware:provider", "hardware:provider", now.AddHours(-2).ToString("O"), now));
        Assert.False(AttentionCooldownPolicy.IsSuppressed(
            "hardware:provider", "hardware:provider", now.AddHours(-25).ToString("O"), now));
        Assert.False(AttentionCooldownPolicy.IsSuppressed(
            "hardware:service", "hardware:provider", now.AddMinutes(-2).ToString("O"), now));
    }
}
