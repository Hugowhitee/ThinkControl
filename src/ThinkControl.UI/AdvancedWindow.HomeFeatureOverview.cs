using System.Windows;
using System.Windows.Controls;
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
            Margin = new Thickness(0, 14, 0, 0)
        };

        var header = new Grid { Margin = new Thickness(2, 0, 2, 8) };
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
            "Gestures · haptics · OSD",
            "Open touchpad controls");
        touchpad.Click += (_, _) => NavigateTouchpad();
        row.Children.Add(touchpad);

        Button sensors = CreateHomeFeatureButton(
            "Sensors",
            "Sensors",
            "Live temperature & fan telemetry",
            "Open sensors");
        sensors.Margin = new Thickness(7, 0, 7, 0);
        sensors.Click += (_, _) => NavigateSensors();
        Grid.SetColumn(sensors, 1);
        row.Children.Add(sensors);

        Button audio = CreateHomeFeatureButton(
            "Audio",
            "Audio",
            "Volume · Dolby profile · tone",
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
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });

        var icon = new PackIconLucide
        {
            Kind = iconKind,
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.SetResourceReference(ForegroundProperty, "Tc.TextMuted");
        grid.Children.Add(icon);

        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 11 });
        var sub = new TextBlock
        {
            Text = detail,
            FontSize = 9.5,
            Margin = new Thickness(0, 3, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(sub);
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);

        var chevron = new TextBlock
        {
            Text = "›",
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        chevron.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);

        return new Button
        {
            Height = 66,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new Border
            {
                Style = TryFindResource("TcSection") as Style,
                Padding = new Thickness(11, 8, 9, 8),
                Child = grid
            },
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = tooltip
        };
    }
}
