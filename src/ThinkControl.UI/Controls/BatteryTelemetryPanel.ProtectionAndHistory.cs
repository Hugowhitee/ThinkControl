using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Battery;
using ThinkControl.Core.Ipc;
using WpfApplication = System.Windows.Application;

namespace ThinkControl.UI.Controls;

public partial class BatteryTelemetryPanel
{
    private bool _batteryProtectionStatusSubscribed;
    private bool _batteryProtectionWritable;
    private bool _batteryProtectionAvailable;
    private bool _batteryProtectionWriteInFlight;
    private bool _syncingChargeProtection;
    private bool _syncingHistoryRetention;
    private int _lastChargeProtectionStart = 80;
    private int _lastChargeProtectionStop = 85;
    private int _historyVisibleDays = 7;

    internal void BringPreservationIntoView()
    {
        ChargeProtectionSwitch.BringIntoView();
        ChargeProtectionSwitch.Focus();
    }

    private void BatteryTelemetryPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (CustomChargeStartComboBox.ItemsSource is null)
        {
            CustomChargeStartComboBox.ItemsSource = Enumerable.Range(8, 11).Select(value => value * 5).ToArray();
            CustomChargeStopComboBox.ItemsSource = Enumerable.Range(9, 11).Select(value => value * 5).ToArray();
        }
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
        if (_batteryProtectionWriteInFlight)
            return;

