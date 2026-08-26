using System.ComponentModel;
using ThinkControl.UI.Services;
using ThinkControl.UI.ViewModels;
using ThinkControl.Core.Notifications;

namespace ThinkControl.UI;

/// <summary>
/// Proactive, deduplicated attention for conditions that otherwise live behind the
/// notification bell. Routine healthy state remains silent. ThinkControl never
/// raises a UAC prompt from an automatic check; update installation and hardware
/// repair still require an explicit user action.
/// </summary>
public partial class App
{
    private readonly AttentionToastService _attentionToast = new();
    private CancellationTokenSource? _hardwareAttentionDelay;
    private UpdateCheckResult? _pendingAttentionUpdate;
    private bool _attentionUpdateInstallBusy;

    private void InitializeAttentionNotifications()
    {
        State.PropertyChanged += AttentionState_PropertyChanged;
        UpdateAvailabilityChanged += Attention_UpdateAvailabilityChanged;
        Activated += Attention_Activated;
        Exit += (_, _) =>
        {
            State.PropertyChanged -= AttentionState_PropertyChanged;
            UpdateAvailabilityChanged -= Attention_UpdateAvailabilityChanged;
            Activated -= Attention_Activated;
            try { _hardwareAttentionDelay?.Cancel(); } catch { }
            _hardwareAttentionDelay?.Dispose();
            _hardwareAttentionDelay = null;
            _attentionToast.Dispose();
        };
    }

    private void Attention_UpdateAvailabilityChanged(object? sender, EventArgs e)
    {
        _pendingAttentionUpdate = LatestUpdateResult is { Available: true } update ? update : null;
        TryShowPendingUpdateAttention();
    }

    private void Attention_Activated(object? sender, EventArgs e)
    {
        TryShowPendingUpdateAttention();
        QueueHardwareAttention(State.DriverStatus);
        TryShowDiagnosticsSharingAttention();
    }

