using ThinkControl.Core.Battery;
using Xunit;

namespace ThinkControl.Core.Tests.Battery;

public sealed class BatteryHistoryRetentionPolicyTests
{
    [Theory]
    [InlineData(1, 7)]
    [InlineData(7, 7)]
    [InlineData(8, 14)]
    [InlineData(14, 14)]
    [InlineData(15, 30)]
    [InlineData(90, 30)]
    public void DetailedRetention_UsesSupportedChoices(int requested, int expected) =>
        Assert.Equal(expected, BatteryHistoryRetentionPolicy.NormalizeDetailedDays(requested));

    [Fact]
    public void ExpiredSamples_DoNotRemoveLongTermSummary()
    {
        DateTimeOffset now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset sessionEnd = now.AddDays(-8);

        Assert.False(BatteryHistoryRetentionPolicy.KeepDetailedSamples(sessionEnd, now, 7));
        Assert.True(BatteryHistoryRetentionPolicy.KeepSummary(sessionEnd, now));
    }

    [Fact]
    public void SummaryExpiresAfterOneYear()
    {
        DateTimeOffset now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

        Assert.False(BatteryHistoryRetentionPolicy.KeepSummary(now.AddDays(-366), now));
    }
}
