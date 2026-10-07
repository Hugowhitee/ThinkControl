namespace ThinkControl.Core.Cooling;

/// <summary>
/// Maps a human 0-100% fan target to the discrete states exposed by the
/// provider. Percentages describe measured RPM relative to its verified maximum,
/// not continuous PWM. A provider-supplied state list requires complete measured
/// data; the legacy seven-state fallback is never used for that path.
/// </summary>
public static class FanOutputMapping
{
    public sealed record State(int HardwareState, int EstimatedPercent);

    private static readonly int[] FallbackPercents = [0, 17, 33, 50, 67, 83, 100];

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
        foreach (State state in states)
        {
            if (state.EstimatedPercent >= target)
                return state;
        }

        return states[^1];
    }

    public static IReadOnlyList<State> BuildStates(
        IReadOnlyDictionary<int, int>? medianRpmByState = null,
        IReadOnlyList<int>? requiredStates = null)
    {
        if (requiredStates is not null)
        {
            if (requiredStates.Count < 2 || requiredStates.Distinct().Count() != requiredStates.Count ||
                medianRpmByState is null || requiredStates.Any(state => !medianRpmByState.TryGetValue(state, out int rpm) || rpm <= 0))
                throw new InvalidOperationException("Measure all supported fan speeds before using a curve.");
            int maximum = medianRpmByState[requiredStates[^1]];
            int previous = 0;
            var measured = new List<State>();
            foreach (int state in requiredStates)
            {
                int percent = (int)Math.Round(medianRpmByState[state] * 100.0 / maximum);
                if (percent > 108) throw new InvalidOperationException("A lower state exceeds the measured maximum.");
                percent = Math.Max(previous, Math.Clamp(percent, 1, 100));
                measured.Add(new State(state, percent));
                previous = percent;
            }
            return measured;
        }
        if (TryBuildCalibratedStates(medianRpmByState, out State[] calibrated))
            return calibrated;

        return Enumerable.Range(1, 7)
            .Select((state, index) => new State(state, FallbackPercents[index]))
            .ToArray();
    }

    private static bool TryBuildCalibratedStates(
        IReadOnlyDictionary<int, int>? rpmByState,
        out State[] states)
    {
        states = [];
        if (rpmByState is null ||
            !rpmByState.TryGetValue(7, out int maximumRpm) ||
            maximumRpm <= 0)
        {
            return false;
        }

        var result = new State[7];
        int previousPercent = 0;
        int measuredNormalStates = 0;

        for (int state = 1; state <= 7; state++)
        {
            int percent;
            if (rpmByState.TryGetValue(state, out int rpm) && rpm >= 0)
            {
                percent = state == 7
                    ? 100
                    : (int)Math.Round(Math.Clamp(rpm / (double)maximumRpm * 100.0, 0, 99));
                measuredNormalStates++;
            }
            else
            {
                percent = FallbackPercents[state - 1];
            }

            // EC states should never display a lower calibrated output than the
            // preceding state. Measurement noise is folded into a monotonic scale.
            percent = Math.Max(previousPercent, percent);
            result[state - 1] = new State(state, percent);
            previousPercent = percent;
        }

        // A lone maximum sample is not enough to redefine all lower steps.
        if (measuredNormalStates < 4)
            return false;

        states = result;
        return true;
    }
}
