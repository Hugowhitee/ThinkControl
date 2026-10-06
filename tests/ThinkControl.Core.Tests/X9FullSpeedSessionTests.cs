using ThinkControl.Core.Ipc;
using ThinkControl.Hardware.X9;
using Xunit;

namespace ThinkControl.Core.Tests;

public sealed class X9FullSpeedSessionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-05T12:00:00Z");

    [Fact]
    public void FullSpeedIsVerifiedAndAutoEndsOwnership()
    {
        byte state = 0x80;
        var writes = new List<byte>();
        var session = new X9FullSpeedSession(() => state, () => { writes.Add(0x40); state = 0x40; }, () => { writes.Add(0x80); state = 0x80; });
        session.Start(Now);
        session.Start(Now.AddSeconds(2));
        Assert.True(session.Owned);
        Assert.Equal(new byte[] { 0x40 }, writes);
        session.Stop();
        Assert.False(session.Owned);
        Assert.Equal(new byte[] { 0x40, 0x80 }, writes);
    }

    [Fact]
    public void UnownedManualStateIsNeverAdoptedOrWritten()
    {
        int writes = 0;
        var session = new X9FullSpeedSession(() => 0x40, () => writes++, () => writes++);
        Assert.Throws<InvalidOperationException>(() => session.Start(Now));
        Assert.False(session.Owned);
        Assert.Equal(0, writes);
    }

    [Fact]
    public void AcknowledgmentWithoutReadbackRollsBack()
    {
        int resets = 0;
        var session = new X9FullSpeedSession(() => 0x80, () => { }, () => resets++);
        Assert.Throws<InvalidOperationException>(() => session.Start(Now));
        Assert.Equal(1, resets);
        Assert.False(session.Owned);
    }

    [Fact]
    public void FailedAutoRetainsOwnershipForLaterCleanup()
    {
        byte state = 0x80;
        bool allowAuto = false;
        var session = new X9FullSpeedSession(() => state, () => state = 0x40, () => { if (allowAuto) state = 0x80; });
        session.Start(Now);
        Assert.Throws<InvalidOperationException>(() => session.Stop());
        Assert.True(session.Owned);
        allowAuto = true;
        session.Stop();
        Assert.False(session.Owned);
    }

    [Fact]
    public void MissingClientReturnsAutoAndLiveClientRenewsLease()
    {
        byte state = 0x80;
        var session = new X9FullSpeedSession(() => state, () => state = 0x40, () => state = 0x80);
        session.Start(Now);
        session.Renew(Now.AddSeconds(30));
        session.Check(Now.AddSeconds(60));
        Assert.True(session.Owned);
        session.Check(Now.AddSeconds(75));
        Assert.False(session.Owned);
        Assert.Equal(0x80, state);
    }

    [Fact]
    public void RepeatedLostReadbackReturnsAutoWithoutReissuingMax()
    {
        byte state = 0x80;
        int maxWrites = 0;
        var session = new X9FullSpeedSession(() => state, () => { maxWrites++; state = 0x40; }, () => state = 0x80);
        session.Start(Now);
        state = 4;
        session.Check(Now.AddSeconds(4));
        Assert.True(session.Owned);
        state = 0x40;
        session.Check(Now.AddSeconds(8));
        state = 4;
        session.Check(Now.AddSeconds(12));
        session.Check(Now.AddSeconds(16));
        Assert.False(session.Owned);
        Assert.Equal(1, maxWrites);
    }

    [Theory]
    [InlineData("Auto", true)]
    [InlineData("Lenovo Auto", true)]
    [InlineData("Max cooling", true)]
    [InlineData("builtin:max", true)]
    [InlineData("Balanced", false)]
    [InlineData("Quiet", false)]
    [InlineData("99%", false)]
    [InlineData("custom:gaming", false)]
    public void LimitedCapabilityNeverAdvertisesLowerOrPercentageProfiles(string profile, bool allowed) =>
        Assert.Equal(allowed, FanControlKinds.SupportsProfile(FanControlKinds.FullSpeedOnly, profile));
}
