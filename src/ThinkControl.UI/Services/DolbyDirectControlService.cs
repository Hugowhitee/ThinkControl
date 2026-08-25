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
/// Best-effort direct controller for OEM Dolby DAX3 builds. It uses semantic DAX
/// operations and verifies readback whenever the installed build exposes it. It
/// never launches Dolby Access for a profile change, edits Dolby registry/state
/// files, or accepts an unverified numeric IEQ candidate.
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

            // Do not advertise a write-only path. A semantic getter is required so
            // ThinkControl can verify that the requested profile really became
            // active instead of treating an accepted COM call as success.
            bool profileControl = profileReadable;
            bool toneControl = subReadable || ieqReadable;
            string detail = profileControl || toneControl
                ? "Dolby DAX direct control detected · changes stay inside ThinkControl"
                : "Dolby DAX is registered, but this build does not expose a compatible direct control surface";

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
            if (!TryGet(dax, "GetActiveProfile", out _))
                return new(false, "This Dolby DAX build cannot verify the active profile, so ThinkControl left audio unchanged.");

            string? lastError = null;
            foreach (string candidate in new[] { profile, profile.ToLowerInvariant() }.Distinct(StringComparer.Ordinal))
            {
                if (!TrySet(dax, "SetActiveProfile", candidate, out lastError))
                    continue;

                Thread.Sleep(100);
                if (TryGet(dax, "GetActiveProfile", out object? readBack))
                {
                    string? normalized = NormalizeProfile(readBack);
                    if (!string.Equals(normalized, profile, StringComparison.OrdinalIgnoreCase))
                        continue;
                    return new(true, $"Dolby Atmos · {profile} · direct DAX readback verified.");
                }

                lastError = $"Dolby DAX accepted '{profile}' but did not confirm it through readback.";
            }

            return new(false, lastError ?? $"The installed Dolby DAX build did not accept direct profile '{profile}'.");
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
            bool subProfileReadable = TryGet(dax, "GetActiveSubProfile", out _);
            bool ieqReadable = TryGet(dax, "GetIEQ", out _);
            if (!subProfileReadable && !ieqReadable)
                return new(false, "This Dolby DAX build cannot verify its tone state, so ThinkControl left audio unchanged.");

            if (TrySet(dax, "SetActiveSubProfile", tone, out _))
            {
                Thread.Sleep(90);
                if (TryGet(dax, "GetActiveSubProfile", out object? readBack) &&
                    string.Equals(NormalizeTone(readBack), tone, StringComparison.OrdinalIgnoreCase))
                {
                    return new(true, $"Dolby tone · {tone} · direct DAX readback verified.");
                }
            }

            if (string.Equals(tone, "Off", StringComparison.OrdinalIgnoreCase) &&
                TryInvokeNoArgs(dax, "ResetIEQ"))
            {
                Thread.Sleep(90);
                if (TryGet(dax, "GetIEQ", out object? resetBack) && NormalizeTone(resetBack) == "Off")
                    return new(true, "Dolby Intelligent Equalizer · Off · direct reset verified.");
            }

            if (TrySet(dax, "SetIEQ", tone, out string? error))
            {
                Thread.Sleep(90);
                if (TryGet(dax, "GetIEQ", out object? readBack) &&
                    string.Equals(NormalizeTone(readBack), tone, StringComparison.OrdinalIgnoreCase))
                {
                    return new(true, $"Dolby Intelligent Equalizer · {tone} · direct DAX readback verified.");
                }
            }

            return new(false, error ?? $"The installed Dolby DAX build does not expose direct IEQ control for {tone}.");
        }
        finally
        {
            Release(dax);
        }
    }

    private static string? NormalizeProfile(object? value)
    {
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        return Profiles.FirstOrDefault(profile =>
            text.Contains(profile, StringComparison.OrdinalIgnoreCase) ||
            profile.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeTone(object? value)
    {
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        string? named = TonePresets.FirstOrDefault(tone =>
            text.Contains(tone, StringComparison.OrdinalIgnoreCase) ||
            tone.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (named is not null)
            return named;

        return null;
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
        ? invocation.InnerException!
        : ex;

    private static void Release(object? instance)
    {
        if (instance is null || !Marshal.IsComObject(instance))
            return;
        try { Marshal.FinalReleaseComObject(instance); } catch { }
    }
}
