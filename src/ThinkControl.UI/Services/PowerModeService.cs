using System.Runtime.InteropServices;

namespace ThinkControl.UI.Services;

public enum ThinkControlPowerMode
{
    Quiet,
    Balanced,
    Performance
}

public sealed class PowerModeService
{
    private static readonly Guid BestEfficiency = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    private static readonly Guid Balanced = Guid.Empty;
    private static readonly Guid BestPerformance = new("ded574b5-45a0-4f42-8737-46345c09c238");

    public bool Set(ThinkControlPowerMode mode)
    {
        Guid guid = mode switch
        {
            ThinkControlPowerMode.Quiet => BestEfficiency,
            ThinkControlPowerMode.Performance => BestPerformance,
            _ => Balanced
        };

        try
        {
            uint ac = PowerSetUserConfiguredACPowerMode(ref guid);
            uint dc = PowerSetUserConfiguredDCPowerMode(ref guid);
            return ac == 0 && dc == 0;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    public ThinkControlPowerMode? GetCurrent(bool onBattery)
    {
        try
        {
            Guid guid;
            uint result = onBattery
                ? PowerGetUserConfiguredDCPowerMode(out guid)
                : PowerGetUserConfiguredACPowerMode(out guid);
            if (result != 0)
                return null;

            if (guid == BestEfficiency)
                return ThinkControlPowerMode.Quiet;
            if (guid == BestPerformance)
                return ThinkControlPowerMode.Performance;
            if (guid == Balanced)
                return ThinkControlPowerMode.Balanced;
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
    }

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerSetUserConfiguredACPowerMode(ref Guid powerModeGuid);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerSetUserConfiguredDCPowerMode(ref Guid powerModeGuid);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerGetUserConfiguredACPowerMode(out Guid powerModeGuid);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerGetUserConfiguredDCPowerMode(out Guid powerModeGuid);
}
