using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard
{
    private void ApplyAlpha30CompactPolish()
    {
        // The quick cards are intentionally compact, but the alpha.29 geometry left
        // the shared 40 px ComboBox exactly against the card's lower clip edge.
        // Give every quick selector the same breathing room without shrinking the
        // shared control contract or introducing page-local ComboBox templates.
        foreach (ComboBox combo in new[]
        {
            CompactPerformanceCombo,
            CompactFanCombo,
            CompactRefreshCombo,
            CompactKeyboardCombo
        })
        {
            combo.Margin = new Thickness(0, 6, 0, 0);
            combo.MinHeight = 40;

            if (combo.Parent is StackPanel stack && stack.Parent is Border card)
                card.Padding = new Thickness(10, 6, 10, 6);
        }
    }
}
