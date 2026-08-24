using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class AudioPanel : UserControl
{
    private App? _app;
    private DolbyAudioService? _dolby;
    private bool _syncing;
    private DolbyAudioStatus? _status;

    public AudioPanel()
    {
        InitializeComponent();
    }

    internal void Initialize(App app)
    {
        _app = app;
        _dolby ??= new DolbyAudioService();
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

        // Main profiles are available when either the direct DAX backend or the
        // safe Dolby Access fallback can apply them. Subprofiles are direct-only.
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

        // Game subprofiles have meaning only under the Game profile. Select Game
        // first through the same verified direct/fallback profile path.
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
