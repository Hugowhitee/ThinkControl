using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ThinkControl.Core.Ipc;
using ThinkControl.UI;
using ThinkControl.UI.Controls;

namespace ThinkControl.ShellSmoke;

internal static partial class Program
{
    private static void ValidateCoolingSelectionSync(App app)
    {
        string previous = app.State.CoolingProfile;
        var panel = new FansPanel();
        panel.Initialize(app);
        var selector = (ComboBox)panel.FindName("ProfileComboBox");
        var window = new Window { Content = panel, Width = 700, Height = 700, ShowActivated = false };
        void Require(string expected)
        {
            if (selector.Text != expected)
                throw new InvalidOperationException($"Cooling selection must immediately show {expected}, got {selector.Text}.");
        }
        try
        {
            app.State.CoolingProfile = "Max cooling";
            window.Show();
            Require("Max cooling");
            app.State.CoolingProfile = "Balanced";
            Require("Balanced");
            window.Hide();
            app.State.CoolingProfile = "Quiet";
            window.Show();
            Require("Quiet");
            var stale = new ServiceResponse(1, true,
                Telemetry: new TelemetrySnapshot(60, "QA", 4400, "QA", "Auto", "QA", "Off", CoolingProfile: "Max cooling"),
                Capabilities: new HardwareCapabilitySnapshot(true, true, true, true, FanControlKind: FanControlKinds.DiscreteEc));
            typeof(FansPanel).GetMethod("ApplyStatus", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, [stale]);
            Require("Quiet");
            window.Hide();
            if ((bool)typeof(FansPanel).GetField("_statusSubscribed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel)!)
                throw new InvalidOperationException("Hidden cooling page retained status subscriptions.");
            Console.WriteLine("Cooling selection: immediate navigation, visible changes, hidden-page reopening, stale telemetry and subscription cleanup passed.");
        }
        finally
        {
            window.Close();
            app.State.CoolingProfile = previous;
        }
    }
}
