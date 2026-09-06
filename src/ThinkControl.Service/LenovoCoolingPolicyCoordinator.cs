using ThinkControl.Core.Cooling;
using ThinkControl.Hardware.Lenovo;

namespace ThinkControl.Service;

internal sealed record LenovoCoolingPolicySnapshot(
    bool Supported,
    bool OverrideActive,
    string Profile,
    string? ProfileId,
    string Status);

/// <summary>
/// Coordinates the verified X9 LITSSvc thermal-policy surface between the Windows
/// performance preference and ThinkControl's built-in cooling profiles.
///
/// The important boundary is that this is firmware policy, not a fake direct RPM
/// writer. A cooling-profile override may temporarily take precedence over Lenovo's
/// thermal policy while Windows power preference remains independently configured.
/// Clearing the cooling profile restores the last power-mode policy observed from
/// the UI, so the two product surfaces do not silently fight each other.
/// </summary>
internal sealed class LenovoCoolingPolicyCoordinator
{
    private readonly LenovoHardwareController _hardware;
    private readonly object _gate = new();
    private string? _basePowerMode;
    private string? _overrideProfile;
    private string? _overrideProfileId;
    private string _status = "Lenovo firmware cooling policy available";

    internal LenovoCoolingPolicyCoordinator(LenovoHardwareController hardware) => _hardware = hardware;

    internal bool Supported => _hardware.Identity.IsVerifiedX9;

    internal LenovoCoolingPolicySnapshot Snapshot()
    {
        lock (_gate)
        {
            return new LenovoCoolingPolicySnapshot(
                Supported,
                _overrideProfile is not null,
                _overrideProfile ?? "Lenovo Auto",
                _overrideProfileId,
                _overrideProfile is not null
                    ? _status
                    : "Lenovo firmware owns cooling; thermal policy follows the active Windows power preference");
        }
    }

    internal bool SetBasePowerMode(string? raw, out string? detail)
    {
        detail = null;
        if (!Supported)
        {
            detail = "Lenovo firmware cooling policy is available only on the verified X9 21Q6/21Q7 profile.";
            return false;
        }
        if (!TryNormalizePowerMode(raw, out string mode))
        {
            detail = "Thermal mode must be Quiet, Balanced or Performance.";
            return false;
        }

        lock (_gate)
        {
            _basePowerMode = mode;
            if (_overrideProfile is not null)
            {
                detail = $"Stored Lenovo {mode} as the power-mode baseline; {_overrideProfile} cooling remains the active firmware-policy override.";
                return true;
            }
        }

        bool success = LenovoThermalPolicyService.TrySetX9Policy(_hardware.Identity, mode, out detail);
        if (success)
        {
            lock (_gate)
                _status = $"Lenovo firmware thermal policy follows {mode}";
        }
        return success;
    }

    internal bool SetBuiltInProfile(string? raw, out string? detail)
    {
        detail = null;
        if (!Supported)
        {
            detail = "This device does not expose the verified Lenovo firmware cooling-profile backend.";
            return false;
        }
        if (!TryMapProfile(raw, out string profile, out string profileId, out string policyMode))
        {
            detail = "Firmware cooling supports the built-in Quiet, Balanced and Max cooling profiles only. Custom curves require a physically accepted direct fan writer.";
            return false;
        }

        lock (_gate)
        {
            if (_basePowerMode is null)
            {
                detail = "The current power-mode baseline is not known yet. Reapply the current Windows power preference before selecting a firmware cooling profile.";
                return false;
            }
        }

        if (!LenovoThermalPolicyService.TrySetX9Policy(_hardware.Identity, policyMode, out string? providerDetail))
        {
            detail = providerDetail;
            return false;
        }

        lock (_gate)
        {
            _overrideProfile = profile;
            _overrideProfileId = profileId;
            _status = $"{profile} · Lenovo firmware {policyMode} cooling policy · smooth OEM fan ownership";
        }
        detail = providerDetail;
        return true;
    }

    internal bool ClearProfileOverride(out string? detail)
    {
        detail = null;
        if (!Supported)
            return true;

        string? baseMode;
        string? activeProfile;
        lock (_gate)
        {
            baseMode = _basePowerMode;
            activeProfile = _overrideProfile;
        }

        if (activeProfile is null)
            return true;
        if (baseMode is null)
        {
            detail = "The Lenovo cooling override cannot be cleared safely because the power-mode baseline is unknown.";
            return false;
        }

        if (!LenovoThermalPolicyService.TrySetX9Policy(_hardware.Identity, baseMode, out string? providerDetail))
        {
            detail = providerDetail;
            return false;
        }

        lock (_gate)
        {
            _overrideProfile = null;
            _overrideProfileId = null;
            _status = $"Lenovo firmware cooling returned to the {baseMode} power-mode policy";
        }
        detail = providerDetail;
        return true;
    }

    internal static bool IsBuiltInProfile(string? raw) =>
        TryMapProfile(raw, out _, out _, out _);

    private static bool TryMapProfile(string? raw, out string profile, out string profileId, out string policyMode)
    {
        string normalized = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        (profile, profileId, policyMode) = normalized switch
        {
            "quiet" or "silent" or FanCurveDefaults.QuietId => ("Quiet", FanCurveDefaults.QuietId, "Quiet"),
            "balanced" or "normal" or FanCurveDefaults.BalancedId => ("Balanced", FanCurveDefaults.BalancedId, "Balanced"),
            "max cooling" or "maxcooling" or "cool" or FanCurveDefaults.MaxCoolingId => ("Max cooling", FanCurveDefaults.MaxCoolingId, "Performance"),
            _ => (string.Empty, string.Empty, string.Empty)
        };
        return profile.Length > 0;
    }

    private static bool TryNormalizePowerMode(string? raw, out string mode)
    {
        mode = raw?.Trim() switch
        {
            var value when value?.Equals("Quiet", StringComparison.OrdinalIgnoreCase) == true => "Quiet",
            var value when value?.Equals("Efficiency", StringComparison.OrdinalIgnoreCase) == true => "Quiet",
            var value when value?.Equals("Balanced", StringComparison.OrdinalIgnoreCase) == true => "Balanced",
            var value when value?.Equals("Performance", StringComparison.OrdinalIgnoreCase) == true => "Performance",
            _ => string.Empty
        };
        return mode.Length > 0;
    }
}
