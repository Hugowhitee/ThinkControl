namespace ThinkControl.UI;

/// <summary>
/// Shared desktop type ramp for ThinkControl. Values follow the Windows Fluent
/// hierarchy closely: 28px title, 20px subtitle, 14px body and 12px caption.
/// Keep normal explanatory copy at Body/Secondary; Caption is reserved for short
/// metadata only and is the practical minimum size used by the product UI.
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
}
