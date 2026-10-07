namespace ThinkControl.UI;

/// <summary>
/// The one typography ramp used by every ThinkControl surface.
///
/// Do not introduce one-off UI font sizes. Pick the semantic role instead.
/// Visual QA validates this contract but never mutates a rendered control.
/// </summary>
public static class TypographyScale
{
    public static System.Windows.Media.FontFamily Family =>
        (System.Windows.Media.FontFamily)System.Windows.Application.Current.FindResource("Tc.Font");
    public static System.Windows.Media.Typeface Typeface => new(Family, System.Windows.FontStyles.Normal,
        System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal);
    public const double PageTitle = 27;
    public const double Subtitle = 19;
    public const double SectionTitle = 17;
    public const double BodyLarge = 15;
    public const double Body = 14;
    public const double Secondary = 13;
    public const double Caption = 12;
    public const double ControlLabel = 14;
    public const double Navigation = 14;
    public const double ControlText = 14;
    public const double Value = 20;
    public const double ValueLarge = 25;
    public const double ValueHero = 25;
    public const double Micro = 11;
    public const double CompactValue = 22;

    private static readonly double[] Allowed =
    [
        Micro,
        CompactValue,
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
