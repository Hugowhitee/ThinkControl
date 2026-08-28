using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string HomeDashboardPolishKey = "ThinkControl.Home.DashboardPolish";

    private void ConfigureHomeDashboardPolish()
    {
        if (Resources.Contains(HomeDashboardPolishKey) ||
            PageHome?.Content is not StackPanel homeStack)
        {
            return;
        }

        Resources[HomeDashboardPolishKey] = true;
        NormalizeHomePowerTerminology();

        FrameworkElement? oldHeader = homeStack.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(element => Equals(element.Tag, HomeSupportCardTag));
        if (oldHeader is not null)
        {
            int index = homeStack.Children.IndexOf(oldHeader);
            homeStack.Children.Remove(oldHeader);
            homeStack.Children.Insert(index, BuildHomeHeader());
        }

        Border? telemetry = homeStack.Children
            .OfType<Border>()
            .FirstOrDefault(border => border.Height is >= 120 and <= 126);
        if (telemetry is not null)
        {
            telemetry.Height = 112;
            telemetry.Padding = new Thickness(14, 11, 14, 11);
            telemetry.Child = BuildHomeTelemetryStrip();
        }

        Grid? controls = homeStack.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => grid.Children.OfType<Border>().Count() >= 4);
        if (controls is not null)
            NormalizeHomeControlGrid(controls);
    }

    private void NormalizeHomePowerTerminology()
    {
        HomeQuiet.Content = "Efficiency";
        HomePerformance.Content = "Performance";

        if (HomeQuiet.Parent is Grid segmentGrid && segmentGrid.Parent is StackPanel performanceCard)
        {
            Grid? heading = performanceCard.Children.OfType<Grid>().FirstOrDefault();
            TextBlock? duplicateMode = heading?.Children.OfType<TextBlock>().FirstOrDefault(text =>
                BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path.Path == "SelectedModeDisplay");
            if (duplicateMode is not null)
                duplicateMode.Visibility = Visibility.Collapsed;

            TextBlock? description = performanceCard.Children.OfType<TextBlock>().FirstOrDefault(text =>
                text.Text.Contains("responsive or efficient", StringComparison.OrdinalIgnoreCase));
            if (description is not null)
                description.Text = "Choose the Windows power behavior for this power source.";
        }

        // Keep the older fallback Performance page coherent too. The enhanced
        // PerformancePanel already uses Efficiency / Balanced / Performance.
        if (PerfQuiet.Content is StackPanel quietCopy)
        {
            TextBlock? title = quietCopy.Children.OfType<TextBlock>().FirstOrDefault();
            if (title is not null)
                title.Text = "Efficiency";
        }
    }

    private Grid BuildHomeHeader()
    {
        var header = new Grid
        {
            Tag = HomeSupportCardTag,
            Margin = new Thickness(2, 0, 2, 12)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition());

        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock
        {
            Text = "Overview",
            FontWeight = FontWeights.SemiBold,
            FontSize = TypographyScale.PageTitle
        });
        TextBlock subtitle = new()
        {
            Text = "Live machine state and the controls you use most",
            FontSize = TypographyScale.Body,
            Margin = new Thickness(0, 4, 0, 0)
        };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        title.Children.Add(subtitle);
        header.Children.Add(title);

        return header;
    }

    private Grid BuildHomeTelemetryStrip()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.55, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });

        Grid battery = BuildBatteryMetric();
        Grid.SetColumn(battery, 0);
        grid.Children.Add(battery);
        AddTelemetryDivider(grid, 1);
        AddTelemetryMetric(grid, 2, "CPU", "CpuTemperatureText", "Live temperature", "System");
        AddTelemetryDivider(grid, 3);
        AddTelemetryMetric(grid, 4, "FANS", "CoolingProfileDisplay", "FanRpmText", "Fans");
        AddTelemetryDivider(grid, 5);
        AddTelemetryMetric(grid, 6, "POWER", "BatteryPowerText", "BatteryAveragePowerText", "Battery");
        AddTelemetryDivider(grid, 7);
        AddTelemetryMetric(grid, 8, "SENSORS", "SensorCountText", "Hardware telemetry", "System", compactValue: true);
        return grid;
    }

    private Grid BuildBatteryMetric()
    {
        var root = new Grid
        {
            Cursor = Cursors.Hand,
            Margin = new Thickness(8, 0, 10, 0)
        };
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Battery copy follows exactly the same left-aligned hierarchy as CPU,
        // Fans, Power and Sensors. The graphic is contextual information, so it
        // sits at the far right instead of pushing the text out of alignment.
        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(CreateMetricLabel("BATTERY"));
        TextBlock value = CreateMetricValue("BatteryPercentText", 20);
        value.Margin = new Thickness(0, 6, 0, 0);
        copy.Children.Add(value);
        TextBlock detail = new()
        {
            FontSize = TypographyScale.Secondary,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        detail.SetBinding(TextBlock.TextProperty, new Binding("BatteryEtaText")
        {
            Converter = ReadableTypography.BatteryTimeConverter
        });
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        copy.Children.Add(detail);
        root.Children.Add(copy);

        var gauge = new BatteryGauge
        {
            Width = 126,
            Height = 50,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        gauge.SetBinding(BatteryGauge.PercentProperty, new Binding("BatteryPercent"));
        gauge.SetBinding(BatteryGauge.IsChargingProperty, new Binding("BatteryCharging"));
        Grid.SetColumn(gauge, 1);
        root.Children.Add(gauge);

        root.MouseLeftButtonUp += (_, _) => Navigate("Battery");
        return root;
    }

    private void AddTelemetryMetric(
        Grid parent,
        int column,
        string label,
        string valuePath,
        string detailPathOrText,
        string page,
        bool accentValue = false,
        bool compactValue = false)
    {
        var hit = new Grid
        {
            Cursor = Cursors.Hand,
            Margin = new Thickness(12, 0, 10, 0)
        };
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(CreateMetricLabel(label));
        TextBlock value = CreateMetricValue(valuePath, compactValue ? 16 : 20);
        value.Margin = new Thickness(0, 6, 0, 0);
        if (accentValue)
            value.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Accent");
        stack.Children.Add(value);

        TextBlock detail = detailPathOrText.Contains(' ')
            ? new TextBlock { Text = detailPathOrText }
            : CreateMetricDetail(detailPathOrText);
        detail.FontSize = TypographyScale.Secondary;
        detail.Margin = new Thickness(0, 2, 0, 0);
        detail.TextTrimming = TextTrimming.CharacterEllipsis;
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        stack.Children.Add(detail);
        hit.Children.Add(stack);
        hit.MouseLeftButtonUp += (_, _) => Navigate(page);
        Grid.SetColumn(hit, column);
        parent.Children.Add(hit);
    }

    private static TextBlock CreateMetricLabel(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = TypographyScale.Caption,
            FontWeight = FontWeights.SemiBold
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        return label;
    }

    private static TextBlock CreateMetricValue(string path, double size)
    {
        var value = new TextBlock
        {
            FontSize = size,
            FontWeight = FontWeights.Light,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        value.SetBinding(TextBlock.TextProperty, new Binding(path));
        return value;
    }

    private static TextBlock CreateMetricDetail(string path)
    {
        var detail = new TextBlock
        {
            FontSize = TypographyScale.Secondary,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        detail.SetBinding(TextBlock.TextProperty, new Binding(path));
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        return detail;
    }

    private void AddTelemetryDivider(Grid grid, int column)
    {
        var divider = new Border
        {
            Width = 1,
            Margin = new Thickness(0, 4, 0, 4)
        };
        divider.SetResourceReference(Border.BackgroundProperty, "Tc.Border");
        Grid.SetColumn(divider, column);
        grid.Children.Add(divider);
    }

    private void NormalizeHomeControlGrid(Grid grid)
    {
        grid.Margin = new Thickness(0, 12, 0, 0);
        foreach (Border card in grid.Children.OfType<Border>())
        {
            card.Height = 184;
            int row = Grid.GetRow(card);
            int column = Grid.GetColumn(card);
            card.Margin = new Thickness(
                column == 0 ? 0 : 6,
                row == 0 ? 0 : 6,
                column == 0 ? 6 : 0,
                row == 0 ? 6 : 0);

            string page = (row, column) switch
            {
                (0, 0) => "Performance",
                (0, 1) => "Fans",
                (1, 0) => "Display",
                (1, 1) => "Keyboard",
                _ => string.Empty
            };
            if (page.Length > 0)
                ConfigureInternalNavigationCard(card, page);
        }
    }

    private void ConfigureInternalNavigationCard(Border card, string page)
    {
        card.Tag = "ThinkControl.Home.InternalNavigation." + page;
        card.Cursor = Cursors.Hand;
        card.ToolTip = null;

        if (card.Child is StackPanel stack)
        {
            Button[] oldLinks = stack.Children.OfType<Button>()
                .Where(button => button.Tag as string == page)
                .ToArray();
            foreach (Button oldLink in oldLinks)
                stack.Children.Remove(oldLink);

            Grid? heading = stack.Children.OfType<Grid>().FirstOrDefault();
            StackPanel? title = heading?.Children.OfType<StackPanel>()
                .FirstOrDefault(panel => panel.Orientation == Orientation.Horizontal);
            if (title is not null && !title.Children.OfType<TextBlock>().Any(text => Equals(text.Tag, "ThinkControl.InternalChevron")))
            {
                var chevron = new TextBlock
                {
                    Tag = "ThinkControl.InternalChevron",
                    Text = "›",
                    FontSize = TypographyScale.SectionTitle,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(7, -1, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                };
                chevron.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
                title.Children.Add(chevron);
            }
        }

        card.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (FindInteractiveAncestor(e.OriginalSource as DependencyObject, card) is not null)
                return;
            Navigate(page);
            e.Handled = true;
        };
    }

    private static DependencyObject? FindInteractiveAncestor(DependencyObject? source, DependencyObject stop)
    {
        DependencyObject? current = source;
        while (current is not null && !ReferenceEquals(current, stop))
        {
            if (current is ButtonBase or ComboBox or Slider or CheckBox)
                return current;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
