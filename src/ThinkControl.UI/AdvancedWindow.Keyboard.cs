using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _keyboardAutoUiHooked;

    private void ConfigureKeyboardAutoUi()
    {
        if (PageKeyboard is null)
            return;

        // PageKeyboard is collapsed during initial Advanced-surface setup, so its
        // visual children may not be materialized yet. Hook visibility once and
        // rerun this idempotent copy pass after WPF has built the visible tree.
        if (!_keyboardAutoUiHooked)
        {
            _keyboardAutoUiHooked = true;
            PageKeyboard.IsVisibleChanged += (_, args) =>
            {
                if (args.NewValue is true)
                    Dispatcher.BeginInvoke(new Action(ConfigureKeyboardAutoUi));
            };
        }

        foreach (TextBlock text in FindVisualChildren<TextBlock>(PageKeyboard))
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
}
