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

        // Build/version metadata can remain deliberately small. Everything else is
        // part of the operating UI and should not depend on 9 px copy to fit.
        bool metadata = path.Equals("AppVersion", StringComparison.Ordinal) ||
                        (!string.IsNullOrWhiteSpace(text.Text) && text.Text.StartsWith("v0.", StringComparison.OrdinalIgnoreCase));
        if (!metadata && text.FontSize < 10.5)
            text.FontSize = 10.5;

        if (path.Equals("BatteryEtaText", StringComparison.Ordinal))
        {
            text.FontSize = Math.Max(text.FontSize, 12);
            text.FontWeight = FontWeights.Medium;
            text.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
            text.SetBinding(TextBlock.TextProperty, new Binding("BatteryEtaText")
            {
                Converter = BatteryTimeConverter
            });
        }
        else if (path.Equals("BatteryCompactLine", StringComparison.Ordinal))
        {
            text.FontSize = Math.Max(text.FontSize, 11);
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
