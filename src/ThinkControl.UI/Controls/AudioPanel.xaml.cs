using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class AudioPanel : UserControl
{
    private readonly WindowsVolumeService _volume = new();
    private readonly DolbyDirectControlService _directDolby = new();
    private readonly DolbyAccessProfileBridge _fusionDolby = new();
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
                ProfileGrid.Visibility = Visibility.Visible;
                FusionControlCard.Visibility = Visibility.Collapsed;
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

                BackendStatusText.Text = "Dolby direct controls are not exposed by this driver. ThinkControl does not invent profile mappings.";
                InstallButton.Visibility = Visibility.Visible;
                OpenButton.IsEnabled = true;
                ProfileGrid.Visibility = Visibility.Collapsed;
                FusionControlCard.Visibility = Visibility.Collapsed;
                SetProfilesEnabled(false);
                UpdateToneSection("Dynamic", directToneAvailable: false);
                ActionStatusText.Text = "Use Dolby Access when the OEM profile API is not exposed.";
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    internal void PrepareFusionForSnapshot()
    {
        _snapshotMode = true;
        _volumeApplyTimer.Stop();
        _volumeRefreshTimer.Stop();
        _status = new DolbyAudioStatus(
            DolbyAccessInstalled: true,
            DaxBackendDetected: false,
            Detail: "Dolby Fusion is active. Profile changes use the installed Dolby Access controls on demand.",
            FusionBackendDetected: true);
        _directState = new DolbyDirectState(
            Available: false,
            CanProfileControl: false,
            CanToneControl: false,
            ActiveProfile: null,
            ActiveTone: null,
            Detail: "Legacy DAX direct API not exposed on this Fusion generation");

        _syncing = true;
        try
        {
            VolumeSlider.IsEnabled = true;
            VolumeSlider.Value = 58;
            VolumeValueText.Text = "58%";
            VolumeDeviceText.Text = "Speakers · default Windows output";
            MuteButton.IsEnabled = true;
            MuteButton.Content = "Mute";
            MuteButton.Tag = false;

            BackendStatusText.Text = _status.Detail;
            InstallButton.Visibility = Visibility.Collapsed;
            OpenButton.IsEnabled = true;
            ProfileGrid.Visibility = Visibility.Visible;
            FusionControlCard.Visibility = Visibility.Visible;
            SetProfilesEnabled(true);

            DynamicProfile.IsChecked = true;
            MovieProfile.IsChecked = false;
            MusicProfile.IsChecked = false;
            GameProfile.IsChecked = false;
            VoiceProfile.IsChecked = false;
            UpdateToneSection("Dynamic", directToneAvailable: false);
            ActionStatusText.Text = "Fusion profile controls are ready on demand; no Dolby UI work runs in the background.";
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

        bool directProfiles = _directState.CanProfileControl;
        bool fusionBridge = !directProfiles && _status.FusionBackendDetected;
        bool canSelectProfiles = directProfiles || fusionBridge;

        BackendStatusText.Text = _directState.Available && (directProfiles || _directState.CanToneControl)
            ? _directState.Detail
            : _status.Detail;

        // OEM Fusion presence proves the Lenovo Dolby stack exists, but not that one
        // per-user package registry view sees Dolby Access. The bridge therefore
        // tries the canonical AUMID only after an explicit profile click.
        InstallButton.Visibility = _status.DolbyAccessInstalled ? Visibility.Collapsed : Visibility.Visible;
        OpenButton.IsEnabled = _status.DolbyAccessInstalled || _status.OemBackendDetected;
        ProfileGrid.Visibility = canSelectProfiles ? Visibility.Visible : Visibility.Collapsed;
        FusionControlCard.Visibility = fusionBridge ? Visibility.Visible : Visibility.Collapsed;

        string profile = NormalizeKnownProfile(_directState.ActiveProfile) ??
                         _app.UserSettings.Current.DolbyProfile;
        string tone = NormalizeKnownTone(_directState.ActiveTone) ??
                      NormalizeKnownTone(_app.UserSettings.Current.DolbySubProfile) ??
                      "Balanced";

        SetProfilesEnabled(canSelectProfiles);
        UpdateToneSection(profile, _directState.CanToneControl && directProfiles);

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

        if (fusionBridge && string.IsNullOrWhiteSpace(ActionStatusText.Text))
            ActionStatusText.Text = "Fusion profile controls are ready on demand; no Dolby UI work runs in the background.";
    }

    private void UpdateToneSection(string profile, bool directToneAvailable)
    {
        bool music = string.Equals(profile, "Music", StringComparison.OrdinalIgnoreCase);
        SubprofileCard.Visibility = music && directToneAvailable ? Visibility.Visible : Visibility.Collapsed;
        SetToneEnabled(music && directToneAvailable);
        SubprofileStatusText.Text = directToneAvailable
            ? "Direct DAX · Music"
            : _status?.FusionBackendDetected == true
                ? "Dolby Fusion · Dolby Access"
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
        if (_snapshotMode || _syncing || _app is null || _dolby is null ||
            sender is not FrameworkElement { Tag: string profile })
        {
            return;
        }

        ActionStatusText.Text = $"Switching to {profile}…";
        SetProfilesEnabled(false);

        DolbyProfileResult result;
        if (_directState?.CanProfileControl == true)
        {
            result = await _directDolby.SetProfileAsync(profile);
        }
        else if (_status?.FusionBackendDetected == true)
        {
            result = await _fusionDolby.SetProfileAsync(profile, _dolby);
        }
        else
        {
            result = new DolbyProfileResult(false, "This Dolby driver does not expose a supported profile-control path.");
        }

        if (result.Success)
            _app.UserSettings.Update(settings => settings with { DolbyProfile = profile });

        RefreshStatus();
        ActionStatusText.Text = result.Success
            ? result.Detail
            : result.Detail + " · Audio was left unchanged.";
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

        if (result.Success)
            _app.UserSettings.Update(settings => settings with { DolbySubProfile = tone });

        RefreshStatus();
        ActionStatusText.Text = result.Success
            ? result.Detail
            : result.Detail + " · Direct tone control was not accepted.";
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshotMode || _app is null || _dolby is null || sender is not Button button)
            return;

        button.IsEnabled = false;
        try
        {
            DolbyProfileResult profile;
            if (_directState?.CanProfileControl == true)
            {
                profile = await _directDolby.SetProfileAsync("Dynamic");
            }
            else if (_status?.FusionBackendDetected == true)
            {
                profile = await _fusionDolby.SetProfileAsync("Dynamic", _dolby);
            }
            else
            {
                profile = new DolbyProfileResult(false, "Direct Dolby reset is unavailable on this driver.");
            }

            if (profile.Success)
            {
                _app.UserSettings.Update(settings => settings with
                {
                    DolbyProfile = "Dynamic",
                    DolbySubProfile = "Balanced"
                });
                RefreshStatus();
                ActionStatusText.Text = "Audio processing reset to Dynamic. Windows output volume was left unchanged.";
            }
            else
            {
                RefreshStatus();
                ActionStatusText.Text = profile.Detail + " Windows output volume was left unchanged.";
            }
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
        if (_snapshotMode || _dolby is null)
            return;

        DolbyLaunchResult launch = _dolby.OpenDolbyAccessWithResult();
        ActionStatusText.Text = launch.Detail;
        if (!launch.Success)
            InstallButton.Visibility = Visibility.Visible;
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
