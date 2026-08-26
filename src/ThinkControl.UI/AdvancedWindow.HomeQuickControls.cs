using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string MoreFanProfilesLabel = "Auto / custom…";

    private void ConfigureHomeQuickControls()
    {
        RefreshHomeFanProfiles();
    }

    private void RefreshHomeFanProfiles()
    {
        if (HomeFanProfileCombo is null)
            return;

        string selected = _app.State.CoolingProfileDisplay;
        _syncing = true;
        try
        {
            HomeFanQuickGrid.IsEnabled = _app.State.CanFanControl;
            HomeFanQuiet.IsChecked = selected.Equals("Quiet", StringComparison.OrdinalIgnoreCase);
            HomeFanBalanced.IsChecked = selected.Equals("Balanced", StringComparison.OrdinalIgnoreCase);
            HomeFanMax.IsChecked = selected.Equals("Max cooling", StringComparison.OrdinalIgnoreCase);

            var values = new List<string> { MoreFanProfilesLabel, "Auto" };
            values.AddRange(_app.FanProfiles.GetProfiles()
                .Where(profile => !_app.FanProfiles.IsBuiltIn(profile.Id))
                .Select(profile => profile.Name));
            HomeFanProfileCombo.ItemsSource = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            HomeFanProfileCombo.SelectedItem = values.Contains(selected, StringComparer.OrdinalIgnoreCase)
                ? selected
                : MoreFanProfilesLabel;
            if (HomeFanProfileCombo.SelectedItem is null)
                HomeFanProfileCombo.SelectedItem = MoreFanProfilesLabel;
            HomeFanProfileCombo.IsEnabled = _app.State.CanFanControl;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void HomeFanProfile_DropDownOpened(object sender, EventArgs e) => RefreshHomeFanProfiles();

    private async void HomeFanProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || HomeFanProfileCombo.SelectedItem is not string profile ||
            profile.Equals(MoreFanProfilesLabel, StringComparison.OrdinalIgnoreCase))
            return;

        HomeFanProfileCombo.IsEnabled = false;
        try
        {
            await _app.SetCoolingProfileAsync(profile);
        }
        finally
        {
            RefreshHomeFanProfiles();
        }
    }

    private async void HomeFanQuick_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not FrameworkElement { Tag: string profile })
            return;

        HomeFanQuickGrid.IsEnabled = false;
        HomeFanProfileCombo.IsEnabled = false;
        try { await _app.SetCoolingProfileAsync(profile); }
        finally { RefreshHomeFanProfiles(); }
    }

    private void HomeBattery_Click(object sender, MouseButtonEventArgs e) => Navigate("Battery");
}
