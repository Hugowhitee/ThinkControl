using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string SupportCardKey = "ThinkControl.Settings.SupportCard";
    private const string BuyMeACoffeeUrl = "https://buymeacoffee.com/hugowhite";

    private void ConfigureSupportCard()
    {
        if (Resources.Contains(SupportCardKey) || PageSettings?.Content is not StackPanel settingsStack)
            return;

        Resources[SupportCardKey] = true;

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

        var mark = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(255, 221, 0)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var cup = new Viewbox { Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
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
        grid.Children.Add(mark);

        var copy = new StackPanel { GridColumn = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
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

        var action = new Button
        {
            Content = "Buy me a coffee  ↗",
            Style = TryFindResource("TcButton") as Style,
            Padding = new Thickness(12, 7, 12, 7),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Open buymeacoffee.com/hugowhite"
        };
        action.Click += (_, _) => OpenBuyMeACoffee();
        Grid.SetColumn(action, 2);
        grid.Children.Add(action);

        card.Child = grid;
        card.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source && IsInsideButton(source))
                return;
            OpenBuyMeACoffee();
        };

        settingsStack.Children.Add(card);
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
