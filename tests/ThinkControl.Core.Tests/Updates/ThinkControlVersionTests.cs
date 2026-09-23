using ThinkControl.Core.Updates;
using Xunit;

namespace ThinkControl.Core.Tests.Updates;

public sealed class ThinkControlVersionTests
{
    [Fact]
    public void CanonicalPrerelease_IsNewerThanSameBaseDevelopmentBuild()
    {
        ThinkControlVersion dev = ThinkControlVersion.Parse("0.1.0-alpha.50-dev.1767");
        ThinkControlVersion published = ThinkControlVersion.Parse("0.1.0-alpha.50");

        Assert.True(published.CompareTo(dev) > 0);
        Assert.True(dev.CompareTo(published) < 0);
        Assert.True(dev.IsPrerelease);
        Assert.Equal(1767, dev.DevelopmentBuild);
    }

    [Fact]
    public void NextAlpha_StillBeatsOlderDevelopmentBuild()
    {
        ThinkControlVersion dev = ThinkControlVersion.Parse("0.1.0-alpha.50-dev.9999");
        ThinkControlVersion next = ThinkControlVersion.Parse("0.1.0-alpha.51");

        Assert.True(next.CompareTo(dev) > 0);
    }

    [Fact]
    public void DevelopmentBuilds_StillOrderWithinSameBase()
    {
        ThinkControlVersion older = ThinkControlVersion.Parse("0.1.0-alpha.50-dev.1767");
        ThinkControlVersion newer = ThinkControlVersion.Parse("0.1.0-alpha.50-dev.1768");

        Assert.True(newer.CompareTo(older) > 0);
    }

    [Fact]
    public void StableRelease_RemainsAbovePrerelease()
    {
        ThinkControlVersion alpha = ThinkControlVersion.Parse("0.1.0-alpha.50");
        ThinkControlVersion stable = ThinkControlVersion.Parse("0.1.0");

        Assert.True(stable.CompareTo(alpha) > 0);
    }
}
