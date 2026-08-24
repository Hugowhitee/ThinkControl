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
            FontSize = 12.5
        });
        copy.Children.Add(new TextBlock
        {
            Text = "Like the project? Buy me a coffee and help fund testing on more laptops.",
            Foreground = TryFindResource("Tc.TextMuted") as Brush,
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);

        Button action = CreateCoffeeAction();
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

        var card = new Border
        {
            Tag = HomeSupportCardTag,
            Style = TryFindResource("TcSection") as Style,
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(12, 9, 12, 9),
            Cursor = System.Windows.Input.Cursors.Hand,
            MinHeight = 54
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Border mark = CreateCoffeeMark(32);
        grid.Children.Add(mark);

        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(new TextBlock
        {
            Text = "Support ThinkControl",
            FontWeight = FontWeights.SemiBold,
            FontSize = 11
        });
        var detail = new TextBlock
        {
            Text = "Help fund testing on more laptops",
            FontSize = 9.5,
            Margin = new Thickness(0, 2, 0, 0)
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);

        Button action = CreateCoffeeAction();
        action.Padding = new Thickness(10, 5, 10, 5);
        Grid.SetColumn(action, 2);
        grid.Children.Add(action);

        card.Child = grid;
        AttachCardClick(card);

        // The Home quick-access row is inserted at index 1. Put support directly
        // below it so the option is visible without turning Home into an ad wall.
        int insertAt = Math.Min(2, homeStack.Children.Count);
        homeStack.Children.Insert(insertAt, card);
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
        var cup = new Viewbox
        {
            Width = size * 0.57,
            Height = size * 0.57,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var cupCanvas = new Canvas { Width = 24, Height = 24 };
        cupCanvas.Children.Add(new Path
        {
            Stroke = Brushes.Black,
            StrokeThickness = 1.9,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Data = Geometry.Parse("M5,8 L17,8 L16,17 C16,19 14.5,20 12.5,20 L9.5,20 C7.5,20 6,19 6,17 Z M17,10 L19,10 C21,10 21,15 17,15 M8,4 C8,5 9,5.5 9,6 M12,3 C12,4 13,4.5 13,5.5")
        });
        cup.Child = cupCanvas;
        mark.Child = cup;
        return mark;
    }

    private Button CreateCoffeeAction()
    {
        var action = new Button
        {
            Content = "Buy me a coffee  ↗",
            Style = TryFindResource("TcButton") as Style,
            Padding = new Thickness(12, 7, 12, 7),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Open buymeacoffee.com/hugowhite"
        };
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
