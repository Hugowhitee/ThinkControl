using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;
using ThinkControl.UI.Services;
using ThinkControl.Core.Power;

namespace ThinkControl.UI;

/// <summary>
/// Low-impact runtime telemetry coordinator. Slow discovery is startup/explicit-only;
/// battery sampling becomes sparse while tray-only and stops during suspend. Hardware
/// snapshots are requested only while a ThinkControl window is visible.
/// </summary>
public partial class App
{
    private static readonly TimeSpan RuntimeBatteryVisibleInterval = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan RuntimeBatteryTrayInterval = TimeSpan.FromMinutes(1);

    private readonly WindowsBatteryStateService _runtimeBattery = new();
    private readonly BatteryEtaEstimator _runtimeBatteryEta = new();
    private DispatcherTimer? _runtimeStatusTimer;
    private bool _runtimeRefreshBusy;
    private bool _runtimeEventsAttached;
    private bool _runtimeSuspended;

    internal void StartRuntimeStatusScheduler()
    {
        _statusTimer?.Stop();
        if (_runtimeStatusTimer is not null)
            return;

        AttachRuntimeEvents();
        _runtimeStatusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = RuntimeBatteryVisibleInterval
        };
        _runtimeStatusTimer.Tick += async (_, _) => await RefreshRuntimeStatusAsync();
        _runtimeStatusTimer.Start();

        RefreshRuntimeDisplayState();
        _ = RefreshRuntimeStatusAsync();
    }

    private void AttachRuntimeEvents()
    {
        if (_runtimeEventsAttached)
            return;

        _runtimeEventsAttached = true;
        SystemEvents.DisplaySettingsChanged += Runtime_DisplaySettingsChanged;
        SystemEvents.PowerModeChanged += Runtime_PowerModeChanged;
        Activated += Runtime_Activated;
        Exit += Runtime_Exit;
    }

    private void Runtime_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            RefreshRuntimeDisplayState();
            if (State.RefreshAutoEnabled)
                ApplyRefreshAuto(IsCurrentlyOnBattery());
        }));
    }

    private void Runtime_PowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            _runtimeSuspended = true;
            _runtimeStatusTimer?.Stop();
            return;
        }

        if (e.Mode == PowerModes.Resume)
        {
            _runtimeSuspended = false;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (_runtimeStatusTimer is not null)
                {
                    UpdateRuntimeTimerCadence();
                    _runtimeStatusTimer.Start();
                }
                _ = RefreshRuntimeStatusAsync();
            }));
        }
    }

    private void Runtime_Activated(object? sender, EventArgs e)
    {
        UpdateRuntimeTimerCadence();
        if (ShouldRefreshHardwareRuntime())
            _ = HardwareClient.GetStatusAsync();
    }

    private void Runtime_Exit(object? sender, ExitEventArgs e)
    {
        _runtimeStatusTimer?.Stop();
        if (!_runtimeEventsAttached)
            return;

        SystemEvents.DisplaySettingsChanged -= Runtime_DisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= Runtime_PowerModeChanged;
        Activated -= Runtime_Activated;
        Exit -= Runtime_Exit;
        _runtimeEventsAttached = false;
    }

    private async Task RefreshRuntimeStatusAsync()
    {
        if (_runtimeRefreshBusy || _runtimeSuspended)
            return;

        _runtimeRefreshBusy = true;
        try
        {
            UpdateRuntimeTimerCadence();
            WindowsBatteryStateSnapshot battery = await Task.Run(_runtimeBattery.Read).ConfigureAwait(true);
            if (battery.Percent is int percent)
                State.BatteryPercent = percent;
            State.BatteryCharging = battery.Charging;
            State.BatteryStatus = battery.Charging
                ? "Charging"
                : battery.OnAc
                    ? State.BatteryPercent >= 100 ? "Fully charged" : "Plugged in"
                    : battery.Discharging ? "On battery" : "Power state unknown";
            State.BatteryPowerWatts = battery.PowerWatts;
            State.BatteryRemainingWh = battery.RemainingWh ?? State.BatteryRemainingWh;
            State.BatteryFullWh = battery.FullWh ?? State.BatteryFullWh;
            BatteryEtaEstimate eta = _runtimeBatteryEta.Update(new BatteryEtaSample(
                DateTimeOffset.UtcNow,
                State.BatteryPercent,
                battery.Charging,
                battery.Discharging,
                battery.PowerWatts,
                State.BatteryRemainingWh,
                State.BatteryFullWh,
                battery.EstimatedRemaining));
            State.BatteryEtaToFull = eta.ToFull;
            State.BatteryEtaRemaining = eta.Remaining;
            State.BatterySource = battery.Source;

            State.BatterySmoothedPowerWatts = eta.SmoothedPowerWatts;

            BatteryHistoryView history = BatteryHistoryService.Record(
                battery.Charging,
                State.BatteryPercent,
                battery.PowerWatts,
                battery.RemainingWh,
                battery.FullWh,
                designWh: null);
            State.ApplyBatteryHistory(history);
            BatteryTelemetryService.SetHistoricalChargePower(history.TypicalChargePowerWatts);

            if (ShouldRefreshHardwareRuntime())
                _ = await HardwareClient.GetStatusAsync().ConfigureAwait(true);

            if (State.RefreshAutoEnabled)
                ApplyRefreshAuto(onBattery: !battery.OnAc);
        }
        finally
        {
            _runtimeRefreshBusy = false;
        }
    }

    private void UpdateRuntimeTimerCadence()
    {
        if (_runtimeStatusTimer is null)
            return;
        TimeSpan desired = ShouldRefreshHardwareRuntime() ? RuntimeBatteryVisibleInterval : RuntimeBatteryTrayInterval;
        if (_runtimeStatusTimer.Interval != desired)
            _runtimeStatusTimer.Interval = desired;
    }

    private bool ShouldRefreshHardwareRuntime() =>
        CompactWindow?.IsVisible == true || _advancedWindow?.IsVisible == true;

    private void RefreshRuntimeDisplayState()
    {
        int refresh = DisplayService.GetCurrentRefreshRate();
        if (refresh > 0)
            State.CurrentRefreshHz = refresh;
    }
}
