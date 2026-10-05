using ThinkControl.Hardware.X9;
using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class ThinkPadFanSelectorTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void SelectionAndRestore_PreserveEveryUnrelatedFirmwareBit(byte selected)
    {
        for (int original = 0; original <= byte.MaxValue; original++)
        {
            byte changed = ThinkPadFanProtocol.WithFanSelector((byte)original, selected);
            Assert.Equal(original & 0xFE, changed & 0xFE);
            Assert.Equal(selected, changed & 1);
            byte restored = ThinkPadFanProtocol.WithFanSelector(changed, (byte)(original & 1));
            Assert.Equal((byte)original, restored);
        }
    }

    [Fact]
    public void UnknownSelector_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThinkPadFanProtocol.WithFanSelector(0xC4, 2));
    }
}
