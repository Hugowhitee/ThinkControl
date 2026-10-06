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
                text.Text = "Choose a backlight level or let your laptop manage it with Auto. Effects add animation to the light.";
            }
            else if (current.StartsWith("Active: High", StringComparison.Ordinal))
            {
                text.Text = "Auto: controlled by firmware";
            }
            else if (current.StartsWith("Auto is a ThinkControl policy", StringComparison.Ordinal))
            {
                text.Text = "Auto lets your laptop manage the keyboard light.";
            }
            else if (current.StartsWith("Auto uses normal verified Off / Low / High", StringComparison.Ordinal))
            {
                text.Text = "Effects are available when your laptop supports repeated light changes.";
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
