using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private const string ShellChromePolishKey = "ThinkControl.Advanced.ShellChromePolish";

    /// <summary>
    /// Advanced uses the real Windows title bar for Snap Layouts, maximize/restore
    /// and the system menu. Keep the application content directly underneath it;
    /// a second in-app title/command strip only duplicates hierarchy and wastes
    /// vertical space.
    /// </summary>
    private void ConfigureShellChromePolish()
    {
        if (Resources.Contains(ShellChromePolishKey))
            return;

        if (Content is not Border { Child: Grid root } || root.RowDefinitions.Count < 2)
            return;

        Resources[ShellChromePolishKey] = true;

        // ConfigureNativeWindow already hands caption ownership to Windows. Make
        // that contract explicit here too so a later page-polish pass cannot revive
        // the legacy XAML caption or add another Compact-view strip.
        root.RowDefinitions[0].Height = new GridLength(0);
        foreach (UIElement captionChild in root.Children
                     .OfType<UIElement>()
                     .Where(element => Grid.GetRow(element) == 0)
                     .ToArray())
        {
            captionChild.Visibility = Visibility.Collapsed;
        }
    }
}
