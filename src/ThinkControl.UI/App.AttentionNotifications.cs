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
    private string _shownUpdateVersionThisRun = string.Empty;

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
    }

    private void AttentionState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.DriverStatus))
            QueueHardwareAttention(State.DriverStatus);
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

        string version = update.Version?.Trim() ?? string.Empty;
        if (UpdatePromptPolicy.IsDismissed(version, UserSettings.Current.DismissedUpdateVersion) ||
            (version.Length > 0 && string.Equals(_shownUpdateVersionThisRun, version, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        bool ready = !string.IsNullOrWhiteSpace(update.InstallerUrl) &&
                     !string.IsNullOrWhiteSpace(update.PayloadUrl) &&
                     !string.IsNullOrWhiteSpace(update.ChecksumUrl);
        string key = "update:" + (version.Length > 0 ? version : update.Status);
        string transition = UpdatePromptPolicy.Transition(UpdateService.CurrentVersion, version);
        string detail = ready
            ? $"{transition}\nReady to update. Download and SHA-256 verification start only after you choose Update; Windows asks once for administrator approval."
            : $"{transition}\n{update.Status}";

        _attentionToast.Show(
            key,
            "ThinkControl update available",
            detail,
            ready ? "Update" : "Open Updates",
            ready
                ? () => _ = InstallUpdateFromAttentionAsync(update)
                : () => OpenAdvancedSafely("Updates"),
            () => DismissUpdatePrompt(update),
            dismissText: "Dismiss");
        if (version.Length > 0)
            _shownUpdateVersionThisRun = version;
    }

    private void DismissUpdatePrompt(UpdateCheckResult update)
    {
        string version = update.Version?.Trim() ?? string.Empty;
        if (version.Length == 0)
            return;

        UserSettings.Update(settings => settings with { DismissedUpdateVersion = version });
        // Keep LatestUpdateResult intact. Dismiss means "do not interrupt me again
        // for this version", not "forget this update"; the notification bell and
        // Updates page continue to expose it.
        UpdateAvailabilityChanged?.Invoke(this, EventArgs.Empty);
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
                    OpenAdvancedSafely("Updates");
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
                UserSettings.Update(settings => settings with { DismissedUpdateVersion = string.Empty });
                return;
            }

            if (CanShowAttentionNow())
            {
                _attentionToast.Show(
                    "update-install-failed:" + (update.Version ?? "latest"),
                    "Update was not started",
                    result.Status,
                    "Open Updates",
                    () => OpenAdvancedSafely("Updates"));
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