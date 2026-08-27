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
            LastEventText.Text = "Last local activity · just now";
            CrashCard.Visibility = Visibility.Collapsed;
            CrashSeparator.Visibility = Visibility.Collapsed;
            LearningCard.Visibility = Visibility.Visible;
            SharingRow.Visibility = Visibility.Visible;
            ShareDeviceButton.Visibility = Visibility.Visible;
            LearningProgress.Maximum = 5;

            if (reportReady)
            {
                CompatibilityStateText.Text = "Compatibility report ready";
                CompatibilityDetailText.Text = "5/5 checks · stable provider and control evidence collected.";
                LearningTitleText.Text = "Compatibility evidence complete";
                LearningProgress.Value = 5;
                LearningProgressText.Text = "5/5";
                SharingStateText.Text = sharingEnabled
                    ? "New compatibility findings are ready to review"
                    : "Report is ready locally · enable review to open the GitHub draft";
                ShareDeviceButton.Content = "Review report";
                ShareDeviceButton.IsEnabled = sharingEnabled;
                StatusText.Text = "The compatibility report stays local until you explicitly review it.";
            }
            else
            {
                CompatibilityStateText.Text = "New device · learning";
                CompatibilityDetailText.Text = "3/5 checks · learning continues quietly while you use ThinkControl.";
                LearningTitleText.Text = "Learning compatibility";
                LearningProgress.Value = 3;
                LearningProgressText.Text = "3/5";
                SharingStateText.Text = sharingEnabled
                    ? "Keep using ThinkControl normally · no report is ready yet"
                    : "Learning stays local · report review is disabled";
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
        CrashCard.Visibility = Visibility.Visible;
        CrashSeparator.Visibility = Visibility.Visible;
        CrashTitleText.Text = "Crashes preserved · 3";
        CrashSummaryText.Text = "NotSupportedException · ToolTip property contract · today 14:32 · repeated 2 times · 2 previous unresolved";
        CrashHistoryCombo.ItemsSource = new[]
        {
            new CrashHistoryOption("snapshot-latest", "NotSupportedException · ×2 · today 14:32"),
            new CrashHistoryOption("snapshot-previous", "InvalidOperationException · today 13:58"),
            new CrashHistoryOption("snapshot-oldest", "COMException · yesterday 22:11")
        };
        CrashHistoryCombo.SelectedValue = "snapshot-latest";
        CrashHistoryCombo.Visibility = Visibility.Visible;
        MarkCrashReportedButton.Visibility = Visibility.Visible;
    }

    internal bool BringCrashQueueIntoViewForSnapshot()
    {
        // This hook is called after the snapshot window has completed Measure /
        // Arrange / Loaded. Re-materialize the requested synthetic state here so
        // normal runtime Refresh() calls can never erase the evidence before capture.
        if (_crashQueueSnapshotRequested)
            ApplyCrashQueueSnapshotState();

        if (CrashCard.Visibility != Visibility.Visible)
            return false;

        CrashCard.UpdateLayout();
        CrashCard.BringIntoView();
        return true;
    }
}
