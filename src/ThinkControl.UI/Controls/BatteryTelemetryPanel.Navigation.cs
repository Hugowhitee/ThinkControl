using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class BatteryTelemetryPanel
{
    private void HistoryTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string view }) SelectHistoryView(view);
    }

    private void SelectHistoryView(string view)
    {
        ChargeHistoryView.Visibility = view == "Charge" ? Visibility.Visible : Visibility.Collapsed;
        DischargeHistoryView.Visibility = view == "Discharge" ? Visibility.Visible : Visibility.Collapsed;
        HealthHistoryView.Visibility = view == "Health" ? Visibility.Visible : Visibility.Collapsed;
        CycleHistoryView.Visibility = view == "Cycles" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ManageHistory_Click(object sender, RoutedEventArgs e)
    {
        bool open = HistorySettings.Visibility != Visibility.Visible;
        HistorySettings.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ManageHistoryButton.Content = open ? "Close history settings" : "History settings";
        if (open) { HistorySettings.UpdateLayout(); HistorySettings.BringIntoView(); }
    }

    private void BatterySessions_Click(object sender, RoutedEventArgs e)
    {
        BatterySessions.BringIntoView();
    }

    internal void ResetNavigationView()
    {
        SelectHistoryView("Charge");
        BatteryHistoryChargeTab.IsChecked = true;
        HistorySettings.Visibility = Visibility.Collapsed;
        ManageHistoryButton.Content = "History settings";
        CustomChargeEditor.Visibility = Visibility.Collapsed;
    }

    internal void PrepareCycleHistoryForSnapshot()
    {
        SelectHistoryView("Cycles");
        BatteryHistoryCyclesTab.IsChecked = true;
    }

    private void PresentChargeSession(BatterySessionDetail? session)
    {
        SessionStateText.Text = session is null ? "No recorded charge session"
            : session.IsActive ? "Current recorded session" : "Latest recorded session";
        SessionRangeText.Text = session is null ? "—" : $"{session.StartPercent}% → {session.EndPercent}%";
        SessionDurationMetric.Value = session is null ? "—" : FormatShortDuration(session.Duration);
        SessionPowerMetric.Value = session?.AveragePowerWatts is double watts
            ? $"{watts.ToString("0.0", CultureInfo.CurrentCulture)} W" : "—";
        SessionEnergyMetric.Value = session?.EnergyWh is double energy
            ? $"+{energy.ToString("0.0", CultureInfo.CurrentCulture)} Wh" : "—";
    }
}
