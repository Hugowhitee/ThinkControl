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
        _notificationSummary.Text = "1 item needs attention";

        SheetMessage[] messages =
        [
            new(
                "Low-level hardware access",
                "PawnIO is installed, but the ThinkControl hardware service cannot open its device. Repair this prerequisite once; dependent fan, sensor and keyboard providers are checked again only after low-level access is healthy.",
                "Repair component",
                SheetAction.HardwareRepair,
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

        // Normal/minimum cover the direct DAX state, unavailable has its dedicated
        // render, and the wide matrix exercises modern Fusion-only layout. This keeps
        // both provider generations under the existing visual gate without inventing
        // a second snapshot harness.
        if (providersAvailable && Width >= 1500)
            panel.PrepareFusionForSnapshot();
        else
            panel.PrepareForSnapshot(providersAvailable);
    }
}
