namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Canonical physical geometry for the two top-corner launch lanes. Recognition and
/// UI hit-testing both use this contract so the visible diagonal lane is the actual
/// place where a launch may begin; there is no hidden square corner target.
/// </summary>
public static class TouchpadCornerZonePolicy
{
    public const double LengthMm = 24.0;
    public const double HalfWidthMm = 4.0;
    public const double StartInsetMm = 1.5;

    public static bool ContainsStart(
        TouchpadCorner corner,
        TouchpadGeometry geometry,
        int x,
        int y)
    {
        double localX = corner == TouchpadCorner.TopLeft
            ? geometry.XToMm(x)
            : geometry.EffectiveWidthMm - geometry.XToMm(x);
        double localY = geometry.YToMm(y);
        return ContainsLocal(localX, localY);
    }

    public static bool ContainsLocal(double localXmm, double localYmm)
    {
        if (!double.IsFinite(localXmm) || !double.IsFinite(localYmm) ||
            localXmm < 0 || localYmm < 0)
        {
            return false;
        }

        const double invSqrt2 = 0.7071067811865476;
        double along = (localXmm + localYmm) * invSqrt2;
        double across = (localYmm - localXmm) * invSqrt2;
        if (along < StartInsetMm || Math.Abs(across) > HalfWidthMm)
            return false;

        double capCenter = LengthMm - HalfWidthMm;
        if (along <= capCenter)
            return true;
        if (along > LengthMm)
            return false;

        double capAlong = along - capCenter;
        return capAlong * capAlong + across * across <= HalfWidthMm * HalfWidthMm;
    }
}
