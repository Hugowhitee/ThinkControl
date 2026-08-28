using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ThinkControl.Core.Diagnostics;

namespace ThinkControl.UI;

public partial class App
{
    private bool _viewTransitionBusy;
    private long _compactDeactivationGeneration;
    private volatile bool _trayInteractionPending;

    internal bool IsViewTransitionInProgress => _viewTransitionBusy;
    internal AdvancedWindow? AdvancedWindowForShellSmoke => _advancedWindow;
    internal Window? AttentionWindowForShellSmoke => _attentionToast.WindowForShellSmoke;
    internal Button? AttentionActionForShellSmoke => _attentionToast.ActionButtonForShellSmoke;

    /// <summary>
    /// Single entry point for commands that want the full surface. This method owns
    /// creation, navigation, show/paint, Compact removal and destination activation.
    /// Older public wrappers may still exist for compatibility, but shell callers
    /// should route here instead of independently hiding or showing either window.
    /// </summary>
    internal void OpenAdvancedSafely(string page = "Home")
    {
        if (CompactWindow is { IsVisible: true })
        {
            SwitchCompactToAdvanced(page);
            return;
        }

        ShowAdvancedOnly(page);
    }

    internal void SwitchCompactToAdvanced(string page = "Home")
    {
        const string operation = "compact-to-full";
        if (!TryBeginViewTransition(operation))
            return;

        try
        {
            AdvancedWindow advanced = EnsureAdvancedWindow();
            advanced.Navigate(page);

            // Paint the destination while Compact is still a real visible surface.
            // Compact deactivation is ignored while this coordinator owns the move.
            advanced.ShowAdvanced(animate: false);
            advanced.UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));

            CompactWindow.HideForViewTransition();
            advanced.Activate();

