using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Services;

/// <summary>
/// Defines which optional hardware capabilities ThinkControl currently expects a
/// detected machine to provide. Unsupported capabilities stay visible in the UI,
/// but they are not treated as faults until an exact/family provider is expected
/// to supply them. Keep this policy separate from hardware implementation and
/// extend it as additional OEM/family/model providers become verified.
/// </summary>
internal static class DeviceCapabilityExpectations
{
    internal static bool ExpectsFanTelemetry(AppState state) =>
        IsVerifiedX9(state.MachineType);

    internal static bool ExpectsWritableFanControl(AppState state) =>
        IsVerifiedX9(state.MachineType);

    internal static bool ExpectsKeyboardBacklight(AppState state) =>
        IsVerifiedX9(state.MachineType);

    internal static bool IsVerifiedX9(string? machineType) =>
        string.Equals(machineType, "21Q6", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(machineType, "21Q7", StringComparison.OrdinalIgnoreCase);
}
