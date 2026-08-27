using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ThinkControl.UI;

/// <summary>
/// Read-only release assertion for the explicit semantic typography system.
/// It never changes the rendered tree.
/// </summary>
internal static class TypographyContract
{
    internal static void Validate(DependencyObject root)
    {
        var invalid = new List<string>();
        ValidateNode(root, invalid);
        ValidateEquivalentSegments(root, invalid);
        if (invalid.Count > 0)
            throw new InvalidOperationException("Typography contract violation: " + string.Join("; ", invalid.Take(16)));
    }

    private static void ValidateNode(DependencyObject node, ICollection<string> invalid)
    {
        if (node is TextBlock text && !TypographyScale.IsAllowed(text.FontSize))
            invalid.Add($"TextBlock '{Preview(text.Text)}' uses {text.FontSize:0.##}");
        else if (node is Control control && !IsGlyphControl(control) && !TypographyScale.IsAllowed(control.FontSize))
            invalid.Add($"{control.GetType().Name} uses {control.FontSize:0.##}");

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            ValidateNode(VisualTreeHelper.GetChild(node, index), invalid);
    }

    private static void ValidateEquivalentSegments(DependencyObject node, ICollection<string> invalid)
    {
        if (node is Panel panel)
        {
            RadioButton[] segments = panel.Children.OfType<RadioButton>()
                .Where(item => item.GroupName.Length > 0)
                .ToArray();
            foreach (IGrouping<string, RadioButton> group in segments.GroupBy(item => item.GroupName))
            {
                if (group.Select(item => item.FontSize).Distinct().Count() > 1 ||
                    group.Select(item => item.MinHeight).Distinct().Count() > 1)
                    invalid.Add($"Segment group '{group.Key}' has unequal typography or height");
            }
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            ValidateEquivalentSegments(VisualTreeHelper.GetChild(node, index), invalid);
    }

    private static bool IsGlyphControl(Control control) =>
        control.FontFamily?.Source.Contains("Icons", StringComparison.OrdinalIgnoreCase) == true ||
        control.FontFamily?.Source.Contains("Symbol", StringComparison.OrdinalIgnoreCase) == true ||
        control is Slider or System.Windows.Controls.ProgressBar;

    private static string Preview(string? value)
    {
        string text = value?.Trim().Replace('\n', ' ') ?? string.Empty;
        return text.Length <= 28 ? text : text[..28] + "…";
    }
}
