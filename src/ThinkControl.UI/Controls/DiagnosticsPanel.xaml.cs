using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.Services;

namespace ThinkControl.UI.Controls;

public partial class DiagnosticsPanel : System.Windows.Controls.UserControl
{
    private bool _syncing;
    private string? _reviewedReportFingerprint;

    public DiagnosticsPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public void Refresh()
    {
        if (System.Windows.Application.Current is not App app)
            return;

        _syncing = true;
        try
        {
            DiagnosticsConsent consent = app.UserSettings.Current.DiagnosticsConsent;
            DiagnosticsSwitch.IsChecked = consent == DiagnosticsConsent.Enabled;
            DeviceValidationState validation = GetValidationState(app.State.MachineType);
            ValidationStateText.Text = validation switch
            {
                DeviceValidationState.Verified => "Verified device profile",
                DeviceValidationState.Experimental => "Experimental compatibility",
                _ => "Not validated · compatibility checks available"
            };

            EventCountText.Text = app.DiagnosticsRecorder.LocalEventCount.ToString();
            LastEventText.Text = app.DiagnosticsRecorder.LastEventAtUtc is DateTimeOffset last
                ? last.ToLocalTime().ToString("g")
                : "—";

            bool useful = DeviceSupportReportService.HasUsefulDiscovery(app.State);
            DeviceSupportReport? currentReport = null;
            if (useful)
            {
                try { currentReport = DeviceSupportReportService.BuildReport(app.State, app.SystemStatusService.Read()); }
                catch { }
            }

            if (_reviewedReportFingerprint is not null &&
                !string.Equals(_reviewedReportFingerprint, currentReport?.Fingerprint, StringComparison.Ordinal))
            {
                _reviewedReportFingerprint = null;
            }

            bool reviewReady = consent == DiagnosticsConsent.Enabled && currentReport is not null;
            bool reviewed = reviewReady && string.Equals(
                _reviewedReportFingerprint,
                currentReport!.Fingerprint,
                StringComparison.Ordinal);

            ReviewDeviceButton.IsEnabled = reviewReady;
            ShareDeviceButton.IsEnabled = reviewed;
            ShareDeviceButton.ToolTip = reviewed
                ? "Open the reviewed report as a pre-filled GitHub issue"
                : "Review the exact device report first";
            UploadStatusText.Text = reviewed ? "Reviewed · ready for GitHub" : reviewReady ? "Review required" : "Not shareable yet";

            DiscoveryReadinessText.Text = useful
                ? DeviceSupportReportService.DiscoverySummary(app.State) + (reviewed ? " · reviewed · ready to share" : " · review required before sharing")
                : "Still learning · wait for hardware discovery to finish or run Hardware setup / Retry detection. Nothing can be shared yet.";
            DiscoveryReadinessText.Foreground = (System.Windows.Media.Brush)FindResource(useful ? "Tc.Success" : "Tc.TextMuted");

            StatusText.Text = consent switch
            {
                DiagnosticsConsent.Enabled when reviewed => "The current redacted device report has been reviewed. Share to GitHub opens that same report as a draft issue; GitHub still requires you to press Submit.",
                DiagnosticsConsent.Enabled when useful => "Useful stable hardware data is available. Review device report opens the exact Markdown locally; Share to GitHub stays locked until that report has been reviewed.",
                DiagnosticsConsent.Enabled => "Compatibility learning is on, but ThinkControl has not finished a useful stable discovery yet. Review and sharing stay disabled.",
                DiagnosticsConsent.Disabled => "Compatibility sharing is disabled. Local troubleshooting history is never uploaded automatically and can be deleted here at any time.",
                _ => "Local compatibility events are not uploaded automatically. Enable compatibility sharing only if you want the review/share workflow available."
            };
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

        DiagnosticsConsent consent = DiagnosticsSwitch.IsChecked == true
            ? DiagnosticsConsent.Enabled
            : DiagnosticsConsent.Disabled;
        app.UserSettings.Update(settings => settings with { DiagnosticsConsent = consent });
        _reviewedReportFingerprint = null;
        app.DiagnosticsRecorder.Record(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "diagnostics.consent_changed",
            ValidationState: GetValidationState(app.State.MachineType),
            Success: true,
            Tags: new Dictionary<string, string> { ["state"] = consent.ToString() }));
        Refresh();
    }

