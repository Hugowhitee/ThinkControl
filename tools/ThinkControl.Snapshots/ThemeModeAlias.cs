namespace ThinkControl.Snapshots;

// .NET 10/WPF also exposes System.Windows.ThemeMode. Keep the snapshot fixture's
// concise ThemeMode.Dark/Light calls bound to ThinkControl's own theme enum.
internal static class ThemeMode
{
    internal static ThinkControl.UI.Services.ThemeMode Dark => ThinkControl.UI.Services.ThemeMode.Dark;
    internal static ThinkControl.UI.Services.ThemeMode Light => ThinkControl.UI.Services.ThemeMode.Light;
}
