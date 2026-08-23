using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _copyPolishConfigured;

    private void ConfigureCopyPolish()
    {
        if (_copyPolishConfigured)
            return;
        _copyPolishConfigured = true;

        foreach (Button button in FindVisualChildren<Button>(this))
        {
            if (button.Content is string text &&
                text.Contains("Commercial Vantage", StringComparison.OrdinalIgnoreCase))
            {
                button.Content = text.Replace("Commercial Vantage", "Lenovo Vantage", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
