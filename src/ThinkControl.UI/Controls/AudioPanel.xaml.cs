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
    private bool _snapshotMode;
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

        _volumeRefreshTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _volumeRefreshTimer.Tick += (_, _) =>
        {
            if (IsVisible && !_volumeDragging && !_snapshotMode)
                RefreshVolume();
        };

        Loaded += (_, _) => UpdateLivePolling(refreshNow: true);
        IsVisibleChanged += (_, e) => UpdateLivePolling(refreshNow: e.NewValue is true);
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
        if (_snapshotMode)
            return;
        UpdateLivePolling(refreshNow: IsVisible);
    }

    private void UpdateLivePolling(bool refreshNow)
    {
        if (_snapshotMode || !IsLoaded || !IsVisible)
        {
            _volumeApplyTimer.Stop();
            _volumeRefreshTimer.Stop();
            return;
        }

        if (refreshNow)
        {
            RefreshVolume();
            RefreshStatus();
        }
        if (!_volumeRefreshTimer.IsEnabled)
            _volumeRefreshTimer.Start();
    }

    internal void PrepareForSnapshot(bool providersAvailable)
    {
        _snapshotMode = true;
        _volumeApplyTimer.Stop();
        _volumeRefreshTimer.Stop();
        _syncing = true;
        try
        {
            if (providersAvailable)
            {
                VolumeSlider.IsEnabled = true;
                VolumeSlider.Value = 58;
                VolumeValueText.Text = "58%";
                VolumeDeviceText.Text = "Speakers · default Windows output";
                MuteButton.IsEnabled = true;
                MuteButton.Content = "Mute";
                MuteButton.Tag = false;

                BackendStatusText.Text = "Dolby DAX direct control detected · semantic profile and Music IEQ readback available";
                InstallButton.Visibility = Visibility.Collapsed;
                OpenButton.IsEnabled = true;
                SetProfilesEnabled(true);

                DynamicProfile.IsChecked = false;
                MovieProfile.IsChecked = false;
                MusicProfile.IsChecked = true;
                GameProfile.IsChecked = false;
                VoiceProfile.IsChecked = false;
                UpdateToneSection("Music", directToneAvailable: true);
                BalancedTone.IsChecked = true;
                DetailedTone.IsChecked = false;
                WarmTone.IsChecked = false;
                OffTone.IsChecked = false;
                ActionStatusText.Text = "Direct DAX · changes stay inside ThinkControl";
            }
            else
            {
                VolumeSlider.IsEnabled = false;
                VolumeSlider.Value = 0;
                VolumeValueText.Text = "—";
                VolumeDeviceText.Text = "Windows audio endpoint unavailable";
                MuteButton.IsEnabled = false;
                MuteButton.Content = "Mute";

                BackendStatusText.Text = "Dolby direct controls are not exposed by this driver. ThinkControl will not invent profile mappings.";
                InstallButton.Visibility = Visibility.Visible;
                OpenButton.IsEnabled = false;
                SetProfilesEnabled(false);
                UpdateToneSection("Dynamic", directToneAvailable: false);
                DynamicProfile.IsChecked = true;
                MovieProfile.IsChecked = MusicProfile.IsChecked = GameProfile.IsChecked = VoiceProfile.IsChecked = false;
                ActionStatusText.Text = "Unavailable controls stay visible but disabled.";
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    internal void RefreshStatus()
    {
        if (_snapshotMode || _app is null || _dolby is null || !IsVisible)
            return;

        _status = _dolby.Probe();
        _directState = _directDolby.Probe();

        BackendStatusText.Text = _directState.Available
            ? _directState.Detail
            : _status.Detail;
        InstallButton.Visibility = _status.DolbyAccessInstalled || _status.OemBackendDetected
            ? Visibility.Collapsed
            : Visibility.Visible;
        OpenButton.IsEnabled = _status.DolbyAccessInstalled;

        string profile = NormalizeKnownProfile(_directState.ActiveProfile) ??
                         _app.UserSettings.Current.DolbyProfile;
        string tone = NormalizeKnownTone(_directState.ActiveTone) ??
                      NormalizeKnownTone(_app.UserSettings.Current.DolbySubProfile) ??
                      "Balanced";

        SetProfilesEnabled(_directState.CanProfileControl);
        UpdateToneSection(profile, _directState.CanToneControl);

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

    private void UpdateToneSection(string profile, bool directToneAvailable)
    {
        bool music = string.Equals(profile, "Music", StringComparison.OrdinalIgnoreCase);
        SubprofileCard.Visibility = music ? Visibility.Visible : Visibility.Collapsed;
        SetToneEnabled(music && directToneAvailable);
        SubprofileStatusText.Text = directToneAvailable
            ? "Direct DAX · Music"
            : _status?.FusionBackendDetected == true
                ? "Dolby Fusion · use Dolby Access"
                : "Not exposed by this Dolby build";
    }

    private void RefreshVolume()
    {
        if (_snapshotMode || !IsVisible)
            return;

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
        if (_snapshotMode || _syncing || !IsLoaded || !IsVisible)
            return;

        int percent = (int)Math.Round(e.NewValue);
        VolumeValueText.Text = $"{percent}%";
        _volumeDragging = true;
        _volumeApplyTimer.Stop();
        _volumeApplyTimer.Start();
    }

    private void VolumeSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_snapshotMode)
            return;
        _volumeApplyTimer.Stop();
        ApplyVolumeSlider();
        _volumeDragging = false;
        RefreshVolume();
    }

    private void ApplyVolumeSlider()
    {
        if (_snapshotMode || _syncing || !VolumeSlider.IsEnabled || !IsVisible)
            return;

        int requested = (int)Math.Round(VolumeSlider.Value);
        if (_volume.Set(requested, out int applied))
            VolumeValueText.Text = $"{applied}%";
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshotMode)
            return;
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
        if (_snapshotMode || _syncing || _app is null || sender is not FrameworkElement { Tag: string profile })
            return;

        ActionStatusText.Text = $"Switching to {profile}…";
        SetProfilesEnabled(false);
        DolbyProfileResult result = await _directDolby.SetProfileAsync(profile);
        ActionStatusText.Text = result.Detail;

        if (result.Success)
            _app.UserSettings.Update(settings => settings with { DolbyProfile = profile });
        else
            ActionStatusText.Text += " · Dolby Access was not opened. Use the explicit button if you want to change it there.";

        RefreshStatus();
    }

    private async void Tone_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshotMode || _syncing || _app is null || MusicProfile.IsChecked != true ||
            sender is not FrameworkElement { Tag: string tone })
        {
            return;
        }

        ActionStatusText.Text = $"Applying {tone} to Music…";
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
        if (_snapshotMode || _app is null || sender is not Button button)
            return;

        button.IsEnabled = false;
        try
        {
            DolbyProfileResult profile = await _directDolby.SetProfileAsync("Dynamic");
            if (profile.Success)
            {
                _app.UserSettings.Update(settings => settings with
                {
                    DolbyProfile = "Dynamic",
                    DolbySubProfile = "Balanced"
                });
                ActionStatusText.Text = "Audio processing reset to Dynamic. Music IEQ default is Balanced; Windows output volume was left unchanged.";
            }
            else
            {
                ActionStatusText.Text = _status?.FusionBackendDetected == true
                    ? "This Lenovo audio stack uses Dolby Fusion. Use Dolby Access for Atmos profile reset; Windows output volume was left unchanged."
                    : "Direct Dolby reset is unavailable on this driver. Windows output volume was left unchanged.";
            }
            RefreshStatus();
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        if (!_snapshotMode)
            DolbyAudioService.OpenStore();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (!_snapshotMode)
            _dolby?.OpenDolbyAccess();
    }

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
