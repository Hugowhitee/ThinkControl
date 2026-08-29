namespace ThinkControl.UI.Controls;

public partial class FansPanel
{
    /// <summary>
    /// Visual-QA only: shows the production temporary-test controls without sending
    /// a fan command or starting the real timeout timer.
    /// </summary>
    internal void PrepareManualFanTestForSnapshot(string label = "72% target", int secondsRemaining = 21)
    {
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
    /// Visual-QA only: expands the normal manual target surface after an OEM
    /// target-RPM provider fixture has already been applied by PrepareForSnapshot.
    /// This deliberately does not start a temporary test or mutate hardware state.
    /// </summary>
    internal void PrepareOemTargetRpmForSnapshot(int percent = 75)
    {
        ManualPercentSlider.Value = Math.Clamp(percent, 0, 100);
        ManualControlExpander.IsExpanded = true;
        UpdateLayout();
    }
}
