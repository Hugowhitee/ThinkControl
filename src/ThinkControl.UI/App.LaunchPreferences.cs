namespace ThinkControl.UI;

public partial class App
{
    private const string TrayLaunchArgument = "--tray";

    internal bool ShouldSuppressInitialCompactLaunch =>
        IsTrayOnlyLaunch() || IsAdvancedOpeningPreferred();

    internal bool IsTrayOnlyLaunch() =>
        Environment.GetCommandLineArgs()
            .Skip(1)
            .Any(argument => string.Equals(argument, TrayLaunchArgument, StringComparison.OrdinalIgnoreCase));

    private bool IsAdvancedOpeningPreferred() =>
        string.Equals(
            UserSettings.Current.DefaultOpeningView,
            "Advanced",
            StringComparison.OrdinalIgnoreCase);

    internal void ApplyPreferredLaunchViewAfterStartup()
    {
        if (IsTrayOnlyLaunch() || !IsAdvancedOpeningPreferred())
            return;

        OpenAdvanced("Home");
    }

    internal void ShowPreferredDesktopLaunchView()
    {
        if (IsAdvancedOpeningPreferred())
        {
            OpenAdvanced("Home");
            if (_advancedWindow is { } advanced)
            {
                if (advanced.WindowState == System.Windows.WindowState.Minimized)
                    advanced.WindowState = System.Windows.WindowState.Normal;
                advanced.ShowInTaskbar = true;
                advanced.Activate();
            }
            return;
        }

        ShowThinkControlFromTray();
    }
}
