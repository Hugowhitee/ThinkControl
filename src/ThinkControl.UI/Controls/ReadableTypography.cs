using System.Globalization;
using System.Windows.Data;

namespace ThinkControl.UI.Controls;

/// <summary>
/// Text-only converters shared by semantically styled surfaces. Typography is
/// intentionally owned by explicit resources/styles, never by visual-tree scans.
/// </summary>
internal static class ReadableTypography
{
    internal static IValueConverter BatteryTimeConverter { get; } = new BatteryTimeTextConverter();

    private sealed class BatteryTimeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return text;

            text = text.TrimStart('~').TrimStart();
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\d+)h\s*(\d{1,2})m", "$1 h $2 min");
            return System.Text.RegularExpressions.Regex.Replace(text, @"(\d+)h\b", "$1 h");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
