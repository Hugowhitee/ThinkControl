using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class CompactDashboard
{
    private readonly WindowsVolumeService _compactVolume = new();
    private bool _quickControlsAdded;
    private bool _syncingQuickControls;

    private void EnsureQuickControls()
    {
        if (_quickControlsAdded || _app is null)
            return;

        _quickControlsAdded = true;
        _syncingQuickControls = true;
        try
        {
            CompactPerformanceCombo.ItemsSource = new[] { "Efficiency", "Balanced", "Performance" };
            CompactFanCombo.ItemsSource = BuildFanOptions();
            CompactRefreshCombo.ItemsSource = BuildRefreshOptions();
            CompactKeyboardCombo.ItemsSource = new[] { "Off", "Low", "High", "Auto" };
        }
        finally
        {
            _syncingQuickControls = false;
        }
    }

    private IReadOnlyList<string> BuildFanOptions()
    {
        if (_app is null)
            return ["Auto"];
        var values = new List<string> { "Auto" };
        values.AddRange(_app.FanProfiles.GetProfiles().Select(profile => profile.Name));
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private IReadOnlyList<string> BuildRefreshOptions()
    {
        if (_app is null)
            return ["Auto"];
        var values = new List<string> { "Auto" };
        IReadOnlyList<int> supported = _app.DisplayService.GetSupportedRefreshRates();
        if (supported.Contains(60))
            values.Add("60 Hz");
        if (supported.Count > 0 || _app.State.MaxRefreshHz > 0)
            values.Add("Max");
        return values;
    }

    private void SyncQuickControls()
    {
        if (!_quickControlsAdded || _app is null)
            return;

        _syncingQuickControls = true;
        try
        {
            CompactPerformanceCombo.SelectedItem = App.PowerPreferenceDisplayName(_app.GetPowerPreference(onBattery: true));
            CompactFanCombo.IsEnabled = _app.State.CanFanControl;
            CompactFanCombo.ItemsSource = BuildFanOptions();
            CompactFanCombo.SelectedItem = DisplayFanName(_app.State.CoolingProfile);

            string? refresh = _app.State.RefreshAutoEnabled
                ? "Auto"
                : _app.State.MaxRefreshHz > 0 && _app.State.CurrentRefreshHz == _app.State.MaxRefreshHz
                    ? "Max"
                    : _app.State.CurrentRefreshHz == 60 ? "60 Hz" : null;
            CompactRefreshCombo.SelectedItem = refresh;

            CompactKeyboardCombo.IsEnabled = _app.State.CanKeyboardBacklight;
            CompactKeyboardCombo.SelectedItem = _app.State.KeyboardMode == "Auto"
                ? "Auto"
                : _app.State.KeyboardStatus.Contains("Off", StringComparison.OrdinalIgnoreCase) ? "Off"
                : _app.State.KeyboardStatus.Contains("Low", StringComparison.OrdinalIgnoreCase) ? "Low"
                : _app.State.KeyboardStatus.Contains("High", StringComparison.OrdinalIgnoreCase) ? "High"
                : null;
        }
        finally
        {
            _syncingQuickControls = false;
        }

        RefreshCompactVolume();
    }

    private static string DisplayFanName(string? raw) => raw?.Trim() switch
    {
        null or "" or "Lenovo Auto" or "Auto" => "Auto",
        "Silent" => "Quiet",
        "Normal" => "Balanced",
        "Cool" => "Max cooling",
        // A manual output is not Lenovo Auto. Returning the raw value intentionally
        // leaves the profile ComboBox with no matching selected item, so choosing
        // Auto is a real selection change that reaches the hardware handoff path.
        string value when value.StartsWith("Manual ", StringComparison.OrdinalIgnoreCase) => value,
        string value => value
    };

    private void CompactPerformance_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingQuickControls || _app is null || CompactPerformanceCombo.SelectedItem is not string raw)
            return;

        ThinkControlPowerMode mode = raw switch
        {
            "Efficiency" => ThinkControlPowerMode.Quiet,
            "Performance" => ThinkControlPowerMode.Performance,
            _ => ThinkControlPowerMode.Balanced
        };

        // Compact intentionally exposes one quick power selector. Make that one
        // selector unambiguous: it is the battery preference. AC remains independently
        // configurable on the full Performance page instead of silently changing too.
        if (!_app.SetPowerPreference(mode, onBattery: true))
            SyncQuickControls();
    }

    private async void CompactFan_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingQuickControls || _app is null || CompactFanCombo.SelectedItem is not string raw)
            return;
        CompactFanCombo.IsEnabled = false;
        try { await _app.SetCoolingProfileAsync(raw); }
        finally { SyncQuickControls(); }
    }

    private void CompactRefresh_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingQuickControls || _app is null || CompactRefreshCombo.SelectedItem is not string raw)
            return;
        if (raw == "Auto") _app.EnableRefreshAuto();
        else if (raw == "60 Hz") _app.SetRefresh(60);
        else if (raw == "Max")
        {
            int max = _app.State.MaxRefreshHz;
            if (max <= 0) max = _app.DisplayService.GetSupportedRefreshRates().DefaultIfEmpty(0).Max();
            if (max > 0) _app.SetRefresh(max);
        }
    }

    private async void CompactKeyboard_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingQuickControls || _app is null || CompactKeyboardCombo.SelectedItem is not string raw)
            return;
        CompactKeyboardCombo.IsEnabled = false;
        try
        {
            if (raw == "Auto") await _app.SetKeyboardModeAsync("Auto");
            else await _app.SetKeyboardStaticLevelAsync(raw);
        }
        finally { SyncQuickControls(); }
    }

    private void CompactBrightness_Commit(object sender, MouseButtonEventArgs e) => CommitCompactBrightness();

    private void CompactBrightness_KeyCommit(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
            CommitCompactBrightness();
    }

    private void CommitCompactBrightness()
    {
        if (_app is not null)
            _app.SetBrightness((int)Math.Round(CompactBrightnessSlider.Value));
    }

    private void CompactVolume_Commit(object sender, MouseButtonEventArgs e) => CommitCompactVolume();

    private void CompactVolume_KeyCommit(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
            CommitCompactVolume();
    }

    private void CommitCompactVolume()
    {
        if (_compactVolume.Set((int)Math.Round(CompactVolumeSlider.Value), out int applied))
        {
            CompactVolumeSlider.Value = applied;
            CompactVolumeText.Text = $"{applied}%";
        }
    }

    private void RefreshCompactVolume()
    {
        WindowsVolumeStatus status = _compactVolume.Read();
        CompactVolumeSlider.IsEnabled = status.Available;
        if (!status.Available)
        {
            CompactVolumeText.Text = "—";
            return;
        }
        CompactVolumeSlider.Value = status.Percent;
        CompactVolumeText.Text = status.Muted ? "Muted" : $"{status.Percent}%";
    }
}