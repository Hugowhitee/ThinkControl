using System.Windows;

namespace ThinkControl.UI;

public partial class App
{
    /// <summary>
    /// A WPF-owned ThinkControl window is part of the same interaction, regardless
    /// of whether the host desktop grants it foreground ownership immediately. This
    /// is especially important for non-activating attention windows: clicking one
    /// must never be interpreted as leaving ThinkControl and dismiss Compact.
    /// </summary>
    internal bool KeepCompactVisibleForInternalWindow()
    {
        if (System.Windows.Application.Current is not Application app || CompactWindow is null)
            return false;

        foreach (Window window in app.Windows.OfType<Window>())
        {
            if (ReferenceEquals(window, CompactWindow) || !window.IsVisible)
                continue;

            if (window.IsActive || ReferenceEquals(window.Owner, CompactWindow))
            {
                RecordShellEvent("shell.compact.deactivation-kept", true, "internal-owned-window");
                return true;
            }
        }

        return false;
    }
}
