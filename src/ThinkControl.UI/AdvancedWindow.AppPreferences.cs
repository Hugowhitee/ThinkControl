using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _preferencesSyncing;
    private void ConfigureAppPreferencesUi()
    {
        RefreshOpeningViewSelection();
        _preferencesSyncing = true;
        try
        {
            string text = $"{_app.UserSettings.Current.BatteryDetailRetentionDays} days";
            SettingsBatteryRetention.SelectedItem = SettingsBatteryRetention.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => Equals(item.Content, text));
        }
        finally { _preferencesSyncing = false; }
    }
    private void BatteryRetention_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_preferencesSyncing || SettingsBatteryRetention?.SelectedItem is not ComboBoxItem { Content: string text } ||
            !int.TryParse(text.Split(' ')[0], out int days) || days == _app.UserSettings.Current.BatteryDetailRetentionDays) return;
        _app.UserSettings.Update(settings => settings with { BatteryDetailRetentionDays = days });
        _app.BatteryHistoryService.ConfigureDetailedRetentionDays(days);
    }
    private void OpeningView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string view }) return;
        _app.UserSettings.Update(settings => settings with { DefaultOpeningView = view });
        RefreshOpeningViewSelection();
    }
    internal void PrepareOpeningViewForSnapshot(string view)
    {
        bool advanced = view.Equals("Advanced", StringComparison.OrdinalIgnoreCase);
        SettingsOpeningCompact.IsChecked = !advanced;
        SettingsOpeningAdvanced.IsChecked = advanced;
    }
    private void RefreshOpeningViewSelection() => PrepareOpeningViewForSnapshot(_app.UserSettings.Current.DefaultOpeningView);
}
