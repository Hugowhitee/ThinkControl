using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private async void PowerModeService_ModeApplied(object? sender, ThinkControlPowerMode mode)
    {
        DeviceValidationState validation = GetDeviceValidationState(State.MachineType, _manufacturer, State.DeviceName);
        if (validation != DeviceValidationState.Verified)
            return;

        DateTimeOffset started = DateTimeOffset.UtcNow;
        var response = await HardwareClient.SetThermalModeAsync(mode.ToString());
        bool thermalApplied = response?.Success == true;

        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "thermal.policy_set",
            Capability: "ThermalPolicy",
            Provider: "LenovoLITS",
            ValidationState: validation,
            Success: thermalApplied,
            ErrorCode: thermalApplied ? null : response is null ? "service_no_response" : "lits_policy_unavailable",
            DurationMs: (int)Math.Clamp((DateTimeOffset.UtcNow - started).TotalMilliseconds, 0, 600_000),
            Tags: new Dictionary<string, string>
            {
                ["state"] = mode.ToString(),
                ["windowsApplied"] = "true"
            }));
    }
}
