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
            FontSize = 10.5,
            Margin = new Thickness(0, 3, 0, 0)
        };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        title.Children.Add(subtitle);
        header.Children.Add(title);

        var live = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        live.Children.Add(CreateHeaderReadout("MODE", "SelectedModeDisplay", "Performance"));
        live.Children.Add(CreateHeaderDivider());
        live.Children.Add(CreateHeaderReadout("DISPLAY", "CurrentRefreshText", "Display"));
        live.Children.Add(CreateHeaderDivider());
        live.Children.Add(CreateHeaderReadout("KEYBOARD", "KeyboardStatus", "Keyboard", 112));
        Grid.SetColumn(live, 1);
        header.Children.Add(live);
        return header;
    }

    private Border CreateHeaderReadout(string label, string path, string page, double width = 92)
    {
        var cell = new Border
        {
            Width = width,
            Padding = new Thickness(10, 2, 10, 2),
            Cursor = Cursors.Hand,
            ToolTip = $"Open {page.ToLowerInvariant()} controls"
        };
        var stack = new StackPanel();
        TextBlock caption = new()
        {
            Text = label,
            FontSize = 8.5,
            FontWeight = FontWeights.SemiBold
        };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextFaint");
        stack.Children.Add(caption);

        TextBlock value = new()
        {
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        value.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
        value.SetBinding(TextBlock.TextProperty, new Binding(path));
        stack.Children.Add(value);
        cell.Child = stack;
        cell.MouseLeftButtonUp += (_, _) => Navigate(page);
        return cell;
    }

    private Border CreateHeaderDivider()
    {
        var divider = new Border
        {
            Width = 1,
            Height = 30,
            VerticalAlignment = VerticalAlignment.Center
        };
        divider.SetResourceReference(Border.BackgroundProperty, "Tc.Border");
        return divider;
    }

    private Grid BuildHomeTelemetryStrip()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
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
        AddTelemetryMetric(grid, 4, "FANS", "FanRpmText", "CoolingProfileDisplay", "Fans", accentDetail: true);
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
        TextBlock detail = CreateMetricDetail("BatteryCompactLine");
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
        bool accentDetail = false,
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
        stack.Children.Add(value);

        TextBlock detail = detailPathOrText.Contains(' ')
            ? new TextBlock { Text = detailPathOrText }
            : CreateMetricDetail(detailPathOrText);
        detail.FontSize = 9.2;
        detail.Margin = new Thickness(0, 2, 0, 0);
        detail.TextTrimming = TextTrimming.CharacterEllipsis;
        detail.SetResourceReference(TextBlock.ForegroundProperty, accentDetail ? "Tc.Accent" : "Tc.TextMuted");
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
            FontSize = 8.8,
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
            FontSize = 9.2,
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
