namespace ThinkControl.Core.Touchpad;

public enum TouchpadEdge
{
    Left,
    Right,
    Top,
    Bottom
}

public enum GestureActionKind
{
    Disabled,
    Volume,
    Brightness,
    MediaSeek,
    PreviousNextTrack,
    PlayPause,
    // Legacy values are kept so existing numeric JSON settings remain readable.
    // Sanitize() migrates them to the current action model.
    Mute,
    TaskView,
    ShowDesktop,
    KeyboardBacklight,
    PerformanceMode,
    CustomShortcut,
    OpenThinkControl
}

public enum GesturePhase
{
    Candidate,
    Claimed,
    Active,
    Released,
    Cancelled
}

public sealed record TouchpadEdgeBinding(
    GestureActionKind Action,
    bool Inverted = false,
    double Sensitivity = 1.0)
{
    public TouchpadEdgeBinding Sanitize() => this with
    {
        Action = SanitizeAction(Action),
        Sensitivity = Math.Clamp(double.IsFinite(Sensitivity) ? Sensitivity : 1.0, 0.25, 4.0)
    };

    private static GestureActionKind SanitizeAction(GestureActionKind action) => action switch
    {
        GestureActionKind.Mute => GestureActionKind.Volume,
        GestureActionKind.TaskView or
        GestureActionKind.ShowDesktop or
        GestureActionKind.KeyboardBacklight or
        GestureActionKind.PerformanceMode or
        GestureActionKind.CustomShortcut => GestureActionKind.Disabled,
        _ when Enum.IsDefined(action) => action,
        _ => GestureActionKind.Disabled
    };
}

public sealed record TouchpadGestureBindings(
    TouchpadEdgeBinding? Left = null,
    TouchpadEdgeBinding? Right = null,
    TouchpadEdgeBinding? Top = null,
    TouchpadEdgeBinding? Bottom = null)
{
    public static TouchpadGestureBindings AsusStyle { get; } = new(
        new(GestureActionKind.Volume),
        new(GestureActionKind.Brightness),
        new(GestureActionKind.MediaSeek),
        new(GestureActionKind.Disabled));

    public TouchpadEdgeBinding Get(TouchpadEdge edge) => edge switch
    {
        TouchpadEdge.Left => Left ?? AsusStyle.Left!,
        TouchpadEdge.Right => Right ?? AsusStyle.Right!,
        TouchpadEdge.Top => Top ?? AsusStyle.Top!,
        TouchpadEdge.Bottom => Bottom ?? AsusStyle.Bottom!,
        _ => new(GestureActionKind.Disabled)
    };

    public TouchpadGestureBindings Sanitize()
    {
        TouchpadEdgeBinding left = (Left ?? AsusStyle.Left)!.Sanitize();
        TouchpadEdgeBinding right = (Right ?? AsusStyle.Right)!.Sanitize();
        TouchpadEdgeBinding top = (Top ?? AsusStyle.Top)!.Sanitize();
        TouchpadEdgeBinding bottom = (Bottom ?? AsusStyle.Bottom)!.Sanitize();

        // A gesture action represents one physical affordance. Keeping the same
        // non-Off action on multiple edges makes the visualizer ambiguous and is
        // almost always accidental. Preserve the first occurrence when loading old
        // settings; the UI actively moves an action when the user reassigns it.
        var used = new HashSet<GestureActionKind>();
        left = KeepUnique(left, used);
        right = KeepUnique(right, used);
        top = KeepUnique(top, used);
        bottom = KeepUnique(bottom, used);
        return new(left, right, top, bottom);
    }

    private static TouchpadEdgeBinding KeepUnique(
        TouchpadEdgeBinding binding,
        HashSet<GestureActionKind> used)
    {
        if (binding.Action == GestureActionKind.Disabled)
            return binding;
        return used.Add(binding.Action)
            ? binding
            : binding with { Action = GestureActionKind.Disabled };
    }
}

public sealed record TouchpadGestureConfiguration(
    bool Enabled = true,
    double EdgeWidthMm = 5.0,
    double ActivationDistanceMm = 2.0,
    double ContinuationToleranceMm = 12.0,
    double DirectionDominance = 1.15,
    bool LockCursor = true,
    bool HideCursorWhenActive = true,
    bool TrackCenterPlayPauseEnabled = false,
    TouchpadGestureBindings? Bindings = null)
{
    public static TouchpadGestureConfiguration Default { get; } = new(
        Bindings: TouchpadGestureBindings.AsusStyle);

    public TouchpadGestureConfiguration Sanitize() => this with
    {
        EdgeWidthMm = Math.Clamp(double.IsFinite(EdgeWidthMm) ? EdgeWidthMm : 5.0, 2.0, 15.0),
        ActivationDistanceMm = Math.Clamp(double.IsFinite(ActivationDistanceMm) ? ActivationDistanceMm : 2.0, 0.5, 8.0),
        ContinuationToleranceMm = Math.Clamp(double.IsFinite(ContinuationToleranceMm) ? ContinuationToleranceMm : 12.0, 4.0, 30.0),
        DirectionDominance = Math.Clamp(double.IsFinite(DirectionDominance) ? DirectionDominance : 1.15, 1.02, 2.5),
        Bindings = (Bindings ?? TouchpadGestureBindings.AsusStyle).Sanitize()
    };

    public TouchpadEdgeBinding BindingFor(TouchpadEdge edge) =>
        (Bindings ?? TouchpadGestureBindings.AsusStyle).Get(edge).Sanitize();
}

public sealed record TouchpadGeometry(
    int XLogicalMin,
    int XLogicalMax,
    int YLogicalMin,
    int YLogicalMax,
    double PhysicalWidthMm,
    double PhysicalHeightMm,
    bool PhysicalSizeEstimated = false)
{
    public int XRange => Math.Max(1, XLogicalMax - XLogicalMin);
    public int YRange => Math.Max(1, YLogicalMax - YLogicalMin);

    public double EffectiveWidthMm => PhysicalWidthMm is >= 20 and <= 400 ? PhysicalWidthMm : 100.0;
    public double EffectiveHeightMm => PhysicalHeightMm is >= 20 and <= 300 ? PhysicalHeightMm : 60.0;

    public double XToMm(int x) =>
        Math.Clamp((x - XLogicalMin) / (double)XRange, 0.0, 1.0) * EffectiveWidthMm;

    public double YToMm(int y) =>
        Math.Clamp((y - YLogicalMin) / (double)YRange, 0.0, 1.0) * EffectiveHeightMm;

    public double DeltaXToMm(int delta) => delta / (double)XRange * EffectiveWidthMm;
    public double DeltaYToMm(int delta) => delta / (double)YRange * EffectiveHeightMm;

    public double DistanceToEdgeMm(TouchpadEdge edge, int x, int y) => edge switch
    {
        TouchpadEdge.Left => XToMm(x),
        TouchpadEdge.Right => EffectiveWidthMm - XToMm(x),
        TouchpadEdge.Top => YToMm(y),
        TouchpadEdge.Bottom => EffectiveHeightMm - YToMm(y),
        _ => double.MaxValue
    };
}

public readonly record struct TouchContact(
    int ContactId,
    int X,
    int Y,
    bool IsDown,
    bool Confidence = true,
    double? Width = null,
    double? Height = null,
    double? Pressure = null);

public sealed record GestureSignal(
    GesturePhase Phase,
    TouchpadEdge? Edge,
    GestureActionKind Action,
    double TotalTravelMm = 0,
    double DeltaMm = 0,
    string? Reason = null,
    int? ContactId = null);
