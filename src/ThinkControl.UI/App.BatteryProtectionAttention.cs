namespace ThinkControl.UI;

public partial class App
{
    private bool? _batteryProtectionObservedCharging;
    private bool? _batteryProtectionObservedOnAc;

    private void ObserveBatteryProtectionTransition(bool charging, bool onAc, int percent)
    {
        bool? previousCharging = _batteryProtectionObservedCharging;
        bool? previousOnAc = _batteryProtectionObservedOnAc;
        _batteryProtectionObservedCharging = charging;
        _batteryProtectionObservedOnAc = onAc;

        // First observation establishes a baseline. Only real later transitions are
        // surfaced, so startup never produces a misleading "paused" notification.
        if (previousCharging is null || previousOnAc is null)
            return;

        if (State.BatteryProtectionEnabled != true ||
            State.BatteryProtectionStartPercent is not int start ||
            State.BatteryProtectionStopPercent is not int stop ||
            !onAc || previousOnAc != true)
        {
            return;
        }

        if (previousCharging == true && !charging && percent >= stop - 2)
        {
            _attentionToast.ShowPassive(
                $"battery-preservation-paused:{start}:{stop}",
                "Battery preservation paused charging",
                $"Battery is at {percent}% · ThinkControl's {start}–{stop}% preservation window is active. Charging resumes below {start}%.",
                TimeSpan.FromSeconds(6));
            return;
        }

        if (previousCharging == false && charging && percent <= start + 2)
        {
            _attentionToast.ShowPassive(
                $"battery-preservation-resumed:{start}:{stop}",
                "Battery preservation resumed charging",
                $"Battery is at {percent}% · charging resumed below the {start}% start threshold and will pause again near {stop}%.",
                TimeSpan.FromSeconds(6));
        }
    }

    internal void ShowBatteryPreservationApplied(int start, int stop)
    {
        _attentionToast.ShowPassive(
            $"battery-preservation-applied:{start}:{stop}",
            "Battery preservation active",
            $"Charging window set to {start}–{stop}%. Charging pauses near {stop}% and resumes below {start}%.",
            TimeSpan.FromSeconds(6));
    }

    internal void ShowBatteryPreservationDisabled()
    {
        _attentionToast.ShowPassive(
            "battery-preservation-disabled",
            "Battery preservation off",
            "Full charging is enabled again.",
            TimeSpan.FromSeconds(5));
    }
}
