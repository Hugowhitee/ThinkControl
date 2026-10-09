namespace ThinkControl.UI.Services;

internal static class BatteryPowerHistoryPriors
{
    private static readonly object Gate = new();
    private static double? _typicalDischargePowerWatts;
    private static double? _typicalChargePowerWatts;

    internal static double? TypicalChargePowerWatts
    {
        get { lock (Gate) return _typicalChargePowerWatts; }
        set { lock (Gate) _typicalChargePowerWatts = value is > 0.4 and < 200 ? value : null; }
    }

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