        ApplyBatteryProtectionStatus(response);
    }

    private void ApplyBatteryProtectionStatus(ServiceResponse? response)
    {
        TelemetrySnapshot? telemetry = response?.Success == true ? response.Telemetry : null;
        _batteryProtectionWritable = response?.Capabilities?.BatteryChargeProtection == true &&
                                     response.Capabilities.BatteryCustomChargeThresholds;
        bool available = telemetry?.BatteryChargeProtectionEnabled is not null || telemetry?.BatteryChargeLimitPercent is not null;
        _batteryProtectionAvailable = available;
        bool enabled = telemetry?.BatteryChargeProtectionEnabled ?? telemetry?.BatteryChargeLimitPercent is < 100;
        int storedStart = telemetry?.BatteryChargeStartPercent ?? 80;
        int storedStop = telemetry?.BatteryChargeStopPercent ??
                         (enabled ? telemetry?.BatteryChargeLimitPercent ?? 85 : 85);
        int start = storedStart;
        int stop = enabled ? storedStop : 100;

        if (IsValidChargeProtectionPair(storedStart, storedStop))
        {
            _lastChargeProtectionStart = storedStart;
            _lastChargeProtectionStop = storedStop;
        }

        int selectedStart = enabled ? start : _lastChargeProtectionStart;
        int selectedStop = enabled ? storedStop : _lastChargeProtectionStop;

        _syncingChargeProtection = true;
        try
        {
            RemoveDynamicChargeProtectionPreset();
            ComboBoxItem? selected = FindChargeProtectionPreset(selectedStart, selectedStop);
            if (selected is null && available)
            {
                selected = new ComboBoxItem
                {
                    Content = $"Custom: {selectedStart}–{selectedStop}%",
                    Tag = $"custom:{selectedStart},{selectedStop}"
                };
                ChargeProtectionComboBox.Items.Insert(0, selected);
            }

            ChargeProtectionComboBox.SelectedItem = selected ?? ChargeProtectionComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
            ChargeProtectionSwitch.IsChecked = enabled;
            ChargeProtectionSwitch.IsEnabled = _batteryProtectionWritable;
            ChargeProtectionComboBox.IsEnabled = _batteryProtectionWritable && enabled;
            CustomChargeLimitsButton.IsEnabled = _batteryProtectionWritable && enabled;
        }
        finally
        {
            _syncingChargeProtection = false;
        }

        if (!available)
        {
            ChargeProtectionStateText.Text = "Not exposed";
            ChargeProtectionImpactText.Text = "Charge limits are not available on the active hardware provider.";
            ChargeProtectionWearText.Text = DescribeBatteryAging(enabled: null, available: false);
        }
        else if (!enabled)
        {
            ChargeProtectionStateText.Text = _batteryProtectionWritable ? "Off" : "Off (read-only)";
            ChargeProtectionImpactText.Text = "Preservation is off; charging is allowed to 100%.";
            ChargeProtectionWearText.Text = DescribeBatteryAging(enabled: false, available: true);
        }
        else
        {
            ChargeProtectionStateText.Text = _batteryProtectionWritable
                ? $"{stop}% limit active"
                : $"{stop}% limit (read-only)";
            ChargeProtectionImpactText.Text = DescribeChargeProtectionImpact(start, stop);
            ChargeProtectionWearText.Text = DescribeBatteryAging(enabled: true, available: true);
        }

        ChargeProtectionWearText.ToolTip = BatteryAgingTooltip;

        ChargeProtectionProviderText.Text = available
            ? "This battery controller can read charge limits but cannot change them."
            : "Charge limits are unavailable. Check your device's battery settings.";
        ChargeProtectionProviderText.ToolTip = telemetry?.BatteryChargeProtectionDetail;
        ChargeProtectionProviderText.Visibility =
            !available || !_batteryProtectionWritable ? Visibility.Visible : Visibility.Collapsed;
        ChargeProtectionFallbackButton.Visibility = _batteryProtectionWritable ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void ChargeProtectionSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_syncingChargeProtection || _batteryProtectionWriteInFlight || !_batteryProtectionWritable ||
            WpfApplication.Current is not App app || sender is not CheckBox toggle)
        {
            return;
        }

        bool enable = toggle.IsChecked == true;
        _batteryProtectionWriteInFlight = true;
        ChargeProtectionSwitch.IsEnabled = false;
        ChargeProtectionComboBox.IsEnabled = false;
        ChargeProtectionStateText.Text = "Applying…";

        try
        {
            ServiceResponse? response;
            int appliedStart = 80;
            int appliedStop = 85;

            if (!enable)
            {
                response = await app.HardwareClient.DisableBatteryChargeThresholdsAsync();
            }
            else
            {
                string tag = (ChargeProtectionComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "80,85";
                if (tag.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                    tag = tag["custom:".Length..];
                if (!TryParseThresholdPair(tag, out appliedStart, out appliedStop))
                {
                    appliedStart = 80;
                    appliedStop = 85;
                }

                response = await app.HardwareClient.SetBatteryChargeThresholdsAsync(appliedStart, appliedStop);
            }

            if (response?.Success == true)
            {
                if (enable)
                    app.ShowBatteryPreservationApplied(appliedStart, appliedStop);
                else
                    app.ShowBatteryPreservationDisabled();

                ApplyBatteryProtectionStatus(response);
                return;
            }

            ChargeProtectionStateText.Text = "Change rejected";
            ChargeProtectionImpactText.Text = response?.Error ?? "The hardware service did not return a verified battery-threshold result.";
            ChargeProtectionWearText.Text = "Wear context will refresh with the verified threshold state.";
            ServiceResponse? current = await app.HardwareClient.GetStatusAsync();
            ApplyBatteryProtectionStatus(current);
        }
        finally
        {
            _batteryProtectionWriteInFlight = false;
            ChargeProtectionSwitch.IsEnabled = _batteryProtectionWritable;
            ChargeProtectionComboBox.IsEnabled = _batteryProtectionWritable && ChargeProtectionSwitch.IsChecked == true;
        }
    }

    private async void ChargeProtection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingChargeProtection || _batteryProtectionWriteInFlight || !_batteryProtectionWritable ||
            ChargeProtectionSwitch.IsChecked != true || WpfApplication.Current is not App app ||
            ChargeProtectionComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        string tag = item.Tag?.ToString() ?? string.Empty;
        if (tag.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) ||
            !TryParseThresholdPair(tag, out int start, out int stop))
        {
            return;
        }

        _batteryProtectionWriteInFlight = true;
        ChargeProtectionSwitch.IsEnabled = false;
        ChargeProtectionComboBox.IsEnabled = false;
        ChargeProtectionStateText.Text = "Applying…";
        try
        {
            ServiceResponse? response = await app.HardwareClient.SetBatteryChargeThresholdsAsync(start, stop);
            if (response?.Success == true)
            {
                app.ShowBatteryPreservationApplied(start, stop);
                ApplyBatteryProtectionStatus(response);
                return;
            }

            ChargeProtectionStateText.Text = "Change rejected";
            ChargeProtectionImpactText.Text = response?.Error ?? "The hardware service did not return a verified battery-threshold result.";
            ChargeProtectionWearText.Text = "Wear context will refresh with the verified threshold state.";
            ServiceResponse? current = await app.HardwareClient.GetStatusAsync();
            ApplyBatteryProtectionStatus(current);
        }
        finally
        {
            _batteryProtectionWriteInFlight = false;
            ChargeProtectionSwitch.IsEnabled = _batteryProtectionWritable;
            ChargeProtectionComboBox.IsEnabled = _batteryProtectionWritable && ChargeProtectionSwitch.IsChecked == true;
        }
    }

    private ComboBoxItem? FindChargeProtectionPreset(int start, int stop)
    {
        return ChargeProtectionComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
        {
            string tag = item.Tag?.ToString() ?? string.Empty;
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
        return parts.Length == 2 &&
               int.TryParse(parts[0], out start) &&
               int.TryParse(parts[1], out stop) &&
               IsValidChargeProtectionPair(start, stop);
    }

    private static bool IsValidChargeProtectionPair(int start, int stop) =>
        start is >= 40 and <= 90 &&
        stop is >= 45 and <= 95 &&
        start % 5 == 0 &&
        stop % 5 == 0 &&
        start < stop;

    private const string BatteryAgingTooltip =
        "Staying plugged in does not itself imply repeated charging. Recharging resumes only below the OEM threshold; actual aging also depends on cycles and calendar time.";

    private static string DescribeBatteryAging(bool? enabled, bool available) =>
        !available
            ? "Battery aging cannot be estimated from unavailable charge settings."
            : enabled == true
                ? "A lower charge limit reduces battery stress at high charge levels. Wear also depends on heat, charge cycles and age."
                : "Staying near 100% for long periods may speed aging, especially when warm.";

    private static string DescribeChargeProtectionImpact(int start, int stop) =>
        $"Charging stops at {stop}% and resumes below {start}% when needed.";

    internal void PrepareCustomChargeEditorForSnapshot()
    {
        CustomChargeStartComboBox.ItemsSource ??= Enumerable.Range(8, 11).Select(value => value * 5).ToArray();
        CustomChargeStopComboBox.ItemsSource ??= Enumerable.Range(9, 11).Select(value => value * 5).ToArray();
        ChargeProtectionSwitch.IsChecked = true;
        CustomChargeLimitsButton.IsEnabled = true;
        CustomChargeStartComboBox.SelectedItem = 50;
        CustomChargeStopComboBox.SelectedItem = 80;
        CustomChargeValidationText.Text = "Charging restarts below 50%, then stops at 80%.";
        CustomChargeEditor.Visibility = Visibility.Visible;
    }

    private void CustomChargeLimits_Click(object sender, RoutedEventArgs e)
    {
        bool open = CustomChargeEditor.Visibility != Visibility.Visible;
        if (open)
        {
            CustomChargeStartComboBox.SelectedItem = _lastChargeProtectionStart;
            CustomChargeStopComboBox.SelectedItem = _lastChargeProtectionStop;
            CustomChargeValidationText.Text = "A lower recharge threshold reduces top-ups; it does not force discharge while plugged in.";
        }
        CustomChargeEditor.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CustomChargeCancel_Click(object sender, RoutedEventArgs e) =>
        CustomChargeEditor.Visibility = Visibility.Collapsed;

    private async void CustomChargeApply_Click(object sender, RoutedEventArgs e)
    {
        if (_batteryProtectionWriteInFlight || !_batteryProtectionWritable ||
            ChargeProtectionSwitch.IsChecked != true || WpfApplication.Current is not App app ||
            CustomChargeStartComboBox.SelectedItem is not int start ||
            CustomChargeStopComboBox.SelectedItem is not int stop)
            return;

        if (!IsValidChargeProtectionPair(start, stop))
        {
            CustomChargeValidationText.Text = "Resume must be below stop. 5% steps: start 40–90%, stop 45–95%.";
            return;
        }

        _batteryProtectionWriteInFlight = true;
        CustomChargeApplyButton.IsEnabled = false;
        ChargeProtectionSwitch.IsEnabled = false;
        ChargeProtectionComboBox.IsEnabled = false;
        CustomChargeValidationText.Text = "Applying…";
        try
        {
            ServiceResponse? response = await app.HardwareClient.SetBatteryChargeThresholdsAsync(start, stop);
            if (response?.Success == true)
            {
                ApplyBatteryProtectionStatus(response);
                CustomChargeEditor.Visibility = Visibility.Collapsed;
                app.ShowBatteryPreservationApplied(start, stop);
            }
            else
            {
                CustomChargeValidationText.Text = response?.Error ??
                    "Lenovo did not confirm these limits. Existing settings were kept.";
                ServiceResponse? current = await app.HardwareClient.GetStatusAsync();
                ApplyBatteryProtectionStatus(current);
            }
        }
        finally
        {
            _batteryProtectionWriteInFlight = false;
            CustomChargeApplyButton.IsEnabled = true;
            ChargeProtectionSwitch.IsEnabled = _batteryProtectionWritable;
            ChargeProtectionComboBox.IsEnabled =
                _batteryProtectionWritable && ChargeProtectionSwitch.IsChecked == true;
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
        HistoryStorageSummaryText.Text = $"Detailed graphs are kept for {days} days, daily summaries for one year. Learned estimates are retained.";
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
            ? $"Show older{(availableDays > 7 ? $" ({Math.Min(7, availableDays - 7)} more days)" : string.Empty)}"
            : "Show recent 7 days";
    }

    private void ResetHistory_Click(object sender, RoutedEventArgs e)
    {
        if (WpfApplication.Current is not App app)
            return;

        MessageBoxResult answer = MessageBox.Show(
            "Reset all ThinkControl battery history?\n\nThis deletes local session summaries, detailed graphs, the health trend and ThinkControl's learned charge/discharge estimates. Current firmware battery health, cycle count and charge protection are not changed.\n\nThinkControl will learn new estimates automatically from future use.",
            "ThinkControl: Reset battery history",
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
