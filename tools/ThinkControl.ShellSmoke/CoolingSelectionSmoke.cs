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

            // A verified discrete EC writer must not present a pretend 0-100%
            // continuous slider. Missing calibration has no actionable choices.
            var speedPicker = (ComboBox)panel.FindName("ManualSpeedComboBox");
            var percentSlider = (Slider)panel.FindName("ManualPercentSlider");
            var testButton = (Button)panel.FindName("ManualPercentApplyButton");
            if (speedPicker.Visibility != Visibility.Visible ||
                percentSlider.Visibility != Visibility.Collapsed ||
                speedPicker.Items.Count != 0 || testButton.IsEnabled)
                throw new InvalidOperationException("Discrete fan tests offered an uncalibrated fake percentage target.");

            int[] levels = [4, 5, 6, 7, 0x40];
            int[] rpms = [3500, 3700, 4000, 4400, 9400];
            var calibration = new FanCharacterizationSnapshot(false, null, 5, 5, "Measured", null,
                levels.Select((level, i) => new FanLevelCalibrationSnapshot(level,
                    [new FanCalibrationFanSnapshot("shared", "Shared tachometer", rpms[i], 30, true)],
                    true)).ToArray());
            var measured = new ServiceResponse(1, true,
                Telemetry: new TelemetrySnapshot(60, "QA", null, "Unavailable", "Auto", "QA", "Off",
                    FanCharacterization: calibration),
                Capabilities: new HardwareCapabilitySnapshot(true, true, true, true,
                    FanControlKind: FanControlKinds.DiscreteEc,
                    FanCalibrationSupported: true));
            typeof(FansPanel).GetMethod("ApplyStatus", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, [measured]);
            if (speedPicker.Items.Count != 5 || speedPicker.SelectedItem is null ||
                !speedPicker.Items.Cast<ThinkControl.Core.Cooling.ManualFanSpeedChoices.Choice>().Last().FullSpeed ||
                !testButton.IsEnabled)
                throw new InvalidOperationException("Verified discrete fan options were not selectable or Full was not explicit.");
            if (percentSlider.Visibility != Visibility.Collapsed)
                throw new InvalidOperationException("Unbounded fan percent slider remained visible for discrete EC.");
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
