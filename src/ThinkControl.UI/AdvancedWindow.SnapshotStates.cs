using System.Windows;
using System.Windows.Controls;
using ThinkControl.UI.Controls;

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
        _notificationSummary.Text = "3 items need attention";

        SheetMessage[] messages =
        [
            new(
                "PawnIO needs repair",
                "PawnIO is installed, but the ThinkControl hardware service cannot open its device. Fans and low-level sensors remain firmware-managed until the device handshake succeeds.",
                "Hardware setup",
                SheetAction.HardwareSetup,
                true),
            new(
                "Sensors are unavailable",
                "LibreHardwareMonitor has not produced usable telemetry yet. Retry rebuilds the provider once after the low-level component is healthy.",
                "Retry sensors",
                SheetAction.RefreshProviders,
                true),
            new(
                "Keyboard control is unavailable",
                "The active hardware provider has not produced a valid readback. ThinkControl will not send unverified keyboard writes.",
                "Retry keyboard",
                SheetAction.RefreshProviders,
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

    internal void PrepareAudioForSnapshot(bool providersAvailable)
    {
        const string audioPageKey = "ThinkControl.Dynamic.PageAudio";
        if (!Resources.Contains(audioPageKey) || Resources[audioPageKey] is not ScrollViewer { Content: AudioPanel panel })
            throw new InvalidOperationException("Audio page could not be prepared for visual QA.");

        panel.PrepareForSnapshot(providersAvailable);
    }
}
