namespace ThinkControl.UI;

/// <summary>
/// The one typography ramp used by every ThinkControl surface.
///
/// Do not introduce one-off UI font sizes. Pick the semantic role instead. The
/// runtime typography pass and visual-QA validation both use this class, so code-
/// built WPF surfaces and XAML surfaces stay on the same hierarchy.
/// </summary>
public static class TypographyScale
{
    public const double PageTitle = 28;
    public const double Subtitle = 20;
    public const double SectionTitle = 16;
    public const double BodyLarge = 15;
    public const double Body = 14;
    public const double Secondary = 13;
    public const double Caption = 12;
    public const double Value = 18;
    public const double ValueLarge = 24;
    public const double ValueHero = 32;

    private static readonly double[] Allowed =
    [
        Caption,
        Secondary,
        Body,
        BodyLarge,
        SectionTitle,
        Value,
        Subtitle,
        ValueLarge,
        PageTitle,
        ValueHero
    ];

    public static bool IsAllowed(double size, double tolerance = 0.01) =>
        Allowed.Any(value => Math.Abs(value - size) <= tolerance);

    public static double Closest(double size)
    {
        if (!double.IsFinite(size) || size <= 0)
            return Body;
        return Allowed.OrderBy(value => Math.Abs(value - size)).First();
    }

    public static double Copy(bool secondary = false) => secondary ? Secondary : Body;

    public static double Heading(int level) => level switch
    {
        <= 1 => PageTitle,
        2 => Subtitle,
        _ => SectionTitle
    };

    public static double DataValue(bool prominent = false, bool hero = false) =>
        hero ? ValueHero : prominent ? ValueLarge : Value;
}
