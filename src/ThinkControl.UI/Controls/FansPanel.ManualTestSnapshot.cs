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
}
