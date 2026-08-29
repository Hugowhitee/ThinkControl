using ThinkControl.Core.Ipc;

namespace ThinkControl.UI.Controls;

public partial class FansPanel
{
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
    /// Visual-QA only: applies a deterministic verified-X9 OEM target-RPM fixture.
    /// It changes presentation state only; no hardware client request is issued.
    /// </summary>
    internal void PrepareOemTargetRpmForSnapshot(int percent = 75)
    {
        int targetPercent = Math.Clamp(percent, 0, 100);
        _fanControlKind = FanControlKinds.OemTargetRpm;

        if (_app is not null)
        {
            var state = _app.State;
            state.HardwareAccess =
                "Full · X9 Lenovo OEM target-RPM fan control + keyboard · Lenovo Other Mode · Fan 1 1,800–5,300 RPM · Fan 2 1,700–5,200 RPM";
            state.FanStateText = "ThinkControl managed · Lenovo OEM target RPM";
            state.ApplyHardwareTelemetry(
            [
                new FanTelemetrySnapshot("lenovo-other-mode-1", "Fan 1", 3650, "Lenovo WMI · Other Mode target-RPM provider", true),
                new FanTelemetrySnapshot("lenovo-other-mode-2", "Fan 2", 3510, "Lenovo WMI · Other Mode target-RPM provider", true)
            ],
            state.Sensors.ToArray());

            ApplyProviderCopy(state, true, FanControlKinds.OemTargetRpm);
            CoolingDetailText.Text = "Balanced · continuous Lenovo OEM target-RPM control";
            AppliedLevelText.Text = $"{targetPercent}% OEM target";
            LiveCurveStatus.Text = $"{state.ControlTemperatureText} · temporary {targetPercent}% OEM target · 3,650 / 3,510 RPM";
        }

        ManualPercentSlider.Value = targetPercent;
        ManualPercentValue.Text = $"{targetPercent}%";
        ManualControlExpander.IsExpanded = true;
        UpdateLayout();
    }
}
