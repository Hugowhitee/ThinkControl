using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string SettingsHierarchyKey = "ThinkControl.Settings.Hierarchy";

    private void ConfigureSettingsHierarchy()
    {
        if (Resources.Contains(SettingsHierarchyKey) || PageSettings.Content is not StackPanel stack)
            return;

        Resources[SettingsHierarchyKey] = true;

        // Settings reads like a document: one page title, then named groups. Keep
        // individual controls visually quiet so borders separate rows without making
        // every preference look like an independent dashboard card.
        FrameworkElement? firstPreference = stack.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(element => element is Border);
        if (firstPreference is not null)
            InsertHeadingBefore(stack, firstPreference, "Appearance & app");

        FrameworkElement? opening = stack.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(element => element is Border border &&
                Equals(border.Tag, DefaultOpeningViewCardTag));
        if (opening is not null && !HasHeadingImmediatelyBefore(stack, opening))
            InsertHeadingBefore(stack, opening, "Behavior");

        FrameworkElement? diagnostics = stack.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(element => FindVisualChildren<Controls.DiagnosticsPanel>(element).Any() ||
                                       element is Controls.DiagnosticsPanel);
        FrameworkElement? github = stack.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(element => element is Border border && Equals(border.Tag, GitHubCardTag));
        FrameworkElement? supportStart = diagnostics ?? github;
        if (supportStart is not null)
            InsertHeadingBefore(stack, supportStart, "Support & privacy");

        foreach (Border border in stack.Children.OfType<Border>())
        {
            if (Equals(border.Tag, GlobalResetCardTag))
                continue;

            // Retain a single subtle bottom rule instead of a framed box. The child
            // keeps its existing padding and interaction layout.
            border.Background = Brushes.Transparent;
            border.BorderThickness = new Thickness(0, 0, 0, 1);
            border.CornerRadius = new CornerRadius(0);
            border.Padding = new Thickness(2, 14, 2, 16);
            border.Margin = new Thickness(0);
            border.SetResourceReference(Border.BorderBrushProperty, "Tc.Border");
        }

        Border? reset = stack.Children.OfType<Border>()
            .FirstOrDefault(border => Equals(border.Tag, GlobalResetCardTag));
        if (reset is not null)
        {
            InsertHeadingBefore(stack, reset, "Reset");
            reset.Margin = new Thickness(0, 4, 0, 0);
        }
    }

    private static void InsertHeadingBefore(StackPanel stack, FrameworkElement anchor, string text)
    {
        int index = stack.Children.IndexOf(anchor);
        if (index < 0)
            return;
        if (index > 0 && stack.Children[index - 1] is TextBlock previous &&
            Equals(previous.Tag, "ThinkControl.Settings.GroupHeading"))
            return;

        var heading = new TextBlock
        {
            Tag = "ThinkControl.Settings.GroupHeading",
            Text = text,
            FontSize = TypographyScale.SectionTitle,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(2, index <= 1 ? 8 : 24, 0, 8)
        };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Text");
        stack.Children.Insert(index, heading);
    }

    private static bool HasHeadingImmediatelyBefore(StackPanel stack, FrameworkElement element)
    {
        int index = stack.Children.IndexOf(element);
        return index > 0 && stack.Children[index - 1] is TextBlock previous &&
               Equals(previous.Tag, "ThinkControl.Settings.GroupHeading");
    }
}
