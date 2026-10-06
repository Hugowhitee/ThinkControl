using System.Windows;
using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class DiagnosticsPanel
{
    private bool _crashQueueSnapshotRequested;

    /// <summary>
    /// Dedicated deterministic state for diagnostics visual QA. These captures
    /// intentionally exercise the unknown-device lifecycle even though the general
    /// demo machine is a verified X9.
    /// </summary>
    internal void PrepareLifecycleForSnapshot(AppState state, DiagnosticsConsent consent)
    {
        _syncing = true;
        try
        {
            bool sharingEnabled = consent == DiagnosticsConsent.Enabled;
            bool reportReady = state.CanSensorTelemetry &&
                               state.CanFanTelemetry &&
                               state.CanFanControl &&
                               state.CanKeyboardBacklight;

            DiagnosticsSwitch.IsChecked = sharingEnabled;
            DiagnosticsSwitch.IsEnabled = true;
            DiagnosticsSwitch.Visibility = Visibility.Visible;
            DiagnosticsSwitch.ToolTip = "Prepare a compact compatibility report locally. Nothing is uploaded automatically.";
            LastEventText.Text = "Last local activity: just now";
            CrashCard.Visibility = Visibility.Collapsed;
            CrashSeparator.Visibility = Visibility.Collapsed;
            LearningCard.Visibility = Visibility.Visible;
            SharingRow.Visibility = Visibility.Visible;
            ShareDeviceButton.Visibility = Visibility.Visible;
            LearningProgress.Maximum = 5;

            if (reportReady)
            {
                CompatibilityStateText.Text = "Compatibility report ready";
                CompatibilityDetailText.Text = "5 of 5 checks completed. Compatibility report ready.";
                LearningTitleText.Text = "Compatibility evidence complete";
                LearningProgress.Value = 5;
                LearningProgressText.Text = "5/5";
                SharingStateText.Text = sharingEnabled
                    ? "New compatibility findings are ready to review"
                    : "Report ready. Enable review to open a GitHub draft.";
                ShareDeviceButton.Content = "Review report";
                ShareDeviceButton.IsEnabled = sharingEnabled;
                StatusText.Text = "The compatibility report stays local until you explicitly review it.";
            }
            else
            {
                CompatibilityStateText.Text = "New device: learning";
                CompatibilityDetailText.Text = "3 of 5 checks completed. Learning continues in the background.";
                LearningTitleText.Text = "Learning compatibility";
                LearningProgress.Value = 3;
                LearningProgressText.Text = "3/5";
                SharingStateText.Text = sharingEnabled
                    ? "No report is ready yet. Keep using ThinkControl normally."
                    : "Learning stays on this device. Report review is disabled.";
                ShareDeviceButton.Content = "Review report";
                ShareDeviceButton.IsEnabled = false;
                StatusText.Text = "Compatibility learning stays local. Nothing is uploaded automatically.";
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    internal void PrepareCrashQueueForSnapshot()
    {
        _crashQueueSnapshotRequested = true;
        ApplyCrashQueueSnapshotState();
    }

    private void ApplyCrashQueueSnapshotState()
    {
        bool previousSyncing = _syncing;
        _syncing = true;
        try
        {
            const string latestId = "snapshot-latest";
            _selectedCrashId = latestId;
            CrashCard.Visibility = Visibility.Visible;
            CrashSeparator.Visibility = Visibility.Visible;
            CrashTitleText.Text = "Saved crashes: 3";
            CrashSummaryText.Text = "NotSupportedException: ToolTip property contract (today 14:32). Repeated 2 times. 2 previous crashes still unresolved.";
            CrashHistoryCombo.ItemsSource = new[]
            {
                new CrashHistoryOption(latestId, "NotSupportedException: today 14:32, 2 occurrences"),
                new CrashHistoryOption("snapshot-previous", "InvalidOperationException: today 13:58"),
                new CrashHistoryOption("snapshot-oldest", "COMException: yesterday 22:11")
            };
            CrashHistoryCombo.SelectedValue = latestId;
            CrashHistoryCombo.Visibility = Visibility.Visible;
            MarkCrashReportedButton.Visibility = Visibility.Visible;
            OpenCrashDraftButton.Content = "Reopen GitHub draft";
            CrashStateText.Text = "GitHub draft opened. Mark it as reported after submitting.";
        }
        finally
        {
            _syncing = previousSyncing;
        }
    }

    internal bool BringCrashQueueIntoViewForSnapshot()
    {
        // Snapshot setup may run before WPF Loaded/Refresh. Re-apply the requested
        // synthetic state after Measure/Arrange so runtime refresh cannot erase the
        // evidence immediately before capture.
        if (_crashQueueSnapshotRequested)
            ApplyCrashQueueSnapshotState();

        if (CrashCard.Visibility != Visibility.Visible)
            return false;

        CrashCard.UpdateLayout();
        CrashCard.BringIntoView();
        return true;
    }
}
