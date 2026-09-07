using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Ipc;
using WpfApplication = System.Windows.Application;

namespace ThinkControl.UI.Controls;

public partial class BatteryTelemetryPanel
{
    private bool _batteryProtectionStatusSubscribed;
    private bool _syncingChargeProtection;
    private bool _syncingHistoryRetention;

    private void BatteryTelemetryPanel_Loaded(object sender, RoutedEventArgs e)
    {
        SyncHistoryManagementUi();
        AttachBatteryProtectionStatus();
    }

    private void BatteryTelemetryPanel_Unloaded(object sender, RoutedEventArgs e) =>
        DetachBatteryProtectionStatus();

    private void AttachBatteryProtectionStatus()
    {
        if (_batteryProtectionStatusSubscribed || WpfApplication.Current is not App app)
            return;
        app.HardwareClient.StatusObserved += BatteryProtection_StatusObserved;
        _batteryProtectionStatusSubscribed = true;
        _ = app.HardwareClient.GetStatusAsync();
    }

    private void DetachBatteryProtectionStatus()
    {
        if (!_batteryProtectionStatusSubscribed || WpfApplication.Current is not App app)
            return;
        app.HardwareClient.StatusObserved -= BatteryProtection_StatusObserved;
        _batteryProtectionStatusSubscribed = false;
    }

    private void BatteryProtection_StatusObserved(object? sender, ServiceResponse? response)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => BatteryProtection_StatusObserved(sender, response));
            return;
        }
        ApplyBatteryProtectionStatus(response);
    }

    private void ApplyBatteryProtectionStatus(ServiceResponse? response)
    {
        TelemetrySnapshot? telemetry = response?.Success == true ? response.Telemetry : null;
        bool writable = response?.Capabilities?.BatteryChargeProtection == true;
        int? limit = telemetry?.BatteryChargeLimitPercent;

        _syncingChargeProtection = true;
        try
        {
            ChargeProtectionComboBox.IsEnabled = writable;
            ChargeProtectionComboBox.SelectedItem = limit is 80 or 100
                ? ChargeProtectionComboBox.Items.OfType<ComboBoxItem>()
                    .FirstOrDefault(item => int.TryParse(item.Tag?.ToString(), out int value) && value == limit)
                : null;
        }
        finally
        {
            _syncingChargeProtection = false;
        }

        if (limit == 80)
        {
            ChargeProtectionStateText.Text = writable ? "Battery care · active" : "Battery care · read-only";
            ChargeProtectionImpactText.Text = "20 percentage points of headroom from full charge · less time at high state of charge. Best for everyday plugged-in use.";
        }
        else if (limit == 100)
        {
            ChargeProtectionStateText.Text = writable ? "Full charge · active" : "Full charge · read-only";
            ChargeProtectionImpactText.Text = "Maximum available runtime. Switch back to Battery care when you do not need the final 20% of capacity.";
        }
        else
        {
            ChargeProtectionStateText.Text = "Not exposed";
            ChargeProtectionImpactText.Text = "ThinkControl did not find a verified writable battery-care contract on the active hardware provider.";
        }

        ChargeProtectionProviderText.Text = telemetry?.BatteryChargeProtectionDetail ??
            "ThinkControl writes only a verified OEM charge-protection semantic and verifies the result by readback.";
        ChargeProtectionFallbackButton.Visibility = writable ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void ChargeProtection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingChargeProtection || WpfApplication.Current is not App app ||
            ChargeProtectionComboBox.SelectedItem is not ComboBoxItem item ||
            !int.TryParse(item.Tag?.ToString(), out int percent) || percent is not 80 and not 100)
        {
            return;
        }

        ChargeProtectionComboBox.IsEnabled = false;
        ChargeProtectionStateText.Text = $"Applying {percent}%…";
        try
        {
            ServiceResponse? response = await app.HardwareClient.SetBatteryChargeLimitAsync(percent);
            if (response?.Success == true)
            {
                ApplyBatteryProtectionStatus(response);
                return;
            }

            ChargeProtectionStateText.Text = "Change rejected";
            ChargeProtectionImpactText.Text = response?.Error ?? "The hardware service did not return a verified battery-protection result.";
            ServiceResponse? current = await app.HardwareClient.GetStatusAsync();
            ApplyBatteryProtectionStatus(current);
        }
        finally
        {
            if (ChargeProtectionStateText.Text != "Not exposed")
                ChargeProtectionComboBox.IsEnabled = app is not null && ChargeProtectionComboBox.SelectedItem is not null;
        }
    }

    private void SyncHistoryManagementUi()
    {
        if (WpfApplication.Current is not App app)
            return;

        int days = app.BatteryHistoryService.DetailedRetentionDays;
        _syncingHistoryRetention = true;
        try
        {
            BatteryHistoryRetentionComboBox.SelectedItem = BatteryHistoryRetentionComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => int.TryParse(item.Tag?.ToString(), out int value) && value == days)
                ?? BatteryHistoryRetentionComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
        }
        finally
        {
            _syncingHistoryRetention = false;
        }
        HistoryStorageSummaryText.Text = $"Detailed graphs: {days} days · compact summaries: 1 year · learned estimates remain available after automatic compaction";
    }

    private void BatteryHistoryRetention_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingHistoryRetention || WpfApplication.Current is not App app ||
            BatteryHistoryRetentionComboBox.SelectedItem is not ComboBoxItem item ||
            !int.TryParse(item.Tag?.ToString(), out int days))
        {
            return;
        }

        app.UserSettings.Update(settings => settings with { BatteryDetailRetentionDays = days });
        app.BatteryHistoryService.ConfigureDetailedRetentionDays(days);
        SyncHistoryManagementUi();
        RefreshHistoryUi();
    }

    private void ResetHistory_Click(object sender, RoutedEventArgs e)
    {
        if (WpfApplication.Current is not App app)
            return;

        MessageBoxResult answer = MessageBox.Show(
            "Reset all ThinkControl battery history?\n\nThis deletes local session summaries, detailed graphs, the health trend and ThinkControl's learned charge/discharge estimates. Current firmware battery health, cycle count and charge protection are not changed.\n\nThinkControl will learn new estimates automatically from future use.",
            "ThinkControl · Reset battery history",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
            return;

        app.ClearBatteryHistory();
        RefreshHistoryUi();
        SyncHistoryManagementUi();
    }
}
