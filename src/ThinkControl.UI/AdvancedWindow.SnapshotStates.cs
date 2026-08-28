using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    /// <summary>
    /// Deterministic visual-QA state for the real in-window Notifications sheet.
    /// No live service, network or driver call is made; the same runtime card
    /// renderer is used so layout regressions remain visible in CI snapshots.
    /// </summary>
    internal void PrepareNotificationSheetForSnapshot()
    {
        EnsureNotificationSheet();
        if (_notificationOverlay is null || _notificationMessages is null || _notificationSummary is null)
            throw new InvalidOperationException("Notifications sheet could not be prepared for visual QA.");

        _notificationMessages.Children.Clear();
        _notificationSummary.Text = "1 item needs attention";

        SheetMessage[] messages =
        [
            new(
                "PawnIO needs repair",
                "PawnIO is installed, but ThinkControl cannot open its driver device. Review one focused repair; fan and sensor providers are checked again only after PawnIO is ready.",
                "Review repair",
                SheetAction.PawnIo,
                true),
            new(
                "ThinkControl is up to date",
                "The installed version is the newest verified prerelease available to this update channel.",
                string.Empty,
                SheetAction.None,
                false)
        ];

        foreach (SheetMessage message in messages)
            _notificationMessages.Children.Add(CreateNotificationCard(message));

        _notificationOverlay.Visibility = Visibility.Visible;
        Panel.SetZIndex(_notificationOverlay, 200);
    }

    internal void PrepareUpdateNotificationSheetForSnapshot(bool dismissed)
    {
        EnsureNotificationSheet();
        if (_notificationOverlay is null || _notificationMessages is null || _notificationSummary is null)
            throw new InvalidOperationException("Notifications sheet could not be prepared for update visual QA.");

        _notificationMessages.Children.Clear();
        _notificationSummary.Text = "1 item needs attention";
        string detail = dismissed
            ? "0.1.0-alpha.28  →  0.1.0-alpha.29. The startup prompt was dismissed, but the update remains available here until it is installed or replaced by a newer release."
            : "0.1.0-alpha.28  →  0.1.0-alpha.29. A newer release is ready to review and install.";
        _notificationMessages.Children.Add(CreateNotificationCard(new SheetMessage(
            "ThinkControl update available",
            detail,
            "Open Updates",
            SheetAction.Updates,
            true)));
        _notificationOverlay.Visibility = Visibility.Visible;
        Panel.SetZIndex(_notificationOverlay, 200);
    }

    internal void PrepareAudioForSnapshot(bool providersAvailable)
    {
        // Normal/minimum cover the direct DAX state, unavailable has its dedicated
        // render, and the wide matrix exercises modern Fusion-only layout. This keeps
        // both provider generations under the existing visual gate without inventing
        // a second snapshot harness.
        if (providersAvailable && Width >= 1500)
            AudioPanelControl.PrepareFusionForSnapshot();
        else
            AudioPanelControl.PrepareForSnapshot(providersAvailable);
    }

    internal void PrepareDiagnosticsForSnapshot(Core.Diagnostics.DiagnosticsConsent consent, bool verifiedDevice)
    {
        if (verifiedDevice)
            DiagnosticsPanelControl?.PrepareForSnapshot(_app.State, consent);
        else
            DiagnosticsPanelControl?.PrepareLifecycleForSnapshot(_app.State, consent);
    }

    internal void PrepareCrashQueueForSnapshot() =>
        DiagnosticsPanelControl?.PrepareCrashQueueForSnapshot();

    internal void ScrollDiagnosticsIntoViewForSnapshot()
    {
        PageSettings.UpdateLayout();
        if (DiagnosticsPanelControl?.BringCrashQueueIntoViewForSnapshot() != true)
            PageSettings.ScrollToEnd();
        PageSettings.UpdateLayout();
    }
}
