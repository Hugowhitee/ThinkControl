using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class LenovoEnergyDrvFanProviderSourceTests
{
    [Fact]
    public void EnergyDrvProbe_UsesOnlyKnownReadSideFanQueryContract()
    {
        string source = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoEnergyDrvFanProvider.cs");

        Assert.Contains("DevicePath = @\"\\\\.\\EnergyDrv\"", source, StringComparison.Ordinal);
        Assert.Contains("QueryFanSpeedIoctl = 0x83102570", source, StringComparison.Ordinal);
        Assert.Contains("GenericRead = 0x80000000", source, StringComparison.Ordinal);
        Assert.Contains("TryQueryFanSpeed(handle, index", source, StringComparison.Ordinal);
        Assert.Contains("lenovo-energydrv-", source, StringComparison.Ordinal);
        Assert.Contains("ReadInterval = TimeSpan.FromSeconds(2)", source, StringComparison.Ordinal);

        Assert.DoesNotContain("GenericWrite", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeFanSpeedIoctl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetFan", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CleanDust", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OemCoordinator_UsesEnergyDrvTelemetryWhenOtherModeWriterIsAbsent()
    {
        string provider = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs");
        string controller = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs");

        Assert.Contains("LenovoEnergyDrvFanProvider _energyDrv", provider, StringComparison.Ordinal);
        Assert.Contains("return BuildReadOnlyEnergyDrvFallback(detail);", provider, StringComparison.Ordinal);
        Assert.Contains("EnergyDrv writer intentionally disabled pending exact X9 command validation", provider, StringComparison.Ordinal);
        Assert.Contains("Available: true", provider, StringComparison.Ordinal);
        Assert.Contains("CanControl: false", provider, StringComparison.Ordinal);

        // The controller already gives any OEM provider telemetry priority over direct
        // EC tachometer reads. Once EnergyDrv returns real fan channels this condition
        // prevents the 0x31/0x84/0x85 path being polled merely for live RPM display.
        Assert.Contains("if (oemFanStatus.Fans.Count == 0 &&", controller, StringComparison.Ordinal);
        Assert.Contains("if (oemFanStatus.Fans.Count > 0)", controller, StringComparison.Ordinal);
        Assert.Contains("return oemFanStatus.Fans;", controller, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] path)
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([root, .. path]));
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new(start);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.Hardware")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for Lenovo EnergyDrv validation.");
    }
}
