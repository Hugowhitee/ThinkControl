using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string Alpha30HomeBatteryPolishKey = "ThinkControl.Alpha30.HomeBattery";

    private void ApplyAlpha30HomePolish()
    {
        if (Resources.Contains(Alpha30HomeBatteryPolishKey) ||
            PageHome?.Content is not StackPanel homeStack)
        {
            return;
        }

        Border? telemetry = homeStack.Children
            .OfType<Border>()
            .FirstOrDefault(border => Math.Abs(border.Height - 112) < 0.1 && border.Child is Grid);
        if (telemetry?.Child is not Grid strip)
            return;

        Grid? battery = strip.Children
            .OfType<Grid>()
            .FirstOrDefault(element => Grid.GetColumn(element) == 0 &&
                                       element.Children.OfType<BatteryGauge>().Any());
        if (battery is null)
            return;

        StackPanel? copy = battery.Children.OfType<StackPanel>().FirstOrDefault();
        BatteryGauge? gauge = battery.Children.OfType<BatteryGauge>().FirstOrDefault();
        TextBlock? detail = copy?.Children.OfType<TextBlock>().LastOrDefault();
        if (copy is null || gauge is null || detail is null)
            return;

        Resources[Alpha30HomeBatteryPolishKey] = true;

        // Keep label/value beside the visual, but let the remaining-time line use
        // the full battery metric width underneath it. Alpha.29 reserved the gauge
        // column for every text row, which caused strings such as
        // "4 h 47 min remaining" to ellipsize unnecessarily.
        copy.Children.Remove(detail);
        battery.RowDefinitions.Clear();
        battery.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        battery.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(copy, 0);
        Grid.SetColumn(copy, 0);
        copy.VerticalAlignment = VerticalAlignment.Top;

        gauge.Width = 130;
        gauge.Height = 52;
        gauge.Margin = new Thickness(6, -5, 0, 0);
        gauge.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetRow(gauge, 0);
        Grid.SetColumn(gauge, 1);

        detail.Margin = new Thickness(0, 1, 0, 0);
        detail.TextTrimming = TextTrimming.None;
        detail.TextWrapping = TextWrapping.NoWrap;
        detail.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetRow(detail, 1);
        Grid.SetColumn(detail, 0);
        Grid.SetColumnSpan(detail, 2);
        battery.Children.Add(detail);
    }
}
