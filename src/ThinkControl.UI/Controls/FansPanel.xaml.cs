using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Cooling;
using ThinkControl.Core.Ipc;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class FansPanel : UserControl
{
    private readonly ObservableCollection<CalibrationRow> _calibrationRows = [];
    private readonly ObservableCollection<FanProfileChoice> _profileChoices = [];
    private readonly FanCurveGraph _activeCurveGraph = new() { IsReadOnly = true, ShowLiveLabel = false };
    private App? _app;
    private bool _statusSubscribed;
    private bool _snapshotMode;
    private bool _syncingProfileSelection;
    private string _currentProfileId = "Lenovo Auto";
    private string _fanControlKind = FanControlKinds.None;
    private bool _autoRecoveryConfirmed;

    private bool UsesFirmwarePolicy =>
        _fanControlKind is FanControlKinds.FirmwarePolicy or FanControlKinds.FullSpeedOnly;

    private bool HasDirectFanWriter =>
        string.Equals(_fanControlKind, FanControlKinds.OemTargetRpm, StringComparison.Ordinal) ||
        string.Equals(_fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);

    public FansPanel()
    {
        InitializeComponent();
        ActiveCurvePreviewHost.Content = _activeCurveGraph;
        CalibrationResults.ItemsSource = _calibrationRows;
        ProfileComboBox.ItemsSource = _profileChoices;
        Loaded += (_, _) => SyncStatusSubscription();
        Unloaded += (_, _) => UnsubscribeStatus();
        IsVisibleChanged += (_, _) => SyncStatusSubscription();
    }

    internal void Initialize(App app)
    {
        if (!ReferenceEquals(_app, app))
        {
            UnsubscribeStatus();
            _app = app;
            DataContext = app.State;
        }

        bool canControl = app.State.CanFanControl;
        _fanControlKind = ResolveFanControlKind(app.State.FanControlKind, canControl);

        SyncProfileSelector(app.State.CoolingProfile, RuntimeProfileIdForDisplay(app.State.CoolingProfile));
        ApplyProviderCopy(canControl, _fanControlKind);
        ApplyCalibrationUi(app.FanCalibrationState, canControl);
        SyncStatusSubscription();
    }

    internal void PrepareForSnapshot(AppState state)
    {
        _snapshotMode = true;
        UnsubscribeStatus();
        DataContext = state;
        bool canControl = state.CanFanControl;
        _fanControlKind = ResolveFanControlKind(state.FanControlKind, canControl);
        SyncProfileSelector(state.CoolingProfile, state.CoolingProfile);
        ApplyProviderCopy(canControl, _fanControlKind);
        CoolingDetailText.Text = canControl
            ? _fanControlKind == FanControlKinds.FullSpeedOnly ? "Auto and Max cooling are available."
                : UsesFirmwarePolicy ? "Lenovo firmware controls the fan speed." : "Direct fan control is available."
            : state.FanAutoRecoverySupported == false
                ? "Cooling is read-only on this firmware. ThinkControl cannot apply profiles or confirm Auto."
                : state.CoolingAvailabilityText;
        CoolingDetailText.ToolTip = !canControl ? state.HardwareAccess : null;
        CoolingOwnerText.Text = canControl
            ? UsesFirmwarePolicy ? "Lenovo firmware" : "Direct control"
            : "Unavailable";
        bool ownershipConflict = UsesFirmwarePolicy && App.IsExternalCoolingOwnerConflict(state.HardwareAccess);
        RecoverAutoButton.Visibility = (ownershipConflict && state.FanAutoRecoverySupported != false) ||
                                      (!canControl && state.FanAutoRecoverySupported == true)
            ? Visibility.Visible : Visibility.Collapsed;
        if (ownershipConflict)
            CoolingDetailText.Text = "Another controller is using the fans. Select Auto before changing cooling.";
        AppliedLevelText.Text = canControl
            ? _fanControlKind == FanControlKinds.FullSpeedOnly ? state.CoolingProfile == "Max cooling" ? "Full speed" : "Auto"
                : UsesFirmwarePolicy ? "Lenovo firmware" : state.FanStateText
            : "Not confirmed";

        // Snapshot fixtures do not have a live service capability object. Model the
        // current discrete-provider fixture as calibration-capable without teaching
        // the production UI anything about a particular OEM or machine type.
        bool canCalibrate = state.CanFanControl && state.CanFanTelemetry &&
                            string.Equals(_fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);
        FanCalibrationUiState calibration = canCalibrate
            ? new FanCalibrationUiState(
                Relevant: true,
                Running: false,
                Ready: false,
                CompletedLevels: 0,
                TotalLevels: 7,
                Status: "Calibration required before percentage fan profiles and manual targets are enabled.")
            : FanCalibrationUiState.None;
        ApplyCalibrationUi(calibration, canControl);
        _calibrationRows.Clear();
        UpdateActiveCurvePreview(ProfileComboBox.SelectedItem as FanProfileChoice, state.ControlTemperatureC, state.FanRpm);
    }

    private void SyncStatusSubscription()
    {
        bool shouldSubscribe = !_snapshotMode && _app is not null && IsLoaded && IsVisible;
        if (shouldSubscribe == _statusSubscribed)
            return;

        if (shouldSubscribe)
        {
            _app!.HardwareClient.StatusObserved += HardwareClient_StatusObserved;
            _statusSubscribed = true;
            _ = _app.HardwareClient.GetStatusAsync();
        }
        else
        {
            UnsubscribeStatus();
        }
    }

    private void UnsubscribeStatus()
    {
        if (!_statusSubscribed || _app is null)
            return;
        _app.HardwareClient.StatusObserved -= HardwareClient_StatusObserved;
        _statusSubscribed = false;
    }

    private void HardwareClient_StatusObserved(object? sender, ServiceResponse? response)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => HardwareClient_StatusObserved(sender, response));
            return;
        }
        if (IsVisible)
            ApplyStatus(response);
    }

    private void ApplyStatus(ServiceResponse? response)
    {
        TelemetrySnapshot? telemetry = response?.Success == true ? response.Telemetry : null;
        bool canControl = response?.Capabilities?.FanControl == true;
        bool canFanTelemetry = response?.Capabilities?.FanTelemetry == true;
        bool hasTelemetry = canFanTelemetry || response?.Capabilities?.SensorTelemetry == true;
        string? explicitKind = response?.Capabilities?.FanControlKind;
        _fanControlKind = ResolveFanControlKind(explicitKind, canControl);

        string profileName = telemetry?.CoolingProfile ??
                             _app?.State.CoolingProfile ??
                             "Lenovo Auto";
        string profileId = telemetry?.CoolingProfileId ?? (profileName.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ? "Lenovo Auto" : profileName);
        SyncProfileSelector(profileName, profileId);
        ApplyProviderCopy(canControl, _fanControlKind);

        CoolingDetailText.Text = !canControl && response?.Capabilities?.FanAutoRecoverySupported == false
            ? "Cooling is read-only on this firmware. ThinkControl cannot apply profiles or confirm Auto."
            : _app?.LastCoolingError is string failure
            ? "Cooling could not be changed. Try Auto or review hardware details in System."
            : _app?.ExternalCoolingOwnerConflictDetail is string conflict
                ? "Another controller is using the fans. Select Auto before changing cooling."
                : canControl
                ? _fanControlKind == FanControlKinds.FullSpeedOnly ? "Auto and Max cooling are available."
                    : UsesFirmwarePolicy ? "Lenovo firmware controls the fan speed." : "Direct fan control is available."
                : _app?.State.CoolingAvailabilityText ?? DescribeUnavailable(hasTelemetry);
        CoolingDetailText.ToolTip = !canControl ? telemetry?.HardwareAccess ?? _app?.LastCoolingError : null;
        CoolingDetailText.SetResourceReference(TextBlock.ForegroundProperty,
            _app?.LastCoolingError is null ? "Tc.TextMuted" : "Tc.Accent");
        CoolingOwnerText.Text = canControl
            ? UsesFirmwarePolicy ? "Lenovo firmware" : "Direct control"
            : "Unavailable";
        if (telemetry is not null && !string.IsNullOrWhiteSpace(telemetry.FanState))
            _autoRecoveryConfirmed = telemetry.FanState.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ||
                                     telemetry.FanState.Equals("Auto", StringComparison.OrdinalIgnoreCase) ||
                                     telemetry.FanState.Equals("Firmware Auto", StringComparison.OrdinalIgnoreCase);
        RecoverAutoButton.Visibility = !_autoRecoveryConfirmed && (
            (!canControl && response?.Capabilities?.FanAutoRecoverySupported == true) ||
            (response?.Capabilities?.FanAutoRecoverySupported != false &&
            UsesFirmwarePolicy && App.IsExternalCoolingOwnerConflict(
                _app?.ExternalCoolingOwnerConflictDetail ??
                _app?.LastCoolingError ?? _app?.State.HardwareAccess)))
                ? Visibility.Visible : Visibility.Collapsed;

        if (!canControl)
        {
            AppliedLevelText.Text = _autoRecoveryConfirmed ? "Auto confirmed" : "Not confirmed";
        }
        else if (UsesFirmwarePolicy && !profileName.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) &&
            !profileName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            AppliedLevelText.Text = _fanControlKind == FanControlKinds.FullSpeedOnly ? "Full speed" : "Lenovo firmware";
        }
        else if (telemetry?.CoolingAppliedPercent is int percent)
        {
            if (_fanControlKind == FanControlKinds.OemTargetRpm && telemetry.CoolingAppliedLevel is null)
                AppliedLevelText.Text = $"{percent}% OEM target";
            else
                AppliedLevelText.Text = $"Approx. {percent}%";
        }
        else if (telemetry?.CoolingAppliedLevel is int legacyLevel)
        {
            AppliedLevelText.Text = $"State {legacyLevel}";
        }
        else
        {
            AppliedLevelText.Text = "Auto";
        }

        FanCharacterizationSnapshot? characterization = telemetry?.FanCharacterization;
        FanCalibrationUiState calibration = _app?.FanCalibrationState ?? FanCalibrationUiState.None;
        ApplyCalibrationUi(calibration, canControl);
        BuildCalibrationRows(characterization);
        UpdateActiveCurvePreview(ProfileComboBox.SelectedItem as FanProfileChoice, telemetry?.ControlTemperatureC, telemetry?.FanRpm);
        UpdateManualFanTestControls();
    }

    private void ApplyCalibrationUi(FanCalibrationUiState calibration, bool canControl)
    {
        bool running = calibration.Running;
        bool ready = calibration.Ready;
        bool attention = calibration.Required || running;
        bool showCalibrationTask = calibration.Relevant && attention;
        CalibrationCard.Visibility = showCalibrationTask ? Visibility.Visible : Visibility.Collapsed;

        if (!calibration.Relevant)
        {
            bool directWriter = canControl && HasDirectFanWriter;
            ProfileComboBox.IsEnabled = canControl;
            EditCurvesButton.IsEnabled = directWriter;
            EditCurvesButton.Visibility = directWriter ? Visibility.Visible : Visibility.Collapsed;
            ProfileCard.Opacity = 1;
            ManualControlExpander.IsEnabled = directWriter;
            ManualControlExpander.Visibility = directWriter ? Visibility.Visible : Visibility.Collapsed;
            ManualControlExpander.Opacity = 1;
            ManualPercentSlider.IsEnabled = directWriter;
            ManualPercentApplyButton.IsEnabled = directWriter;
            RawEcStepsExpander.IsEnabled = directWriter;
            return;
        }

        CalibrationCard.SetResourceReference(Border.BorderBrushProperty, "Tc.Accent");
        CalibrationCard.BorderThickness = new Thickness(1);

        CharacterizationTitleText.Text = running
            ? "Fan calibration in progress"
            : "Fan calibration required";
        CalibrationDescriptionText.Text = running
            ? "ThinkControl is measuring each fan level using real tachometer readings. Other fan controls stay locked until calibration finishes or you stop it; firmware Auto is restored automatically."
            : "Calibration measures how this laptop's fan responds before percentage-based profiles and temporary tests can be enabled. Firmware Auto stays in control until calibration completes.";
        CharacterizationStatusText.Text = calibration.Status;
        CharacterizationStatusText.Visibility = string.IsNullOrWhiteSpace(calibration.Status)
            ? Visibility.Collapsed
            : Visibility.Visible;

        CharacterizeButton.Content = "Calibrate now";
        CharacterizeButton.IsEnabled = canControl && !running;
        CharacterizeButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        StopCharacterizationButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        StopCharacterizationButton.IsEnabled = running;
        CharacterizationProgress.Maximum = Math.Max(1, calibration.TotalLevels);
        CharacterizationProgress.Value = Math.Clamp(calibration.CompletedLevels, 0, calibration.TotalLevels);
        CharacterizationProgress.Visibility = running || calibration.CompletedLevels > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        bool semanticControlsEnabled = canControl && ready;
        ProfileComboBox.IsEnabled = canControl && !running;
        EditCurvesButton.IsEnabled = semanticControlsEnabled;
        EditCurvesButton.Visibility = semanticControlsEnabled ? Visibility.Visible : Visibility.Collapsed;
        ProfileCard.Opacity = 1;
        ManualControlExpander.IsEnabled = semanticControlsEnabled;
        ManualControlExpander.Visibility = semanticControlsEnabled ? Visibility.Visible : Visibility.Collapsed;
        ManualControlExpander.Opacity = 1;
        ManualPercentSlider.IsEnabled = semanticControlsEnabled;
        ManualPercentApplyButton.IsEnabled = semanticControlsEnabled;
        RawEcStepsExpander.IsEnabled = semanticControlsEnabled;

        if (!semanticControlsEnabled)
        {
            CoolingDetailText.Text = running
                ? "Calibration currently owns fan output. Profile controls return after the run finishes or is stopped."
                : "Measure fan speeds to enable curves. Auto and Max remain available.";
        }
    }

    private void BuildCalibrationRows(FanCharacterizationSnapshot? characterization)
    {
        _calibrationRows.Clear();
        if (characterization is null)
            return;

        FanLevelCalibrationSnapshot? maximum = characterization.Levels.MaxBy(static level => level.Level);
        double? maximumRpm = maximum?.Fans.Count > 0 ? maximum.Fans.Average(fan => fan.MedianRpm) : null;

        int speedIndex = 0;
        foreach (FanLevelCalibrationSnapshot point in characterization.Levels.OrderBy(level => level.Level))
        {
            string label = point.Level == maximum?.Level && characterization.Levels.Count == characterization.TotalLevels
                ? "Max" : $"Speed {++speedIndex}";
            string rpm;
            if (point.Fans.Count == 0)
            {
                rpm = "No tachometer sample";
            }
            else
            {
                double average = point.Fans.Average(fan => fan.MedianRpm);
                string values = string.Join(", ", point.Fans.Select(fan => $"{fan.Label}: {fan.MedianRpm:N0} RPM"));
                if (maximumRpm is > 0 && maximum is not null)
                {
                    int relative = point.Level == maximum.Level ? 100 : (int)Math.Round(Math.Clamp(average / maximumRpm.Value * 100.0, 0, 99));
                    rpm = $"{values}, approximately {relative}% of measured maximum speed";
                }
                else
                {
                    rpm = values;
                }
            }

            _calibrationRows.Add(new CalibrationRow(label, rpm, point.Stable ? "Stable" : "Variable"));
        }
    }

    private void SyncProfileSelector(string? profileName, string? profileId)
    {
        bool manual = IsManualProfile(profileName);
        _currentProfileId = manual
            ? profileName!.Trim()
            : string.IsNullOrWhiteSpace(profileId) ? "Lenovo Auto" : profileId;
        RebuildProfileChoices(manual ? _currentProfileId : null);

        _syncingProfileSelection = true;
        try
        {
            FanProfileChoice? selected = _profileChoices.FirstOrDefault(choice => ProfileIdsEqual(choice.Id, _currentProfileId));
            if (selected is null && !manual)
            {
                string display = DisplayProfile(profileName);
                selected = _profileChoices.FirstOrDefault(choice => string.Equals(choice.Name, display, StringComparison.OrdinalIgnoreCase));
            }

            if (selected is null && !string.IsNullOrWhiteSpace(profileName))
            {
                // An unrecognized observed state is not Auto and must not leave an
                // empty selector. This row is informational, never a write request.
                bool readOnly = (DataContext as AppState)?.CanFanControl == false;
                selected = new FanProfileChoice(_currentProfileId, readOnly ? "Read-only" : DisplayProfile(profileName), Selectable: false);
                _profileChoices.Add(selected);
            }

            ProfileComboBox.SelectedItem = selected;
            UpdateActiveCurvePreview(selected, _app?.State.ControlTemperatureC, _app?.State.FanRpm);
        }
        finally
        {
            _syncingProfileSelection = false;
        }
    }

    private void UpdateActiveCurvePreview(FanProfileChoice? choice, double? temperatureC, int? rpm)
    {
        if (UsesFirmwarePolicy)
        {
            ActiveCurvePreview.Visibility = Visibility.Collapsed;
            _activeCurveGraph.SetLiveState(null, null, null);
            return;
        }

        FanCurveDefinition? curve = choice is null || !choice.Selectable ? null : _app?.FanProfiles.Find(choice.Id);
        if (curve is null)
        {
            ActiveCurvePreview.Visibility = Visibility.Collapsed;
            _activeCurveGraph.SetLiveState(null, null, null);
            return;
        }

        _activeCurveGraph.SetCurve(curve.Points);
        _activeCurveGraph.SelectedIndex = -1;
        int? target = temperatureC is double temperature
            ? FanCurveGraphPolicy.ResolvePercent(curve.Points, temperature)
            : null;
        _activeCurveGraph.SetLiveState(temperatureC, target, rpm);
        LiveCurveStatus.Text = temperatureC is double live && target is int percent
            ? $"{live:0.0} °C → {percent}% target" + (rpm is int actual ? $", current speed: {actual:N0} RPM" : string.Empty)
            : "Waiting for control temperature";
        ActiveCurvePreview.Visibility = Visibility.Visible;
    }

    private void RebuildProfileChoices(string? manualState)
    {
        if (_app is null)
            return;

        var desired = new List<FanProfileChoice>();
        if (IsManualProfile(manualState) && !UsesFirmwarePolicy)
            desired.Add(new FanProfileChoice(manualState!.Trim(), manualState.Trim(), Selectable: false));
        desired.Add(new FanProfileChoice("Lenovo Auto", "Auto"));
        IEnumerable<FanCurveDefinition> profiles = _app.FanProfiles.GetProfiles();
        if (UsesFirmwarePolicy)
            profiles = profiles.Where(profile => _app.FanProfiles.IsBuiltIn(profile.Id));
        profiles = profiles.Where(profile => FanControlKinds.SupportsProfile(_fanControlKind, profile.Id));
        if (_app.FanCalibrationState.Required)
            profiles = profiles.Where(profile => profile.Id == FanCurveDefaults.MaxCoolingId);
        desired.AddRange(profiles.Select(profile => new FanProfileChoice(profile.Id, profile.Name)));

        if (_profileChoices.SequenceEqual(desired))
            return;

        _profileChoices.Clear();
        foreach (FanProfileChoice choice in desired)
            _profileChoices.Add(choice);
    }

    private static bool ProfileIdsEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
        (string.Equals(left, "Lenovo Auto", StringComparison.OrdinalIgnoreCase) && string.Equals(right, "Auto", StringComparison.OrdinalIgnoreCase)) ||
        (string.Equals(right, "Lenovo Auto", StringComparison.OrdinalIgnoreCase) && string.Equals(left, "Auto", StringComparison.OrdinalIgnoreCase));

    private static bool IsManualProfile(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().StartsWith("Manual ", StringComparison.OrdinalIgnoreCase);

    private string RuntimeProfileIdForDisplay(string? profileName)
    {
        if (IsManualProfile(profileName))
            return profileName!.Trim();

        string runtime = profileName?.Trim() ?? string.Empty;
        if (runtime.Length == 0 || runtime.Equals("Lenovo Auto", StringComparison.OrdinalIgnoreCase) ||
            runtime.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return "Lenovo Auto";
        }
        if (runtime.Equals("Quiet", StringComparison.OrdinalIgnoreCase) || runtime.Equals("Silent", StringComparison.OrdinalIgnoreCase))
            return FanCurveDefaults.QuietId;
        if (runtime.Equals("Balanced", StringComparison.OrdinalIgnoreCase) || runtime.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            return FanCurveDefaults.BalancedId;
        if (runtime.Equals("Max cooling", StringComparison.OrdinalIgnoreCase) || runtime.Equals("Cool", StringComparison.OrdinalIgnoreCase))
            return FanCurveDefaults.MaxCoolingId;

        // A custom profile has a display name in AppState. Reuse the persisted id only
        // when that id resolves to the same runtime name; never let an unrelated saved
        // preference paint the selector as applied.
        if (_app?.FanProfiles.Find(_app.UserSettings.Current.CoolingProfile) is FanCurveDefinition persisted &&
            string.Equals(persisted.Name, runtime, StringComparison.OrdinalIgnoreCase))
        {
            return persisted.Id;
        }

        return runtime;
    }

    private static string DisplayProfile(string? raw) => raw?.Trim() switch
    {
        null or "" or "Lenovo Auto" or "Auto" => "Auto",
        "Silent" => "Quiet",
        "Normal" => "Balanced",
        "Cool" or "Max cooling" => "Max cooling",
        string value => value
    };

    private void ProfileComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (_app is null || _app.FanCalibrationState.Running)
            return;
        SyncProfileSelector(_app.State.CoolingProfile, RuntimeProfileIdForDisplay(_app.State.CoolingProfile));
        ProfileComboBox.IsDropDownOpen = true;
    }

    private async void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingProfileSelection || _app is null || ProfileComboBox.SelectedItem is not FanProfileChoice choice)
            return;
        if (!choice.Selectable || ProfileIdsEqual(choice.Id, _currentProfileId))
            return;

        ProfileComboBox.IsEnabled = false;
        try
        {
            if (!await _app.SetCoolingProfileAsync(choice.Id))
            {
                CoolingDetailText.Text = _app.LastCoolingError ??
                    "The cooling profile could not be applied. Check System for hardware access.";
                SyncProfileSelector(_app.State.CoolingProfile, RuntimeProfileIdForDisplay(_app.State.CoolingProfile));
                return;
            }

            SyncProfileSelector(_app.State.CoolingProfile, RuntimeProfileIdForDisplay(_app.State.CoolingProfile));
        }
        finally
        {
            ProfileComboBox.IsEnabled =
                _app.State.CanFanControl &&
                !_app.FanCalibrationState.Running;
        }
    }

    private void EditCurves_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || _app.FanCalibrationState.Required || !HasDirectFanWriter)
            return;
        ProfileComboBox.IsDropDownOpen = false;
        var editor = new FanCurveEditorWindow(_app) { Owner = Window.GetWindow(this) };
        editor.ShowDialog();
        SyncProfileSelector(_app.State.CoolingProfile, RuntimeProfileIdForDisplay(_app.State.CoolingProfile));
        if (IsVisible)
            _ = _app.HardwareClient.GetStatusAsync();
    }

    private void ManualPercentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ManualPercentValue is not null)
            ManualPercentValue.Text = $"{Math.Round(e.NewValue):0}%";
    }

    private async void ManualPercentApply_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not Button button || _app.FanCalibrationState.Required || !HasDirectFanWriter)
            return;
        int percent = (int)Math.Round(ManualPercentSlider.Value);
        button.IsEnabled = false;
        try
        {
            if (!await _app.SetManualFanPercentAsync(percent))
                CoolingDetailText.Text = "Couldn’t apply the temporary fan target. Retry, or open System if it keeps failing.";
        }
        finally { button.IsEnabled = HasDirectFanWriter && !_app.FanCalibrationState.Required; }
    }

    private async void RecoverAuto_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;
        RecoverAutoButton.IsEnabled = false;
        ShowAutoRecoveryResult("Returning control to Lenovo Auto…");
        try
        {
            // Explicit user Auto is the only recovery action allowed to clear a
            // verified full-speed bit left by another service instance/utility.
            bool success = await _app.SetCoolingProfileAsync("Lenovo Auto");
            if (!success)
                ShowAutoRecoveryResult(_app.LastCoolingError ?? "Lenovo Auto was not confirmed.");
            else
            {
                ConfirmAutoRecovery();
            }
            SyncProfileSelector(_app.State.CoolingProfile, RuntimeProfileIdForDisplay(_app.State.CoolingProfile));
        }
        finally
        {
            RecoverAutoButton.IsEnabled = true;
        }
    }

    private void ShowAutoRecoveryResult(string message)
    {
        // This records the explicit action's result, not a continuously verified
        // ownership state. Ordinary telemetry refreshes must not erase it.
        AutoRecoveryResultText.Text = message;
        AutoRecoveryResultText.Visibility = Visibility.Visible;
    }

    private void ConfirmAutoRecovery()
    {
        _autoRecoveryConfirmed = true;
        RecoverAutoButton.Visibility = Visibility.Collapsed;
        AppliedLevelText.Text = "Auto confirmed";
        ShowAutoRecoveryResult("Last recovery: Lenovo Auto confirmed.");
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || sender is not Button button)
            return;
        button.IsEnabled = false;
        try { _ = await _app.ResetFanDefaultsAsync(); }
        finally { button.IsEnabled = true; }
    }

    private void ApplyProviderCopy(bool canControl, string fanControlKind)
    {
        bool firmwarePolicy = canControl && string.Equals(fanControlKind, FanControlKinds.FirmwarePolicy, StringComparison.Ordinal);
        bool oemTargetRpm = canControl && string.Equals(fanControlKind, FanControlKinds.OemTargetRpm, StringComparison.Ordinal);
        bool discreteEcWriter = canControl && string.Equals(fanControlKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal);
        bool directWriter = oemTargetRpm || discreteEcWriter;
        AdvancedFanControlsExpander.Visibility = directWriter ? Visibility.Visible : Visibility.Collapsed;

        FanMappingDetailText.Text = fanControlKind == FanControlKinds.FullSpeedOnly
            ? "Auto and Max cooling are available. Lower fixed speeds are unavailable."
            : !canControl
            ? "Fan controls are unavailable right now."
            : firmwarePolicy
                ? "Custom curves are unavailable with this controller."
                : directWriter
                    ? "Custom curves and temporary fan tests are available."
                    : "Advanced controls depend on the active fan controller.";
        FanProviderDetailText.ToolTip = null;
        if (discreteEcWriter)
            FanMappingDetailText.Text = "Targets use the next measured speed. 0% keeps the fan running; gaps between available speeds can be large.";

        // Raw EC diagnostics exist only for a provider that explicitly advertises
        // the discrete-EC semantic contract. They are never a generic laptop option.
        RawEcStepsExpander.Visibility = Visibility.Collapsed;
        ManualControlExpander.Visibility = directWriter ? Visibility.Visible : Visibility.Collapsed;
        ManualControlDescriptionText.Text = firmwarePolicy
            ? "Manual percentage tests are intentionally unavailable on the firmware-policy backend. Use the built-in profiles above; they keep Lenovo's own smooth fan loop in control."
            : oemTargetRpm
                ? "Temporary 30-second test. 0% requests the provider-reported minimum running target and 100% its reported maximum target RPM; the previous profile is restored automatically. Firmware Auto remains a separate ownership state."
                : discreteEcWriter
                    ? "Try a measured speed for 30 seconds. Targets use the next available speed; low targets keep the fan running. The previous profile is restored afterwards."
                    : "Temporary tests use only the active provider's verified output range and restore the previous profile automatically. Provider-specific raw diagnostics appear only when that exact semantic contract is exposed.";
        ManualControlExpander.IsEnabled = directWriter;
        EditCurvesButton.IsEnabled = directWriter && !_app?.FanCalibrationState.Required == true;
        EditCurvesButton.Visibility = directWriter ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string ResolveFanControlKind(string? explicitKind, bool canControl)
    {
        if (!canControl)
            return FanControlKinds.None;
        if (string.Equals(explicitKind, FanControlKinds.FirmwarePolicy, StringComparison.Ordinal) ||
            string.Equals(explicitKind, FanControlKinds.FullSpeedOnly, StringComparison.Ordinal) ||
            string.Equals(explicitKind, FanControlKinds.OemTargetRpm, StringComparison.Ordinal) ||
            string.Equals(explicitKind, FanControlKinds.DiscreteEc, StringComparison.Ordinal))
        {
            return explicitKind!;
        }
        return FanControlKinds.None;
    }

    private static string DescribeUnavailable(bool telemetryReady)
    {
        return telemetryReady
            ? "Fan readings are available. Manual profiles are unavailable; firmware controls cooling."
            : "Firmware controls cooling. Fan readings and manual profiles are unavailable; review System for provider status.";
    }

    private async void Characterize_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;
        CharacterizeButton.IsEnabled = false;
        CharacterizationTitleText.Text = "Starting fan calibration…";
        if (!await _app.StartFanCharacterizationAsync())
        {
            CharacterizationStatusText.Text = _app.State.HardwareAccess;
            CharacterizeButton.IsEnabled = true;
        }
    }

    private async void StopCharacterization_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null)
            return;
        StopCharacterizationButton.IsEnabled = false;
        CharacterizationStatusText.Text = "Stopping calibration and returning fan ownership to firmware Auto…";
        if (!await _app.StopFanCharacterizationAsync())
        {
            CharacterizationStatusText.Text = _app.State.HardwareAccess;
            StopCharacterizationButton.IsEnabled = true;
        }
    }

    private async void ManualLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_app is null || _app.FanCalibrationState.Required || !HasDirectFanWriter ||
            sender is not FrameworkElement { Tag: string raw } || !int.TryParse(raw, out int level))
        {
            return;
        }
        ServiceResponse? response = await _app.HardwareClient.SetFanLevelAsync(level);
        if (response?.Success != true)
        {
            _app.State.HardwareAccess = response?.Error ?? "Manual fan control unavailable";
            _ = _app.HardwareClient.GetStatusAsync();
        }
    }

    private sealed record CalibrationRow(string LevelText, string RpmText, string StabilityText);
    private sealed record FanProfileChoice(string Id, string Name, bool Selectable = true);
}
