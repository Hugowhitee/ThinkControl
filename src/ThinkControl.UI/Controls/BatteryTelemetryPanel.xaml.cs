using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using ThinkControl.UI.Services;
using WpfApplication = System.Windows.Application;

namespace ThinkControl.UI.Controls;

public partial class BatteryTelemetryPanel : System.Windows.Controls.UserControl
{
    public BatteryTelemetryPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyBatteryGaugePolish();
    }

    private void ApplyBatteryGaugePolish()
    {
        BatteryGauge? gauge = FindVisualChild<BatteryGauge>(this);
        if (gauge is null)
            return;

        gauge.Width = 198;
        gauge.Height = 58;
    }

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;
            T? nested = FindVisualChild<T>(child);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (WpfApplication.Current is not App app)
            return;

        MessageBoxResult answer = MessageBox.Show(
            "Clear ThinkControl's locally stored battery charge history?\n\nCurrent Windows battery health and cycle-count values are not changed.",
            "ThinkControl · Clear battery history",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        BatteryHistoryView view = app.BatteryHistoryService.Clear();
        app.State.ApplyBatteryHistory(view);
        app.BatteryTelemetryService.SetHistoricalChargePower(view.TypicalChargePowerWatts);
    }

    private void OpenVantage_Click(object sender, RoutedEventArgs e)
    {
        if (LenovoSoftwareLauncher.TryOpenVantage())
            return;

        try
        {
            Process.Start(new ProcessStartInfo(
                "ms-windows-store://search/?query=Lenovo%20Commercial%20Vantage")
            {
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}