    private void ReviewDeviceReport_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;

        if (app.UserSettings.Current.DiagnosticsConsent != DiagnosticsConsent.Enabled)
        {
            StatusText.Text = "Enable compatibility sharing first. Review does not upload anything.";
            return;
        }

        if (!DeviceSupportReportService.HasUsefulDiscovery(app.State))
        {
            StatusText.Text = "ThinkControl has not finished useful stable hardware discovery yet. Run Hardware setup / Retry detection and review again after the provider state settles.";
            Refresh();
            return;
        }

        try
        {
            DeviceSupportReport report = DeviceSupportReportService.BuildReport(app.State, app.SystemStatusService.Read());
            string path = Path.Combine(Path.GetTempPath(), "ThinkControl-device-report-review.md");
            File.WriteAllText(path, report.Body);
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
            _reviewedReportFingerprint = report.Fingerprint;
            Refresh();
            StatusText.Text = "Opened the exact redacted device report locally. If it looks correct, Share to GitHub now opens this unchanged report as a draft issue.";
        }
        catch
        {
            _reviewedReportFingerprint = null;
            Refresh();
            StatusText.Text = "Could not open the local device-report review. Nothing was shared.";
        }
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

        if (!DeviceSupportReportService.HasUsefulDiscovery(app.State))
        {
            _reviewedReportFingerprint = null;
            Refresh();
            StatusText.Text = "The hardware report is no longer ready to share because discovery state changed. Review again after hardware detection settles.";
            return;
        }

        try
        {
            DeviceSupportReport report = DeviceSupportReportService.BuildReport(app.State, app.SystemStatusService.Read());
            if (!string.Equals(_reviewedReportFingerprint, report.Fingerprint, StringComparison.Ordinal))
            {
                _reviewedReportFingerprint = null;
                Refresh();
                StatusText.Text = "The device report changed since review. Review the updated report before sharing it to GitHub.";
                return;
            }

            string url = DeviceSupportReportService.BuildIssueUrl(report);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            StatusText.Text = "Opened the reviewed report as a pre-filled GitHub issue. Nothing is submitted until you press Submit new issue on GitHub.";
        }
        catch
        {
            StatusText.Text = "Could not open the reviewed device support report in your browser. Nothing was shared.";
        }
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
            StatusText.Text = "Opened a redacted diagnostics preview in Notepad. This is separate from the GitHub device report.";
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
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ThinkControl",
            "hardware-service.log");

        if (!File.Exists(path))
        {
            StatusText.Text = "No hardware-service log exists yet. The service creates it after startup or a hardware-provider event.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"")
            {
                UseShellExecute = true
            });
            StatusText.Text = "Opened the local hardware-service log in Notepad.";
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = true
                });
                StatusText.Text = "Opened the hardware-service log location in File Explorer.";
            }
            catch
            {
                StatusText.Text = $"Hardware-service log: {path}";
            }
        }
    }

    private void OpenBugReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                "https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml")
            {
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
            return;

        MessageBoxResult result = MessageBox.Show(
            Window.GetWindow(this),
            "Delete all local ThinkControl diagnostic events?",
            "Delete diagnostics",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        app.DiagnosticsRecorder.DeleteLocal();
        _reviewedReportFingerprint = null;
        Refresh();
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
        app.DiagnosticsRecorder.ExportBundle(
            path,
            device,
            version,
            channel,
            Environment.OSVersion.VersionString);
    }

    private static DeviceValidationState GetValidationState(string? machineType)
    {
        if (string.Equals(machineType, "21Q6", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(machineType, "21Q7", StringComparison.OrdinalIgnoreCase))
            return DeviceValidationState.Verified;

        return DeviceValidationState.NotValidated;
    }

    private static string SafeFileToken(string? value)
    {
        string safe = new((value ?? "device").Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "device" : safe;
    }
}
