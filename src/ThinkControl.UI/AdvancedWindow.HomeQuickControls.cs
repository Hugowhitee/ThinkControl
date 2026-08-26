using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
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
            var values = new List<string> { "Auto" };
            values.AddRange(_app.FanProfiles.GetProfiles().Select(profile => profile.Name));
            HomeFanProfileCombo.ItemsSource = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            HomeFanProfileCombo.SelectedItem = selected;
            if (HomeFanProfileCombo.SelectedItem is null)
                HomeFanProfileCombo.SelectedItem = "Auto";
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
        if (_syncing || HomeFanProfileCombo.SelectedItem is not string profile)
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

    private void HomeBattery_Click(object sender, MouseButtonEventArgs e) => Navigate("Battery");
}
