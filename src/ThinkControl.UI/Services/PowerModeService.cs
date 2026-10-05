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
    private readonly Func<Guid, bool, bool> _configure;
    private readonly Func<Guid, uint> _writeOverlay;
    private readonly Func<(bool Success, Guid Mode)> _readOverlay;
    private readonly Func<Guid?> _readPlan;

    public PowerModeService() : this(
        ConfigureGuid,
        PowerSetActiveOverlayScheme,
        () => (TryGetEffective(out Guid mode), mode),
        ReadActivePlan)
    { }

    // A narrow test seam for Windows acceptance/readback; no second state owner.
    internal PowerModeService(
        Func<Guid, bool, bool> configure,
        Func<Guid, uint> writeOverlay,
        Func<(bool Success, Guid Mode)> readOverlay,
        Func<Guid?> readPlan)
    {
        _configure = configure;
        _writeOverlay = writeOverlay;
        _readOverlay = readOverlay;
        _readPlan = readPlan;
    }

    public event Action<ThinkControlPowerMode>? ModeApplied;
    public string? LastEffectiveError { get; private set; }

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
        bool configured = _configure(guid, onBattery);
        if (!configured)
        {
            LastEffectiveError = "Windows could not save the power mode for this power source.";
            return false;
        }
        bool effective = !makeEffective || SetEffective(mode);
        // Persisting a source preference is not proof that Windows applied it.
        return effective;
    }

    public bool SetEffective(ThinkControlPowerMode mode)
    {
        bool changed = TrySetEffective(ToGuid(mode), out string? detail);
        LastEffectiveError = changed ? null : detail;
        if (changed)
        {
            try { ModeApplied?.Invoke(mode); }
            catch { }
        }
        return changed;
    }

    public bool Configure(ThinkControlPowerMode mode, bool onBattery) =>
        _configure(ToGuid(mode), onBattery);

    public ThinkControlPowerMode? GetConfigured(bool onBattery)
    {
        try
        {
            Guid configured;
            uint result = onBattery
                ? PowerGetUserConfiguredDCPowerMode(out configured)
                : PowerGetUserConfiguredACPowerMode(out configured);
            return result == 0 ? TryFromGuid(configured) : null;
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
        (bool success, Guid effective) = _readOverlay();
        if (success)
            return TryFromGuid(effective);
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

    internal string? GetPowerPlanError() => _readPlan() switch
    {
        Guid plan when plan == new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") =>
            "Windows power modes are unavailable while the High performance power plan is active. Select the Balanced power plan in Windows Power Options, then retry.",
        Guid plan when plan == new Guid("a1841308-3541-4fab-bc81-f71556f20b4a") =>
            "Windows power modes are unavailable while the Power saver power plan is active. Select the Balanced power plan in Windows Power Options, then retry.",
        _ => null
    };

    private bool TrySetEffective(Guid requested, out string? detail)
    {
        detail = GetPowerPlanError();
        if (detail is not null)
            return false;
        if (!_effectiveOverlayAvailable)
        {
            detail = "The Windows power-overlay API is unavailable on this system.";
            return false;
        }

        try
        {
            uint result = _writeOverlay(requested);
            if (result != 0)
            {
                detail = $"Windows rejected the power-mode request (code {result}).";
                return false;
            }

            (bool success, Guid effective) = _readOverlay();
            if (!success)
            {
                detail = "Windows accepted the request but did not confirm its effective power mode.";
                return false;
            }

            if (effective != requested)
            {
                string observed = TryFromGuid(effective) is ThinkControlPowerMode mode
                    ? DisplayName(mode) : $"an unknown power overlay ({effective})";
                detail = $"Windows still reports {observed}; requested {DisplayName(FromGuid(requested))}. Check the active power plan in Windows Power Options.";
                return false;
            }

            return true;
        }
        catch (EntryPointNotFoundException)
        {
            _effectiveOverlayAvailable = false;
        }
        catch (DllNotFoundException)
        {
            _effectiveOverlayAvailable = false;
        }

        detail = "The Windows power-overlay API is unavailable on this system.";
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

    private static ThinkControlPowerMode? TryFromGuid(Guid guid) =>
        guid == BestEfficiency ? ThinkControlPowerMode.Quiet :
        guid == BestPerformance ? ThinkControlPowerMode.Performance :
        guid == Balanced ? ThinkControlPowerMode.Balanced : null;

    private static Guid? ReadActivePlan()
    {
        IntPtr pointer = IntPtr.Zero;
        try
        {
            return PowerGetActiveScheme(IntPtr.Zero, out pointer) == 0 && pointer != IntPtr.Zero
                ? Marshal.PtrToStructure<Guid>(pointer) : null;
        }
        catch (EntryPointNotFoundException) { return null; }
        catch (DllNotFoundException) { return null; }
        finally
        {
            if (pointer != IntPtr.Zero)
                LocalFree(pointer);
        }
    }

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr LocalFree(IntPtr memory);

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
