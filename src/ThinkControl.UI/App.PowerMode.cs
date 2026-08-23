using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    /// <summary>
    /// Applies the Windows power mode and, only on the verified X9 21Q6/21Q7
    /// profile, also asks Lenovo Intelligent Thermal Solution to apply its
    /// corresponding AC/DC policy. Windows mode remains the primary supported
    /// surface; a missing Lenovo pipe never prevents the Windows change.
    /// </summary>
    public async Task<bool> SetPowerModeWithLenovoAsync(ThinkControlPowerMode mode)
    {
        bool windowsChanged = SetPowerMode(mode);
        DeviceValidationState validation = GetDeviceValidationState(State.MachineType, _manufacturer, State.DeviceName);

        if (validation != DeviceValidationState.Verified)
            return windowsChanged;

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
                ["windowsApplied"] = windowsChanged ? "true" : "false"
            }));

        // A Lenovo policy response can succeed even if the Windows overlay API is
        // unavailable on a future build, and vice versa. Either is a real change.
        if (thermalApplied)
            State.SelectedMode = mode.ToString();

        return windowsChanged || thermalApplied;
    }
}
