namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Single editor-selection state for the six user-selectable touchpad zones.
/// Exactly one edge or corner owns selection at a time; switching zone kinds
/// clears the previous kind by construction.
/// </summary>
public readonly record struct TouchpadZoneSelection(TouchpadEdge? Edge, TouchpadCorner? Corner)
{
    public static TouchpadZoneSelection ForEdge(TouchpadEdge edge) => new(edge, null);

    public static TouchpadZoneSelection ForCorner(TouchpadCorner corner) => new(null, corner);

    public bool IsEdge => Edge is not null && Corner is null;

    public bool IsCorner => Corner is not null && Edge is null;

    public TouchpadZoneSelection SelectEdge(TouchpadEdge edge) => ForEdge(edge);

    public TouchpadZoneSelection SelectCorner(TouchpadCorner corner) => ForCorner(corner);

    public TouchpadZoneSelection Sanitize() => IsEdge || IsCorner
        ? this
        : ForEdge(TouchpadEdge.Top);
}
