using System.Windows.Controls;
using System.Windows.Input;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _sliderCommitConfigured;

    private void ConfigureSliderCommitBehavior()
    {
        if (_sliderCommitConfigured)
            return;
        _sliderCommitConfigured = true;

        foreach (string name in new[] { "HomeBrightnessSlider", "DisplayBrightnessSlider" })
        {
            if (FindName(name) is not Slider slider)
                continue;
            slider.PreviewMouseLeftButtonUp += BrightnessSlider_Commit;
            slider.PreviewKeyUp += BrightnessSlider_KeyCommit;
        }
    }

    private void BrightnessSlider_Commit(object sender, MouseButtonEventArgs e)
    {
        if (_syncing || sender is not Slider slider)
            return;

        if (!_app.SetBrightness((int)Math.Round(slider.Value)))
            SyncControls();
    }

    private void BrightnessSlider_KeyCommit(object sender, KeyEventArgs e)
    {
        if (_syncing || sender is not Slider slider ||
            e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown))
        {
            return;
        }

        if (!_app.SetBrightness((int)Math.Round(slider.Value)))
            SyncControls();
    }
}
