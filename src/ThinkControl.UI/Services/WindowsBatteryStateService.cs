using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace ThinkControl.UI.Services;

public sealed record WindowsBatteryStateSnapshot(
    int? Percent,
    bool OnAc,
    bool Charging,
    bool Discharging,
    double? PowerWatts,
    double? RemainingWh,
    double? FullWh,
    TimeSpan? EstimatedRemaining,
    string Source);

/// <summary>
/// Lightweight battery state for the always-on scheduler. This calls the Windows
/// power manager directly instead of running root\WMI BatteryStatus every few
/// seconds. Slow/static battery metadata remains owned by BatteryTelemetryService
/// when a full refresh is explicitly requested.
/// </summary>
public sealed class WindowsBatteryStateService
{
    private const int SystemBatteryState = 5;
    private const uint UnknownCapacity = uint.MaxValue;
    private const uint UnknownTime = uint.MaxValue;

    public WindowsBatteryStateSnapshot Read()
    {
        int size = Marshal.SizeOf<SystemBatteryStateNative>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (CallNtPowerInformation(SystemBatteryState, IntPtr.Zero, 0, buffer, (uint)size) == 0)
            {
                SystemBatteryStateNative state = Marshal.PtrToStructure<SystemBatteryStateNative>(buffer);
                if (state.BatteryPresent != 0)
                {
                    double? fullWh = state.MaxCapacity is > 0 and not UnknownCapacity
                        ? state.MaxCapacity / 1000d
                        : null;
                    double? remainingWh = state.RemainingCapacity is not UnknownCapacity
                        ? state.RemainingCapacity / 1000d
                        : null;
                    int? percent = fullWh is > 0 && remainingWh.HasValue
                        ? Math.Clamp((int)Math.Round(remainingWh.Value / fullWh.Value * 100d), 0, 100)
                        : null;

                    // SYSTEM_BATTERY_STATE.Rate is signed milliwatts. Firmware varies
                    // on sign convention, while Charging/Discharging is authoritative,
                    // so expose magnitude only and let those flags describe direction.
                    double? power = state.Rate != 0 && state.Rate != int.MinValue
                        ? Math.Abs((double)state.Rate) / 1000d
                        : null;
                    if (power is <= 0.05 or > 500)
                        power = null;

                    TimeSpan? remaining = state.Discharging != 0 && state.EstimatedTime is > 0 and not UnknownTime
                        ? TimeSpan.FromSeconds(state.EstimatedTime)
                        : null;

                    return new WindowsBatteryStateSnapshot(
                        percent,
                        state.AcOnLine != 0,
                        state.Charging != 0,
                        state.Discharging != 0,
                        power,
                        remainingWh,
                        fullWh,
                        remaining,
                        "Windows power manager");
                }
            }
        }
        catch
        {
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        Forms.PowerStatus fallback = Forms.SystemInformation.PowerStatus;
        int? fallbackPercent = fallback.BatteryLifePercent is >= 0 and <= 1
            ? Math.Clamp((int)Math.Round(fallback.BatteryLifePercent * 100d), 0, 100)
            : null;
        bool onAc = fallback.PowerLineStatus == Forms.PowerLineStatus.Online;
        TimeSpan? eta = fallback.BatteryLifeRemaining >= 0
            ? TimeSpan.FromSeconds(fallback.BatteryLifeRemaining)
            : null;
        return new WindowsBatteryStateSnapshot(
            fallbackPercent,
            onAc,
            Charging: false,
            Discharging: !onAc,
            PowerWatts: null,
            RemainingWh: null,
            FullWh: null,
            EstimatedRemaining: eta,
            Source: "Windows power-status fallback");
    }

    [DllImport("powrprof.dll", SetLastError = false)]
    private static extern uint CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        uint inputBufferLength,
        IntPtr outputBuffer,
        uint outputBufferLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemBatteryStateNative
    {
        public byte AcOnLine;
        public byte BatteryPresent;
        public byte Charging;
        public byte Discharging;
        public byte Spare1;
        public byte Spare2;
        public byte Spare3;
        public byte Spare4;
        public uint MaxCapacity;
        public uint RemainingCapacity;
        public int Rate;
        public uint EstimatedTime;
        public uint DefaultAlert1;
        public uint DefaultAlert2;
    }
}
