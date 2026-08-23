using System.Diagnostics;
using System.Windows;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class BatteryTelemetryPanel : System.Windows.Controls.UserControl
{
    public BatteryTelemetryPanel()
    {
        InitializeComponent();
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
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
