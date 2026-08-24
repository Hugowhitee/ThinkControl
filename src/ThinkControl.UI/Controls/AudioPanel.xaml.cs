using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class AudioPanel : UserControl
{
    private readonly WindowsVolumeService _volume = new();
    private readonly DispatcherTimer _volumeApplyTimer;
    private readonly DispatcherTimer _volumeRefreshTimer;
    private App? _app;
    private DolbyAudioService? _dolby;
    private bool _syncing;
    private bool _volumeDragging;
    private DolbyAudioStatus? _status;

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
        BackendStatusText.Text = _status.Detail;
        InstallButton.Visibility = _status.DolbyAccessInstalled || _status.DaxBackendDetected
            ? Visibility.Collapsed
            : Visibility.Visible;
        OpenButton.IsEnabled = _status.DolbyAccessInstalled;

        bool profileAvailable = _status.DirectApiAvailable || _status.DolbyAccessInstalled;
        DynamicProfile.IsEnabled = MovieProfile.IsEnabled = MusicProfile.IsEnabled =
            GameProfile.IsEnabled = VoiceProfile.IsEnabled = profileAvailable;

        bool subprofilesAvailable = _status.DirectApiAvailable;
        SetSubprofilesEnabled(subprofilesAvailable);
        SubprofileStatusText.Text = subprofilesAvailable ? "Direct DAX" : "Unavailable";

        string profile = NormalizeKnownProfile(_status.ActiveProfile) ?? _app.UserSettings.Current.DolbyProfile;
        string subprofile = NormalizeKnownSubProfile(_status.ActiveSubProfile) ?? _app.UserSettings.Current.DolbySubProfile;

        _syncing = true;
        try
        {
            DynamicProfile.IsChecked = profile == "Dynamic";
            MovieProfile.IsChecked = profile == "Movie";
            MusicProfile.IsChecked = profile == "Music";
            GameProfile.IsChecked = profile == "Game";
            VoiceProfile.IsChecked = profile == "Voice";

            FpsSubprofile.IsChecked = subprofile == "FPS";
            RacingSubprofile.IsChecked = subprofile == "Racing";
            RtsSubprofile.IsChecked = subprofile == "RTS";
            RpgSubprofile.IsChecked = subprofile == "RPG";
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
        if (_syncing || _app is null || _dolby is null || sender is not FrameworkElement { Tag: string profile })
            return;

        RefreshStatus();
        if (_status?.DirectApiAvailable != true && _status?.DolbyAccessInstalled != true)
        {
            MessageBoxResult answer = MessageBox.Show(
                "A compatible Dolby DAX backend or Dolby Access is required to switch profiles. Open the Microsoft Store page for Dolby Access?",
                "ThinkControl · Dolby Audio",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (answer == MessageBoxResult.Yes)
                DolbyAudioService.OpenStore();
            RefreshStatus();
            return;
        }

        ActionStatusText.Text = $"Switching to {profile}…";
        SetProfilesEnabled(false);
        DolbyProfileResult result = await _dolby.SetProfileAsync(profile);
        ActionStatusText.Text = result.Detail;

        if (result.Success)
        {
            _app.UserSettings.Update(settings => settings with
            {
                DolbyProfile = profile,
                DolbySubProfile = profile == "Game" ? settings.DolbySubProfile : string.Empty
            });
        }

        RefreshStatus();
    }

    private async void Subprofile_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || _app is null || _dolby is null || sender is not FrameworkElement { Tag: string subProfile })
            return;

        RefreshStatus();
        if (_status?.DirectApiAvailable != true)
        {
            ActionStatusText.Text = "This Dolby driver does not expose direct subprofile control, so ThinkControl left Dolby untouched.";
            RefreshStatus();
            return;
        }

        string activeProfile = NormalizeKnownProfile(_status.ActiveProfile) ?? _app.UserSettings.Current.DolbyProfile;
        if (!string.Equals(activeProfile, "Game", StringComparison.OrdinalIgnoreCase))
        {
            DolbyProfileResult game = await _dolby.SetProfileAsync("Game");
            if (!game.Success)
            {
                ActionStatusText.Text = game.Detail;
                RefreshStatus();
                return;
            }
        }

        SetSubprofilesEnabled(false);
        ActionStatusText.Text = $"Switching Game tuning to {subProfile}…";
        DolbyProfileResult result = await _dolby.SetSubProfileAsync(subProfile);
        ActionStatusText.Text = result.Detail;

        if (result.Success)
        {
            _app.UserSettings.Update(settings => settings with
            {
                DolbyProfile = "Game",
                DolbySubProfile = subProfile
            });
        }

        RefreshStatus();
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not Button button)
            return;

        button.IsEnabled = false;
        try
        {
            await _app.ResetAudioDefaultsAsync();
            ActionStatusText.Text = "Audio processing preferences reset to Dynamic. Windows output volume was left unchanged.";
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
        bool available = enabled && (_status?.DirectApiAvailable == true || _status?.DolbyAccessInstalled == true);
        DynamicProfile.IsEnabled = MovieProfile.IsEnabled = MusicProfile.IsEnabled =
            GameProfile.IsEnabled = VoiceProfile.IsEnabled = available;
    }

    private void SetSubprofilesEnabled(bool enabled)
    {
        FpsSubprofile.IsEnabled = RacingSubprofile.IsEnabled = RtsSubprofile.IsEnabled = RpgSubprofile.IsEnabled = enabled;
    }

    private static string? NormalizeKnownProfile(string? value) =>
        DolbyAudioService.OfficialProfiles.FirstOrDefault(profile =>
            !string.IsNullOrWhiteSpace(value) &&
            (value.Contains(profile, StringComparison.OrdinalIgnoreCase) || profile.Contains(value, StringComparison.OrdinalIgnoreCase)));

    private static string? NormalizeKnownSubProfile(string? value) =>
        DolbyAudioService.GameSubProfiles.FirstOrDefault(profile =>
            !string.IsNullOrWhiteSpace(value) &&
            (value.Contains(profile, StringComparison.OrdinalIgnoreCase) || profile.Contains(value, StringComparison.OrdinalIgnoreCase)));
}
