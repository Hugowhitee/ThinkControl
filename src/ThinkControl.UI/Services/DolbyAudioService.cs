using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace ThinkControl.UI.Services;

public sealed record DolbyAudioStatus(
    bool DolbyAccessInstalled,
    bool DaxBackendDetected,
    string Detail);

public sealed record DolbyProfileResult(bool Success, string Detail);

public sealed class DolbyAudioService
{
    public static readonly IReadOnlyList<string> OfficialProfiles = ["Dynamic", "Movie", "Music", "Game", "Voice"];

    private const string AppUserModelId = "DolbyLaboratories.DolbyAccess_rz1tebttyb220!App";
    private const string StoreUri = "ms-windows-store://pdp/?ProductId=9N0866FS04W8";
    private const string DaxClsid = "{20532D01-15BE-4BB9-A727-CA34555D881C}";
    private const uint WmClose = 0x0010;

    public DolbyAudioStatus Probe()
    {
        bool access = IsDolbyAccessInstalled();
        bool dax = IsKnownDaxBackendRegistered();
        string detail = (access, dax) switch
        {
            (true, true) => "Dolby Access and a Dolby DAX backend are installed.",
            (true, false) => "Dolby Access is installed. The active OEM Dolby backend will be verified when a profile is applied.",
            (false, true) => "Dolby processing is present, but Dolby Access is not installed.",
            _ => "Dolby Access is not installed. Lenovo's audio driver may also be required for Dolby Atmos processing."
        };
        return new DolbyAudioStatus(access, dax, detail);
    }

    public Task<DolbyProfileResult> SetProfileAsync(string profile, CancellationToken cancellationToken = default)
    {
        if (!OfficialProfiles.Contains(profile, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(new DolbyProfileResult(false, "Unsupported Dolby profile."));
        return Task.Run(() => SetProfile(profile, cancellationToken), cancellationToken);
    }

    public bool OpenDolbyAccess()
    {
        try
        {
            string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            Process.Start(new ProcessStartInfo(explorer, $"shell:AppsFolder\\{AppUserModelId}") { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool OpenStore()
    {
        try
        {
            Process.Start(new ProcessStartInfo(StoreUri) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private DolbyProfileResult SetProfile(string profile, CancellationToken cancellationToken)
    {
        DolbyAudioStatus status = Probe();
        if (!status.DolbyAccessInstalled)
            return new DolbyProfileResult(false, "Dolby Access is not installed.");

        IntPtr window = FindDolbyWindow();
        bool launched = window == IntPtr.Zero;
        if (launched && !OpenDolbyAccess())
            return new DolbyProfileResult(false, "Dolby Access could not be launched.");

        try
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
            while (window == IntPtr.Zero && DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(200);
                window = FindDolbyWindow();
            }
            if (window == IntPtr.Zero)
                return new DolbyProfileResult(false, "Dolby Access did not expose a window in time.");

            AutomationElement root = AutomationElement.FromHandle(window);
            if (root is null)
                return new DolbyProfileResult(false, "Dolby Access UI could not be inspected.");

            // Dolby Access commonly keeps the Atmos profiles on its Settings page.
            // Invoking Settings is harmless when the profile controls are already visible.
            AutomationElement? settings = FindByName(root, "Settings");
            if (settings is not null)
            {
                TryInvoke(settings);
                Thread.Sleep(350);
            }

            AutomationElement? target = null;
            deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(8);
            while (target is null && DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                target = FindByName(root, profile);
                if (target is null)
                    Thread.Sleep(200);
            }
            if (target is null)
                return new DolbyProfileResult(false, $"The '{profile}' control was not found in this Dolby Access version.");

            if (!TryInvoke(target))
                return new DolbyProfileResult(false, $"Dolby Access exposed '{profile}' but did not provide an invokable selection control.");

            Thread.Sleep(300);
            bool? selected = TryReadSelected(target);
            string verified = selected == true
                ? "selection read back"
                : selected == false
                    ? "selection did not read back"
                    : "selection invoked; this Dolby UI exposes no selection readback";
            if (selected == false)
                return new DolbyProfileResult(false, $"Dolby Access did not confirm {profile}.");

            return new DolbyProfileResult(true, $"Dolby Atmos · {profile} · {verified}.");
        }
        catch (OperationCanceledException)
        {
            return new DolbyProfileResult(false, "Dolby profile change cancelled.");
        }
        catch (ElementNotAvailableException)
        {
            return new DolbyProfileResult(false, "Dolby Access changed its UI while the profile was being selected.");
        }
        catch (Exception ex)
        {
            return new DolbyProfileResult(false, $"Dolby Access automation failed safely: {ex.Message}");
        }
        finally
        {
            if (launched && window != IntPtr.Zero)
                PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static bool IsDolbyAccessInstalled()
    {
        try
        {
            using RegistryKey? packages = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
            if (packages?.GetSubKeyNames().Any(name => name.StartsWith("DolbyLaboratories.DolbyAccess_", StringComparison.OrdinalIgnoreCase)) == true)
                return true;
        }
        catch
        {
        }

        // The AUMID may still work even if Repository visibility is restricted.
        return FindDolbyWindow() != IntPtr.Zero;
    }

    private static bool IsKnownDaxBackendRegistered()
    {
        try
        {
            using RegistryKey? key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{DaxClsid}");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    private static AutomationElement? FindByName(AutomationElement root, string name)
    {
        try
        {
            return root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, name, PropertyConditionFlags.IgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static bool TryInvoke(AutomationElement element)
    {
        AutomationElement? current = element;
        for (int depth = 0; current is not null && depth < 4; depth++)
        {
            try
            {
                if (current.TryGetCurrentPattern(InvokePattern.Pattern, out object? invoke) && invoke is InvokePattern invokePattern)
                {
                    invokePattern.Invoke();
                    return true;
                }
                if (current.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object? select) && select is SelectionItemPattern selection)
                {
                    selection.Select();
                    return true;
                }
                current = TreeWalker.ControlViewWalker.GetParent(current);
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    private static bool? TryReadSelected(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object? pattern) && pattern is SelectionItemPattern selection)
                return selection.Current.IsSelected;
        }
        catch
        {
        }
        return null;
    }

    private static IntPtr FindDolbyWindow()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            var title = new StringBuilder(256);
            _ = GetWindowText(window, title, title.Capacity);
            if (title.ToString().Contains("Dolby Access", StringComparison.OrdinalIgnoreCase))
            {
                found = window;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
