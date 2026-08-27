using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ThinkControl.UI.Controls;

internal static class ReadableTypography
{
    internal static IValueConverter BatteryTimeConverter { get; } = new BatteryTimeTextConverter();

    internal static void Apply(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock text)
                PolishTextBlock(text);
            Apply(child);
        }
    }

    private static void PolishTextBlock(TextBlock text)
    {
        Binding? binding = BindingOperations.GetBinding(text, TextBlock.TextProperty);
        string path = binding?.Path?.Path ?? string.Empty;
        string literal = text.Text?.Trim() ?? string.Empty;

        // ThinkControl follows one desktop type ramp instead of allowing every page
        // to invent 9.5/10.5/11px helper text. Fluent Windows uses 14px body and 12px
        // captions; captions are reserved here for terse build/metric metadata only.
        bool metadata = path.Equals("AppVersion", StringComparison.Ordinal) ||
                        (!string.IsNullOrWhiteSpace(literal) && literal.StartsWith("v0.", StringComparison.OrdinalIgnoreCase));
        double minimum = metadata ? TypographyScale.Caption : TypographyScale.Secondary;
        if (text.FontSize < minimum)
            text.FontSize = minimum;

        // Preserve compact technical metric labels (CPU / POWER / RPM), but give
        // semantic headings a predictable hierarchy. Existing 20-24px page titles
        // become the shared 28px title; normal semibold section labels become 16px.
        bool allCapsMetric = literal.Length is > 0 and <= 18 &&
                             literal.Any(char.IsLetter) &&
                             string.Equals(literal, literal.ToUpperInvariant(), StringComparison.Ordinal);
        bool headingWeight = text.FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();
        if (!metadata && !allCapsMetric && headingWeight)
        {
            if (text.FontSize >= 20 && text.FontSize < TypographyScale.PageTitle)
                text.FontSize = TypographyScale.PageTitle;
            else if (text.FontSize >= TypographyScale.Caption && text.FontSize < TypographyScale.SectionTitle)
                text.FontSize = TypographyScale.SectionTitle;
        }

        if (path.Equals("BatteryEtaText", StringComparison.Ordinal))
        {
            text.FontSize = Math.Max(text.FontSize, TypographyScale.Body);
            text.FontWeight = FontWeights.SemiBold;
            text.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
            text.SetBinding(TextBlock.TextProperty, new Binding("BatteryEtaText")
            {
                Converter = BatteryTimeConverter
            });
        }
        else if (path.Equals("BatteryCompactLine", StringComparison.Ordinal))
        {
            text.FontSize = Math.Max(text.FontSize, TypographyScale.Secondary);
            text.SetBinding(TextBlock.TextProperty, new Binding("BatteryCompactLine")
            {
                Converter = BatteryTimeConverter
            });
        }
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
