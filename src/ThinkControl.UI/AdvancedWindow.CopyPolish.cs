using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _copyPolishConfigured;

    private void ConfigureCopyPolish()
    {
        if (_copyPolishConfigured)
            return;
        _copyPolishConfigured = true;

        // Keep the home shortcut vocabulary identical to Performance and compact.
        // The underlying enum/tag intentionally remains Quiet for compatibility.
        if (HomeQuiet?.Content is StackPanel homeEfficiency)
        {
            TextBlock[] labels = homeEfficiency.Children.OfType<TextBlock>().ToArray();
            if (labels.Length > 0)
                labels[0].Text = "Efficiency";
            if (labels.Length > 1)
                labels[1].Text = "Lower power";
        }

        // System is hidden when the advanced shell is first composed. A visual-tree
        // scan therefore misses its buttons in both normal startup and snapshot QA.
        // Walk the logical tree instead so user-facing copy is correct before the
        // page is ever rendered.
        foreach (WpfButton button in FindLogicalButtons(this))
        {
            if (button.Content is not string text ||
                !text.Contains("Commercial Vantage", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            button.Content = text.Replace(
                "Commercial Vantage",
                "Lenovo Vantage",
                StringComparison.OrdinalIgnoreCase);
            button.Tag = "ms-windows-store://search/?query=Lenovo%20Vantage";
        }
    }

    private static IEnumerable<WpfButton> FindLogicalButtons(DependencyObject root)
    {
        if (root is WpfButton button)
            yield return button;

        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject dependencyObject)
                continue;

            foreach (WpfButton nested in FindLogicalButtons(dependencyObject))
                yield return nested;
        }
    }
}
