using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private async void PowerModeService_ModeApplied(ThinkControlPowerMode mode)
    {
        DeviceValidationState validation = GetDeviceValidationState(State.MachineType, _manufacturer, State.DeviceName);
        if (validation != DeviceValidationState.Verified)
            return;

        // Keep the UI responsive: Windows mode changes immediately, while the
        // verified X9 Lenovo Intelligent Cooling policy follows asynchronously.
        await HardwareClient.SetThermalModeAsync(mode.ToString());
    }
}