    private void AttentionState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.DriverStatus))
            QueueHardwareAttention(State.DriverStatus);

        if (e.PropertyName is nameof(AppState.HardwareAccess)
            or nameof(AppState.CanSensorTelemetry)
            or nameof(AppState.CanFanTelemetry)
            or nameof(AppState.CanFanControl)
            or nameof(AppState.CanKeyboardBacklight))
        {
            TryShowDiagnosticsSharingAttention();
        }
    }

    private void TryShowDiagnosticsSharingAttention()
    {
        ThinkControlUserSettings preferences = UserSettings.Current;
        if (preferences.DiagnosticsSharingPrompted ||
            preferences.DiagnosticsConsent != ThinkControl.Core.Diagnostics.DiagnosticsConsent.Enabled ||
            !CanShowAttentionNow() ||
            !DeviceSupportReportService.HasUsefulDiscovery(State))
        {
            return;
        }

        DeviceSupportReport? report;
        try { report = DeviceSupportReportService.PrepareReport(State, SystemStatusService.Read()); }
        catch { return; }
        if (report is null)
            return;

        UserSettings.Update(settings => settings with { DiagnosticsSharingPrompted = true });
        _attentionToast.Show(
            "diagnostics-sharing-ready",
            "Device compatibility data is ready",
            DeviceSupportReportService.DiscoverySummary(State) + ". Sharing the redacted report can help ThinkControl support more devices. Nothing is submitted automatically.",
            "Review report",
            () => OpenAdvanced("Settings"));
    }

    private void QueueHardwareAttention(string? rawStatus)
    {
        string status = rawStatus?.Trim() ?? string.Empty;
        if (!NeedsProactiveHardwareAttention(status))
        {
            try { _hardwareAttentionDelay?.Cancel(); } catch { }
            if (status.Equals("Ready", StringComparison.OrdinalIgnoreCase) &&
                UserSettings.Current.AttentionAcknowledgedKey.StartsWith("hardware:", StringComparison.Ordinal))
            {
                UserSettings.Update(settings => settings with
                {
                    AttentionAcknowledgedKey = string.Empty,
                    AttentionAcknowledgedAtUtc = string.Empty
                });
            }
            return;
        }

        string key = AttentionCooldownPolicy.HardwareKey(status);
        ThinkControlUserSettings preferences = UserSettings.Current;
        if (AttentionCooldownPolicy.IsSuppressed(
                key,
                preferences.AttentionAcknowledgedKey,
                preferences.AttentionAcknowledgedAtUtc,
                DateTimeOffset.UtcNow))
        {
            return;
        }

        CancellationTokenSource next = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _hardwareAttentionDelay, next);
        try { previous?.Cancel(); } catch { }
        previous?.Dispose();

        _ = ShowHardwareAttentionAfterStabilityAsync(status, next);
    }

    private async Task ShowHardwareAttentionAfterStabilityAsync(string status, CancellationTokenSource owner)
    {
        try
        {
            bool connectionFailure = status.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                                     status.Contains("not installed", StringComparison.OrdinalIgnoreCase) ||
                                     status.Contains("stopped", StringComparison.OrdinalIgnoreCase);
            await Task.Delay(connectionFailure ? TimeSpan.FromSeconds(6) : TimeSpan.FromSeconds(7), owner.Token)
                .ConfigureAwait(false);

            await Dispatcher.InvokeAsync(() =>
            {
                if (owner.IsCancellationRequested ||
                    _hardwareSetupWindow?.IsVisible == true ||
                    !string.Equals(State.DriverStatus, status, StringComparison.Ordinal) ||
                    !CanShowAttentionNow())
                {
                    return;
                }

                (string title, string detail) = HardwareAttentionCopy(status);
                string key = AttentionCooldownPolicy.HardwareKey(status);
                ThinkControlUserSettings preferences = UserSettings.Current;
                if (AttentionCooldownPolicy.IsSuppressed(
                        key,
                        preferences.AttentionAcknowledgedKey,
                        preferences.AttentionAcknowledgedAtUtc,
                        DateTimeOffset.UtcNow))
                {
                    return;
                }
                _attentionToast.Show(
                    key,
                    title,
                    detail,
                    "Open hardware",
                    () =>
                    {
                        AcknowledgeHardwareAttention(key);
                        OpenHardwareAttention();
                    },
                    () => AcknowledgeHardwareAttention(key));
            });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_hardwareAttentionDelay, owner))
            {
                Interlocked.CompareExchange(ref _hardwareAttentionDelay, null, owner);
                owner.Dispose();
            }
        }
    }

    private void AcknowledgeHardwareAttention(string key)
    {
        UserSettings.Update(settings => settings with
        {
            AttentionAcknowledgedKey = key,
            AttentionAcknowledgedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    private void TryShowPendingUpdateAttention()
    {
        if (_pendingAttentionUpdate is not { Available: true } update || !CanShowAttentionNow())
            return;

        bool ready = !string.IsNullOrWhiteSpace(update.InstallerUrl) &&
                     !string.IsNullOrWhiteSpace(update.PayloadUrl) &&
                     !string.IsNullOrWhiteSpace(update.ChecksumUrl);
        string key = "update:" + (update.Version ?? update.Status);
        string detail = ready
            ? $"{update.Version ?? "A newer version"} is downloaded only after you choose Install update. Windows will ask once for administrator approval."
            : update.Status;

        _attentionToast.Show(
            key,
            "ThinkControl update available",
            detail,
            ready ? "Install update" : "Open Updates",
            ready
                ? () => _ = InstallUpdateFromAttentionAsync(update)
                : () => OpenAdvanced("Updates"));
    }

    private void EvaluatePreviousUpdateHandoff()
    {
        UpdateHandoffOutcome? outcome = UpdateHandoffService.Evaluate(UpdateService.CurrentVersion);
        if (outcome is null)
            return;

        State.UpdateStatus = outcome.Status;
        if (!CanShowAttentionNow())
            return;

        if (outcome.Completed)
        {
            // A completed update is confirmation, not a decision. Keep it passive so
            // there is no misleading Later/Ignore action and no click path that can
            // change the current ThinkControl window state.
            _attentionToast.ShowPassive(
                "update-complete:" + UpdateService.CurrentVersion,
                "ThinkControl updated",
                outcome.Status);
            return;
        }

        _attentionToast.Show(
            "update-handoff-incomplete",
            "Update needs attention",
            outcome.Status,
            "Open install log",
            () =>
            {
                if (!UpdateHandoffService.TryOpenLog(outcome.LogPath))
                    OpenAdvanced("Updates");
            });
    }

    private async Task InstallUpdateFromAttentionAsync(UpdateCheckResult update)
    {
        if (_attentionUpdateInstallBusy)
            return;

        _attentionUpdateInstallBusy = true;
        try
        {
            var progress = new Progress<string>(status => State.UpdateStatus = status);
            State.UpdateStatus = $"Downloading {update.Version ?? "update"}…";
            UpdateInstallResult result = await UpdateService.DownloadAndLaunchAsync(update, progress);
            State.UpdateStatus = result.Status;
            if (result.Success)
            {
                _pendingAttentionUpdate = null;
                return;
            }

            if (CanShowAttentionNow())
            {
                _attentionToast.Show(
                    "update-install-failed:" + (update.Version ?? "latest"),
                    "Update was not started",
                    result.Status,
                    "Open Updates",
                    () => OpenAdvanced("Updates"));
            }
        }
        finally
        {
            _attentionUpdateInstallBusy = false;
        }
    }

    private bool CanShowAttentionNow()
    {
        if (IsTrayOnlyLaunch())
            return false;

        return CompactWindow?.IsVisible == true || _advancedWindow?.IsVisible == true;
    }

    private static bool NeedsProactiveHardwareAttention(string status)
    {
        if (string.IsNullOrWhiteSpace(status) ||
            status.Equals("Ready", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Checking", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Refreshing", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Verifying", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Starting", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Restarting", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Installing", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("Repairing", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return status.Contains("attention", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("not installed", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("stopped", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("does not respond", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("needs repair", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("did not pass", StringComparison.OrdinalIgnoreCase);
    }

    private static (string Title, string Detail) HardwareAttentionCopy(string status)
    {
        if (status.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("does not respond", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "Hardware service needs attention",
                "Windows sees the ThinkControl service, but the app cannot complete its local IPC handshake. Open Hardware to repair and verify the connection.");
        }

        if (status.Contains("low-level", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("missing", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "Hardware component required",
                "ThinkControl needs its verified low-level component for this device. Open Hardware to install or repair it and then re-check providers.");
        }

        return (
            "Hardware provider needs attention",
            "One expected hardware provider did not pass readback. Controls remain safe and disabled until it verifies. Open Hardware for the exact provider status.");
    }
}
