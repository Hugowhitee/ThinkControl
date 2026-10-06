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

    [Fact]
    public void RegulatedTransitionsVerifyOwnershipAndBridgeMaxToLower()
    {
        byte state = 0x80;
        bool oemOwned = false;
        var writes = new List<byte>();
        var session = new X9FullSpeedSession(() => state, () => state = 0x40,
            () => { writes.Add(0x80); state = 0x80; },
            next => { oemOwned = true; writes.Add(next); state = next; },
            () => oemOwned, () => oemOwned = false);
        session.SetRegulatedState(7, Now);
        session.SetRegulatedState(7, Now.AddSeconds(1));
        session.SetRegulatedState(0x40, Now.AddSeconds(2));
        session.SetRegulatedState(6, Now.AddSeconds(3));
        Assert.Equal(new byte[] { 7, 0x40, 0x80, 6 }, writes);
        Assert.True(session.Regulated);
        Assert.Equal(6, session.State);
        session.Stop();
        Assert.False(oemOwned);
        Assert.False(session.Owned);
        Assert.Equal(0x80, state);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(8)] [InlineData(99)]
    public void UnreviewedRegulatedStatesNeverReachWriter(byte requested)
    {
        int writes = 0;
        var session = new X9FullSpeedSession(() => 0x80, () => writes++, () => writes++,
            _ => writes++, () => true, () => writes++);
        Assert.Throws<InvalidOperationException>(() => session.SetRegulatedState(requested, Now));
        Assert.Equal(0, writes);
        Assert.False(session.Owned);
    }

    [Fact]
    public void LostOemOwnershipIsNotReacquiredAndCleanupRetainsFailedRecovery()
    {
        byte state = 0x80;
        bool oemOwned = false, canRestore = false;
        int writes = 0;
        var session = new X9FullSpeedSession(() => state, () => state = 0x40, () => state = 0x80,
            next => { writes++; state = next; oemOwned = true; }, () => oemOwned,
            () => { if (!canRestore) throw new IOException("Provider offline"); oemOwned = false; });
        session.SetRegulatedState(4, Now);
        oemOwned = false;
        Assert.Throws<InvalidOperationException>(() => session.SetRegulatedState(5, Now.AddSeconds(1)));
        session.Check(Now.AddSeconds(4));
        Assert.Throws<IOException>(() => session.Check(Now.AddSeconds(8)));
        Assert.True(session.Owned);
        Assert.Equal(0x80, state); // OEM failure still releases the independent EC owner.
        Assert.Equal(1, writes);
        canRestore = true;
        session.Stop();
        Assert.False(session.Owned);
    }

    [Fact]
    public void RegulatedLeaseExpiryReleasesOemAndEcWithoutRewritingOutput()
    {
        byte state = 0x80;
        bool oemOwned = false;
        int writes = 0;
        var session = new X9FullSpeedSession(() => state, () => state = 0x40, () => state = 0x80,
            next => { writes++; state = next; oemOwned = true; }, () => oemOwned, () => oemOwned = false);
        session.SetRegulatedState(5, Now);
        session.Check(Now.AddSeconds(45));
        Assert.False(session.Owned);
        Assert.False(oemOwned);
        Assert.Equal(0x80, state);
        Assert.Equal(1, writes);
    }
}
