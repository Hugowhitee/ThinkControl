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

        // ShowConfiguredInitialView already routes an Advanced preference through
        // OpenAdvancedSafely before post-startup polish evaluates an update handoff.
        // Do not perform a second no-op Advanced transition afterwards: besides
        // unnecessary shell work, that would dismiss the just-shown passive
        // "ThinkControl updated" confirmation. Only recover the preference when the
        // expected surface genuinely is not visible/non-minimized.
        if (_advancedWindow is { IsVisible: true, WindowState: not System.Windows.WindowState.Minimized })
            return;

        OpenAdvancedSafely("Home");
    }

    internal void ShowPreferredDesktopLaunchView()
    {
        if (IsAdvancedOpeningPreferred())
        {
            OpenAdvancedSafely("Home");
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
