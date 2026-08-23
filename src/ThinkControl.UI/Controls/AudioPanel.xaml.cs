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

    private void RefreshStatus()
    {
        if (_app is null || _dolby is null)
            return;

        _status = _dolby.Probe();
        BackendStatusText.Text = _status.Detail;
        InstallButton.Visibility = _status.DolbyAccessInstalled ? Visibility.Collapsed : Visibility.Visible;
        OpenButton.IsEnabled = _status.DolbyAccessInstalled;

        bool enabled = _status.DolbyAccessInstalled;
        DynamicProfile.IsEnabled = MovieProfile.IsEnabled = MusicProfile.IsEnabled = GameProfile.IsEnabled = VoiceProfile.IsEnabled = enabled;

        string profile = _app.UserSettings.Current.DolbyProfile;
        _syncing = true;
        try
        {
            DynamicProfile.IsChecked = profile == "Dynamic";
            MovieProfile.IsChecked = profile == "Movie";
            MusicProfile.IsChecked = profile == "Music";
            GameProfile.IsChecked = profile == "Game";
            VoiceProfile.IsChecked = profile == "Voice";
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
        if (_status?.DolbyAccessInstalled != true)
        {
            MessageBoxResult answer = MessageBox.Show(
                "Dolby Access is required to select the official Dolby Atmos profiles. Open its Microsoft Store page now?",
                "ThinkControl · Dolby Access",
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
        SetProfilesEnabled(true);
        ActionStatusText.Text = result.Detail;

        if (result.Success)
        {
            _app.UserSettings.Update(settings => settings with { DolbyProfile = profile });
            RefreshStatus();
        }
        else
        {
            RefreshStatus();
        }
    }

    private void Install_Click(object sender, RoutedEventArgs e) => DolbyAudioService.OpenStore();

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        _dolby?.OpenDolbyAccess();
    }

    private void SetProfilesEnabled(bool enabled)
    {
        bool available = enabled && _status?.DolbyAccessInstalled == true;
        DynamicProfile.IsEnabled = MovieProfile.IsEnabled = MusicProfile.IsEnabled = GameProfile.IsEnabled = VoiceProfile.IsEnabled = available;
    }
}
