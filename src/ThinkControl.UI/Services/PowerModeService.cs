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
    private static bool _effectiveOverlayAvailable = true;

    public event EventHandler<ThinkControlPowerMode>? ModeApplied;

    public bool Set(ThinkControlPowerMode mode)
    {
        Guid guid = ToGuid(mode);

        bool configured = false;
        try
        {
            uint ac = PowerSetUserConfiguredACPowerMode(ref guid);
            uint dc = PowerSetUserConfiguredDCPowerMode(ref guid);
            configured = ac == 0 && dc == 0;
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }

        bool effective = TrySetEffective(guid);
        bool changed = effective || configured;
        if (changed)
        {
            try { ModeApplied?.Invoke(this, mode); }
            catch { }
        }

        return changed;
    }

    public ThinkControlPowerMode? GetCurrent(bool onBattery)
    {
        if (TryGetEffective(out Guid effective))
            return FromGuid(effective);

        try
        {
            Guid configured;
            uint result = onBattery
                ? PowerGetUserConfiguredDCPowerMode(out configured)
                : PowerGetUserConfiguredACPowerMode(out configured);
            return result == 0 ? FromGuid(configured) : null;
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

    private static bool TrySetEffective(Guid requested)
    {
        if (!_effectiveOverlayAvailable)
            return false;

        try
        {
            if (PowerSetActiveOverlayScheme(requested) != 0)
                return false;

            return !TryGetEffective(out Guid effective) || effective == requested;
        }
        catch (EntryPointNotFoundException)
        {
            _effectiveOverlayAvailable = false;
        }
        catch (DllNotFoundException)
        {
            _effectiveOverlayAvailable = false;
        }

        return false;
    }

    private static bool TryGetEffective(out Guid mode)
    {
        mode = Guid.Empty;
        if (!_effectiveOverlayAvailable)
            return false;

        try
        {
            return PowerGetEffectiveOverlayScheme(out mode) == 0;
        }
        catch (EntryPointNotFoundException)
        {
            _effectiveOverlayAvailable = false;
        }
        catch (DllNotFoundException)
        {
            _effectiveOverlayAvailable = false;
        }

        return false;
    }

    private static Guid ToGuid(ThinkControlPowerMode mode) => mode switch
    {
        ThinkControlPowerMode.Quiet => BestEfficiency,
        ThinkControlPowerMode.Performance => BestPerformance,
        _ => Balanced
    };

    private static ThinkControlPowerMode FromGuid(Guid guid)
    {
        if (guid == BestEfficiency) return ThinkControlPowerMode.Quiet;
        if (guid == BestPerformance) return ThinkControlPowerMode.Performance;
        return ThinkControlPowerMode.Balanced;
    }

    [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveOverlayScheme")]
    private static extern uint PowerSetActiveOverlayScheme(Guid powerModeGuid);

    [DllImport("powrprof.dll", EntryPoint = "PowerGetEffectiveOverlayScheme")]
    private static extern uint PowerGetEffectiveOverlayScheme(out Guid powerModeGuid);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerSetUserConfiguredACPowerMode(ref Guid powerModeGuid);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerSetUserConfiguredDCPowerMode(ref Guid powerModeGuid);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerGetUserConfiguredACPowerMode(out Guid powerModeGuid);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerGetUserConfiguredDCPowerMode(out Guid powerModeGuid);
}
