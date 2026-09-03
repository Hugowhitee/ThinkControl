using Xunit;

namespace ThinkControl.Core.Tests.Hardware;

public sealed class FanAutoHandoffSourceTests
{
    [Fact]
    public void ExplicitAuto_ReassertsFirmwareOwnershipBeyondInMemoryOwnerTracking()
    {
        string root = FindRepositoryRoot();
        string controller = ReadNormalized(Path.Combine(root, "src", "ThinkControl.Hardware", "Lenovo", "LenovoHardwareController.cs"));
        string otherMode = ReadNormalized(Path.Combine(root, "src", "ThinkControl.Hardware", "Lenovo", "LenovoOtherModeFanProvider.cs"));

        Assert.Contains("This method represents an explicit user/safety request for Lenovo Auto", controller, StringComparison.Ordinal);
        Assert.Contains("_otherModeFans.RequestFirmwareAuto(out _, out error)", controller, StringComparison.Ordinal);
        Assert.Contains("byte control = _ec.ReadFanControl();", controller, StringComparison.Ordinal);
        Assert.Contains("if (IsThinkControlFanState(control))", controller, StringComparison.Ordinal);
        Assert.Contains("_ec.ReturnToBios();", controller, StringComparison.Ordinal);
        Assert.Contains("control != ThinkPadRegisters.BiosControl", controller, StringComparison.Ordinal);

        Assert.Contains("internal bool RequestFirmwareAuto(out string? detail, out string? error)", otherMode, StringComparison.Ordinal);
        Assert.Contains("liveWritable.Length < 2", otherMode, StringComparison.Ordinal);
        Assert.Contains("TrySetFeatureValue(method, channel.AttributeId, 0", otherMode, StringComparison.Ordinal);
        Assert.Contains("Lenovo Auto reasserted through OEM target 0", otherMode, StringComparison.Ordinal);

        // Automatic refresh/dispose cleanup remains ownership-aware. The wider
        // reassertion path is reserved for an explicit Auto/safety request.
        Assert.Contains("if (_activeFanControlKind == LenovoFanControlKind.LenovoOtherModeTargetRpm)", controller, StringComparison.Ordinal);
        Assert.Contains("try { _otherModeFans.ReturnToAuto(out _); }", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReturnAllFanProvidersToAutoUnlocked();\n            _otherModeFans.RequestFirmwareAuto", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void SavedAutoPreference_IsActuallyAppliedOnX9Startup()
    {
        string root = FindRepositoryRoot();
        string cooling = ReadNormalized(Path.Combine(root, "src", "ThinkControl.UI", "App.Cooling.cs"));

        Assert.Contains("bool wantsAuto = selected.Equals(\"Lenovo Auto\"", cooling, StringComparison.Ordinal);
        Assert.Contains("DeviceCapabilityExpectations.IsVerifiedX9(State.MachineType)", cooling, StringComparison.Ordinal);
        Assert.Contains("response.Capabilities?.FanControl != true && !(wantsAuto && verifiedX9)", cooling, StringComparison.Ordinal);
        Assert.Contains("ServiceResponse? auto = await HardwareClient.ReturnFanToAutoAsync();", cooling, StringComparison.Ordinal);
        Assert.Contains("Saved Lenovo Auto preference could not be reasserted", cooling, StringComparison.Ordinal);

        // Do not regress to the old UI-only restore that merely painted the combo
        // as Auto without asking the service/hardware to hand ownership back.
        Assert.DoesNotContain("if (selected == \"Lenovo Auto\")\n        {\n            State.CoolingProfile = \"Lenovo Auto\";\n            return;", cooling, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualOutput_NeverMakesFanSelectorsPretendAutoIsSelected()
    {
        string root = FindRepositoryRoot();
        string fans = ReadNormalized(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "FansPanel.xaml.cs"));
        string compact = ReadNormalized(Path.Combine(root, "src", "ThinkControl.UI", "Controls", "CompactDashboard.QuickControls.cs"));
        string home = ReadNormalized(Path.Combine(root, "src", "ThinkControl.UI", "AdvancedWindow.HomeQuickControls.cs"));

        Assert.Contains("bool manual = IsManualProfile(profileName);", fans, StringComparison.Ordinal);
        Assert.Contains("RebuildProfileChoices(manual ? _currentProfileId : null);", fans, StringComparison.Ordinal);
        Assert.Contains("new FanProfileChoice(manualState!.Trim(), manualState.Trim(), Selectable: false)", fans, StringComparison.Ordinal);
        Assert.Contains("if (!choice.Selectable || ProfileIdsEqual(choice.Id, _currentProfileId))", fans, StringComparison.Ordinal);
        Assert.Contains("selecting Auto afterwards must be a real", fans, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileComboBox.SelectedItem = selected ?? _profileChoices.FirstOrDefault();", fans, StringComparison.Ordinal);

        Assert.Contains("if (IsManualFanState(current))", compact, StringComparison.Ordinal);
        Assert.Contains("values.Add(current);", compact, StringComparison.Ordinal);
        Assert.Contains("if (IsManualFanState(raw))", compact, StringComparison.Ordinal);
        Assert.DoesNotContain("StartsWith(\"Manual \", StringComparison.OrdinalIgnoreCase) => \"Auto\"", compact, StringComparison.Ordinal);

        Assert.Contains("if (e.PropertyName == nameof(ViewModels.AppState.CoolingProfile))", home, StringComparison.Ordinal);
        Assert.Contains("if (manual)\n                values.Add(selected);", home, StringComparison.Ordinal);
        Assert.Contains("IsManualHomeFanState(profile)", home, StringComparison.Ordinal);
        Assert.Contains("manual target is shown truthfully", home, StringComparison.Ordinal);
    }

    private static string ReadNormalized(string path) =>
        File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new(start);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "src", "ThinkControl.Hardware")) &&
                    Directory.Exists(Path.Combine(current.FullName, "tests", "ThinkControl.Core.Tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the ThinkControl repository root for Auto handoff validation.");
    }
}
