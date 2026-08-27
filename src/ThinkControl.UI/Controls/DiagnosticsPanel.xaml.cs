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
    private string? _selectedCrashId;

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
            DiagnosticsSwitch.Visibility = verified ? Visibility.Collapsed : Visibility.Visible;
            DiagnosticsSwitch.ToolTip = "Prepare a compact compatibility report locally for this new device. Nothing is uploaded automatically.";

            CompatibilityStateText.Text = status.Label;
            CompatibilityDetailText.Text = status.Detail;

            if (verified)
            {
                CompatibilityStateText.Text = "Supported device";
                CompatibilityDetailText.Text = "Verified ThinkControl profile · compatibility learning and compatibility reports are not needed.";
                LearningCard.Visibility = Visibility.Collapsed;
                SharingRow.Visibility = Visibility.Collapsed;
            }
            else
            {
                LearningCard.Visibility = Visibility.Visible;
                SharingRow.Visibility = Visibility.Visible;
                ShareDeviceButton.Visibility = Visibility.Visible;
                LearningProgress.Maximum = Math.Max(1, status.TotalChecks);
                LearningProgress.Value = Math.Clamp(status.CompletedChecks, 0, Math.Max(1, status.TotalChecks));
                LearningProgressText.Text = $"{Math.Max(0, status.CompletedChecks)}/{Math.Max(0, status.TotalChecks)}";

                switch (status.Phase)
                {
                    case DeviceSupportPhase.Learning:
                        LearningTitleText.Text = "Learning compatibility";
                        SharingStateText.Text = sharingEnabled
                            ? "Keep using ThinkControl normally · no report is ready yet"
                            : "Learning stays local · report review is disabled";
                        ShareDeviceButton.Content = "Review report";
                        ShareDeviceButton.IsEnabled = false;
                        break;
                    case DeviceSupportPhase.ReadyToShare:
                        LearningTitleText.Text = "Compatibility evidence complete";
                        SharingStateText.Text = sharingEnabled
                            ? "New compatibility findings are ready to review"
                            : "Report is ready locally · enable review to open the GitHub draft";
                        ShareDeviceButton.Content = "Review report";
                        ShareDeviceButton.IsEnabled = sharingEnabled;
                        break;
                    case DeviceSupportPhase.Shared:
                        LearningTitleText.Text = "Compatibility learned";
                        SharingStateText.Text = "Shared · no new compatibility findings";
                        ShareDeviceButton.Content = "No new report";
                        ShareDeviceButton.IsEnabled = false;
                        break;
                    default:
                        ShareDeviceButton.IsEnabled = false;
                        break;
                }
            }

            IReadOnlyList<CrashReport> crashes = app.PendingCrashReports;
            int crashCount = crashes.Count;
            CrashReport? crash = crashes.FirstOrDefault(item =>
                    string.Equals(item.Id, _selectedCrashId, StringComparison.OrdinalIgnoreCase))
                ?? crashes.FirstOrDefault();
            bool hasCrash = crash is not null;
            CrashCard.Visibility = hasCrash ? Visibility.Visible : Visibility.Collapsed;
            CrashSeparator.Visibility = hasCrash ? Visibility.Visible : Visibility.Collapsed;
            CrashHistoryCombo.Visibility = crashCount > 1 ? Visibility.Visible : Visibility.Collapsed;
            if (crash is not null)
            {
                _selectedCrashId = crash.Id;
                CrashTitleText.Text = crashCount == 1 ? "Previous crash" : $"Crashes preserved · {crashCount}";
                CrashSummaryText.Text = FormatCrashSummary(crash, crashCount);
                CrashHistoryCombo.ItemsSource = crashes.Select(item => new CrashHistoryOption(
                    item.Id,
                    FormatCrashPickerLabel(item))).ToArray();
                CrashHistoryCombo.SelectedValue = crash.Id;
                MarkCrashReportedButton.Visibility = crash.State == CrashReportState.Opened
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                _selectedCrashId = null;
                CrashHistoryCombo.ItemsSource = null;
            }

            LastEventText.Text = app.DiagnosticsRecorder.LastEventAtUtc is DateTimeOffset last
                ? $"Last local activity · {last.ToLocalTime():g}"
                : "No local troubleshooting activity yet";

            StatusText.Text = verified
                ? "Routine troubleshooting history is local and separate from compatibility reporting."
                : status.Phase == DeviceSupportPhase.Shared
                    ? "This compatibility fingerprint is already handled. Another report appears only after materially new evidence."
                    : "Compatibility learning is local. Nothing is uploaded unless you explicitly review and submit a GitHub draft.";
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
            DiagnosticsSwitch.Visibility = verified ? Visibility.Collapsed : Visibility.Visible;
            CompatibilityStateText.Text = verified ? "Supported device" : "New device · learning";
            LastEventText.Text = "Last local activity · just now";
            CrashCard.Visibility = Visibility.Collapsed;
            CrashSeparator.Visibility = Visibility.Collapsed;

            if (verified)
            {
                CompatibilityDetailText.Text = "Verified ThinkControl profile · compatibility learning and reports are not needed.";
                LearningCard.Visibility = Visibility.Collapsed;
                SharingRow.Visibility = Visibility.Collapsed;
                StatusText.Text = "Routine troubleshooting history stays local on this PC.";
            }
            else
            {
                SharingRow.Visibility = Visibility.Visible;
                LearningCard.Visibility = Visibility.Visible;
                LearningTitleText.Text = "Learning compatibility";
                LearningProgress.Maximum = 5;
                LearningProgress.Value = 3;
                LearningProgressText.Text = "3/5";
                CompatibilityDetailText.Text = "3/5 checks · learning continues quietly while you use ThinkControl";
                SharingStateText.Text = "Keep using ThinkControl normally · no report is ready yet";
                ShareDeviceButton.Visibility = Visibility.Visible;
                ShareDeviceButton.IsEnabled = false;
                ShareDeviceButton.Content = "Review report";
                StatusText.Text = "Compatibility learning stays local. Nothing is uploaded automatically.";
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
            StatusText.Text = "Enable compatibility report review first. Nothing is submitted automatically.";
            return;
        }

        if (!app.OpenCurrentDeviceReportOnGitHub())
        {
            Refresh();
            StatusText.Text = "There is no new stable compatibility report to review.";
            return;
        }

        Refresh();
        StatusText.Text = "Opened a pre-filled GitHub draft and marked this compatibility fingerprint handled locally.";
    }

    private void ReportCrash_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;
        if (!app.OpenCrashReportOnGitHub(_selectedCrashId))
        {
            Refresh();
            return;
        }
        Refresh();
        StatusText.Text = "Opened the redacted crash draft on GitHub. The local report remains until you explicitly dismiss it or mark it reported.";
    }

    private void DismissCrash_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;
        app.DismissCrashReport(_selectedCrashId);
        Refresh();
        StatusText.Text = "Crash report dismissed. Any other unresolved crashes remain available.";
    }

    private void MarkCrashReported_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;
        app.MarkCrashReported(_selectedCrashId);
        Refresh();
        StatusText.Text = "Crash marked reported locally. Any other unresolved crash remains available.";
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


    private void CrashHistoryCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_syncing || CrashHistoryCombo.SelectedValue is not string id ||
            string.Equals(id, _selectedCrashId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _selectedCrashId = id;
        Refresh();
    }

    private static string FormatCrashPickerLabel(CrashReport crash)
    {
        string type = crash.ExceptionType?.Split('.').LastOrDefault() ?? "Unexpected error";
        string when = DateTimeOffset.TryParse(crash.LastSeenUtc, out DateTimeOffset timestamp)
            ? timestamp.ToLocalTime().ToString("g")
            : "previous run";
        string repeats = crash.OccurrenceCount > 1 ? $" · ×{crash.OccurrenceCount}" : string.Empty;
        return $"{type}{repeats} · {when}";
    }

    private sealed record CrashHistoryOption(string Id, string Label);

    private static string FormatCrashSummary(CrashReport crash, int unresolvedCount)
    {
        string when = DateTimeOffset.TryParse(crash.LastSeenUtc, out DateTimeOffset timestamp)
            ? timestamp.ToLocalTime().ToString("g")
            : "previous run";
        string type = crash.ExceptionType?.Split('.').LastOrDefault() ?? "Unexpected error";
        string message = string.Join(' ', (crash.Message ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (message.Length > 150)
            message = message[..147] + "…";
        string repeats = crash.OccurrenceCount > 1 ? $" · repeated {crash.OccurrenceCount} times" : string.Empty;
        string previous = unresolvedCount > 1 ? $" · {unresolvedCount - 1} previous unresolved" : string.Empty;
        return (string.IsNullOrWhiteSpace(message)
            ? $"{type} · {when}"
            : $"{type} · {message} · {when}") + repeats + previous;
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
