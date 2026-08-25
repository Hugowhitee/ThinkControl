using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

/// <summary>
/// Lightweight always-on status scheduler. The original global refresh called WMI,
/// powercfg and display discovery together on a fixed cadence; on some Lenovo
/// firmware that produced a visible system-wide hitch every few seconds. Runtime
/// state now uses direct Windows APIs and the service's cached snapshot. Full/slow
/// discovery still happens on startup and explicit page/repair actions.
/// </summary>
public partial class App
{
    private readonly WindowsBatteryStateService _runtimeBattery = new();
    private DispatcherTimer? _runtimeStatusTimer;
    private bool _runtimeRefreshBusy;
    private double? _runtimeAveragePowerWatts;

    internal void StartRuntimeStatusScheduler()
    {
        _statusTimer?.Stop();

        if (_runtimeStatusTimer is not null)
            return;

        _runtimeStatusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _runtimeStatusTimer.Tick += async (_, _) => await RefreshRuntimeStatusAsync();
        _runtimeStatusTimer.Start();
        _ = RefreshRuntimeStatusAsync();
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

            // EnumDisplaySettings is a direct user32 call and is cheap enough to keep
            // compact/full refresh selectors synchronized without periodic WMI.
            int refresh = DisplayService.GetCurrentRefreshRate();
            if (refresh > 0)
                State.CurrentRefreshHz = refresh;

            _ = await HardwareClient.GetStatusAsync().ConfigureAwait(true);

            if (State.RefreshAutoEnabled)
                ApplyRefreshAuto(onBattery: !battery.OnAc);
        }
        finally
        {
            _runtimeRefreshBusy = false;
        }
    }
}
