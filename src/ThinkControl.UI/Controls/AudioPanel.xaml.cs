using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class AudioPanel : UserControl
{
    private readonly WindowsVolumeService _volume = new();
    private readonly DolbyDirectControlService _directDolby = new();
    private readonly DispatcherTimer _volumeApplyTimer;
    private readonly DispatcherTimer _volumeRefreshTimer;
    private App? _app;
    private DolbyAudioService? _dolby;
    private bool _syncing;
    private bool _volumeDragging;
    private DolbyAudioStatus? _status;
    private DolbyDirectState? _directState;

    public AudioPanel()
    {
        InitializeComponent();

        _volumeApplyTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(45)
        };
        _volumeApplyTimer.Tick += (_, _) =>
        {
            _volumeApplyTimer.Stop();
            ApplyVolumeSlider();
        };

        _volumeRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _volumeRefreshTimer.Tick += (_, _) =>
        {
            if (!_volumeDragging)
                RefreshVolume();
        };

        Loaded += (_, _) =>
        {
            RefreshVolume();
            _volumeRefreshTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            _volumeApplyTimer.Stop();
            _volumeRefreshTimer.Stop();
        };
    }

    internal void Initialize(App app)
    {
        _app = app;
        _dolby ??= new DolbyAudioService();
        RefreshVolume();
        RefreshStatus();
    }

    internal void RefreshStatus()
    {
        if (_app is null || _dolby is null)
            return;

        _status = _dolby.Probe();
        _directState = _directDolby.Probe();

        BackendStatusText.Text = _directState.Available
            ? _directState.Detail
            : _status.Detail;
        InstallButton.Visibility = _status.DolbyAccessInstalled || _status.DaxBackendDetected
            ? Visibility.Collapsed
            : Visibility.Visible;
        OpenButton.IsEnabled = _status.DolbyAccessInstalled;

        SetProfilesEnabled(_directState.CanProfileControl);
        SetToneEnabled(_directState.CanToneControl);
        SubprofileStatusText.Text = _directState.CanToneControl ? "Direct DAX" : "Unavailable";

        string profile = NormalizeKnownProfile(_directState.ActiveProfile) ??
                         NormalizeKnownProfile(_status.ActiveProfile) ??
                         _app.UserSettings.Current.DolbyProfile;
        string tone = NormalizeKnownTone(_directState.ActiveTone) ??
                      NormalizeKnownTone(_app.UserSettings.Current.DolbySubProfile) ??
                      "Balanced";

        _syncing = true;
        try
        {
            DynamicProfile.IsChecked = profile == "Dynamic";
            MovieProfile.IsChecked = profile == "Movie";
            MusicProfile.IsChecked = profile == "Music";
            GameProfile.IsChecked = profile == "Game";
            VoiceProfile.IsChecked = profile == "Voice";

            BalancedTone.IsChecked = tone == "Balanced";
            DetailedTone.IsChecked = tone == "Detailed";
            WarmTone.IsChecked = tone == "Warm";
            OffTone.IsChecked = tone == "Off";
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RefreshVolume()
    {
        WindowsVolumeStatus status = _volume.Read();
        _syncing = true;
        try
        {
            VolumeSlider.IsEnabled = status.Available;
            MuteButton.IsEnabled = status.Available;
            VolumeDeviceText.Text = status.Detail;
            if (!status.Available)
            {
                VolumeValueText.Text = "—";
                MuteButton.Content = "Mute";
                return;
            }

            VolumeSlider.Value = status.Percent;
            VolumeValueText.Text = status.Muted ? $"{status.Percent}% · muted" : $"{status.Percent}%";
            MuteButton.Content = status.Muted ? "Unmute" : "Mute";
            MuteButton.Tag = status.Muted;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !IsLoaded)
            return;

        int percent = (int)Math.Round(e.NewValue);
        VolumeValueText.Text = $"{percent}%";
        _volumeDragging = true;
        _volumeApplyTimer.Stop();
        _volumeApplyTimer.Start();
    }

    private void VolumeSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _volumeApplyTimer.Stop();
        ApplyVolumeSlider();
        _volumeDragging = false;
        RefreshVolume();
    }

    private void ApplyVolumeSlider()
    {
        if (_syncing || !VolumeSlider.IsEnabled)
            return;

        int requested = (int)Math.Round(VolumeSlider.Value);
        if (_volume.Set(requested, out int applied))
            VolumeValueText.Text = $"{applied}%";
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        WindowsVolumeStatus current = _volume.Read();
        if (!current.Available)
        {
            RefreshVolume();
            return;
        }

        _volume.SetMuted(!current.Muted);
        RefreshVolume();
    }

    private async void Profile_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _app is null || sender is not FrameworkElement { Tag: string profile })
            return;

        ActionStatusText.Text = $"Switching to {profile}…";
        SetProfilesEnabled(false);
        DolbyProfileResult result = await _directDolby.SetProfileAsync(profile);
        ActionStatusText.Text = result.Detail;

        if (result.Success)
        {
            _app.UserSettings.Update(settings => settings with { DolbyProfile = profile });
        }
        else
        {
            ActionStatusText.Text += " · Dolby Access was not opened. Use the explicit button if you want to change it there.";
        }

        RefreshStatus();
    }

    private async void Tone_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _app is null || sender is not FrameworkElement { Tag: string tone })
            return;

        ActionStatusText.Text = $"Applying {tone} tone…";
        SetToneEnabled(false);
        DolbyProfileResult result = await _directDolby.SetToneAsync(tone);
        ActionStatusText.Text = result.Detail;

        if (result.Success)
            _app.UserSettings.Update(settings => settings with { DolbySubProfile = tone });
        else
            ActionStatusText.Text += " · Dolby Access was not opened.";

        RefreshStatus();
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not Button button)
            return;

        button.IsEnabled = false;
        try
        {
            DolbyProfileResult profile = await _directDolby.SetProfileAsync("Dynamic");
            DolbyProfileResult tone = await _directDolby.SetToneAsync("Balanced");
            if (profile.Success || tone.Success)
            {
                _app.UserSettings.Update(settings => settings with
                {
                    DolbyProfile = profile.Success ? "Dynamic" : settings.DolbyProfile,
                    DolbySubProfile = tone.Success ? "Balanced" : settings.DolbySubProfile
                });
            }

            ActionStatusText.Text = profile.Success || tone.Success
                ? "Audio processing reset toward Dynamic · Balanced. Windows output volume was left unchanged."
                : "Direct Dolby reset is unavailable on this driver. Windows output volume was left unchanged.";
            RefreshStatus();
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void Install_Click(object sender, RoutedEventArgs e) => DolbyAudioService.OpenStore();

    private void Open_Click(object sender, RoutedEventArgs e) => _dolby?.OpenDolbyAccess();

    private void SetProfilesEnabled(bool enabled)
    {
        DynamicProfile.IsEnabled = MovieProfile.IsEnabled = MusicProfile.IsEnabled =
            GameProfile.IsEnabled = VoiceProfile.IsEnabled = enabled;
    }

    private void SetToneEnabled(bool enabled)
    {
        BalancedTone.IsEnabled = DetailedTone.IsEnabled = WarmTone.IsEnabled = OffTone.IsEnabled = enabled;
    }

    private static string? NormalizeKnownProfile(string? value) =>
        DolbyDirectControlService.Profiles.FirstOrDefault(profile =>
            !string.IsNullOrWhiteSpace(value) &&
            (value.Contains(profile, StringComparison.OrdinalIgnoreCase) || profile.Contains(value, StringComparison.OrdinalIgnoreCase)));

    private static string? NormalizeKnownTone(string? value) =>
        DolbyDirectControlService.TonePresets.FirstOrDefault(tone =>
            !string.IsNullOrWhiteSpace(value) &&
            (value.Contains(tone, StringComparison.OrdinalIgnoreCase) || tone.Contains(value, StringComparison.OrdinalIgnoreCase)));
}
