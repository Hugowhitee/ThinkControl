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
        string normalizedProvider = provider.Replace("\r\n", "\n", StringComparison.Ordinal);
        string controller = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs");

        Assert.Contains("LenovoEnergyDrvFanProvider _energyDrv", provider, StringComparison.Ordinal);
        Assert.Contains("return BuildReadOnlyEnergyDrvFallback(detail);", provider, StringComparison.Ordinal);
        Assert.Contains("EnergyDrv writer intentionally disabled pending exact X9 command validation", provider, StringComparison.Ordinal);
        Assert.Contains(
            "new LenovoOtherModeFanStatus(\n            true,\n            false,\n            energy.Fans,\n            []",
            normalizedProvider,
            StringComparison.Ordinal);

        // Native Lenovo telemetry owns observation ahead of direct EC tachometers.
        Assert.Contains("if (oemFanStatus.Fans.Count == 0 &&", controller, StringComparison.Ordinal);
        Assert.Contains("if (oemFanStatus.Fans.Count > 0)", controller, StringComparison.Ordinal);
        Assert.Contains("return oemFanStatus.Fans;", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeX9OemTelemetry_DisablesKnownInferiorEcWriterUntilOemWriterIsValidated()
    {
        string controller = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs");

        Assert.Contains("bool nativeOemFanTelemetry = _identity.IsVerifiedX9 && HasNativeOemFanTelemetry(oemFanStatus);", controller, StringComparison.Ordinal);
        Assert.Contains("ResolveFanControlKind(oemFanControl, nativeOemFanTelemetry, ecAvailable)", controller, StringComparison.Ordinal);
        Assert.Contains("if (_identity.IsVerifiedX9 && nativeOemFanTelemetry)\n            return LenovoFanControlKind.None;", controller.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("Raw EC steps are disabled because the X9 exposes native Lenovo fan telemetry", controller, StringComparison.Ordinal);
        Assert.Contains("native fan writer pending validation", controller, StringComparison.Ordinal);
        Assert.Contains("Lenovo managed · OEM fan telemetry", controller, StringComparison.Ordinal);

        // EC access may still exist as a read-only thermal fallback. The safety rule is
        // that native two-channel OEM fan evidence can no longer grant EC fan writes.
        Assert.Contains("bool ecAvailable = !nativeOemFanTelemetry || needEcForThermals", controller, StringComparison.Ordinal);
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
