using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ThinkControl.UI.Services;

internal sealed record DolbyDirectState(
    bool Available,
    bool CanProfileControl,
    bool CanToneControl,
    string? ActiveProfile,
    string? ActiveTone,
    string Detail);

/// <summary>
/// Conservative direct controller for OEM Dolby DAX builds. ThinkControl only
/// exposes semantic profile/tone operations that the installed DAX object can read
/// back. It does not edit Dolby registry/state files, invent game subprofiles or
/// guess numeric preset identifiers.
/// </summary>
internal sealed class DolbyDirectControlService
{
    internal static readonly IReadOnlyList<string> Profiles = ["Dynamic", "Movie", "Music", "Game", "Voice"];
    internal static readonly IReadOnlyList<string> TonePresets = ["Balanced", "Detailed", "Warm", "Off"];

    private const string DaxClsid = "{20532D01-15BE-4BB9-A727-CA34555D881C}";

    internal DolbyDirectState Probe()
    {
        if (!TryCreate(out object? dax, out string? reason) || dax is null)
            return new(false, false, false, null, null, reason ?? "Dolby DAX direct API unavailable");

        try
        {
            bool profileReadable = TryGet(dax, "GetActiveProfile", out object? profileRaw);
            bool subReadable = TryGet(dax, "GetActiveSubProfile", out object? subRaw);
            bool ieqReadable = TryGet(dax, "GetIEQ", out object? ieqRaw);

            string? profile = profileReadable ? NormalizeProfile(profileRaw) : null;
            string? tone = subReadable ? NormalizeTone(subRaw) : null;
            tone ??= ieqReadable ? NormalizeTone(ieqRaw) : null;

            // A readable semantic state is the non-mutating capability boundary.
            // __ComObject reflection frequently claims no members even though
            // IDispatch calls work, so we deliberately do not infer capability from
            // reflection alone and we never probe setters by changing user state.
            bool profileControl = profileReadable && profile is not null;
            bool toneControl = (subReadable || ieqReadable) && tone is not null;
            string detail = profileControl || toneControl
                ? "Dolby DAX direct state detected · supported changes are verified by readback"
                : "Dolby DAX is registered, but this build does not expose a readable semantic control surface";

            return new(true, profileControl, toneControl, profile, tone, detail);
        }
        finally
        {
            Release(dax);
        }
    }

