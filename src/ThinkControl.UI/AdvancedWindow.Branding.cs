using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThinkControl.UI.Controls;
using WpfButton = System.Windows.Controls.Button;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _advancedBrandingConfigured;

    private void ConfigureAdvancedBranding()
    {
        if (_advancedBrandingConfigured)
            return;

        try
        {
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/ThinkControl.ico", UriKind.Absolute));
        }
        catch
        {
            // The executable icon remains the fallback if WPF cannot decode it.
        }

        if (NavHome.Parent is not StackPanel navStack ||
            navStack.Children.OfType<Grid>().FirstOrDefault() is not Grid dockRow)
        {
            return;
        }

        navStack.Margin = new Thickness(0);
        dockRow.Tag = "ThinkControl.BrandRow";
        dockRow.Height = 64;
        dockRow.Margin = new Thickness(14, 5, 10, 0);

        WpfButton? notificationButton = dockRow.Children
            .OfType<WpfButton>()
            .FirstOrDefault(child => child.Tag as string == "ThinkControl.NotificationSlot");
        WpfButton? compactButton = dockRow.Children
            .OfType<WpfButton>()
            .FirstOrDefault(child => !ReferenceEquals(child, notificationButton));

        FrameworkElement[] legacy = dockRow.Children
            .OfType<FrameworkElement>()
            .Where(child => !ReferenceEquals(child, notificationButton) && !ReferenceEquals(child, compactButton))
            .ToArray();
        foreach (FrameworkElement element in legacy)
            dockRow.Children.Remove(element);

        if (notificationButton is not null)
            dockRow.Children.Remove(notificationButton);
        if (compactButton is not null)
            dockRow.Children.Remove(compactButton);

        var wordmark = new BrandWordmark
        {
            Width = 150,
            Height = 48,
            Margin = new Thickness(-4, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(wordmark, 0);
        Grid.SetColumnSpan(wordmark, Math.Max(1, dockRow.ColumnDefinitions.Count));
        dockRow.Children.Add(wordmark);

        Border brandDivider = CreateSidebarDivider("ThinkControl.BrandDivider");
        navStack.Children.Insert(1, brandDivider);

        var utilityRow = new Grid
        {
            Tag = "ThinkControl.UtilityRow",
            Height = 44,
            Margin = new Thickness(13, 3, 10, 3),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        utilityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        utilityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (compactButton is not null)
        {
            compactButton.Width = 34;
            compactButton.Height = 34;
            compactButton.Padding = new Thickness(0);
            compactButton.Margin = new Thickness(0, 0, 4, 0);
            compactButton.BorderThickness = new Thickness(0);
            compactButton.Background = Brushes.Transparent;
            compactButton.BorderBrush = Brushes.Transparent;
            compactButton.Content = new PackIconLucide
            {
                Kind = "CompactView",
                Width = 18,
                Height = 18,
                Foreground = (Brush)FindResource("Tc.TextMuted")
            };
            TcToolTip.Apply(compactButton, "Compact view");
            Grid.SetColumn(compactButton, 0);
            utilityRow.Children.Add(compactButton);
        }

        if (notificationButton is not null)
        {
            Grid.SetColumn(notificationButton, 1);
            utilityRow.Children.Add(notificationButton);
        }

        navStack.Children.Insert(2, utilityRow);
        navStack.Children.Insert(3, CreateSidebarDivider("ThinkControl.NavigationDivider"));

        if (navStack.Parent is Grid sidebarGrid)
        {
            StackPanel? footer = sidebarGrid.Children
                .OfType<StackPanel>()
                .FirstOrDefault(child => Grid.GetRow(child) == 1);
            if (footer is not null)
            {
                footer.Margin = new Thickness(17, 0, 12, 10);
                footer.Children.OfType<TextBlock>().FirstOrDefault()?.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
            }
        }

        _advancedBrandingConfigured = true;
    }

    private Border CreateSidebarDivider(string tag)
    {
        var divider = new Border
        {
            Tag = tag,
            Height = 1,
            Margin = new Thickness(17, 0, 17, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        divider.SetResourceReference(Border.BackgroundProperty, "Tc.Border");
        return divider;
    }
}
