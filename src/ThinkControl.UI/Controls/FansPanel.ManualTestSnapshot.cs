using ThinkControl.Core.Ipc;
using ThinkControl.Core.Cooling;

namespace ThinkControl.UI.Controls;

public partial class FansPanel
{
    internal void PrepareMeasuredCurveForSnapshot()
    {
        if (_app is null) return;
        _fanControlKind = FanControlKinds.DiscreteEc;
        _app.State.FanControlKind = _fanControlKind;
        _app.State.ControlTemperatureC = 66;
        _app.State.ApplyHardwareTelemetry(
            [new FanTelemetrySnapshot("measured-shared", "Shared tachometer", 4400, "Measured provider fixture", true, true)], []);
        ApplyProviderCopy(true, _fanControlKind);
        ApplyCalibrationUi(new FanCalibrationUiState(true, false, true, 5, 5, "Five speeds measured"), true);
        _syncingProfileSelection = true;
        try
        {
            var choice = new FanProfileChoice("custom:measured-snapshot", "My curve");
            _profileChoices.Add(choice);
            ProfileComboBox.SelectedItem = choice;
        }
        finally { _syncingProfileSelection = false; }
        _activeCurveGraph.SetCurve([new(35, 37), new(60, 43), new(75, 47), new(85, 100), new(92, 100)]);
        _activeCurveGraph.SetLiveState(66, 45, 4400);
        LiveCurveStatus.Text = "66.0 °C → 45% target, current speed: 4,400 RPM";
        CoolingDetailText.Text = "My curve is active.";
        AppliedLevelText.Text = "Approx. 47%";
        CoolingOwnerText.Text = "Direct control";
        ActiveCurvePreview.Visibility = System.Windows.Visibility.Visible;
        AdvancedFanControlsExpander.IsExpanded = true;
        UpdateLayout();
    }

    /// <summary>
    /// Visual-QA only: makes the baseline discrete-provider fixture exercise the
    /// generic calibration prerequisite without teaching production UI about a
    /// machine type or parsing provider-detail text.
    /// </summary>
    internal void PrepareCalibrationRequiredForSnapshot()
    {
        if (_app is null)
            return;

        _fanControlKind = FanControlKinds.DiscreteEc;
        _app.State.FanControlKind = FanControlKinds.DiscreteEc;
        _app.State.FanStateText = "Firmware Auto";
        ApplyProviderCopy(_app.State.CanFanControl, _fanControlKind);
        ApplyCalibrationUi(
            new FanCalibrationUiState(
                Relevant: true,
                Running: false,
                Ready: false,
                CompletedLevels: 0,
                TotalLevels: 7,
                Status: string.Empty),
            _app.State.CanFanControl);
        AppliedLevelText.Text = _app.State.FanStateText;
        UpdateLayout();
    }

    /// <summary>
    /// Visual-QA only: shows the production temporary-test controls without sending
    /// a fan command or starting the real timeout timer. The deterministic fixture
    /// deliberately uses the richer OEM target-RPM path so screenshots exercise the
    /// provider-specific copy, dual-fan telemetry and hidden EC-only controls.
    /// </summary>
    internal void PrepareManualFanTestForSnapshot(string label = "72% target", int secondsRemaining = 21)
    {
        PrepareOemTargetRpmForSnapshot(72);
        ConfigureManualFanTestSafety();
        _manualFanTestTimer.Stop();
        _manualFanRestoreProfile = "Balanced";
        _manualFanTestActive = true;
        _manualFanTestEnding = false;
        _manualFanTestEndsAt = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(secondsRemaining, 1, ManualFanTestDurationSeconds));
        UpdateManualFanTestUi(label);
        ManualControlExpander.IsExpanded = true;
        UpdateLayout();
    }

    /// <summary>
    /// Visual-QA only: renders a real named curve through the direct-writer presentation
    /// path, including its directly accessible editing action.
    /// </summary>
    internal void PrepareActiveFanCurveForSnapshot()
    {
        PrepareOemTargetRpmForSnapshot(72);
        if (_app is null)
            return;

        SyncProfileSelector("Balanced", RuntimeProfileIdForDisplay("Balanced"));
        UpdateActiveCurvePreview(
            ProfileComboBox.SelectedItem as FanProfileChoice,
            _app.State.ControlTemperatureC,
            _app.State.FanRpm);
        if (_app.State.ControlTemperatureC is double temperature)
            AppliedLevelText.Text = $"{FanCurveGraphPolicy.ResolvePercent(FanCurveDefaults.Balanced.Points, temperature)}% OEM target";
        AdvancedFanControlsExpander.IsExpanded = false;
        ManualControlExpander.IsExpanded = false;
        UpdateLayout();
    }

    /// <summary>
    /// Visual-QA only: applies a deterministic OEM target-RPM fixture.
    /// It changes presentation state only; no hardware client request is issued.
    /// </summary>
    internal void PrepareOemTargetRpmForSnapshot(int percent = 75)
    {
        int targetPercent = Math.Clamp(percent, 0, 100);
        _fanControlKind = FanControlKinds.OemTargetRpm;

        if (_app is not null)
        {
            var state = _app.State;
            state.FanControlKind = FanControlKinds.OemTargetRpm;
            state.HardwareAccess =
                "Full · verified OEM target-RPM fan provider · Fan 1 1,800–5,300 RPM · Fan 2 1,700–5,200 RPM";
            state.FanStateText = "ThinkControl managed: target RPM";
            state.ApplyHardwareTelemetry(
            [
                new FanTelemetrySnapshot("oem-target-rpm-1", "Fan 1", 3650, "OEM target-RPM provider", true),
                new FanTelemetrySnapshot("oem-target-rpm-2", "Fan 2", 3510, "OEM target-RPM provider", true)
            ],
            state.Sensors.ToArray());

            ApplyProviderCopy(true, FanControlKinds.OemTargetRpm);
            ApplyCalibrationUi(FanCalibrationUiState.None, canControl: true);
            CoolingOwnerText.Text = "Direct control";
            CoolingDetailText.Text = "Balanced: target RPM control";
            AppliedLevelText.Text = $"{targetPercent}% OEM target";
            LiveCurveStatus.Text = $"Temperature: {state.ControlTemperatureText}, temporary target: {targetPercent}%, speeds: 3,650 / 3,510 RPM";
        }

        ManualPercentSlider.Value = targetPercent;
        ManualPercentValue.Text = $"{targetPercent}%";
        UpdateLayout();
    }
}
