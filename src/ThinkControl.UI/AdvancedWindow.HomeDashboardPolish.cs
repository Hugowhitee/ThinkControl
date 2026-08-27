using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock
        {
            Text = "Overview",
            FontWeight = FontWeights.SemiBold,
            FontSize = 20
        });
        TextBlock subtitle = new()
        {
            Text = "Live machine state and the controls you use most",
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0)
        };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        title.Children.Add(subtitle);
        header.Children.Add(title);

        var eta = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24, 0, 6, 0)
        };
        TextBlock etaLabel = new()
        {
            Text = "BATTERY",
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        etaLabel.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        eta.Children.Add(etaLabel);

        TextBlock etaValue = new()
        {
            FontSize = 12.5,
            FontWeight = FontWeights.Medium,
            Margin = new Thickness(0, 3, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        etaValue.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        etaValue.SetBinding(TextBlock.TextProperty, new Binding("BatteryEtaText")
        {
            Converter = ReadableTypography.BatteryTimeConverter
        });
        eta.Children.Add(etaValue);
        Grid.SetColumn(eta, 1);
        header.Children.Add(eta);
        return header;
    }

    private Grid BuildHomeTelemetryStrip()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.75, GridUnitType.Star) });
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
        AddTelemetryMetric(grid, 4, "FANS", "CoolingProfileDisplay", "FanRpmText", "Fans", accentValue: true);
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
            ToolTip = "Open battery details"
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        root.ColumnDefinitions.Add(new ColumnDefinition());

        var gauge = new BatteryGauge
        {
            Width = 96,
            Height = 37,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        gauge.SetBinding(BatteryGauge.PercentProperty, new Binding("BatteryPercent"));
        gauge.SetBinding(BatteryGauge.IsChargingProperty, new Binding("BatteryCharging"));
        root.Children.Add(gauge);

        var copy = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0)
        };
        copy.Children.Add(CreateMetricLabel("BATTERY"));
        TextBlock value = CreateMetricValue("BatteryPercentText", 23);
        value.Margin = new Thickness(0, 2, 0, 0);
        copy.Children.Add(value);
        TextBlock detail = CreateMetricDetail("BatteryPowerText");
        detail.Margin = new Thickness(0, 1, 0, 0);
        copy.Children.Add(detail);
        Grid.SetColumn(copy, 1);
        root.Children.Add(copy);
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
            Margin = new Thickness(12, 0, 10, 0),
            ToolTip = $"Open {page.ToLowerInvariant()} details"
        };
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(CreateMetricLabel(label));
        TextBlock value = CreateMetricValue(valuePath, compactValue ? 15.5 : 20);
        value.Margin = new Thickness(0, 6, 0, 0);
        if (accentValue)
            value.SetResourceReference(TextBlock.ForegroundProperty, "Tc.Accent");
        stack.Children.Add(value);

        TextBlock detail = detailPathOrText.Contains(' ')
            ? new TextBlock { Text = detailPathOrText }
            : CreateMetricDetail(detailPathOrText);
        detail.FontSize = 10.5;
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
            FontSize = 10.5,
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
            FontSize = 10.5,
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

    private static void NormalizeHomeControlGrid(Grid grid)
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
        }
    }
}
