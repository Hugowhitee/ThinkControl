namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Canonical physical geometry for the two top-corner launch zones. Recognition and
/// UI hit-testing share this contract so every visible guard/lane/cap is a real input
/// area. The right corner is always obtained by mirroring the same left-local shape.
/// </summary>
public static class TouchpadCornerZonePolicy
{
    public const double LengthMm = 24.0;
    public const double HalfWidthMm = 4.0;
    public const double OuterGuardRadiusMm = 10.0;

    public static double InnerCapCenterMm => LengthMm - HalfWidthMm;

    public static double LaneStartAlongMm =>
        Math.Sqrt(Math.Max(0, OuterGuardRadiusMm * OuterGuardRadiusMm - HalfWidthMm * HalfWidthMm));

    public static bool ContainsStart(
        TouchpadCorner corner,
        TouchpadGeometry geometry,
        int x,
        int y)
    {
        (double localX, double localY) = ToLocal(corner, geometry, x, y);
        return ContainsLocal(localX, localY);
    }

    public static CornerGestureDirection? ClassifyStart(
        TouchpadCorner corner,
        TouchpadGeometry geometry,
        int x,
        int y,
        bool reverseCloseEnabled)
    {
        (double localX, double localY) = ToLocal(corner, geometry, x, y);
        if (reverseCloseEnabled && ContainsReverseStartLocal(localX, localY))
            return CornerGestureDirection.Outward;
        return ContainsLocal(localX, localY)
            ? CornerGestureDirection.Inward
            : null;
    }

    public static bool ContainsLocal(double localXmm, double localYmm)
    {
        if (!IsFiniteInsideCorner(localXmm, localYmm))
            return false;

        // The quarter-disc is deliberately a real guard rather than decorative UI:
        // when a launch is enabled it reserves the physical corner before the nearby
        // top/side edge can claim the same finger. This makes hitting the corner
        // gesture tolerant of finger placement without creating a hidden target.
        if (localXmm * localXmm + localYmm * localYmm <= OuterGuardRadiusMm * OuterGuardRadiusMm)
            return true;

        ToLaneCoordinates(localXmm, localYmm, out double along, out double across);
        if (along < LaneStartAlongMm || Math.Abs(across) > HalfWidthMm)
            return false;

        if (along <= InnerCapCenterMm)
            return true;
        if (along > LengthMm)
            return false;

        double capAlong = along - InnerCapCenterMm;
        return capAlong * capAlong + across * across <= HalfWidthMm * HalfWidthMm;
    }

    public static bool ContainsReverseStartLocal(double localXmm, double localYmm)
    {
        if (!IsFiniteInsideCorner(localXmm, localYmm))
            return false;

        ToLaneCoordinates(localXmm, localYmm, out double along, out double across);
        if (along < InnerCapCenterMm || along > LengthMm)
            return false;

        double capAlong = along - InnerCapCenterMm;
        return capAlong * capAlong + across * across <= HalfWidthMm * HalfWidthMm;
    }

    private static (double X, double Y) ToLocal(
        TouchpadCorner corner,
        TouchpadGeometry geometry,
        int x,
        int y)
    {
        double localX = corner == TouchpadCorner.TopLeft
            ? geometry.XToMm(x)
            : geometry.EffectiveWidthMm - geometry.XToMm(x);
        return (localX, geometry.YToMm(y));
    }

    private static bool IsFiniteInsideCorner(double localXmm, double localYmm) =>
        double.IsFinite(localXmm) && double.IsFinite(localYmm) && localXmm >= 0 && localYmm >= 0;

    private static void ToLaneCoordinates(
        double localXmm,
        double localYmm,
        out double along,
        out double across)
    {
        const double invSqrt2 = 0.7071067811865476;
        along = (localXmm + localYmm) * invSqrt2;
        across = (localYmm - localXmm) * invSqrt2;
    }
}
