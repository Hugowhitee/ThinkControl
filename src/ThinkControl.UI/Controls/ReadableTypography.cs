using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ThinkControl.UI.Controls;

internal static class ReadableTypography
{
    private const string AppliedMarker = "ThinkControl.Typography.Applied";

    internal static IValueConverter BatteryTimeConverter { get; } = new BatteryTimeTextConverter();

    /// <summary>
    /// Applies the shared ThinkControl type hierarchy to an already-created visual
    /// tree. XAML and code-built surfaces therefore resolve to the same semantic
    /// sizes even when an older control still contains a legacy numeric FontSize.
    /// </summary>
    internal static void Apply(DependencyObject root)
    {
        if (root is FrameworkElement frameworkRoot)
            frameworkRoot.Resources[AppliedMarker] = true;

        ApplyNode(root);
    }

    internal static void Validate(DependencyObject root)
    {
        var invalid = new List<string>();
        ValidateNode(root, invalid);
        if (invalid.Count > 0)
        {
            throw new InvalidOperationException(
                "Typography contract violation. Rendered UI must use TypographyScale roles only: " +
                string.Join("; ", invalid.Take(12)));
        }
    }

    private static void ApplyNode(DependencyObject node)
    {
        switch (node)
        {
            case TextBlock text:
                PolishTextBlock(text);
                break;
            case AccessText access:
                access.FontSize = NormalizeControlSize(access.FontSize);
                break;
            case Control control:
                control.FontSize = NormalizeControlSize(control.FontSize);
                break;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            ApplyNode(VisualTreeHelper.GetChild(node, i));
    }

    private static void ValidateNode(DependencyObject node, ICollection<string> invalid)
    {
        switch (node)
        {
            case TextBlock text when !TypographyScale.IsAllowed(text.FontSize):
                invalid.Add($"TextBlock '{Preview(text.Text)}' = {text.FontSize:0.##}");
                break;
            case AccessText access when !TypographyScale.IsAllowed(access.FontSize):
                invalid.Add($"AccessText '{Preview(access.Text)}' = {access.FontSize:0.##}");
                break;
            case Control control when !TypographyScale.IsAllowed(control.FontSize):
                // Font-only glyph controls are allowed to size their vector-like
                // symbol font independently. Ordinary textual controls are not.
                if (!IsGlyphControl(control))
                    invalid.Add($"{control.GetType().Name} = {control.FontSize:0.##}");
                break;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            ValidateNode(VisualTreeHelper.GetChild(node, i), invalid);
    }

    private static void PolishTextBlock(TextBlock text)
    {
        Binding? binding = BindingOperations.GetBinding(text, TextBlock.TextProperty);
        string path = binding?.Path?.Path ?? string.Empty;
        string literal = text.Text?.Trim() ?? string.Empty;

        bool versionMetadata = path.Equals("AppVersion", StringComparison.Ordinal) ||
                               (!string.IsNullOrWhiteSpace(literal) && literal.StartsWith("v0.", StringComparison.OrdinalIgnoreCase));
        bool terseMetadata = versionMetadata || IsTerseMetadata(text, literal, path);
        bool allCapsMetric = IsAllCapsMetric(literal);
        bool headingWeight = text.FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();
        bool explanatoryCopy = text.TextWrapping != TextWrapping.NoWrap ||
                               literal.Length >= 34 ||
                               literal.Contains('.');
        bool numericValue = LooksLikeDataValue(literal, path);

        if (versionMetadata || terseMetadata || allCapsMetric)
        {
            text.FontSize = TypographyScale.Caption;
        }
        else if (headingWeight)
        {
            text.FontSize = text.FontSize >= TypographyScale.Subtitle
                ? TypographyScale.PageTitle
                : text.FontSize >= TypographyScale.SectionTitle
                    ? TypographyScale.Subtitle
                    : TypographyScale.SectionTitle;
        }
        else if (numericValue && text.FontSize >= TypographyScale.Value)
        {
            text.FontSize = text.FontSize >= 29
                ? TypographyScale.ValueHero
                : text.FontSize >= 22
                    ? TypographyScale.ValueLarge
                    : TypographyScale.Value;
        }
        else if (explanatoryCopy)
        {
            text.FontSize = TypographyScale.Body;
        }
        else
        {
            // Short labels/status text can use Secondary, but arbitrary legacy
            // values such as 10.5/11/12.5/13.5 are never preserved.
            text.FontSize = text.FontSize >= TypographyScale.BodyLarge
                ? TypographyScale.BodyLarge
                : text.FontSize >= TypographyScale.Body
                    ? TypographyScale.Body
                    : TypographyScale.Secondary;
        }

        if (path.Equals("BatteryEtaText", StringComparison.Ordinal))
        {
            text.FontSize = TypographyScale.Body;
            text.FontWeight = FontWeights.SemiBold;
            text.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
            text.SetBinding(TextBlock.TextProperty, new Binding("BatteryEtaText")
            {
                Converter = BatteryTimeConverter
            });
        }
        else if (path.Equals("BatteryCompactLine", StringComparison.Ordinal))
        {
            text.FontSize = TypographyScale.Secondary;
            text.SetBinding(TextBlock.TextProperty, new Binding("BatteryCompactLine")
            {
                Converter = BatteryTimeConverter
            });
        }
    }

    private static double NormalizeControlSize(double size)
    {
        if (!double.IsFinite(size) || size <= 0)
            return TypographyScale.Body;
        if (size < TypographyScale.Secondary)
            return TypographyScale.Secondary;
        return TypographyScale.Closest(size);
    }

    private static bool IsTerseMetadata(TextBlock text, string literal, string path)
    {
        if (path.Contains("Version", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Build", StringComparison.OrdinalIgnoreCase))
            return true;

        if (literal.Length == 0 || literal.Length > 22)
            return false;

        return literal.Contains("RPM", StringComparison.OrdinalIgnoreCase) ||
               literal.Contains("Hz", StringComparison.OrdinalIgnoreCase) ||
               literal.Contains("W avg", StringComparison.OrdinalIgnoreCase) ||
               literal.Contains("sensor", StringComparison.OrdinalIgnoreCase) && text.FontSize <= 12.5;
    }

    private static bool IsAllCapsMetric(string literal) =>
        literal.Length is > 0 and <= 18 &&
        literal.Any(char.IsLetter) &&
        string.Equals(literal, literal.ToUpperInvariant(), StringComparison.Ordinal);

    private static bool LooksLikeDataValue(string literal, string path)
    {
        if (path.Contains("Percent", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Temperature", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Rpm", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Value", StringComparison.OrdinalIgnoreCase))
            return true;

        if (literal.Length == 0 || literal.Length > 18)
            return false;

        return literal.Any(char.IsDigit) &&
               (literal.Contains('%') || literal.Contains('°') || literal.Contains(" W", StringComparison.OrdinalIgnoreCase) ||
                literal.Contains("RPM", StringComparison.OrdinalIgnoreCase) || literal.Contains("Hz", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGlyphControl(Control control) =>
        control.FontFamily?.Source.Equals("Segoe UI Symbol", StringComparison.OrdinalIgnoreCase) == true ||
        control.FontFamily?.Source.Equals("Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase) == true ||
        control.FontFamily?.Source.Equals("Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase) == true;

    private static string Preview(string? value)
    {
        string text = value?.Trim().Replace('\n', ' ') ?? string.Empty;
        return text.Length <= 28 ? text : text[..28] + "…";
    }

    private sealed class BatteryTimeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return text;

            text = text.TrimStart('~').TrimStart();
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\d+)h\s*(\d{1,2})m", "$1 h $2 min");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\d+)h\b", "$1 h");
            return text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
