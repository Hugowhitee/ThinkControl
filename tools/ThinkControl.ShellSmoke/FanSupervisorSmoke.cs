using System.Reflection;
using System.IO;
using ThinkControl.Core.Cooling;
using ThinkControl.Hardware.Lenovo;
using ThinkControl.Hardware.X9;
using ThinkControl.Service;

namespace ThinkControl.ShellSmoke;

internal static partial class Program
{
    private static async Task ValidateFanSupervisorRecovery()
    {
        var hardware = new FanHardwareFixture();
        using var supervisor = new FanSupervisor(hardware, _ => { });
        async Task Tick() => await (Task)typeof(FanSupervisor)
            .GetMethod("ApplyProfileTickAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(supervisor, [CancellationToken.None])!;
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        Require(supervisor.SetCurve(FanCurveDefaults.Balanced, out _), "A warm system rejected a curve.");
        await Tick();
        Require(hardware.Writes == 1, "The supervisor did not apply its curve to the provider.");
        hardware.Temperature = null;
        await Tick();
        Require(supervisor.Snapshot().ProfileId == FanCurveDefaults.Balanced.Id && supervisor.Snapshot().SafetyOverride,
            "A missing sensor erased the user's curve instead of pausing it in Auto.");
        int resets = hardware.AutoWrites;
        await Tick();
        Require(hardware.AutoWrites == resets, "A missing sensor repeatedly wrote Auto.");
        hardware.Temperature = 85;
        await Tick();
        Require(hardware.Writes == 1, "The curve resumed on a single recovery sample.");
        await Tick();
        Require(hardware.Writes == 2 && !supervisor.Snapshot().SafetyOverride, "The curve did not resume after stable sensor recovery.");

        hardware.Temperature = 95;
        await Tick();
        Require(supervisor.Snapshot().SafetyOverride, "Critical temperature did not yield to firmware.");
        Require(supervisor.SetCurve(FanCurveDefaults.Quiet, out _), "A hot-system selection was discarded instead of queued.");
        Require(supervisor.Snapshot().ProfileId == FanCurveDefaults.Quiet.Id, "Queued curve was not retained.");
        int writes = hardware.Writes;
        await Tick();
        Require(hardware.Writes == writes, "A queued curve wrote a fan target while critically hot.");
        hardware.Temperature = 89;
        await Tick();
        hardware.Temperature = 91;
        await Tick();
        hardware.Temperature = 89;
        await Tick();
        Require(hardware.Writes == writes, "Recovery did not require consecutive safe samples.");
        await Tick();
        Require(hardware.Writes == writes + 1, "The queued curve did not resume.");

        // A failed actual write is different from a missing sample. Never keep
        // retrying a controller that may have reclaimed ownership.
        Require(supervisor.SetCurve(FanCurveDefaults.Balanced, out _), "Failed to select the final test curve.");
        hardware.FailWrites = true;
        await Tick();
        Require(supervisor.Snapshot().ProfileId is null, "A rejected write retained an automatically retrying curve.");
        writes = hardware.Writes;
        await Tick();
        Require(hardware.Writes == writes, "A rejected provider was repeatedly reacquired.");
        hardware.FailWrites = false;
        Require(supervisor.SetCurve(FanCurveDefaults.Balanced, out _), "Could not select the recovery test curve.");
        await Tick();
        hardware.ThrowStatus = true;
        hardware.FailAuto = true;
        await Tick();
        writes = hardware.Writes;
        hardware.ThrowStatus = false;
        await Tick(); await Tick();
        Require(hardware.Writes == writes && supervisor.Snapshot().SafetyOverride,
            "A curve resumed without a confirmed Auto handoff.");
        hardware.FailAuto = false;
        await Tick(); await Tick(); await Tick();
        Require(hardware.Writes == writes + 1, "A failed Auto handoff could not recover after confirmation.");
        hardware.Temperature = null;
        await Tick();
        hardware.Temperature = 53;
        hardware.Kind = LenovoFanControlKind.ThinkPadEcDiscrete;
        await Tick();
        Require(supervisor.Snapshot().ProfileId is null, "A paused curve silently migrated to another provider.");
        Console.WriteLine("Fan supervisor: warm input/output, missing-sensor retention, bounded Auto writes, consecutive-sample recovery, hot selection queue and rejected-write handoff passed (simulated provider).");
    }

    private sealed class FanHardwareFixture : IFanHardwareController
    {
        public double? Temperature = 53;
        public int Writes;
        public int AutoWrites;
        public bool FailWrites;
        public bool FailAuto;
        public bool ThrowStatus;
        public LenovoFanControlKind Kind = LenovoFanControlKind.LenovoOtherModeTargetRpm;
        public HardwareDeviceIdentity Identity { get; } = new("QA", "Simulated provider", "QA", false);
        public IReadOnlyList<int> FanCalibrationStates => [1, 2, 3, 4, 5, 6, 7];
        public string FanCalibrationIdentity => "qa";
        public bool OwnsManagedFan { get; private set; }
        public bool CheckFullSpeedSession() => false;
        public LenovoHardwareStatus ReadStatus() => ThrowStatus ? throw new IOException("Simulated telemetry failure") : new(Identity, Temperature, "QA", Temperature, "QA", [],
            null, "QA", [], "QA", "QA", "QA", "QA", false, true,
            Kind, false, true, true);
        public bool SetFanLevel(int level, out string? error) => throw new InvalidOperationException("Wrong provider route.");
        public bool SetFanPercent(int percent, out string? detail, out string? error)
        {
            Writes++;
            detail = null;
            error = FailWrites ? "Fan control was reclaimed." : null;
            OwnsManagedFan = !FailWrites;
            return !FailWrites;
        }
        public bool ReturnFanToAuto(out string? error)
        {
            AutoWrites++;
            if (!FailAuto) OwnsManagedFan = false;
            error = FailAuto ? "Simulated Auto readback failure" : null;
            return !FailAuto;
        }
    }
}
