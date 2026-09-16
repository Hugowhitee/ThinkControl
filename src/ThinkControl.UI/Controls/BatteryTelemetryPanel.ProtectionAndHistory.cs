using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Ipc;
using WpfApplication = System.Windows.Application;

namespace ThinkControl.UI.Controls;

public partial class BatteryTelemetryPanel
{
    private bool _batteryProtectionStatusSubscribed;
    private bool _batteryProtectionWritable;
    private bool _syncingChargeProtection;
    private bool _syncingHistoryRetention;
    private int _historyVisibleDays = 7;

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
        _batteryProtectionWritable = response?.Capabilities?.BatteryChargeProtection == true &&
                                     response.Capabilities.BatteryCustomChargeThresholds;
        bool available = telemetry?.BatteryChargeProtectionEnabled is not null || telemetry?.BatteryChargeLimitPercent is not null;
        bool enabled = telemetry?.BatteryChargeProtectionEnabled ?? telemetry?.BatteryChargeLimitPercent is < 100;
        int start = telemetry?.BatteryChargeStartPercent ?? 75;
        int stop = enabled
            ? telemetry?.BatteryChargeStopPercent ?? telemetry?.BatteryChargeLimitPercent ?? 85
            : 100;

        _syncingChargeProtection = true;
        try
        {
            RemoveDynamicChargeProtectionPreset();
            ComboBoxItem? selected = enabled
                ? FindChargeProtectionPreset(start, stop)
                : FindChargeProtectionPreset(enabled: false);
            if (enabled && selected is null && available)
            {
                selected = new ComboBoxItem
                {
                    Content = $"Custom · {start}–{stop}%",
                    Tag = $"custom:{start},{stop}"
                };
                ChargeProtectionComboBox.Items.Insert(0, selected);
            }

            ChargeProtectionComboBox.SelectedItem = selected;
            ChargeProtectionComboBox.IsEnabled = _batteryProtectionWritable;
        }
        finally
        {
            _syncingChargeProtection = false;
        }

        if (!available)
        {
            ChargeProtectionStateText.Text = "Not exposed";
            ChargeProtectionImpactText.Text = "ThinkControl did not find the Lenovo PM Device threshold contract on the active hardware provider.";
        }
        else if (!enabled)
        {
            ChargeProtectionStateText.Text = _batteryProtectionWritable ? "Full charge · active" : "Full charge · read-only";
            ChargeProtectionImpactText.Text = "Maximum available unplugged runtime. No charge ceiling is active, so the battery may remain near 100% while plugged in and receives no high-charge wear reduction from a threshold.";
        }
        else
        {
            ChargeProtectionStateText.Text = _batteryProtectionWritable
                ? $"{start}–{stop}% · active"
                : $"{start}–{stop}% · read-only";
            ChargeProtectionImpactText.Text = DescribeChargeProtectionImpact(start, stop);
        }

        ChargeProtectionProviderText.Text = telemetry?.BatteryChargeProtectionDetail ??
            "ThinkControl changes only a verified OEM threshold provider. Wear reduction is qualitative; it does not claim a fixed cycle-life multiplier.";
        ChargeProtectionFallbackButton.Visibility = _batteryProtectionWritable ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void ChargeProtection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingChargeProtection || !_batteryProtectionWritable || WpfApplication.Current is not App app ||
            ChargeProtectionComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        string tag = item.Tag?.ToString() ?? string.Empty;
        if (tag.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            return;

        ChargeProtectionComboBox.IsEnabled = false;
        ChargeProtectionStateText.Text = "Applying…";
        try
        {
            ServiceResponse? response;
            if (tag.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                response = await app.HardwareClient.DisableBatteryChargeThresholdsAsync();
            }
            else if (TryParseThresholdPair(tag, out int start, out int stop))
            {
                response = await app.HardwareClient.SetBatteryChargeThresholdsAsync(start, stop);
            }
            else
            {
                ChargeProtectionStateText.Text = "Invalid preset";
                return;
            }

            if (response?.Success == true)
            {
                ApplyBatteryProtectionStatus(response);
                return;
            }

            ChargeProtectionStateText.Text = "Change rejected";
            ChargeProtectionImpactText.Text = response?.Error ?? "The hardware service did not return a verified battery-threshold result.";
            ServiceResponse? current = await app.HardwareClient.GetStatusAsync();
            ApplyBatteryProtectionStatus(current);
        }
        finally
        {
            ChargeProtectionComboBox.IsEnabled = _batteryProtectionWritable;
        }
    }

    private ComboBoxItem? FindChargeProtectionPreset(int start = 0, int stop = 0, bool enabled = true)
    {
        return ChargeProtectionComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
        {
            string tag = item.Tag?.ToString() ?? string.Empty;
            if (!enabled)
                return tag.Equals("off", StringComparison.OrdinalIgnoreCase);
            return TryParseThresholdPair(tag, out int candidateStart, out int candidateStop) &&
                   candidateStart == start && candidateStop == stop;
        });
    }

    private void RemoveDynamicChargeProtectionPreset()
    {
        ComboBoxItem? dynamic = ChargeProtectionComboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString()?.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) == true);
        if (dynamic is not null)
            ChargeProtectionComboBox.Items.Remove(dynamic);
    }

    private static bool TryParseThresholdPair(string raw, out int start, out int stop)
    {
        start = 0;
        stop = 0;
        string[] parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out start) && int.TryParse(parts[1], out stop) && start < stop;
    }

    private static string DescribeChargeProtectionImpact(int start, int stop)
    {
        int headroom = Math.Max(0, 100 - stop);
        int window = Math.Max(0, stop - start);
        string wear = stop <= 65
            ? "Battery wear stress: much lower than routinely staying near 100%, because high state-of-charge exposure is strongly reduced."
            : stop <= 80
                ? "Battery wear stress: lower than routinely staying near 100%, because the battery spends less time at very high charge."
                : "Battery wear stress: somewhat lower than routinely staying near 100%, while preserving most unplugged capacity.";
        string use = stop <= 65
            ? "Best when the laptop is plugged in most of the day."
            : stop <= 80
                ? "Good for frequent desk use while keeping useful unplugged reserve."
                : "A balanced everyday limit with most unplugged capacity still available.";
        return $"{wear} Avoids routine charging in the top {headroom}% of capacity · charging resumes below {start}% and stops at {stop}% · {window}% hysteresis avoids constant tiny top-ups. {use} Exact lifetime improvement still depends on temperature and use.";
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

    private void HistoryRange_Click(object sender, RoutedEventArgs e)
    {
        _historyVisibleDays = _historyVisibleDays <= 7 ? 14 : 7;
        RefreshHistoryUi();
    }

    private void UpdateHistoryRangeButton(int availableDays)
    {
        bool hasOlder = availableDays > 7;
        HistoryRangeButton.Visibility = hasOlder ? Visibility.Visible : Visibility.Collapsed;
        HistoryRangeButton.Content = _historyVisibleDays <= 7
            ? $"Show older{(availableDays > 7 ? $" · {Math.Min(7, availableDays - 7)} more days" : string.Empty)}"
            : "Show recent 7 days";
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
        _historyVisibleDays = 7;
        RefreshHistoryUi();
        SyncHistoryManagementUi();
    }
}
