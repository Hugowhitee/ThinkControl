using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private async Task CompleteStartupIdentityValidationAsync(DiagnosticsConsent initialConsent)
    {
        // The rich WMI inventory is deliberately off the startup/UI critical path.
        // SystemStatusService caches it, so RefreshStatusAsync can share the same
        // single discovery pass rather than creating a second recurring probe.
        SystemStatusSnapshot system;
        try
        {
            system = await Task.Run(SystemStatusService.Read);
        }
        catch
        {
            return;
        }

        _manufacturer = system.Manufacturer;
        DeviceValidationState validation = GetDeviceValidationState(
            system.MachineType,
            system.Manufacturer,
            system.DeviceName);

        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "compatibility.device_detected",
            ValidationState: validation,
            Success: true,
            Tags: new Dictionary<string, string>
            {
                ["state"] = validation.ToString()
            }));

        // Silent Windows startup must remain silent. Unknown-device consent can be
        // changed from Settings after the user opens ThinkControl; never surface a
        // modal compatibility prompt during logon merely because --tray was used.
        if (IsTrayOnlyLaunch() ||
            validation == DeviceValidationState.Verified ||
            initialConsent != DiagnosticsConsent.Unknown)
        {
            return;
        }

        PromptForDeviceValidation(system, validation);
    }
}
