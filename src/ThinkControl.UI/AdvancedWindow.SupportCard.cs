using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string SupportCardKey = "ThinkControl.Settings.SupportCard";
    private const string HomeSupportCardTag = "ThinkControl.Home.SupportCard";
    private const string BuyMeACoffeeUrl = "https://buymeacoffee.com/hugowhite";
    private static readonly Geometry BuyMeACoffeeGeometry = Geometry.Parse("M6.898 0L5.682 2.799H3.877v2.523h.695L5.277 9.8H4.172l1.46 8.23.938-.01L7.512 24h8.918l.062-.4.88-5.58.888.01 1.46-8.231h-1.056l.705-4.477h.756V2.8h-1.918L16.99 0H6.898zm.528.805h9.043l.771 1.78H6.652l.774-1.78zm-2.75 2.797H19.32v.92H4.676v-.92zm.453 6.998h13.635l-1.176 6.62-5.649-.06-5.636.06-1.174-6.62z");

    private void ConfigureSupportCard()
    {
        if (Resources.Contains(SupportCardKey))
            return;

        Resources[SupportCardKey] = true;

        if (PageSettings?.Content is StackPanel settingsStack)
            AddSettingsSupportCard(settingsStack);
        AddHomeSupportCard();
    }

    private void AddSettingsSupportCard(StackPanel settingsStack)
    {
        var card = new Border
        {
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 14, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(CreateCoffeeMark(42));

        var copy = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0)
        };
        copy.Children.Add(new TextBlock
        {
            Text = "Support ThinkControl",
            FontWeight = FontWeights.SemiBold,
            FontSize = TypographyScale.ControlLabel
        });
        copy.Children.Add(new TextBlock
        {
            Text = "Like the project? Buy me a coffee and help fund testing on more laptops.",
            Foreground = TryFindResource("Tc.TextMuted") as Brush,
            FontSize = TypographyScale.Caption,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);

        Button action = CreateCoffeeAction(compact: false);
        Grid.SetColumn(action, 2);
        grid.Children.Add(action);

        card.Child = grid;
        AttachCardClick(card);
        settingsStack.Children.Add(card);
    }

    private void AddHomeSupportCard()
    {
        if (PageHome?.Content is not StackPanel homeStack ||
            homeStack.Children.OfType<FrameworkElement>().Any(element => Equals(element.Tag, HomeSupportCardTag)))
        {
            return;
        }

        // Home is an instrument panel, not a promotion surface. Keep the support
        // action in Settings and leave the primary dashboard header visually quiet.
        var header = new StackPanel
        {
            Tag = HomeSupportCardTag,
            Margin = new Thickness(2, 0, 2, 12)
        };
        header.Children.Add(new TextBlock
        {
            Text = "Overview",
            FontWeight = FontWeights.SemiBold,
            FontSize = TypographyScale.PageTitle
        });
        var detail = new TextBlock
        {
            Text = "Live status and the controls you use most",
            FontSize = TypographyScale.Caption,
            Margin = new Thickness(0, 3, 0, 0)
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        header.Children.Add(detail);
        homeStack.Children.Insert(0, header);
    }

    private Border CreateCoffeeMark(double size)
    {
        var mark = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size >= 40 ? 10 : 8),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 221, 0)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        mark.Child = new Viewbox
        {
            Width = size * 0.52,
            Height = size * 0.52,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Fill = Brushes.Black,
                Data = BuyMeACoffeeGeometry
            }
        };
        return mark;
    }

    private Button CreateCoffeeAction(bool compact)
    {
        var action = new Button
        {
            Style = TryFindResource("TcButton") as Style,
            Padding = compact ? new Thickness(10, 5, 10, 5) : new Thickness(12, 7, 12, 7),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 221, 0)),
            Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 194, 0)),
            ToolTip = "Open Buy Me a Coffee"
        };
        action.FocusVisualStyle = null;
        action.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        var scale = new ScaleTransform(1, 1);
        action.RenderTransform = scale;
        action.MouseEnter += (_, _) => { scale.ScaleX = 1.035; scale.ScaleY = 1.035; };
        action.MouseLeave += (_, _) => { scale.ScaleX = 1; scale.ScaleY = 1; };
        action.GotKeyboardFocus += (_, _) => { scale.ScaleX = 1.035; scale.ScaleY = 1.035; };
        action.LostKeyboardFocus += (_, _) =>
        {
            if (!action.IsMouseOver)
            {
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }
        };

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new Viewbox
        {
            Width = compact ? 14 : 15,
            Height = compact ? 14 : 15,
            Margin = new Thickness(0, 0, 7, 0),
            Child = new Path { Fill = Brushes.Black, Data = BuyMeACoffeeGeometry }
        });
        content.Children.Add(new TextBlock
        {
            Text = compact ? "Buy me a coffee" : "Buy me a coffee  ↗",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center
        });
        action.Content = content;
        action.Click += (_, _) => OpenBuyMeACoffee();
        return action;
    }

    private void AttachCardClick(Border card)
    {
        card.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source && IsInsideButton(source))
                return;
            OpenBuyMeACoffee();
        };
    }

    private static bool IsInsideButton(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is Button)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static void OpenBuyMeACoffee()
    {
        try
        {
            Process.Start(new ProcessStartInfo(BuyMeACoffeeUrl) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
