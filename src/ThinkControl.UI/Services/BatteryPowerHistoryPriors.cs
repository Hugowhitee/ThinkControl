namespace ThinkControl.UI.Services;

internal static class BatteryPowerHistoryPriors
{
    private static readonly object Gate = new();
    private static double? _typicalDischargePowerWatts;

    internal static double? TypicalDischargePowerWatts
    {
        get { lock (Gate) return _typicalDischargePowerWatts; }
        set
        {
            lock (Gate)
                _typicalDischargePowerWatts = value is > 0.4 and < 200 ? value : null;
        }
    }
}