            VerifyPrimarySurfaceState(operation, expectCompact: false, expectAdvanced: true);
            RecordShellEvent("shell.transition.completed", true, operation);
        }
        catch (Exception ex)
        {
            RecordShellException(operation, ex);
            RecoverCompactAfterFailedTransition();
        }
        finally
        {
            EndViewTransition();
        }
    }

    internal void SwitchAdvancedToCompact()
    {
        const string operation = "full-to-compact";
        if (!TryBeginViewTransition(operation))
            return;

        try
        {
            CompactWindow.ShowNearTray(animate: false);
            CompactWindow.UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));

            _advancedWindow?.HideAnimated();
            CompactWindow.Activate();

            VerifyPrimarySurfaceState(operation, expectCompact: true, expectAdvanced: false);
            RecordShellEvent("shell.transition.completed", true, operation);
        }
        catch (Exception ex)
        {
            RecordShellException(operation, ex);
            try { CompactWindow.HideForViewTransition(); } catch { }
            try { _advancedWindow?.ShowAdvanced(animate: false); } catch { }
        }
        finally
        {
            EndViewTransition();
        }
    }

    private void ShowAdvancedOnly(string page)
    {
        const string operation = "open-full";
        if (!TryBeginViewTransition(operation))
            return;

        try
        {
            AdvancedWindow advanced = EnsureAdvancedWindow();
            advanced.Navigate(page);
            advanced.ShowAdvanced(animate: false);
            advanced.Activate();
            VerifyPrimarySurfaceState(operation, expectCompact: false, expectAdvanced: true);
            RecordShellEvent("shell.transition.completed", true, operation);
        }
        catch (Exception ex)
        {
            RecordShellException(operation, ex);
        }
        finally
        {
            EndViewTransition();
        }
    }

    private AdvancedWindow EnsureAdvancedWindow()
    {
        if (_advancedWindow is not null)
            return _advancedWindow;

        _advancedWindow = new AdvancedWindow(this) { DataContext = State };
        _advancedWindow.Closed += (_, _) => _advancedWindow = null;
        return _advancedWindow;
    }

    private bool TryBeginViewTransition(string operation)
    {
        if (_viewTransitionBusy)
        {
            RecordShellEvent("shell.transition.ignored", false, operation + ":busy");
            return false;
        }

        _viewTransitionBusy = true;
        Interlocked.Increment(ref _compactDeactivationGeneration);
        RecordShellEvent("shell.transition.started", null, operation);
        return true;
    }

    private void EndViewTransition()
    {
        _viewTransitionBusy = false;
        Interlocked.Increment(ref _compactDeactivationGeneration);
    }

    private void RecoverCompactAfterFailedTransition()
    {
        try { _advancedWindow?.HideAnimated(); } catch { }
        try
        {
            CompactWindow.ShowNearTray(animate: false);
            CompactWindow.Activate();
        }
        catch { }
    }

    /// <summary>
    /// Compact is a persistent utility surface. Losing focus to Chrome, a browser
    /// tab, an editor, or any other application must not make it disappear. Hiding
    /// is owned only by explicit close/tray-toggle/view-transition commands.
    /// Deactivation remains observable for diagnostics, but never schedules Hide().
    /// </summary>
    internal void OnCompactDeactivated()
    {
        if (CompactWindow is null || !CompactWindow.IsVisible)
            return;

        Interlocked.Increment(ref _compactDeactivationGeneration);
        RecordShellEvent(
            "shell.compact.deactivation-kept",
            true,
            _viewTransitionBusy ? "view-transition" : "external-focus-explicit-close-only");
    }

    internal void OnCompactActivated() =>
        Interlocked.Increment(ref _compactDeactivationGeneration);

    internal void NotifyTrayInteractionStarted()
    {
        _trayInteractionPending = true;
        Interlocked.Increment(ref _compactDeactivationGeneration);
    }

    internal void NotifyTrayInteractionCompleted()
    {
        _trayInteractionPending = false;
        Interlocked.Increment(ref _compactDeactivationGeneration);
    }

    private static bool ForegroundBelongsToThinkControl()
    {
        if (!OperatingSystem.IsWindows())
            return Application.Current?.Windows.OfType<Window>().Any(window => window.IsActive) == true;

        try
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
                return false;

            _ = GetWindowThreadProcessId(foreground, out uint processId);
            return processId == (uint)Environment.ProcessId;
        }
        catch
        {
            return Application.Current?.Windows.OfType<Window>().Any(window => window.IsActive) == true;
        }
    }

    private void VerifyPrimarySurfaceState(string operation, bool expectCompact, bool expectAdvanced)
    {
        bool compactVisible = CompactWindow?.IsVisible == true;
        bool advancedVisible = _advancedWindow?.IsVisible == true;
        if (compactVisible != expectCompact || advancedVisible != expectAdvanced)
        {
            throw new InvalidOperationException(
                $"{operation}: unexpected shell state (Compact={compactVisible}, Full={advancedVisible}).");
        }
    }

    internal void RecordShellException(string source, Exception ex)
    {
        Trace.WriteLine($"ThinkControl shell failure [{source}]: {ex}");
        RecordShellEvent(
            "shell.exception",
            false,
            source + ":" + ex.Message,
            ex.GetType().Name);
    }

    private void RecordShellEvent(string name, bool? success, string state, string? errorCode = null)
    {
        RecordDiagnostic(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            name,
            Capability: "Shell",
            Provider: "WPF",
            ValidationState: GetCurrentDeviceValidationState(),
            Success: success,
            ErrorCode: errorCode,
            Tags: new Dictionary<string, string>
            {
                ["state"] = state,
                ["source"] = ShellStateSnapshot()
            }));
    }

    private string ShellStateSnapshot() =>
        $"compact:{CompactWindow?.IsVisible == true};full:{_advancedWindow?.IsVisible == true};transition:{_viewTransitionBusy}";

    internal void ShowAttentionForShellSmoke(Action callback)
    {
        _attentionToast.Show(
            "shell-smoke-attention-" + Guid.NewGuid().ToString("N"),
            "ThinkControl interaction test",
            "This window exercises Compact activation ownership.",
            "Continue",
            callback);
    }

    /// <summary>
    /// Retained as a lower-level constructor/layout regression gate. The dedicated
    /// ShellSmoke executable now additionally invokes the real Compact button and
    /// explicitly activates/clicks the attention window to cover the focus sequence
    /// that alpha.23 did not exercise.
    /// </summary>
    internal void RunViewTransitionSmokeForVisualQa(int cycles = 3)
    {
        if (cycles < 1)
            throw new ArgumentOutOfRangeException(nameof(cycles));

        if (CompactWindow is null)
            CompactWindow = new MainWindow(this) { DataContext = State };

        try
        {
            CompactWindow.ShowNearTray(animate: false);
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(static () => { }));

            for (int i = 0; i < cycles; i++)
            {
                SwitchCompactToAdvanced("Home");
                VerifyPrimarySurfaceState($"direct cycle {i + 1} Full", expectCompact: false, expectAdvanced: true);

                SwitchAdvancedToCompact();
                VerifyPrimarySurfaceState($"direct cycle {i + 1} Compact", expectCompact: true, expectAdvanced: false);
            }
        }
        finally
        {
            try { _advancedWindow?.ForceClose(); } catch { }
            try { CompactWindow.ForceClose(); } catch { }
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
