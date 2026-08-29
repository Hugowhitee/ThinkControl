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
    public void NativeX9OemTelemetry_LatchesSafetyBoundaryAcrossTransientMissesAndProviderRefresh()
    {
        string controller = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs");
        string normalized = controller.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("private bool _nativeOemFanTelemetryConfirmed;", controller, StringComparison.Ordinal);
        Assert.Contains("bool nativeOemFanTelemetryNow = _identity.IsVerifiedX9 && HasNativeOemFanTelemetry(oemFanStatus);", controller, StringComparison.Ordinal);
        Assert.Contains("if (nativeOemFanTelemetryNow)\n                _nativeOemFanTelemetryConfirmed = true;", normalized, StringComparison.Ordinal);
        Assert.Contains("bool nativeOemSafetyBoundary = _identity.IsVerifiedX9 && _nativeOemFanTelemetryConfirmed;", controller, StringComparison.Ordinal);
        Assert.Contains("bool ecAvailable = !nativeOemSafetyBoundary || needEcForThermals", controller, StringComparison.Ordinal);
        Assert.Contains("ResolveFanControlKind(oemFanControl, nativeOemSafetyBoundary, ecAvailable)", controller, StringComparison.Ordinal);
        Assert.Contains("if (_identity.IsVerifiedX9 && nativeOemSafetyBoundary)\n            return LenovoFanControlKind.None;", normalized, StringComparison.Ordinal);
        Assert.Contains("transient telemetry miss cannot re-enable the EC fallback", controller, StringComparison.Ordinal);
        Assert.Contains("Deliberately do not clear _nativeOemFanTelemetryConfirmed", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("_nativeOemFanTelemetryConfirmed = false", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeX9OemTelemetry_DisablesKnownInferiorEcWriterUntilOemWriterIsValidated()
    {
        string controller = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs");

        Assert.Contains("if (HasNativeOemFanTelemetry(oem))\n                _nativeOemFanTelemetryConfirmed = true;", controller.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("if (_nativeOemFanTelemetryConfirmed)", controller, StringComparison.Ordinal);
        Assert.Contains("Raw EC steps are disabled because this X9 has already exposed a native Lenovo two-fan path", controller, StringComparison.Ordinal);
        Assert.Contains("native fan writer pending validation", controller, StringComparison.Ordinal);
        Assert.Contains("Lenovo managed · OEM fan telemetry", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void EcCleanup_OnlyReturnsStatesThatThisControllerActuallyOwns()
    {
        string controller = ReadSource("src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs");
        string normalized = controller.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("_activeFanControlKind = LenovoFanControlKind.ThinkPadEcDiscrete;", controller, StringComparison.Ordinal);
        Assert.Contains(
            "if (_activeFanControlKind == LenovoFanControlKind.ThinkPadEcDiscrete &&\n                IsThinkControlFanState(_fanControl) && _ec is not null)",
            normalized,
            StringComparison.Ordinal);
        Assert.Contains("merely reading\n            // an external/manual EC state does not make ThinkControl its owner", normalized, StringComparison.Ordinal);

        // Startup/status probing may observe another tool's EC state. Cleanup must not
        // claim that state merely because the numeric register resembles a manual step.
        Assert.DoesNotContain(
            "if (IsThinkControlFanState(_fanControl) && _ec is not null)\n        {\n            try { _ec.ReturnToBios(); }",
            normalized,
            StringComparison.Ordinal);
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
