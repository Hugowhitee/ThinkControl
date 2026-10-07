using ThinkControl.Hardware.X9;

namespace ThinkControl.Hardware.Lenovo;

/// <summary>The existing supervisor's verified provider boundary; no UI or raw writes.</summary>
public interface IFanHardwareController
{
    HardwareDeviceIdentity Identity { get; }
    IReadOnlyList<int> FanCalibrationStates { get; }
    string FanCalibrationIdentity { get; }
    bool OwnsManagedFan { get; }
    bool CheckFullSpeedSession();
    LenovoHardwareStatus ReadStatus();
    bool SetFanLevel(int level, out string? error);
    bool SetFanPercent(int percent, out string? detail, out string? error);
    bool ReturnFanToAuto(out string? error);
}
