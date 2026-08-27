using ThinkControl.UI;
using ThinkControl.UI.Services;

namespace ThinkControl.ShellSmoke;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            var app = App.CreateForVisualQa();
            app.InitializeComponent();
            ThemeService.Apply(ThemeMode.Dark);
            app.State.DeviceName = "ThinkPad X9-15 Gen 1";
            app.State.SelectedMode = "Balanced";
            app.State.BatteryPercent = 72;
            app.State.BatteryStatus = "On battery";
            app.State.CurrentRefreshHz = 120;
            app.State.MaxRefreshHz = 120;
            app.State.CoolingProfile = "Balanced";
            app.RunViewTransitionSmokeForVisualQa(cycles: 3);
            Console.WriteLine("Compact <-> Full view transition smoke passed (3 cycles).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
