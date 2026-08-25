using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace ThinkControl.UI.Services;

/// <summary>
/// On-demand profile bridge for modern Dolby Fusion systems that do not expose the
/// verified legacy DAX3 semantic API. A user profile click may drive the official
/// Dolby Access UI by semantic control names only. No private Fusion IDs, state
/// files, registry writes or background polling are used.
/// </summary>
internal sealed class DolbyAccessProfileBridge
{
    private const uint WmClose = 0x0010;
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(5);

    internal Task<DolbyProfileResult> SetProfileAsync(
        string profile,
        DolbyAudioService launcher,
        CancellationToken cancellationToken = default)
    {
        if (!DolbyDirectControlService.Profiles.Contains(profile, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(new DolbyProfileResult(false, "Unsupported Dolby profile."));

        return Task.Run(() => SetProfile(profile, launcher, cancellationToken), cancellationToken);
    }

    private static DolbyProfileResult SetProfile(
        string profile,
        DolbyAudioService launcher,
        CancellationToken cancellationToken)
    {
        IntPtr window = FindDolbyWindow();
        bool launched = window == IntPtr.Zero;
        if (launched && !launcher.OpenDolbyAccess())
            return new(false, "Windows could not open Dolby Access for profile control.");

        try
        {
            window = WaitForWindow(window, LaunchTimeout, cancellationToken);
            if (window == IntPtr.Zero)
                return new(false, "Dolby Access did not expose a window in time. Repair or install Dolby Access and try again.");

            AutomationElement root = AutomationElement.FromHandle(window);
            if (root is null)
                return new(false, "Dolby Access opened, but its controls could not be inspected.");

            ActionableElement? target = FindActionableByName(root, profile);
            if (target is null)
            {
                ActionableElement? settings = FindActionableByName(root, "Settings");
                if (settings is not null)
                {
                    _ = Activate(settings);
                    Thread.Sleep(300);
                }

                DateTimeOffset deadline = DateTimeOffset.UtcNow + NavigationTimeout;
                while (target is null && DateTimeOffset.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    target = FindActionableByName(root, profile);
                    if (target is null)
                        Thread.Sleep(150);
                }
            }

            if (target is null)
                return new(false, $"Dolby Access did not expose the '{profile}' profile control in this app version.");

            if (!Activate(target))
                return new(false, $"Dolby Access exposed '{profile}', but Windows could not select it.");

            Thread.Sleep(250);
            bool? selected = ReadSelected(target.Element);
            if (selected == false)
            {
                ActionableElement? refreshed = FindActionableByName(root, profile);
                selected = refreshed is null ? false : ReadSelected(refreshed.Element);
            }

            if (selected == false)
                return new(false, $"Dolby Access did not confirm the {profile} profile after selection.");

            return selected == true
                ? new(true, $"Dolby Atmos · {profile} · selected through Dolby Access and read back.")
                : new(true, $"Dolby Atmos · {profile} · selected through the official Dolby Access control.");
        }
        catch (OperationCanceledException)
        {
            return new(false, "Dolby profile change cancelled.");
        }
        catch (ElementNotAvailableException)
        {
            return new(false, "Dolby Access changed its UI while the profile was being selected. Try again.");
        }
        catch (Exception ex)
        {
            return new(false, $"Dolby Access profile bridge failed safely: {ex.Message}");
        }
        finally
        {
            // If ThinkControl opened the official app solely for this explicit user
            // action, restore the previous desktop state afterwards. Never close a
            // Dolby Access window the user already had open.
            if (launched && window != IntPtr.Zero)
                _ = PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static IntPtr WaitForWindow(IntPtr existing, TimeSpan timeout, CancellationToken cancellationToken)
    {
        IntPtr window = existing;
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (window == IntPtr.Zero && DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(150);
            window = FindDolbyWindow();
        }
        return window;
    }

    private static ActionableElement? FindActionableByName(AutomationElement root, string name)
    {
        try
        {
            AutomationElementCollection matches = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(
                    AutomationElement.NameProperty,
                    name,
                    PropertyConditionFlags.IgnoreCase));

            ActionableElement? invokeFallback = null;
            foreach (AutomationElement match in matches)
            {
                ActionableElement? actionable = ResolveActionableAncestor(match);
                if (actionable is null)
                    continue;

                if (SupportsSelection(actionable.Element))
                    return actionable;
                invokeFallback ??= actionable;
            }
            return invokeFallback;
        }
        catch
        {
            return null;
        }
    }

    private static ActionableElement? ResolveActionableAncestor(AutomationElement element)
    {
        AutomationElement? current = element;
        for (int depth = 0; current is not null && depth < 5; depth++)
        {
            try
            {
                if (SupportsSelection(current) || SupportsInvoke(current))
                    return new(current);
                current = TreeWalker.ControlViewWalker.GetParent(current);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private static bool Activate(ActionableElement actionable)
    {
        try
        {
            if (actionable.Element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object? select) &&
                select is SelectionItemPattern selection)
            {
                selection.Select();
                return true;
            }

            if (actionable.Element.TryGetCurrentPattern(InvokePattern.Pattern, out object? invoke) &&
                invoke is InvokePattern invokePattern)
            {
                invokePattern.Invoke();
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool SupportsSelection(AutomationElement element)
    {
        try { return element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out _); }
        catch { return false; }
    }

    private static bool SupportsInvoke(AutomationElement element)
    {
        try { return element.TryGetCurrentPattern(InvokePattern.Pattern, out _); }
        catch { return false; }
    }

    private static bool? ReadSelected(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object? pattern) &&
                pattern is SelectionItemPattern selection)
            {
                return selection.Current.IsSelected;
            }
        }
        catch
        {
        }
        return null;
    }

    private static IntPtr FindDolbyWindow()
    {
        IntPtr found = IntPtr.Zero;
        _ = EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window))
                return true;

            var title = new StringBuilder(256);
            _ = GetWindowText(window, title, title.Capacity);
            string value = title.ToString();
            if (value.Equals("Dolby Access", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Dolby Access", StringComparison.OrdinalIgnoreCase))
            {
                found = window;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private sealed record ActionableElement(AutomationElement Element);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
