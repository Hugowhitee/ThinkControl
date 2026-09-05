using System.Windows;
using ThinkControl.Core.Touchpad;

namespace ThinkControl.UI.Controls;

public partial class TouchpadPanel
{
    /// <summary>
    /// Snapshot rendering has no physical Precision Touchpad, so haptic controls
    /// cannot inherit live Windows/provider state. Keep the deterministic fixture
    /// internally coherent with the synthetic detected-touchpad header without
    /// touching hardware or user settings.
    /// </summary>
    internal void PrepareHapticsForSnapshot()
    {
        HapticSwitch.IsEnabled = true;
        HapticSwitch.IsChecked = true;
        HapticStrengthSlider.IsEnabled = true;
        HapticStrengthSlider.Value = 50;
        HapticStrengthValue.Text = "Medium";
        ClickForceSlider.IsEnabled = true;
        ClickForceSlider.Value = 50;
        ClickForceValue.Text = "Medium";
        HapticStatusText.Text = "Uses the same discrete levels as Windows touchpad settings.";
    }

    /// <summary>
    /// Reuse the existing right-corner snapshot pair to cover the opt-in reverse
    /// close affordance as well as the normal left-corner launch fixtures. This does
    /// not persist settings or route an action; it only prepares deterministic UI.
    /// </summary>
    internal void PrepareReverseCornerForSnapshot(TouchpadCorner corner, bool live)
    {
        // Build the selected corner from a non-live baseline first. The caller no
        // longer composes an inward live fixture before this method, so the reverse
        // trail starts clean instead of inheriting/connecting to inward contact 1.
        PrepareCornerForSnapshot(corner, live: false);

        TouchpadCornerLaunchBindings launches = _configuration.CornerLaunches ?? new TouchpadCornerLaunchBindings();
        launches = corner switch
        {
            TouchpadCorner.TopLeft => launches with { TopLeftReverseClose = true },
            TouchpadCorner.TopRight => launches with { TopRightReverseClose = true },
            _ => launches
        };

        _syncing = true;
        try
        {
            _configuration = (_configuration with { CornerLaunches = launches }).Sanitize();
            Visualizer.Configuration = _configuration;
            SyncCornerLaunchControls();
        }
        finally
        {
            _syncing = false;
        }

        if (!live)
        {
            Visualizer.SetTestFrame(Array.Empty<TouchContact>(), null);
            RefreshGestureZoneVisuals(null);
            GestureStatusText.Text = corner == TouchpadCorner.TopLeft
                ? "Top-left corner selected · Compact · reverse close on"
                : "Top-right corner selected · Advanced · reverse close on";
            return;
        }

        GestureActionKind action = _configuration.LaunchFor(corner);
        var reverse = new GestureSignal(
            GesturePhase.Active,
            Edge: null,
            Action: action,
            TotalTravelMm: 9.9,
            DeltaMm: 2.4,
            ContactId: 1,
            Corner: corner,
            CornerDirection: CornerGestureDirection.Outward);

        if (corner == TouchpadCorner.TopLeft)
        {
            Visualizer.SetTestFrame([new TouchContact(1, 1650, 1650, true)], reverse);
            Visualizer.SetTestFrame([new TouchContact(1, 950, 950, true)], reverse);
        }
        else
        {
            Visualizer.SetTestFrame([new TouchContact(1, 11850, 1650, true)], reverse);
            Visualizer.SetTestFrame([new TouchContact(1, 12550, 950, true)], reverse);
        }

        RefreshGestureZoneVisuals(reverse);
        GestureStatusText.Text = corner == TouchpadCorner.TopLeft
            ? "Top-left reverse · closing ThinkControl"
            : "Top-right reverse · closing ThinkControl";
    }

    /// <summary>
    /// Live corner Candidate/Active emphasis must never change editor visibility or
    /// child structure. Alpha.33 fixed the reflow regression; keep that invariant as
    /// an executable visual-QA assertion while still allowing opacity/hit-test state
    /// to communicate temporary runtime ownership.
    /// </summary>
    internal void ValidateCornerEditorLayoutForSnapshot(TouchpadCorner corner, bool live)
    {
        if (_selectedZone.Corner != corner)
            throw new InvalidOperationException("Snapshot corner selection drifted before layout validation.");
        if (_edgeEditorCard?.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Selecting a corner must hide only the edge editor before live input begins.");
        if (_cornerEditorCard?.Visibility != Visibility.Visible)
            throw new InvalidOperationException("Selected corner editor must stay visible in both selected and live fixtures.");

        if (live)
        {
            if (_cornerEditorCard.IsHitTestVisible || _cornerEditorCard.Opacity >= 0.99)
                throw new InvalidOperationException("Live corner ownership must dim/disable the existing editor without reflowing it.");
        }
        else if (!_cornerEditorCard.IsHitTestVisible || Math.Abs(_cornerEditorCard.Opacity - 1) > 0.01)
        {
            throw new InvalidOperationException("Selected corner editor should be fully interactive outside live ownership.");
        }
    }
}
