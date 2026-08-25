namespace ThinkControl.Core.Cooling;

/// <summary>
/// Maps a human 0-100% fan target to the discrete states exposed by the
/// ThinkPad EC. When characterization data is available, percentages are based
/// on measured RPM relative to the separately verified full-speed state. This
/// avoids pretending that EC step 7 is 100% when the hardware proves otherwise.
/// </summary>
public static class FanOutputMapping
{
    public sealed record State(int HardwareState, int EstimatedPercent, bool FullSpeed);

    private static readonly int[] FallbackPercents = [0, 16, 32, 48, 64, 80, 99, 100];

    public static State Resolve(
        int targetPercent,
        IReadOnlyDictionary<int, int>? medianRpmByState = null)
    {
        int target = Math.Clamp(targetPercent, 0, 100);
        IReadOnlyList<State> states = BuildStates(medianRpmByState);

        if (target >= 100)
            return states[^1];

        // Cooling targets are floors rather than cosmetic labels. Pick the lowest
        // available state that can meet or exceed the requested calibrated output.
        // If step 7 measures only ~55% of full speed, a 60% target therefore moves
        // to the separate full-speed state instead of silently under-cooling.
        foreach (State state in states)
        {
            if (state.EstimatedPercent >= target)
                return state;
        }

        return states[^1];
    }

    public static IReadOnlyList<State> BuildStates(
        IReadOnlyDictionary<int, int>? medianRpmByState = null)
    {
        if (TryBuildCalibratedStates(medianRpmByState, out State[] calibrated))
            return calibrated;

        return Enumerable.Range(1, 8)
            .Select((state, index) => new State(state, FallbackPercents[index], state == 8))
            .ToArray();
    }

    private static bool TryBuildCalibratedStates(
        IReadOnlyDictionary<int, int>? rpmByState,
        out State[] states)
    {
        states = [];
        if (rpmByState is null ||
            !rpmByState.TryGetValue(8, out int fullSpeedRpm) ||
            fullSpeedRpm <= 0)
        {
            return false;
        }

        var result = new State[8];
        int previousPercent = 0;
        int measuredNormalStates = 0;

        for (int state = 1; state <= 7; state++)
        {
            int percent;
            if (rpmByState.TryGetValue(state, out int rpm) && rpm >= 0)
            {
                percent = (int)Math.Round(Math.Clamp(rpm / (double)fullSpeedRpm * 100.0, 0, 99));
                measuredNormalStates++;
            }
            else
            {
                percent = FallbackPercents[state - 1];
            }

            // EC states should never display a lower calibrated output than the
            // preceding state. Measurement noise is folded into a monotonic scale.
            percent = Math.Max(previousPercent, percent);
            result[state - 1] = new State(state, percent, false);
            previousPercent = percent;
        }

        result[7] = new State(8, 100, true);

        // A lone full-speed sample is not enough to redefine all normal steps.
        // Require several measured normal states before using the calibrated scale.
        if (measuredNormalStates < 3)
            return false;

        states = result;
        return true;
    }
}
