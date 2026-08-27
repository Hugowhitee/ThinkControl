using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ThinkControl.Core.Diagnostics;
using ThinkControl.UI.Services;

namespace ThinkControl.UI;

public partial class App
{
    private readonly DiagnosticLifecycleStore _diagnosticLifecycleStore = new();
    private readonly CrashReportService _crashReports = new();
    private DispatcherTimer? _compatibilityLearningTimer;
    private DeviceSupportStatus? _deviceSupportStatus;
    private bool _diagnosticsLifecycleInitialized;
    private bool _crashAttentionShownThisRun;

    internal event EventHandler? DeviceSupportStatusChanged;

    internal DeviceSupportStatus DeviceSupportStatus =>
        _deviceSupportStatus ?? EvaluateDeviceSupportLifecycle(showAttention: false);

    internal CrashReport? PendingCrashReport => _crashReports.TryGetPending();

    /// <summary>
    /// Starts lifecycle-aware diagnostics once the real desktop shell exists. This
    /// deliberately does not run for snapshot/test applications unless they create
    /// a MainWindow and explicitly opt into the real lifecycle.
    /// </summary>
    internal void InitializeDiagnosticsLifecycle()
    {
        if (_diagnosticsLifecycleInitialized)
            return;
        _diagnosticsLifecycleInitialized = true;

        // alpha.24 used one global boolean for the old "useful diagnostics" toast.
        // Retire that path so only the fingerprinted lifecycle below can prompt.
        if (!UserSettings.Current.DiagnosticsSharingPrompted)
            UserSettings.Update(settings => settings with { DiagnosticsSharingPrompted = true });

        bool previousRunWasUnclean = _crashReports.BeginRun();
        if (previousRunWasUnclean && _crashReports.TryGetPending() is null)
        {
            RecordDiagnostic(new DiagnosticEvent(
                DateTimeOffset.UtcNow,
                "app.previous_run_unclean",
                ValidationState: GetCurrentDeviceValidationState(),
                Success: false,
                ErrorCode: "unclean_exit_no_managed_crash"));
        }

        DispatcherUnhandledException += DiagnosticsLifecycle_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += DiagnosticsLifecycle_AppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += DiagnosticsLifecycle_UnobservedTaskException;
        State.PropertyChanged += DiagnosticsLifecycle_StatePropertyChanged;
        Activated += DiagnosticsLifecycle_Activated;
        Exit += DiagnosticsLifecycle_Exit;

        _compatibilityLearningTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _compatibilityLearningTimer.Tick += (_, _) => EvaluateDeviceSupportLifecycle(showAttention: true);

        DeviceSupportStatus status = EvaluateDeviceSupportLifecycle(showAttention: false);
        if (status.Phase != DeviceSupportPhase.Verified)
            _compatibilityLearningTimer.Start();
    }

