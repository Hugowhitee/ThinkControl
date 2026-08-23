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

    public event Action<ThinkControlPowerMode>? ModeApplied;

    /// <summary>
    /// Applies a mode to the currently active power source and stores it only for
    /// that source. Alpha.3 incorrectly wrote the same choice to AC and DC.
    /// </summary>
    public bool Set(ThinkControlPowerMode mode)
    {
        bool onBattery = TryGetOnBattery(out bool battery) && battery;
        return SetForSource(mode, onBattery, makeEffective: true);
    }

    public bool SetForSource(ThinkControlPowerMode mode, bool onBattery, bool makeEffective)
    {
        Guid guid = ToGuid(mode);
        bool configured = ConfigureGuid(guid, onBattery);
        bool effective = !makeEffective || TrySetEffective(guid);
        bool changed = configured || (makeEffective && effective);

        if (makeEffective && changed)
        {
            try { ModeApplied?.Invoke(mode); }
            catch { }
        }

        return changed;
    }

    public bool Configure(ThinkControlPowerMode mode, bool onBattery) =>
        ConfigureGuid(ToGuid(mode), onBattery);

    public ThinkControlPowerMode? GetConfigured(bool onBattery)
    {
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

    public ThinkControlPowerMode? GetCurrent(bool onBattery)
    {
        if (TryGetEffective(out Guid effective))
            return FromGuid(effective);
        return GetConfigured(onBattery);
    }

    public static string DisplayName(ThinkControlPowerMode mode) => mode switch
    {
        ThinkControlPowerMode.Quiet => "Efficiency",
        ThinkControlPowerMode.Balanced => "Balanced",
        ThinkControlPowerMode.Performance => "Performance",
        _ => mode.ToString()
    };

    private static bool ConfigureGuid(Guid guid, bool onBattery)
    {
        try
        {
            uint result = onBattery
                ? PowerSetUserConfiguredDCPowerMode(ref guid)
                : PowerSetUserConfiguredACPowerMode(ref guid);
            return result == 0;
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

    private static bool TryGetOnBattery(out bool onBattery)
    {
        onBattery = false;
        if (!GetSystemPowerStatus(out SystemPowerStatus status) || status.AcLineStatus == 255)
            return false;
        onBattery = status.AcLineStatus == 0;
        return true;
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
