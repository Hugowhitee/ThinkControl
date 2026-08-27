using System.Windows;
using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class DiagnosticsPanel
{
    /// <summary>
    /// Dedicated deterministic state for the diagnostics visual-QA snapshots.
    /// These captures intentionally exercise the unknown-device lifecycle even
    /// though the general demo machine is a verified X9.
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
            DiagnosticsSwitch.ToolTip = "Allow ThinkControl to prepare a redacted compatibility report locally. Nothing is uploaded automatically.";
            EventCountText.Text = reportReady ? "18" : "7";
            LastEventText.Text = "Just now";
            CrashCard.Visibility = Visibility.Collapsed;
            LearningCard.Visibility = Visibility.Visible;
            ShareDeviceButton.Visibility = Visibility.Visible;
            LearningProgress.Maximum = 5;

            if (reportReady)
            {
                CompatibilityStateText.Text = "Device report ready";
                CompatibilityDetailText.Text = "5/5 checks · stable provider and control evidence collected";
                LearningTitleText.Text = "Background learning complete";
                LearningProgress.Value = 5;
                LearningProgressText.Text = "5/5";
                SharingStateText.Text = sharingEnabled
                    ? "New compatibility findings are ready to review"
                    : "Report is ready locally · enable sharing to review it on GitHub";
                ShareDeviceButton.Content = "Review report";
                ShareDeviceButton.IsEnabled = sharingEnabled;
                StatusText.Text = "Compatibility learning is complete. The redacted report stays local until you explicitly open the reviewed GitHub draft.";
            }
            else
            {
                CompatibilityStateText.Text = "New device · learning";
                CompatibilityDetailText.Text = "3/5 checks · learning continues quietly while you use ThinkControl";
                LearningTitleText.Text = "Background learning";
                LearningProgress.Value = 3;
                LearningProgressText.Text = "3/5";
                SharingStateText.Text = sharingEnabled
                    ? "No report yet · keep using ThinkControl normally"
                    : "Learning locally · sharing is disabled";
                ShareDeviceButton.Content = "Review report";
                ShareDeviceButton.IsEnabled = false;
                StatusText.Text = "Compatibility learning runs quietly in the background. Nothing is uploaded automatically.";
            }
        }
        finally
        {
            _syncing = false;
        }
    }
}