    internal bool OpenCurrentDeviceReportOnGitHub()
    {
        DeviceSupportStatus status = EvaluateDeviceSupportLifecycle(showAttention: false);
        DeviceSupportReport? report = status.Report;
        if (report is null || status.Phase == DeviceSupportPhase.Learning)
            return false;

        try
        {
            DeviceSupportReportService.WritePreparedReport(report);
            Process.Start(new ProcessStartInfo(DeviceSupportReportService.BuildIssueUrl(report)) { UseShellExecute = true });
            _diagnosticLifecycleStore.MarkHandled(report.Fingerprint);
            EvaluateDeviceSupportLifecycle(showAttention: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal bool OpenPendingCrashReportOnGitHub()
    {
        CrashReport? report = _crashReports.TryGetPending();
        if (report is null)
            return false;
        try
        {
            Process.Start(new ProcessStartInfo(_crashReports.BuildIssueUrl(report)) { UseShellExecute = true });
            _crashReports.ClearPending();
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal void DismissPendingCrashReport() => _crashReports.ClearPending();

    internal void ResetDiagnosticLifecycleData()
    {
        _diagnosticLifecycleStore.Clear();
        _crashReports.ClearPending();
        DeviceSupportReportService.DeletePreparedReport();
        _deviceSupportStatus = null;
        EvaluateDeviceSupportLifecycle(showAttention: false);
    }

    private DeviceSupportStatus EvaluateDeviceSupportLifecycle(bool showAttention)
    {
        DeviceSupportStatus previous = _deviceSupportStatus ?? new DeviceSupportStatus(
            DeviceSupportPhase.Learning, -1, -1, string.Empty, string.Empty, null);
        DeviceSupportStatus next = DeviceSupportReportService.Evaluate(
            State,
            _manufacturer,
            DiagnosticsRecorder,
            _diagnosticLifecycleStore);
        _deviceSupportStatus = next;

        if (next.Phase == DeviceSupportPhase.Verified)
            _compatibilityLearningTimer?.Stop();
        else if (_compatibilityLearningTimer?.IsEnabled == false)
            _compatibilityLearningTimer.Start();

        if (previous.Phase != next.Phase ||
            previous.CompletedChecks != next.CompletedChecks ||
            !string.Equals(previous.Report?.Fingerprint, next.Report?.Fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            DeviceSupportStatusChanged?.Invoke(this, EventArgs.Empty);
        }

        if (showAttention && next is { Phase: DeviceSupportPhase.ReadyToShare, Report: not null } &&
            UserSettings.Current.DiagnosticsConsent == DiagnosticsConsent.Enabled &&
            !_diagnosticLifecycleStore.WasPrompted(next.Report.Fingerprint) &&
            CanShowAttentionNow())
        {
            _diagnosticLifecycleStore.MarkPrompted(next.Report.Fingerprint);
            _attentionToast.Show(
                "device-learning-ready:" + next.Report.Fingerprint[..Math.Min(16, next.Report.Fingerprint.Length)],
                "New device profile is ready",
                "ThinkControl finished learning stable compatibility evidence in the background. Review the compact redacted report when convenient.",
                "Review report",
                () => OpenAdvancedSafely("Settings"));
        }

        return next;
    }

    private void DiagnosticsLifecycle_StatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(State.DriverStatus)
            or nameof(State.HardwareAccess)
            or nameof(State.CanSensorTelemetry)
            or nameof(State.CanFanTelemetry)
            or nameof(State.CanFanControl)
            or nameof(State.CanKeyboardBacklight)
            or nameof(State.CanCpuTemperature)
            or nameof(State.SensorCountText)
            or nameof(State.FanCountText))
        {
            EvaluateDeviceSupportLifecycle(showAttention: true);
        }
    }

    private void DiagnosticsLifecycle_Activated(object? sender, EventArgs e)
    {
        EvaluateDeviceSupportLifecycle(showAttention: true);
        TryShowPendingCrashAttention();
    }

    private void TryShowPendingCrashAttention()
    {
        if (_crashAttentionShownThisRun || !CanShowAttentionNow())
            return;

        CrashReport? report = _crashReports.TryGetPending();
        if (report is null)
            return;

        _crashAttentionShownThisRun = true;
        string detail = string.IsNullOrWhiteSpace(report.Message)
            ? "A redacted crash report was saved locally before ThinkControl closed. Review it before deciding whether to open a GitHub issue."
            : $"{report.ExceptionType}: {report.Message}";
        _attentionToast.Show(
            "crash-recovery:" + report.Fingerprint[..Math.Min(16, report.Fingerprint.Length)],
            "ThinkControl closed unexpectedly",
            detail,
            "Review crash report",
            () => OpenAdvancedSafely("Settings"));
    }

    private void DiagnosticsLifecycle_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _crashReports.CaptureFatal("wpf-dispatcher", e.Exception, State, DiagnosticsRecorder);
        RecordShellException("dispatcher", e.Exception);
        // Do not set e.Handled. Capturing diagnostics must never turn a fatal UI
        // exception into a half-corrupted process that keeps running.
    }

    private void DiagnosticsLifecycle_AppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            _crashReports.CaptureFatal("app-domain", exception, State, DiagnosticsRecorder);
    }

    private void DiagnosticsLifecycle_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Exception exception = e.Exception.GetBaseException();
        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            "app.task_exception_unobserved",
            ValidationState: GetCurrentDeviceValidationState(),
            Success: false,
            ErrorCode: exception.GetType().Name));
    }

    private void DiagnosticsLifecycle_Exit(object? sender, ExitEventArgs e)
    {
        _compatibilityLearningTimer?.Stop();
        State.PropertyChanged -= DiagnosticsLifecycle_StatePropertyChanged;
        Activated -= DiagnosticsLifecycle_Activated;
        DispatcherUnhandledException -= DiagnosticsLifecycle_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= DiagnosticsLifecycle_AppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= DiagnosticsLifecycle_UnobservedTaskException;
        _crashReports.CompleteCleanRun();
    }
}