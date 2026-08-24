namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    // WPF + WinForms are both referenced by the UI project, so Color can become
    // ambiguous in partial UI files. Keep notification-sheet color creation pinned
    // to WPF without changing the project's global using behavior.
    private static class Color
    {
        internal static System.Windows.Media.Color FromArgb(byte a, byte r, byte g, byte b) =>
            System.Windows.Media.Color.FromArgb(a, r, g, b);
    }
}
