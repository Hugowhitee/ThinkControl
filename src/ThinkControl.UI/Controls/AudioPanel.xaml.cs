using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class AudioPanel : UserControl
{
    private readonly WindowsVolumeService _volume = new();
    private readonly DolbyDirectControlService _directDolby = new();
    private readonly DolbyAccessProfileBridge _accessDolby = new();
    private readonly DispatcherTimer _volumeRefreshTimer;
    private readonly DispatcherTimer _volumeAutomationCommitTimer;
    private readonly DispatcherTimer _microphoneAutomationCommitTimer;
    private App? _app;
    private DolbyAudioService? _dolby;
    private bool _syncing;
    private bool _volumeDragging;
    private bool _microphoneDragging;
    private int? _volumeInteractionStartPercent;
    private int? _microphoneInteractionStartPercent;
    private bool _snapshotMode;
    private int _statusProbeGeneration;
    private int _volumeProbeGeneration;
    private int _volumeProbeRunning;
    private WindowsVolumeStatus? _cachedOutput;
    private WindowsVolumeStatus? _cachedInput;
    private DolbyAudioStatus? _status;
    private DolbyDirectState? _directState;

    public AudioPanel()
    {
        InitializeComponent();

        _volumeRefreshTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _volumeRefreshTimer.Tick += (_, _) =>
        {
            if (IsVisible &&
                !_volumeDragging &&
                !_microphoneDragging &&
                !_volumeAutomationCommitTimer.IsEnabled &&
                !_microphoneAutomationCommitTimer.IsEnabled &&
                !_snapshotMode)
            {
                QueueVolumeRefresh(applyCacheFirst: false);
            }
        };

        _volumeAutomationCommitTimer = CreateAutomationCommitTimer(ApplyVolumeSlider);
        _microphoneAutomationCommitTimer = CreateAutomationCommitTimer(ApplyMicrophoneSlider);

        VolumeSlider.LostKeyboardFocus += VolumeSlider_LostKeyboardFocus;
        MicrophoneSlider.LostKeyboardFocus += MicrophoneSlider_LostKeyboardFocus;

        Loaded += (_, _) => UpdateLivePolling(refreshNow: true);
        IsVisibleChanged += (_, e) => UpdateLivePolling(refreshNow: e.NewValue is true);
        Unloaded += (_, _) =>
        {
            Interlocked.Increment(ref _statusProbeGeneration);
            Interlocked.Increment(ref _volumeProbeGeneration);
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
            Interlocked.Increment(ref _statusProbeGeneration);
            Interlocked.Increment(ref _volumeProbeGeneration);
            _volumeRefreshTimer.Stop();
            return;
        }

        if (refreshNow)
        {
            // CoreAudio endpoint activation can block for seconds on some OEM audio
            // stacks. Never enumerate endpoints on the WPF dispatcher. Reopening the
            // page paints the last known state immediately, then one coalesced worker
            // refreshes output + input together. Dolby probing is independently
            // backgrounded below for the same reason.
            QueueVolumeRefresh(applyCacheFirst: true);
            RefreshStatus();
        }
        if (!_volumeRefreshTimer.IsEnabled)
            _volumeRefreshTimer.Start();
    }

    internal void PrepareForSnapshot(bool providersAvailable)
    {
        _snapshotMode = true;
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
                PrepareMicrophoneSnapshot(72, available: true);

                BackendStatusText.Text = "Direct Dolby control available";
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
                PrepareMicrophoneSnapshot(0, available: false);

                BackendStatusText.Text = "Direct Dolby control unavailable";
                InstallButton.Visibility = Visibility.Visible;
                OpenButton.IsEnabled = true;
                ProfileGrid.Visibility = Visibility.Collapsed;
                FusionControlCard.Visibility = Visibility.Collapsed;
                SetProfilesEnabled(false);
                UpdateToneSection("Dynamic", directToneAvailable: false);
                ActionStatusText.Text = "Use Dolby Access for additional controls.";
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
        _volumeRefreshTimer.Stop();
        _status = new DolbyAudioStatus(
            DolbyAccessInstalled: true,
            DaxBackendDetected: false,
            Detail: "Dolby Access available for profile changes",
            FusionBackendDetected: true);
        _directState = new DolbyDirectState(
            Available: false,
            CanProfileControl: false,
            CanToneControl: false,
            ActiveProfile: null,
            ActiveTone: null,
            Detail: "Direct profile control unavailable");

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
            PrepareMicrophoneSnapshot(72, available: true);

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
            ActionStatusText.Text = "Dolby Access available for profile changes.";
        }
        finally
        {
            _syncing = false;
        }
    }

    internal void RefreshStatus() => _ = RefreshStatusAsync();

    private async Task RefreshStatusAsync()
    {
        if (_snapshotMode || _app is null || _dolby is null || !IsVisible)
            return;

        int generation = Interlocked.Increment(ref _statusProbeGeneration);
        DolbyAudioService dolby = _dolby;
        try
        {
            (DolbyAudioStatus status, DolbyDirectState direct) = await ProbeDolbyAsync(dolby);
            if (generation != Volatile.Read(ref _statusProbeGeneration) || _snapshotMode || !IsVisible || _app is null)
                return;

            _status = status;
            _directState = direct;
            ApplyDolbyStatus();
        }
        catch (Exception ex)
        {
            if (generation != Volatile.Read(ref _statusProbeGeneration) || !IsVisible)
                return;

            BackendStatusText.Text = "Dolby capability check did not complete. Windows volume remains available.";
            ActionStatusText.Text = ex.Message;
        }
    }

    private Task<(DolbyAudioStatus Status, DolbyDirectState Direct)> ProbeDolbyAsync(DolbyAudioService dolby)
    {
        var completion = new TaskCompletionSource<(DolbyAudioStatus, DolbyDirectState)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult((dolby.Probe(), _directDolby.Probe()));
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "ThinkControl Dolby probe"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private void ApplyDolbyStatus()
    {
        if (_status is null || _directState is null || _app is null)
            return;

        bool directProfiles = _directState.CanProfileControl;
        bool accessBridge = !directProfiles && CanUseDolbyAccessBridge(_status);
        bool canSelectProfiles = directProfiles || accessBridge;

        BackendStatusText.Text = _directState.Available && (directProfiles || _directState.CanToneControl)
            ? _directState.Detail
            : _status.Detail;

        // Lenovo ships multiple DAX generations. The semantic Access bridge is safe
        // for both Fusion and OEM DAX3 because it uses only the official packaged app
        // on an explicit click; no private profile IDs or persistent automation.
        InstallButton.Visibility = _status.DolbyAccessInstalled ? Visibility.Collapsed : Visibility.Visible;
        OpenButton.IsEnabled = _status.DolbyAccessInstalled || _status.OemBackendDetected;
        ProfileGrid.Visibility = canSelectProfiles ? Visibility.Visible : Visibility.Collapsed;
        FusionControlCard.Visibility = accessBridge ? Visibility.Visible : Visibility.Collapsed;

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

        if (accessBridge && string.IsNullOrWhiteSpace(ActionStatusText.Text))
            ActionStatusText.Text = "Dolby Access available for profile changes.";
    }

    private static bool CanUseDolbyAccessBridge(DolbyAudioStatus status) =>
        status.DolbyAccessInstalled || status.OemBackendDetected;

    private void UpdateToneSection(string profile, bool directToneAvailable)
    {
        bool music = string.Equals(profile, "Music", StringComparison.OrdinalIgnoreCase);
        SubprofileCard.Visibility = music && directToneAvailable ? Visibility.Visible : Visibility.Collapsed;
        SetToneEnabled(music && directToneAvailable);
        SubprofileStatusText.Text = directToneAvailable
            ? "Direct DAX · Music"
            : _status is not null && CanUseDolbyAccessBridge(_status)
                ? "Dolby Access fallback"
                : "Not exposed by this Dolby build";
    }

    private void QueueVolumeRefresh(bool applyCacheFirst)
    {
        if (_snapshotMode || !IsVisible)
            return;

        if (applyCacheFirst && _cachedOutput is not null && _cachedInput is not null)
            ApplyVolumeStatus(_cachedOutput, _cachedInput);

        if (Interlocked.CompareExchange(ref _volumeProbeRunning, 1, 0) != 0)
            return;

        int generation = Volatile.Read(ref _volumeProbeGeneration);
        _ = RefreshVolumeAsync(generation);
    }

    private async Task RefreshVolumeAsync(int generation)
    {
        try
        {
            (WindowsVolumeStatus output, WindowsVolumeStatus input) = await Task.Run(() =>
                (_volume.Read(), _volume.Read(DataFlow.Capture)));

            if (generation != Volatile.Read(ref _volumeProbeGeneration) || _snapshotMode || !IsVisible)
                return;

            _cachedOutput = output;
            _cachedInput = input;
            ApplyVolumeStatus(output, input);
        }
        catch
        {
            // WindowsVolumeService already turns endpoint failures into status values;
            // this catch is only a final guard against a worker/runtime failure.
        }
        finally
        {
            Interlocked.Exchange(ref _volumeProbeRunning, 0);
        }
    }

    private void ApplyVolumeStatus(WindowsVolumeStatus status, WindowsVolumeStatus microphone)
    {
        if (_snapshotMode || !IsVisible)
            return;

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
            }
            else
            {
                if (!_volumeDragging && !_volumeAutomationCommitTimer.IsEnabled)
                {
                    VolumeSlider.Value = status.Percent;
                    VolumeValueText.Text = status.Muted ? $"{status.Percent}% · muted" : $"{status.Percent}%";
                }
                MuteButton.Content = status.Muted ? "Unmute" : "Mute";
                MuteButton.Tag = status.Muted;
            }

            MicrophoneSlider.IsEnabled = microphone.Available;
            MicrophoneMuteButton.IsEnabled = microphone.Available;
            MicrophoneDeviceText.Text = microphone.Detail;
            if (microphone.Available)
            {
                if (!_microphoneDragging && !_microphoneAutomationCommitTimer.IsEnabled)
                {
                    MicrophoneSlider.Value = microphone.Percent;
                    MicrophoneValueText.Text = microphone.Muted ? $"{microphone.Percent}% · muted" : $"{microphone.Percent}%";
                }
                MicrophoneMuteButton.Content = microphone.Muted ? "Unmute" : "Mute";
            }
            else
            {
                MicrophoneValueText.Text = "—";
                MicrophoneMuteButton.Content = "Mute";
            }
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

        VolumeValueText.Text = $"{(int)Math.Round(e.NewValue)}%";
        if (!_volumeDragging)
            RestartAutomationCommit(_volumeAutomationCommitTimer);
    }

    private void VolumeSlider_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _volumeAutomationCommitTimer.Stop();
        _volumeInteractionStartPercent = (int)Math.Round(VolumeSlider.Value);
        _volumeDragging = true;
    }

    private void VolumeSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_snapshotMode)
            return;

        _volumeAutomationCommitTimer.Stop();
        ApplyVolumeSlider(_volumeInteractionStartPercent);
        _volumeDragging = false;
        _volumeInteractionStartPercent = null;
        QueueVolumeRefresh(applyCacheFirst: false);
    }

    private void VolumeSlider_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!IsSliderAdjustmentKey(e.Key))
            return;

        _volumeAutomationCommitTimer.Stop();
        if (!_volumeDragging)
            _volumeInteractionStartPercent = (int)Math.Round(VolumeSlider.Value);
        _volumeDragging = true;
    }

    private void VolumeSlider_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_snapshotMode || !IsSliderAdjustmentKey(e.Key))
            return;

        _volumeAutomationCommitTimer.Stop();
        ApplyVolumeSlider(_volumeInteractionStartPercent);
        _volumeDragging = false;
        _volumeInteractionStartPercent = null;
        QueueVolumeRefresh(applyCacheFirst: false);
    }

    private void VolumeSlider_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (_snapshotMode || !_volumeDragging || VolumeSlider.IsMouseCaptureWithin)
            return;

        _volumeAutomationCommitTimer.Stop();
        ApplyVolumeSlider(_volumeInteractionStartPercent);
        _volumeDragging = false;
        _volumeInteractionStartPercent = null;
        QueueVolumeRefresh(applyCacheFirst: false);
    }

    private void ApplyVolumeSlider() => ApplyVolumeSlider(interactionStartPercent: null);

    private void ApplyVolumeSlider(int? interactionStartPercent)
    {
        if (_snapshotMode || _syncing || !VolumeSlider.IsEnabled || !IsVisible)
            return;

        int requested = (int)Math.Round(VolumeSlider.Value);
        if (interactionStartPercent == requested ||
            (interactionStartPercent is null && _cachedOutput?.Available == true && _cachedOutput.Percent == requested))
        {
            return;
        }

        if (_volume.Set(requested, out int applied))
        {
            VolumeValueText.Text = $"{applied}%";
            if (_cachedOutput is WindowsVolumeStatus cached)
                _cachedOutput = cached with { Percent = applied, Muted = false };
        }
    }

    private async void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshotMode)
            return;

        bool? cachedMuted = _cachedOutput?.Available == true ? _cachedOutput.Muted : null;
        MuteButton.IsEnabled = false;
        try
        {
            bool changed = await Task.Run(() =>
            {
                bool muted;
                if (cachedMuted is bool known)
                {
                    muted = known;
                }
                else
                {
                    WindowsVolumeStatus current = _volume.Read();
                    if (!current.Available)
                        return false;
                    muted = current.Muted;
                }
                return _volume.SetMuted(!muted);
            });
            if (changed && _cachedOutput is WindowsVolumeStatus cached)
                _cachedOutput = cached with { Muted = !cached.Muted };
        }
        finally
        {
            MuteButton.IsEnabled = true;
            QueueVolumeRefresh(applyCacheFirst: true);
        }
    }

    private void PrepareMicrophoneSnapshot(int percent, bool available)
    {
        MicrophoneSlider.IsEnabled = available;
        MicrophoneSlider.Value = percent;
        MicrophoneValueText.Text = available ? $"{percent}%" : "—";
        MicrophoneDeviceText.Text = available ? "Microphone Array · default Windows input" : "Windows input endpoint unavailable";
        MicrophoneMuteButton.IsEnabled = available;
        MicrophoneMuteButton.Content = "Mute";
    }

    private void MicrophoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_snapshotMode || _syncing || !IsLoaded || !IsVisible)
            return;

        MicrophoneValueText.Text = $"{(int)Math.Round(e.NewValue)}%";
        if (!_microphoneDragging)
            RestartAutomationCommit(_microphoneAutomationCommitTimer);
    }

    private void MicrophoneSlider_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _microphoneAutomationCommitTimer.Stop();
        _microphoneInteractionStartPercent = (int)Math.Round(MicrophoneSlider.Value);
        _microphoneDragging = true;
    }

    private void MicrophoneSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_snapshotMode)
            return;

        _microphoneAutomationCommitTimer.Stop();
        ApplyMicrophoneSlider(_microphoneInteractionStartPercent);
        _microphoneDragging = false;
        _microphoneInteractionStartPercent = null;
        QueueVolumeRefresh(applyCacheFirst: false);
    }

    private void MicrophoneSlider_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!IsSliderAdjustmentKey(e.Key))
            return;

        _microphoneAutomationCommitTimer.Stop();
        if (!_microphoneDragging)
            _microphoneInteractionStartPercent = (int)Math.Round(MicrophoneSlider.Value);
        _microphoneDragging = true;
    }

    private void MicrophoneSlider_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_snapshotMode || !IsSliderAdjustmentKey(e.Key))
            return;

        _microphoneAutomationCommitTimer.Stop();
        ApplyMicrophoneSlider(_microphoneInteractionStartPercent);
        _microphoneDragging = false;
        _microphoneInteractionStartPercent = null;
        QueueVolumeRefresh(applyCacheFirst: false);
    }

    private void MicrophoneSlider_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (_snapshotMode || !_microphoneDragging || MicrophoneSlider.IsMouseCaptureWithin)
            return;

        _microphoneAutomationCommitTimer.Stop();
        ApplyMicrophoneSlider(_microphoneInteractionStartPercent);
        _microphoneDragging = false;
        _microphoneInteractionStartPercent = null;
        QueueVolumeRefresh(applyCacheFirst: false);
    }

    private static bool IsSliderAdjustmentKey(System.Windows.Input.Key key) =>
        key is System.Windows.Input.Key.Left or System.Windows.Input.Key.Right or
        System.Windows.Input.Key.Up or System.Windows.Input.Key.Down or
        System.Windows.Input.Key.Home or System.Windows.Input.Key.End or
        System.Windows.Input.Key.PageUp or System.Windows.Input.Key.PageDown;

    private static DispatcherTimer CreateAutomationCommitTimer(Action commit)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            commit();
        };
        return timer;
    }

    private static void RestartAutomationCommit(DispatcherTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    private void ApplyMicrophoneSlider() => ApplyMicrophoneSlider(interactionStartPercent: null);

    private void ApplyMicrophoneSlider(int? interactionStartPercent)
    {
        if (_snapshotMode || _syncing || !MicrophoneSlider.IsEnabled || !IsVisible)
            return;

        int requested = (int)Math.Round(MicrophoneSlider.Value);
        if (interactionStartPercent == requested ||
            (interactionStartPercent is null && _cachedInput?.Available == true && _cachedInput.Percent == requested))
        {
            return;
        }

        if (_volume.Set(requested, out int applied, DataFlow.Capture))
        {
            MicrophoneValueText.Text = $"{applied}%";
            if (_cachedInput is WindowsVolumeStatus cached)
                _cachedInput = cached with { Percent = applied, Muted = false };
        }
    }

    private async void MicrophoneMute_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshotMode)
            return;

        bool? cachedMuted = _cachedInput?.Available == true ? _cachedInput.Muted : null;
        MicrophoneMuteButton.IsEnabled = false;
        try
        {
            bool changed = await Task.Run(() =>
            {
                bool muted;
                if (cachedMuted is bool known)
                {
                    muted = known;
                }
                else
                {
                    WindowsVolumeStatus current = _volume.Read(DataFlow.Capture);
                    if (!current.Available)
                        return false;
                    muted = current.Muted;
                }
                return _volume.SetMuted(!muted, DataFlow.Capture);
            });
            if (changed && _cachedInput is WindowsVolumeStatus cached)
                _cachedInput = cached with { Muted = !cached.Muted };
        }
        finally
        {
            MicrophoneMuteButton.IsEnabled = true;
            QueueVolumeRefresh(applyCacheFirst: true);
        }
    }

    private async void Profile_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshotMode || _syncing || _app is null || _dolby is null ||
            sender is not FrameworkElement { Tag: string profile })
        {
            return;
        }

        Interlocked.Increment(ref _statusProbeGeneration);
        ActionStatusText.Text = $"Switching to {profile}…";
        SetProfilesEnabled(false);

        DolbyProfileResult result;
        if (_directState?.CanProfileControl == true)
        {
            result = await _directDolby.SetProfileAsync(profile);
        }
        else if (_status is not null && CanUseDolbyAccessBridge(_status))
        {
            result = await _accessDolby.SetProfileAsync(profile, _dolby);
        }
        else
        {
            result = new DolbyProfileResult(false, "This Dolby driver does not expose a supported profile-control path.");
        }

        if (result.Success)
            _app.UserSettings.Update(settings => settings with { DolbyProfile = profile });

        await RefreshStatusAsync();
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

        await RefreshStatusAsync();
        ActionStatusText.Text = result.Success
            ? result.Detail
            : result.Detail + " · Direct tone control was not accepted.";
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshotMode || _app is null || _dolby is null || sender is not Button button)
            return;

        Interlocked.Increment(ref _statusProbeGeneration);
        button.IsEnabled = false;
        try
        {
            DolbyProfileResult profile;
            if (_directState?.CanProfileControl == true)
            {
                profile = await _directDolby.SetProfileAsync("Dynamic");
            }
            else if (_status is not null && CanUseDolbyAccessBridge(_status))
            {
                profile = await _accessDolby.SetProfileAsync("Dynamic", _dolby);
            }
            else
            {
                profile = new DolbyProfileResult(false, "Dolby reset is unavailable on this driver.");
            }

            if (profile.Success)
            {
                _app.UserSettings.Update(settings => settings with
                {
                    DolbyProfile = "Dynamic",
                    DolbySubProfile = "Balanced"
                });
                await RefreshStatusAsync();
                ActionStatusText.Text = "Audio processing reset to Dynamic. Windows output volume was left unchanged.";
            }
            else
            {
                await RefreshStatusAsync();
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
