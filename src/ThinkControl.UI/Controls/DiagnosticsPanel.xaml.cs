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
            UploadStatusText.Text = "Private endpoint pending";
            SendButton.IsEnabled = false;
            StatusText.Text = consent switch
            {
                DiagnosticsConsent.Enabled => "Diagnostics upload consent is enabled. Local events are redacted now; network upload remains off until the project private endpoint is configured.",
                DiagnosticsConsent.Disabled => "Diagnostics upload is disabled. You can still preview/export the local redacted support bundle manually.",
                _ => "Choose whether ThinkControl may submit redacted compatibility summaries. No detailed bundle is uploaded without consent."
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
        app.DiagnosticsRecorder.Record(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "diagnostics.consent_changed",
            ValidationState: GetValidationState(app.State.MachineType),
            Success: true,
            Tags: new Dictionary<string, string> { ["state"] = consent.ToString() }));
        Refresh();
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
            StatusText.Text = $"Preview saved to {path}";
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

    private void Send_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            Window.GetWindow(this),
            "The private ThinkControl diagnostics endpoint is intentionally not configured in this alpha source build yet. The app will never embed a GitHub PAT. Use Preview data or Export support bundle for now.",
            "ThinkControl diagnostics",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