    internal Task<DolbyProfileResult> SetProfileAsync(string profile, CancellationToken cancellationToken = default)
    {
        if (!Profiles.Contains(profile, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(new DolbyProfileResult(false, "Unsupported Dolby profile."));
        return Task.Run(() => SetProfile(profile), cancellationToken);
    }

    internal Task<DolbyProfileResult> SetToneAsync(string tone, CancellationToken cancellationToken = default)
    {
        if (!TonePresets.Contains(tone, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(new DolbyProfileResult(false, "Unsupported Dolby Intelligent Equalizer tone."));
        return Task.Run(() => SetTone(tone), cancellationToken);
    }

    private static DolbyProfileResult SetProfile(string profile)
    {
        if (!TryCreate(out object? dax, out string? reason) || dax is null)
            return new(false, reason ?? "Dolby DAX direct API unavailable.");

        try
        {
            string? lastError = null;
            foreach (string candidate in new[] { profile, profile.ToLowerInvariant() }.Distinct(StringComparer.Ordinal))
            {
                if (!TrySet(dax, "SetActiveProfile", candidate, out lastError))
                    continue;

                Thread.Sleep(100);
                if (!TryGet(dax, "GetActiveProfile", out object? readBack))
                    continue;

                string? normalized = NormalizeProfile(readBack);
                if (string.Equals(normalized, profile, StringComparison.OrdinalIgnoreCase))
                    return new(true, $"Dolby Atmos · {profile} · direct DAX readback verified.");
            }

            return new(false, lastError ?? $"The installed Dolby DAX build did not verify direct profile '{profile}'.");
        }
        finally
        {
            Release(dax);
        }
    }

    private static DolbyProfileResult SetTone(string tone)
    {
        if (!TryCreate(out object? dax, out string? reason) || dax is null)
            return new(false, reason ?? "Dolby DAX direct API unavailable.");

        try
        {
            if (string.Equals(tone, "Off", StringComparison.OrdinalIgnoreCase) &&
                TryInvokeNoArgs(dax, "ResetIEQ"))
            {
                Thread.Sleep(90);
                if (TryGet(dax, "GetIEQ", out object? resetBack) &&
                    string.Equals(NormalizeTone(resetBack), "Off", StringComparison.OrdinalIgnoreCase))
                {
                    return new(true, "Dolby Intelligent Equalizer · Off · direct DAX readback verified.");
                }
            }

            if (TrySet(dax, "SetActiveSubProfile", tone, out string? subError))
            {
                Thread.Sleep(90);
                if (TryGet(dax, "GetActiveSubProfile", out object? readBack) &&
                    string.Equals(NormalizeTone(readBack), tone, StringComparison.OrdinalIgnoreCase))
                {
                    return new(true, $"Dolby Intelligent Equalizer · {tone} · direct subprofile readback verified.");
                }
            }

            if (TrySet(dax, "SetIEQ", tone, out string? ieqError))
            {
                Thread.Sleep(90);
                if (TryGet(dax, "GetIEQ", out object? readBack) &&
                    string.Equals(NormalizeTone(readBack), tone, StringComparison.OrdinalIgnoreCase))
                {
                    return new(true, $"Dolby Intelligent Equalizer · {tone} · direct DAX readback verified.");
                }
            }

            string detail = !string.IsNullOrWhiteSpace(ieqError) ? ieqError! :
                !string.IsNullOrWhiteSpace(subError) ? subError! :
                $"The installed Dolby DAX build does not expose a verified semantic IEQ control for {tone}.";
            return new(false, detail);
        }
        finally
        {
            Release(dax);
        }
    }

    private static string? NormalizeProfile(object? value)
    {
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return null;
        return Profiles.FirstOrDefault(profile =>
            text.Contains(profile, StringComparison.OrdinalIgnoreCase) ||
            profile.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeTone(object? value)
    {
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return null;
        return TonePresets.FirstOrDefault(tone =>
            text.Contains(tone, StringComparison.OrdinalIgnoreCase) ||
            tone.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryCreate(out object? instance, out string? reason)
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
            reason = $"Dolby DAX direct API unavailable: {Unwrap(ex).Message}";
            return false;
        }
    }

    private static bool TrySet(object instance, string method, object value, out string? error)
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
                culture: CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Dolby DAX {method} rejected direct control: {Unwrap(ex).Message}";
            return false;
        }
    }

    private static bool TryInvokeNoArgs(object instance, string method)
    {
        try
        {
            instance.GetType().InvokeMember(
                method,
                BindingFlags.InvokeMethod,
                binder: null,
                target: instance,
                args: null,
                culture: CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGet(object instance, string method, out object? value)
    {
        value = null;
        try
        {
            value = instance.GetType().InvokeMember(
                method,
                BindingFlags.InvokeMethod,
                binder: null,
                target: instance,
                args: null,
                culture: CultureInfo.InvariantCulture);
            if (value is not null)
                return true;
        }
        catch
        {
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
                    culture: CultureInfo.InvariantCulture);
                value = args[0] ?? result;
                if (value is not null)
                    return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static Exception Unwrap(Exception ex) => ex is TargetInvocationException { InnerException: not null } invocation
        ? invocation.InnerException
        : ex;

    private static void Release(object? instance)
    {
        if (instance is null || !Marshal.IsComObject(instance))
            return;
        try { Marshal.FinalReleaseComObject(instance); } catch { }
    }
}
