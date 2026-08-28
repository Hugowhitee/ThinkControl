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
        // visual-QA render the same Auto contract without dispatcher timing tricks.
        foreach (TextBlock text in FindKeyboardTextBlocks(PageKeyboard))
        {
            string current = text.Text ?? string.Empty;
            if (current.StartsWith("Hardware levels and ThinkControl effects are kept separate:", StringComparison.Ordinal))
            {
                text.Text = "Hardware levels stay separate from effects: Off / Low / High are device states; Auto prefers verified Lenovo firmware when the active backend exposes it, while Breathing / Reactive / Audio remain user-session effects.";
            }
            else if (current.StartsWith("Active: High", StringComparison.Ordinal))
            {
                text.Text = "Preferred: Lenovo Auto    Fallback: High → Low at 15 s → Off at 35 s";
            }
            else if (current.StartsWith("Auto is a ThinkControl policy", StringComparison.Ordinal))
            {
                text.Text = "Auto first requests Lenovo's native firmware state and verifies readback. If the active backend cannot set it, ThinkControl falls back to the idle policy above.";
            }
            else if (current.StartsWith("Auto uses normal verified Off / Low / High", StringComparison.Ordinal))
            {
                text.Text = "Auto uses Lenovo firmware Auto when verified and otherwise falls back to normal Off / Low / High idle control. Breathing, Reactive and Audio require the stricter direct-effect backend.";
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
