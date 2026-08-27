using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;

namespace ThinkControl.UI.Controls;

public partial class DiagnosticsPanel : System.Windows.Controls.UserControl
{
    private bool _syncing;
    private App? _subscribedApp;

    public DiagnosticsPanel()
    {
        InitializeComponent();
        Loaded += DiagnosticsPanel_Loaded;
        Unloaded += DiagnosticsPanel_Unloaded;
    }

    private void DiagnosticsPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            if (!ReferenceEquals(_subscribedApp, app))
            {
                if (_subscribedApp is not null)
                    _subscribedApp.DeviceSupportStatusChanged -= App_DeviceSupportStatusChanged;
                _subscribedApp = app;
                app.DeviceSupportStatusChanged += App_DeviceSupportStatusChanged;
            }
        }
        Refresh();
    }

    private void DiagnosticsPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedApp is not null)
            _subscribedApp.DeviceSupportStatusChanged -= App_DeviceSupportStatusChanged;
        _subscribedApp = null;
    }

    private void App_DeviceSupportStatusChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(Refresh);
            return;
        }
        Refresh();
    }

    public void Refresh()
    {
        if (System.Windows.Application.Current is not App app)
            return;

        _syncing = true;
        try
        {
            DiagnosticsConsent consent = app.UserSettings.Current.DiagnosticsConsent;
            DeviceSupportStatus status = app.DeviceSupportStatus;
            bool verified = status.Phase == DeviceSupportPhase.Verified;
            bool sharingEnabled = consent == DiagnosticsConsent.Enabled;

            DiagnosticsSwitch.IsChecked = sharingEnabled;
            DiagnosticsSwitch.IsEnabled = !verified;
            DiagnosticsSwitch.ToolTip = verified
                ? "This device already has a verified ThinkControl profile; compatibility learning is not needed."
                : "Allow ThinkControl to prepare a redacted compatibility report locally. Nothing is uploaded automatically.";

            CompatibilityStateText.Text = status.Label;
            CompatibilityDetailText.Text = status.Detail;

            if (verified)
            {
                CompatibilityStateText.Text = "Supported device · no compatibility learning required";
                LearningCard.Visibility = Visibility.Collapsed;
                SharingStateText.Text = "Known profile · no compatibility report is needed";
                ShareDeviceButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                LearningCard.Visibility = Visibility.Visible;
                ShareDeviceButton.Visibility = Visibility.Visible;
                LearningProgress.Maximum = Math.Max(1, status.TotalChecks);
                LearningProgress.Value = Math.Clamp(status.CompletedChecks, 0, Math.Max(1, status.TotalChecks));
                LearningProgressText.Text = $"{Math.Max(0, status.CompletedChecks)}/{Math.Max(0, status.TotalChecks)}";

                switch (status.Phase)
                {
                    case DeviceSupportPhase.Learning:
                        LearningTitleText.Text = "Background learning";
                        SharingStateText.Text = sharingEnabled
                            ? "No report yet · keep using ThinkControl normally"
                            : "Learning locally · sharing is disabled";
                        ShareDeviceButton.Content = "Review report";
                        ShareDeviceButton.IsEnabled = false;
                        break;
                    case DeviceSupportPhase.ReadyToShare:
                        LearningTitleText.Text = "Background learning complete";
                        SharingStateText.Text = sharingEnabled
                            ? "New compatibility findings are ready to review"
                            : "Report is ready locally · enable sharing to review it on GitHub";
                        ShareDeviceButton.Content = "Review report";
                        ShareDeviceButton.IsEnabled = sharingEnabled;
                        break;
                    case DeviceSupportPhase.Shared:
                        LearningTitleText.Text = "Compatibility profile learned";
                        SharingStateText.Text = "Shared · no new compatibility findings";
                        ShareDeviceButton.Content = "No new report";
                        ShareDeviceButton.IsEnabled = false;
                        break;
                    default:
                        ShareDeviceButton.IsEnabled = false;
                        break;
                }
            }

            CrashReport? crash = app.PendingCrashReport;
            CrashCard.Visibility = crash is null ? Visibility.Collapsed : Visibility.Visible;
            if (crash is not null)
            {
                string when = DateTimeOffset.TryParse(crash.TimestampUtc, out DateTimeOffset timestamp)
                    ? timestamp.ToLocalTime().ToString("g")
                    : "previous run";
                CrashSummaryText.Text = string.IsNullOrWhiteSpace(crash.Message)
                    ? $"{crash.ExceptionType} · {when}"
                    : $"{crash.ExceptionType}: {crash.Message} · {when}";
            }

            EventCountText.Text = app.DiagnosticsRecorder.LocalEventCount.ToString();
            LastEventText.Text = app.DiagnosticsRecorder.LastEventAtUtc is DateTimeOffset last
                ? last.ToLocalTime().ToString("g")
                : "—";

            StatusText.Text = verified
                ? "Routine troubleshooting data stays bounded on this PC. Crash and support reports are never uploaded automatically."
                : status.Phase == DeviceSupportPhase.Shared
                    ? "That compatibility fingerprint has already been handled. ThinkControl will only surface another report after materially new evidence appears."
                    : "Compatibility learning runs quietly in the background. Reports stay local until you explicitly open a reviewed draft on GitHub.";
        }
        finally
        {
            _syncing = false;
        }
    }

    internal void PrepareForSnapshot(AppState state, DiagnosticsConsent consent)
    {
        _syncing = true;
        try
        {
            bool verified = GetValidationState(state.MachineType) == DeviceValidationState.Verified;
            DiagnosticsSwitch.IsChecked = consent == DiagnosticsConsent.Enabled;
            DiagnosticsSwitch.IsEnabled = !verified;
            CompatibilityStateText.Text = verified
                ? "Supported device · no compatibility learning required"
                : "New device · learning";
            EventCountText.Text = verified ? "12" : "18";
            LastEventText.Text = "Just now";
            CrashCard.Visibility = Visibility.Collapsed;

            if (verified)
            {
                LearningCard.Visibility = Visibility.Collapsed;
                SharingStateText.Text = "Known profile · no compatibility report is needed";
                ShareDeviceButton.Visibility = Visibility.Collapsed;
                StatusText.Text = "Routine troubleshooting data stays bounded on this PC. Nothing is uploaded automatically.";
            }
            else
            {
                LearningCard.Visibility = Visibility.Visible;
                LearningTitleText.Text = "Background learning";
                LearningProgress.Maximum = 5;
                LearningProgress.Value = 3;
                LearningProgressText.Text = "3/5";
                CompatibilityDetailText.Text = "3/5 checks · learning continues quietly while you use ThinkControl";
                SharingStateText.Text = "No report yet · keep using ThinkControl normally";
                ShareDeviceButton.Visibility = Visibility.Visible;
                ShareDeviceButton.IsEnabled = false;
                ShareDeviceButton.Content = "Review report";
                StatusText.Text = "Compatibility learning runs quietly in the background. Nothing is uploaded automatically.";
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void DiagnosticsSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing || System.Windows.Application.Current is not App app)
            return;
        if (app.DeviceSupportStatus.Phase == DeviceSupportPhase.Verified)
        {
            Refresh();
            return;
        }

        DiagnosticsConsent consent = DiagnosticsSwitch.IsChecked == true
            ? DiagnosticsConsent.Enabled
            : DiagnosticsConsent.Disabled;
        app.UserSettings.Update(settings => settings with { DiagnosticsConsent = consent });
        if (consent == DiagnosticsConsent.Disabled)
            DeviceSupportReportService.DeletePreparedReport();
        app.RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "diagnostics.consent_changed",
            ValidationState: GetValidationState(app.State.MachineType),
            Success: true,
            Tags: new Dictionary<string, string> { ["state"] = consent.ToString() }));
        Refresh();
    }

    private void ShareDeviceReport_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;
        if (app.UserSettings.Current.DiagnosticsConsent != DiagnosticsConsent.Enabled)
        {
            StatusText.Text = "Enable compatibility sharing first. Nothing is submitted automatically.";
            return;
        }

        if (!app.OpenCurrentDeviceReportOnGitHub())
        {
            Refresh();
            StatusText.Text = "There is no new stable compatibility report to review.";
            return;
        }

        Refresh();
        StatusText.Text = "Opened a pre-filled GitHub draft and marked this compatibility fingerprint handled locally. It will not be offered again unless new evidence appears.";
    }

    private void ReportCrash_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;
        if (!app.OpenPendingCrashReportOnGitHub())
        {
            Refresh();
            return;
        }
        Refresh();
        StatusText.Text = "Opened the redacted crash draft on GitHub. Nothing is submitted until you press Submit new issue.";
    }

    private void DismissCrash_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;
        app.DismissPendingCrashReport();
        Refresh();
        StatusText.Text = "Crash report dismissed and removed from this PC.";
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;
        string path = Path.Combine(Path.GetTempPath(), "ThinkControl-diagnostics-preview.json");
        WriteBundle(app, path);
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
            StatusText.Text = "Opened a redacted diagnostics preview in Notepad.";
        }
        catch
        {
            StatusText.Text = $"Diagnostics preview saved to {path}";
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;
        var dialog = new SaveFileDialog
        {
            Title = "Export ThinkControl support bundle",
            Filter = "ThinkControl diagnostics (*.json)|*.json",
            FileName = $"ThinkControl-Support-{SafeFileToken(app.State.MachineType)}-{DateTime.Now:yyyyMMdd-HHmm}.json",
            AddExtension = true,
            DefaultExt = ".json"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;
        WriteBundle(app, dialog.FileName);
        StatusText.Text = "Redacted support bundle exported.";
    }

    private void OpenHardwareLog_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ThinkControl", "hardware-service.log");
        if (!File.Exists(path))
        {
            StatusText.Text = "No hardware-service log exists yet.";
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
            StatusText.Text = "Opened the local hardware-service log in Notepad.";
        }
        catch
        {
            StatusText.Text = $"Hardware-service log: {path}";
        }
    }

    private void OpenBugReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml") { UseShellExecute = true });
        }
        catch { }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;
        MessageBoxResult result = MessageBox.Show(
            Window.GetWindow(this),
            "Delete local ThinkControl diagnostics, compatibility lifecycle state and any pending crash report?",
            "Delete diagnostics",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        app.DiagnosticsRecorder.DeleteLocal();
        app.ResetDiagnosticLifecycleData();
        Refresh();
        StatusText.Text = "Local diagnostics and report state deleted.";
    }

    private static void WriteBundle(App app, string path)
    {
        SystemStatusSnapshot system = app.SystemStatusService.Read();
        DeviceValidationState validation = GetValidationState(system.MachineType);
        var device = new DiagnosticDeviceInfo(
            system.Manufacturer,
            system.DeviceName,
            system.MachineType == "—" ? null : system.MachineType,
            system.BiosVersion == "—" ? null : system.BiosVersion,
            validation);
        string version = UpdateService.CurrentVersion;
        string channel = version.Contains('-', StringComparison.Ordinal) ? "alpha" : "stable";
        app.DiagnosticsRecorder.ExportBundle(path, device, version, channel, Environment.OSVersion.VersionString);
    }

    private static DeviceValidationState GetValidationState(string? machineType) =>
        string.Equals(machineType, "21Q6", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(machineType, "21Q7", StringComparison.OrdinalIgnoreCase)
            ? DeviceValidationState.Verified
            : DeviceValidationState.NotValidated;

    private static string SafeFileToken(string? value)
    {
        string safe = new((value ?? "device").Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "device" : safe;
    }
}
