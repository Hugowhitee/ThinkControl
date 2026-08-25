using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string HomeFeatureOverviewTag = "ThinkControl.Home.FeatureOverview";

    private void ConfigureHomeFeatureOverview()
    {
        FixHomeDisplayCardHeight();
        NormalizeLenovoVantageLabel();

        if (PageHome.Content is not StackPanel stack ||
            stack.Children.OfType<FrameworkElement>().Any(element => Equals(element.Tag, HomeFeatureOverviewTag)))
        {
            return;
        }

        var section = new StackPanel
        {
            Tag = HomeFeatureOverviewTag,
            Margin = new Thickness(0, 16, 0, 1)
        };

        var header = new Grid { Margin = new Thickness(2, 0, 2, 9) };
        header.Children.Add(new TextBlock
        {
            Text = "Quick access",
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold
        });
        section.Children.Add(header);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition());

        Button touchpad = CreateHomeFeatureButton(
            "Touchpad",
            "Touchpad",
            "Gestures, haptics and OSD",
            "Open touchpad controls");
        touchpad.Click += (_, _) => NavigateTouchpad();
        row.Children.Add(touchpad);

        Button sensors = CreateHomeFeatureButton(
            "Sensors",
            "Sensors",
            "Temperatures, power and fans",
            "Open sensors");
        sensors.Margin = new Thickness(8, 0, 8, 0);
        sensors.Click += (_, _) => NavigateSensors();
        Grid.SetColumn(sensors, 1);
        row.Children.Add(sensors);

        Button audio = CreateHomeFeatureButton(
            "Audio",
            "Audio",
            "Volume, Dolby and tone",
            "Open audio controls");
        audio.Click += (_, _) => NavigateAudio();
        Grid.SetColumn(audio, 2);
        row.Children.Add(audio);

        section.Children.Add(row);

        int insertAt = Math.Min(1, stack.Children.Count);
        stack.Children.Insert(insertAt, section);
    }

    private void FixHomeDisplayCardHeight()
    {
        if (PageHome?.Content is not StackPanel stack)
            return;

        foreach (Grid row in stack.Children.OfType<Grid>())
        {
            bool displayRow = FindVisualChildren<TextBlock>(row)
                .Any(block => string.Equals(block.Text, "Display", StringComparison.Ordinal));
            if (!displayRow)
                continue;

            foreach (Border card in row.Children.OfType<Border>())
            {
                card.Height = 190;
                card.MinHeight = 190;
                card.ClipToBounds = false;
            }
            return;
        }
    }

    private void NormalizeLenovoVantageLabel()
    {
        if (PageSystem is null)
            return;

        Button? vantage = FindVisualChildren<Button>(PageSystem)
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Commercial Vantage", StringComparison.Ordinal));
        if (vantage is null)
            return;

        vantage.Content = "Lenovo Vantage";
        vantage.Tag = "ms-windows-store://search/?query=Lenovo%20Vantage";
    }

    private Button CreateHomeFeatureButton(string title, string iconKind, string detail, string tooltip)
    {
        var icon = new PackIconLucide
        {
            Kind = iconKind,
            Width = 17,
            Height = 17,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.SetResourceReference(ForegroundProperty, "Tc.Text");

        var iconSurface = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(6),
            Child = icon,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconSurface.SetResourceReference(Border.BackgroundProperty, "Tc.SurfaceAlt");

        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11.5
        });
        var sub = new TextBlock
        {
            Text = detail,
            FontSize = 9.5,
            Margin = new Thickness(0, 3, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(sub);

        var chevron = new TextBlock
        {
            Text = "›",
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        chevron.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        grid.Children.Add(iconSurface);
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);
        Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);

        // The card itself is now the button surface. The previous implementation
        // nested a TcSection inside a TcButton, producing two independent borders and
        // a selector-like pressed/focus layer. One surface matches the rest of Home.
        var button = new Button
        {
            Height = 68,
            Style = TryFindResource("TcButton") as Style,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8, 10, 8),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = grid,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = tooltip,
            Focusable = false,
            IsTabStop = false
        };
        button.SetResourceReference(Control.BackgroundProperty, "Tc.Surface");
        button.SetResourceReference(Control.BorderBrushProperty, "Tc.BorderStrong");
        return button;
    }
}
