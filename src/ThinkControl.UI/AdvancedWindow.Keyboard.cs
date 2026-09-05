using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private void ConfigureKeyboardAutoUi()
    {
        if (PageKeyboard is null)
            return;

        // The Keyboard page is normally collapsed while Advanced initializes. WPF
        // does not guarantee a materialized visual tree for collapsed content, but
        // the XAML logical tree already exists. Use that stable tree so runtime and
        // visual-QA render the same keyboard mode/effect contract without dispatcher
        // timing tricks.
        foreach (TextBlock text in FindKeyboardTextBlocks(PageKeyboard))
        {
            string current = text.Text ?? string.Empty;
            if (current.StartsWith("Hardware levels and ThinkControl effects are kept separate:", StringComparison.Ordinal))
            {
                text.Text = "Hardware levels stay separate from effects: Off / Low / High are device states; Auto is the active provider's verified firmware-managed mode when available; Breathing / Reactive / Audio are ThinkControl user-session effects.";
            }
            else if (current.StartsWith("Active: High", StringComparison.Ordinal))
            {
                text.Text = "Firmware Auto · provider managed";
            }
            else if (current.StartsWith("Auto is a ThinkControl policy", StringComparison.Ordinal))
            {
                text.Text = "Auto requests the active provider's verified firmware-managed state and requires readback. ThinkControl does not emulate Auto with an idle-dimming effect.";
            }
            else if (current.StartsWith("Auto uses normal verified Off / Low / High", StringComparison.Ordinal))
            {
                text.Text = "Breathing, Reactive and Audio require a provider that advertises safe repeated backlight writes; fallbacks without that capability keep effects disabled.";
            }
        }
    }

    private static IEnumerable<TextBlock> FindKeyboardTextBlocks(DependencyObject parent)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is TextBlock text)
                yield return text;

            if (child is not DependencyObject dependency)
                continue;

            foreach (TextBlock descendant in FindKeyboardTextBlocks(dependency))
                yield return descendant;
        }
    }
}
