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

        // Build/version metadata can remain deliberately small. Everything else is
        // operating UI: 9-10 px helper copy was visually weaker than the controls it
        // explained and became hard to scan at normal laptop viewing distance.
        bool metadata = path.Equals("AppVersion", StringComparison.Ordinal) ||
                        (!string.IsNullOrWhiteSpace(literal) && literal.StartsWith("v0.", StringComparison.OrdinalIgnoreCase));
        if (!metadata && text.FontSize < 11)
            text.FontSize = 11;

        // Strengthen real section headings without inflating compact instrument
        // labels such as CPU / POWER / RPM. This keeps ThinkControl technical while
        // making hierarchy as obvious as the larger, bolder reference interfaces.
        bool allCapsMetric = literal.Length is > 0 and <= 18 &&
                             literal.Any(char.IsLetter) &&
                             string.Equals(literal, literal.ToUpperInvariant(), StringComparison.Ordinal);
        bool headingWeight = text.FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();
        if (!metadata && !allCapsMetric && headingWeight && text.FontSize is >= 10 and < 13.5)
            text.FontSize = 13.5;

        if (path.Equals("BatteryEtaText", StringComparison.Ordinal))
        {
            text.FontSize = Math.Max(text.FontSize, 12.5);
            text.FontWeight = FontWeights.SemiBold;
            text.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
            text.SetBinding(TextBlock.TextProperty, new Binding("BatteryEtaText")
            {
                Converter = BatteryTimeConverter
            });
        }
        else if (path.Equals("BatteryCompactLine", StringComparison.Ordinal))
        {
            text.FontSize = Math.Max(text.FontSize, 11.5);
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
