using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

/// <summary>
/// Low-impact runtime telemetry coordinator. Slow discovery is startup/explicit-only;
/// the steady-state path samples the native Windows battery API at the same cadence
/// used by battery history, reacts to display changes through Windows events, and
/// requests hardware-service snapshots only while a ThinkControl window is visible.
/// The hardware service remains responsible for fan safety when the UI is hidden.
/// </summary>
public partial class App
{
    private static readonly TimeSpan RuntimeBatteryInterval = TimeSpan.FromSeconds(10);

    private readonly WindowsBatteryStateService _runtimeBattery = new();
    private DispatcherTimer? _runtimeStatusTimer;
    private bool _runtimeRefreshBusy;
    private bool _runtimeEventsAttached;
    private double? _runtimeAveragePowerWatts;

    internal void StartRuntimeStatusScheduler()
    {
        // Alpha.14 and earlier used the heavyweight RefreshStatusAsync timer. Stop it
        // permanently once the one startup discovery pass has completed.
        _statusTimer?.Stop();

        if (_runtimeStatusTimer is not null)
            return;

        AttachRuntimeEvents();
        _runtimeStatusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = RuntimeBatteryInterval
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

    private void Runtime_Activated(object? sender, EventArgs e)
    {
        // Resuming interaction should show current fan/sensor/keyboard state without
        // keeping the named-pipe + hardware snapshot path alive while tray-only.
        if (ShouldRefreshHardwareRuntime())
            _ = HardwareClient.GetStatusAsync();
    }

    private void Runtime_Exit(object? sender, ExitEventArgs e)
    {
        _runtimeStatusTimer?.Stop();
        if (!_runtimeEventsAttached)
            return;

        SystemEvents.DisplaySettingsChanged -= Runtime_DisplaySettingsChanged;
        Activated -= Runtime_Activated;
        Exit -= Runtime_Exit;
        _runtimeEventsAttached = false;
    }

    private async Task RefreshRuntimeStatusAsync()
    {
        if (_runtimeRefreshBusy)
            return;

        _runtimeRefreshBusy = true;
        try
        {
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
            State.BatteryEtaToFull = null;
            State.BatteryEtaRemaining = battery.Discharging ? battery.EstimatedRemaining : null;
            State.BatterySource = battery.Source;

            if (battery.PowerWatts is double watts)
            {
                _runtimeAveragePowerWatts = _runtimeAveragePowerWatts.HasValue
                    ? _runtimeAveragePowerWatts.Value + 0.12 * (watts - _runtimeAveragePowerWatts.Value)
                    : watts;
                State.BatterySmoothedPowerWatts = _runtimeAveragePowerWatts;
            }
            else if (!battery.Charging && !battery.Discharging)
            {
                _runtimeAveragePowerWatts = null;
                State.BatterySmoothedPowerWatts = null;
            }

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

    private bool ShouldRefreshHardwareRuntime() =>
        CompactWindow?.IsVisible == true || _advancedWindow?.IsVisible == true;

    private void RefreshRuntimeDisplayState()
    {
        int refresh = DisplayService.GetCurrentRefreshRate();
        if (refresh > 0)
            State.CurrentRefreshHz = refresh;
    }
}
