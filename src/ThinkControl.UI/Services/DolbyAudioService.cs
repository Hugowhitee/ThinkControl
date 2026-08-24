using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace ThinkControl.UI.Services;

public sealed record DolbyAudioStatus(
    bool DolbyAccessInstalled,
    bool DaxBackendDetected,
    string Detail,
    bool DirectApiAvailable = false,
    string? ActiveProfile = null,
    string? ActiveSubProfile = null);

public sealed record DolbyProfileResult(bool Success, string Detail);

public sealed class DolbyAudioService
{
    public static readonly IReadOnlyList<string> OfficialProfiles = ["Dynamic", "Movie", "Music", "Game", "Voice"];
    public static readonly IReadOnlyList<string> GameSubProfiles = ["FPS", "Racing", "RTS", "RPG"];

    private const string AppUserModelId = "DolbyLaboratories.DolbyAccess_rz1tebttyb220!App";
    private const string StoreUri = "ms-windows-store://pdp/?ProductId=9N0866FS04W8";
    private const string DaxClsid = "{20532D01-15BE-4BB9-A727-CA34555D881C}";
    private const uint WmClose = 0x0010;

    public DolbyAudioStatus Probe()
    {
        bool access = IsDolbyAccessInstalled();
        bool dax = IsKnownDaxBackendRegistered();
        bool direct = TryReadDirectState(out string? activeProfile, out string? activeSubProfile);
        string detail = (access, dax, direct) switch
        {
            (_, true, true) => "Dolby DAX direct control is available; profile changes do not need to open Dolby Access.",
            (true, true, false) => "Dolby DAX is installed. Direct control is not exposed by this driver, so main profiles can use the Dolby Access fallback.",
            (true, false, false) => "Dolby Access is installed. The OEM DAX backend will be verified when a profile is applied.",
            (false, true, false) => "Dolby processing is present, but Dolby Access is not installed and this DAX build does not expose direct automation.",
            _ => "Dolby Access is not installed. Lenovo's audio driver may also be required for Dolby Atmos processing."
        };
        return new DolbyAudioStatus(access, dax, detail, direct, activeProfile, activeSubProfile);
    }

