using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ThinkControl.UI;

/// <summary>Read-only release assertions for shared shell and control geometry.</summary>
internal static class UiLayoutContract
{
    internal static void Validate(DependencyObject root)
    {
        var invalid = new List<string>();
        ValidateNode(root, invalid);
        if (invalid.Count > 0)
            throw new InvalidOperationException("UI layout contract violation: " + string.Join("; ", invalid.Take(16)));
    }

    private static void ValidateNode(DependencyObject node, ICollection<string> invalid)
    {
        if (node is ComboBox combo && combo.Visibility == Visibility.Visible && combo.ActualHeight > 0)
        {
            double requiredHeight = Math.Max(38, combo.FontSize * 1.25 + combo.Padding.Top + combo.Padding.Bottom);
            if (combo.ActualHeight + 0.5 < requiredHeight)
                invalid.Add($"ComboBox '{combo.Name}' is {combo.ActualHeight:0.#} px high; needs {requiredHeight:0.#}");

            ContentPresenter? presenter = Descendants(combo).OfType<ContentPresenter>()
                .FirstOrDefault(item => item.ActualHeight > 0);
            if (presenter is not null && presenter.ActualHeight + 0.5 < combo.FontSize * 1.2)
                invalid.Add($"ComboBox '{combo.Name}' content presenter clips its {combo.FontSize:0.#} px text");
        }

        if (node is Panel panel)
            ValidateUtilityOrder(panel, invalid);

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            ValidateNode(VisualTreeHelper.GetChild(node, index), invalid);
    }

    private static void ValidateUtilityOrder(Panel panel, ICollection<string> invalid)
    {
        Button[] buttons = panel.Children.OfType<Button>().ToArray();
        int notification = Array.FindIndex(buttons, item => Equals(item.Tag, ShellUtilityOrder.NotificationTag));
        int viewMode = Array.FindIndex(buttons, item => Equals(item.Tag, ShellUtilityOrder.ViewModeTag));
        if (notification < 0 || viewMode < 0)
            return;
        if (notification >= viewMode)
            invalid.Add("Shell utilities must place notifications before the view-mode control");
        if (panel is Grid && Grid.GetColumn(buttons[notification]) == Grid.GetColumn(buttons[viewMode]))
            invalid.Add("Shell utility controls overlap in the same grid column");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
                yield return descendant;
        }
    }
}
