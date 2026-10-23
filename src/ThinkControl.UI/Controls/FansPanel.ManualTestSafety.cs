using ThinkControl.Core.Cooling;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkControl.Core.Ipc;

namespace ThinkControl.UI.Controls;

public partial class FansPanel
{
    private const int ManualFanTestDurationSeconds = 30;

    private readonly DispatcherTimer _manualFanTestTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private Button? _manualFanApplyButton;
    private TextBlock? _manualFanTestStatus;
    private string? _manualFanRestoreProfile;
    private string _manualFanTestLabel = string.Empty;
    private DateTimeOffset _manualFanTestEndsAt;
    private bool _manualFanTestActive;
    private bool _manualFanTestEnding;
    private bool _manualFanSafetyConfigured;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        ConfigureManualFanTestSafety();
    }

    private void ConfigureManualFanTestSafety()
    {
        if (_manualFanSafetyConfigured)
            return;
        _manualFanSafetyConfigured = true;

        if (ManualPercentSlider.Parent is Grid row)
        {
            Button apply = ManualPercentApplyButton;
            apply.Click -= ManualPercentApply_Click;
            apply.Click += ManualPercentTestApply_Click;
            _manualFanApplyButton = apply;

            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _manualFanTestStatus = new TextBlock
            {
                Margin = new Thickness(1, 6, 0, 0),
                FontSize = TypographyScale.Caption,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            _manualFanTestStatus.SetResourceReference(TextBlock.ForegroundProperty, "Tc.TextMuted");
            Grid.SetRow(_manualFanTestStatus, 1);
            Grid.SetColumnSpan(_manualFanTestStatus, 2);
            row.Children.Add(_manualFanTestStatus);
        }

        // Raw EC diagnostics live inside a collapsed Expander, so walk the logical
        // tree rather than the generated visual tree. Every raw state uses the same
        // bounded temporary-test/auto-restore contract as the percentage target.
        foreach (Button button in FindLogicalDescendants<Button>(RawEcStepsExpander))
        {
            if (button.Tag is not string raw || !int.TryParse(raw, out int level) || level is < 1 or > 7)
                continue;
            button.Click -= ManualLevel_Click;
            button.Click += ManualLevelTest_Click;
        }

        ProfileComboBox.DropDownOpened += (_, _) =>
        {
            if (!_manualFanTestActive)
                return;
            ProfileComboBox.IsDropDownOpen = false;
            ProfileComboBox.IsEnabled = false;
            UpdateManualFanTestUi();
        };

        _manualFanTestTimer.Tick += ManualFanTestTimer_Tick;
        IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is false && _manualFanTestActive && !_manualFanTestEnding)
                _ = EndManualFanTestAsync("Fan page closed");
        };
    }

    private async void ManualPercentTestApply_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || _manualFanTestEnding)
            return;

        if (_manualFanTestActive)
        {
            await EndManualFanTestAsync("Ended by user");
            return;
        }

        if (_manualFanApplyButton is not null)
            _manualFanApplyButton.IsEnabled = false;
        try
        {
            if (string.Equals(_fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal))
            {
                // No arbitrary percent mapping for discrete EC. Call the existing
                // verified level writer only for a complete, stable calibration.
                if (ManualSpeedComboBox.SelectedItem is not ManualFanSpeedChoices.Choice selected ||
                    !_manualSpeedChoices.Contains(selected))
                {
                    ShowManualTestError("Select a calibrated fan speed before starting a test.");
                    return;
                }

                await BeginOrRefreshManualFanTestAsync(async () =>
                {
                    ServiceResponse? response = await _app.HardwareClient.SetFanLevelAsync(selected.Level);
                    if (response?.Success == true)
                        return true;
                    _app.State.HardwareAccess = response?.Error ?? "Fan speed was not accepted by the service.";
                    return false;
                }, selected.FullSpeed ? "Full speed" : $"{selected.MedianRpm:N0} calibrated RPM");
            }
            else
            {
                int percent = (int)Math.Round(ManualPercentSlider.Value);
                await BeginOrRefreshManualFanTestAsync(
                    () => _app.SetManualFanPercentAsync(percent),
                    $"{percent}% OEM target");
            }
        }
        catch (Exception ex)
        {
            ShowManualTestError($"The fan test could not start: {ex.Message}");
            try { await _app.HardwareClient.ReturnFanToAutoAsync(); } catch { }
        }
        finally
        {
            UpdateManualFanTestControls();
        }
    }

    private async void ManualLevelTest_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not Button { Tag: string raw } || !int.TryParse(raw, out int level))
            return;

        SetButtonBusy(sender, true);
        try
        {
            await BeginOrRefreshManualFanTestAsync(async () =>
            {
                ServiceResponse? response = await _app.HardwareClient.SetFanLevelAsync(level);
                if (response?.Success == true)
                    return true;
                _app.State.HardwareAccess = response?.Error ?? "Manual fan control unavailable";
                return false;
            }, $"EC step {level}");
        }
        finally
        {
            SetButtonBusy(sender, false);
        }
    }

    private static void SetButtonBusy(object source, bool busy)
    {
        if (source is Button button)
            button.IsEnabled = !busy;
    }

    private async Task BeginOrRefreshManualFanTestAsync(Func<Task<bool>> apply, string testLabel)
    {
        if (_app is null || _manualFanTestEnding)
            return;

        string restoreProfile = _manualFanTestActive
            ? _manualFanRestoreProfile ?? _app.UserSettings.Current.CoolingProfile
            : _app.UserSettings.Current.CoolingProfile;

        bool applied = await apply();
        if (!applied)
        {
            UpdateManualFanTestUi();
            ShowManualTestError(_app.State.HardwareAccess);
            return;
        }

        if (!_manualFanTestActive)
            _manualFanRestoreProfile = restoreProfile;

        _manualFanTestActive = true;
        _manualFanTestLabel = testLabel;
        _manualFanTestEndsAt = DateTimeOffset.UtcNow.AddSeconds(ManualFanTestDurationSeconds);
        _manualFanTestTimer.Start();
        UpdateManualFanTestUi(testLabel);
    }

    private async void ManualFanTestTimer_Tick(object? sender, EventArgs e)
    {
        if (!_manualFanTestActive)
        {
            _manualFanTestTimer.Stop();
            return;
        }

        if (DateTimeOffset.UtcNow >= _manualFanTestEndsAt)
        {
            await EndManualFanTestAsync("Time limit reached");
            return;
        }

        UpdateManualFanTestUi();
    }

    private async Task EndManualFanTestAsync(string reason)
    {
        if (_app is null || !_manualFanTestActive || _manualFanTestEnding)
            return;

        _manualFanTestEnding = true;
        _manualFanTestTimer.Stop();
        string restore = _manualFanRestoreProfile ?? _app.UserSettings.Current.CoolingProfile;
        string restoreName = FriendlyProfileName(restore);
        if (_manualFanTestStatus is not null)
        {
            _manualFanTestStatus.Visibility = Visibility.Visible;
            _manualFanTestStatus.Text = $"Restoring {restoreName}…";
        }
        UpdateManualFanTestControls();

        bool restored = false;
        try
        {
            restored = await _app.SetCoolingProfileAsync(restore);
            if (!restored)
            {
                ServiceResponse? fallback = await _app.HardwareClient.ReturnFanToAutoAsync();
                if (fallback?.Success == true)
                {
                    _app.State.CoolingProfile = "Lenovo Auto";
                    restored = true;
                    restoreName = "Auto";
                }
            }
        }
        catch
        {
            // A timeout or service restart must not let an async-void timer
            // exception bring down the UI. Best-effort handoff to firmware Auto.
            try
            {
                ServiceResponse? fallback = await _app.HardwareClient.ReturnFanToAutoAsync();
                restored = fallback?.Success == true;
                if (restored) restoreName = "Auto";
            }
            catch { }
        }
        finally
        {
            _manualFanTestActive = false;
            _manualFanTestEnding = false;
            _manualFanRestoreProfile = null;
            _manualFanTestLabel = string.Empty;
            UpdateManualFanTestControls();
            if (_manualFanTestStatus is not null)
            {
                _manualFanTestStatus.Visibility = Visibility.Visible;
                _manualFanTestStatus.Text = restored
                    ? $"{reason}. Restored {restoreName}."
                    : $"{reason}. Restoration failed. Select Auto or a profile.";
            }
            ProfileComboBox.IsEnabled = _app.State.CanFanControl && !_app.FanCalibrationState.Running;
            _ = _app.HardwareClient.GetStatusAsync();
        }
    }

    private void UpdateManualFanTestUi(string? testLabel = null)
    {
        UpdateManualFanTestControls();
        if (_app is null || _manualFanTestStatus is null)
            return;

        if (!_manualFanTestActive)
        {
            _manualFanTestStatus.Visibility = Visibility.Collapsed;
            return;
        }

        int seconds = Math.Max(0, (int)Math.Ceiling((_manualFanTestEndsAt - DateTimeOffset.UtcNow).TotalSeconds));
        string restoreName = FriendlyProfileName(_manualFanRestoreProfile);
        string label = string.IsNullOrWhiteSpace(testLabel) ? _manualFanTestLabel : testLabel;
        string measured = _app.State.FanRpm is int rpm
            ? $"Last reported: {rpm:N0} RPM." : "Live fan RPM unavailable.";
        _manualFanTestStatus.Visibility = Visibility.Visible;
        _manualFanTestStatus.Text = $"{label}. {measured} Restores {restoreName} in {seconds} s.";
        ProfileComboBox.IsEnabled = false;
    }

    private void ShowManualTestError(string? detail)
    {
        if (_manualFanTestStatus is null)
            return;
        _manualFanTestStatus.Visibility = Visibility.Visible;
        _manualFanTestStatus.Text = string.IsNullOrWhiteSpace(detail)
            ? "The fan test could not be applied. Your cooling profile remains unchanged."
            : detail;
    }

    private void UpdateManualFanTestControls()
    {
        if (_app is null)
            return;

        bool canControl = _manualServiceCanControl && !_app.FanCalibrationState.Required;
        bool discrete = string.Equals(_fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);
        bool hasChoice = !discrete || ManualSpeedComboBox.SelectedItem is ManualFanSpeedChoices.Choice;
        if (_manualFanApplyButton is not null)
        {
            _manualFanApplyButton.Content = _manualFanTestActive ? "End test" : "Start test";
            _manualFanApplyButton.IsEnabled = !_manualFanTestEnding &&
                (_manualFanTestActive || canControl && hasChoice);
        }

        bool canEditTarget = canControl && !_manualFanTestActive && !_manualFanTestEnding;
        ManualPercentSlider.IsEnabled = canEditTarget && !discrete;
        ManualSpeedComboBox.IsEnabled = canEditTarget && discrete && hasChoice;
        RawEcStepsExpander.IsEnabled = canEditTarget;
    }

    private string FriendlyProfileName(string? id)
    {
        if (_app is null || string.IsNullOrWhiteSpace(id))
            return "Auto";
        return _app.FanProfiles.Find(id)?.Name ?? DisplayProfile(id);
    }

    private static IEnumerable<T> FindLogicalDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject dependency)
                continue;
            if (dependency is T match)
                yield return match;
            foreach (T nested in FindLogicalDescendants<T>(dependency))
                yield return nested;
        }
    }
}