    public Task<DolbyProfileResult> SetProfileAsync(string profile, CancellationToken cancellationToken = default)
    {
        if (!OfficialProfiles.Contains(profile, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(new DolbyProfileResult(false, "Unsupported Dolby profile."));
        return Task.Run(() => SetProfile(profile, cancellationToken), cancellationToken);
    }

    public Task<DolbyProfileResult> SetSubProfileAsync(string subProfile, CancellationToken cancellationToken = default)
    {
        if (!GameSubProfiles.Contains(subProfile, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(new DolbyProfileResult(false, "Unsupported Dolby subprofile."));
        return Task.Run(() => SetDirectSubProfile(subProfile), cancellationToken);
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
        DolbyProfileResult direct = SetDirectProfile(profile);
        if (direct.Success)
            return direct;

        DolbyAudioStatus status = Probe();
        if (!status.DolbyAccessInstalled)
            return new DolbyProfileResult(false, direct.Detail + " Dolby Access is not installed for the safe fallback.");

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
            if (selected == false)
                return new DolbyProfileResult(false, $"Dolby Access did not confirm {profile}.");

            string verified = selected == true ? "selection read back" : "selection invoked";
            return new DolbyProfileResult(true, $"Dolby Atmos · {profile} · {verified} via safe fallback.");
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

    private static DolbyProfileResult SetDirectProfile(string profile)
    {
        if (!TryCreateDaxObject(out object? dax, out string? reason))
            return new DolbyProfileResult(false, reason ?? "Dolby DAX direct API is unavailable.");

        try
        {
            if (!TryInvokeDirectSetter(dax, "SetActiveProfile", profile, out string? error))
                return new DolbyProfileResult(false, error ?? "This Dolby DAX build does not accept named direct profile selection.");

            Thread.Sleep(120);
            if (TryInvokeDirectGetter(dax, "GetActiveProfile", out string? readBack) &&
                !string.IsNullOrWhiteSpace(readBack) &&
                !ProfileMatches(readBack, profile))
            {
                return new DolbyProfileResult(false, $"Dolby DAX did not verify {profile}; it reported '{readBack}'.");
            }

            return new DolbyProfileResult(true, $"Dolby Atmos · {profile} · applied through the direct DAX backend.");
        }
        finally
        {
            ReleaseCom(dax);
        }
    }

    private static DolbyProfileResult SetDirectSubProfile(string subProfile)
    {
        if (!TryCreateDaxObject(out object? dax, out string? reason))
            return new DolbyProfileResult(false, reason ?? "Dolby DAX direct API is unavailable.");

        try
        {
            if (!TryInvokeDirectSetter(dax, "SetActiveSubProfile", subProfile, out string? error))
                return new DolbyProfileResult(false, error ?? "This Dolby DAX build does not expose named subprofile control.");

            Thread.Sleep(120);
            if (TryInvokeDirectGetter(dax, "GetActiveSubProfile", out string? readBack) &&
                !string.IsNullOrWhiteSpace(readBack) &&
                !ProfileMatches(readBack, subProfile))
            {
                return new DolbyProfileResult(false, $"Dolby DAX did not verify {subProfile}; it reported '{readBack}'.");
            }

            return new DolbyProfileResult(true, $"Dolby Game · {subProfile} · applied directly without opening Dolby Access.");
        }
        finally
        {
            ReleaseCom(dax);
        }
    }

    private static bool TryReadDirectState(out string? profile, out string? subProfile)
    {
        profile = null;
        subProfile = null;
        if (!TryCreateDaxObject(out object? dax, out _))
            return false;

        try
        {
            bool profileReadable = TryInvokeDirectGetter(dax, "GetActiveProfile", out profile);
            bool subReadable = TryInvokeDirectGetter(dax, "GetActiveSubProfile", out subProfile);
            return profileReadable || subReadable;
        }
        finally
        {
            ReleaseCom(dax);
        }
    }

    private static bool TryCreateDaxObject(out object? instance, out string? reason)
    {
        instance = null;
        reason = null;
        try
        {
            Type? type = Type.GetTypeFromCLSID(Guid.Parse(DaxClsid), throwOnError: false);
            if (type is null)
            {
                reason = "Dolby DAX COM class is not registered.";
                return false;
            }
            instance = Activator.CreateInstance(type);
            if (instance is null)
            {
                reason = "Dolby DAX COM class could not be activated.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            reason = $"Dolby DAX direct API is unavailable: {ex.Message}";
            return false;
        }
    }

    private static bool TryInvokeDirectSetter(object instance, string method, string value, out string? error)
    {
        error = null;
        try
        {
            instance.GetType().InvokeMember(
                method,
                BindingFlags.InvokeMethod,
                binder: null,
                target: instance,
                args: [value],
                culture: System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Dolby DAX {method} is not available as a named automation method: {Unwrap(ex).Message}";
            return false;
        }
    }

    private static bool TryInvokeDirectGetter(object instance, string method, out string? value)
    {
        value = null;
        try
        {
            object? result = instance.GetType().InvokeMember(
                method,
                BindingFlags.InvokeMethod,
                binder: null,
                target: instance,
                args: null,
                culture: System.Globalization.CultureInfo.InvariantCulture);
            if (result is not null)
                value = Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture);
            return result is not null;
        }
        catch
        {
            // Some COM type libraries model getters as HRESULT GetX([out] value).
        }

        foreach (object seed in new object[] { string.Empty, 0 })
        {
            try
            {
                object?[] args = [seed];
                object? result = instance.GetType().InvokeMember(
                    method,
                    BindingFlags.InvokeMethod,
                    binder: null,
                    target: instance,
                    args: args,
                    culture: System.Globalization.CultureInfo.InvariantCulture);
                object? read = args[0] ?? result;
                if (read is not null)
                    value = Convert.ToString(read, System.Globalization.CultureInfo.InvariantCulture);
                return read is not null;
            }
            catch
            {
            }
        }
        return false;
    }

    private static bool ProfileMatches(string reported, string requested) =>
        reported.Contains(requested, StringComparison.OrdinalIgnoreCase) ||
        requested.Contains(reported, StringComparison.OrdinalIgnoreCase);

    private static Exception Unwrap(Exception ex) => ex is TargetInvocationException { InnerException: not null } invocation
        ? invocation.InnerException
        : ex;

    private static void ReleaseCom(object? instance)
    {
        if (instance is null || !Marshal.IsComObject(instance))
            return;
        try { Marshal.FinalReleaseComObject(instance); } catch { }
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
