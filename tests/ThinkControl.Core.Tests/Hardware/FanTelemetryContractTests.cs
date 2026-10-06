using System.Text.Json;
using ThinkControl.Core.Ipc;
using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class FanTelemetryContractTests
{
    [Fact]
    public void OlderServicePayload_PreservesFanWithoutInventingSharedIdentity()
    {
        var fan = JsonSerializer.Deserialize<FanTelemetrySnapshot>(
            """{"id":"fan-1","label":"Fan 1","rpm":4500,"source":"OEM","primary":true}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(fan);
        Assert.Equal(4500, fan.Rpm);
        Assert.True(fan.Primary);
        Assert.False(fan.Shared);
    }

    [Fact]
    public void SharedTachometer_RetainsItsScopeAcrossServiceBoundary()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var fan = new FanTelemetrySnapshot("shared", "Shared tachometer", 4800, "Provider", true, true);
        Assert.Equal(fan, JsonSerializer.Deserialize<FanTelemetrySnapshot>(JsonSerializer.Serialize(fan, options), options));
    }
}
