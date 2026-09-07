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
/// Coordinates the verified X9 Lenovo cooling surfaces between the Windows power
/// preference and ThinkControl's built-in cooling profiles.
///
/// Quiet/Balanced remain semantic LITSSvc thermal-policy transitions. Max cooling is
/// intentionally different: alpha.40 physical evidence showed that LITSSvc
/// Performance alone can report high RPM yet remain audibly below Lenovo Auto's real
/// high-cooling state. Alpha.41 therefore uses Lenovo Other Mode's known global
/// full-speed boolean only when the exact X9 exposes that exact feature by live GET
/// (and does not explicitly reject writes). The feature has verified readback and a
/// bounded ownership/rollback lifecycle; the rejected fanX_target writer stays off.
/// </summary>
internal sealed class LenovoCoolingPolicyCoordinator
{
    private readonly LenovoHardwareController _hardware;
    private readonly object _gate = new();
    private string? _basePowerMode;
    private string? _overrideProfile;
    private string? _overrideProfileId;
    private bool _fullSpeedOwned;
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

        string? activeProfile;
        lock (_gate)
        {
            _basePowerMode = mode;
            activeProfile = _overrideProfile;
        }

        // Lenovo exposes different LITSSvc commands for AC and DC, and firmware/OEM
        // components may re-apply their source policy after resume or a power-source
        // transition. Keeping only our in-memory override label would make telemetry
        // say Quiet/Balanced/Max while the machine had physically returned to its base
        // policy. Whenever the baseline is refreshed, re-assert the selected cooling
        // override through the same verified provider path for the current source.
        if (activeProfile is not null)
        {
            if (!SetBuiltInProfile(activeProfile, out string? reassertDetail))
            {
                detail = $"Stored Lenovo {mode} as the power-mode baseline, but the active {activeProfile} cooling override could not be reasserted for the current power source. " +
                         (reassertDetail ?? "Lenovo firmware rejected the reassertion.");
                return false;
            }

            detail = $"Stored Lenovo {mode} as the power-mode baseline and reasserted {activeProfile} cooling for the current power source. " +
                     (reassertDetail ?? string.Empty);
            return true;
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

        string? baseMode;
        string? previousProfile;
        bool fullSpeedOwned;
        lock (_gate)
        {
            baseMode = _basePowerMode;
            previousProfile = _overrideProfile;
            fullSpeedOwned = _fullSpeedOwned;
        }
        if (baseMode is null)
        {
            detail = "The current power-mode baseline is not known yet. Reapply the current Windows power preference before selecting a firmware cooling profile.";
            return false;
        }

        bool wantsFullSpeed = profileId == FanCurveDefaults.MaxCoolingId;
        LenovoOtherModeFullSpeedStatus fullSpeed = LenovoOtherModeFullSpeedService.Read(_hardware.Identity);

        if (!wantsFullSpeed && fullSpeed.Enabled && !fullSpeedOwned)
        {
            detail = "A Lenovo full-speed override is already active but was not started by this ThinkControl service instance. Quiet/Balanced will not silently disable another utility's fan ownership; return that utility to Auto first.";
            return false;
        }

        // Leaving ThinkControl-owned Max cooling must release the global full-speed
        // bit before applying a lower Lenovo policy. Failed rollback keeps ownership
        // and blocks the transition rather than pretending Quiet/Balanced succeeded.
        if (!wantsFullSpeed && fullSpeedOwned)
        {
            if (!LenovoOtherModeFullSpeedService.TrySet(_hardware.Identity, enabled: false, out _, out string? releaseDetail))
            {
                detail = releaseDetail ?? "ThinkControl-owned Lenovo full-speed state could not be released.";
                return false;
            }
            lock (_gate)
                _fullSpeedOwned = false;
        }

        if (!LenovoThermalPolicyService.TrySetX9Policy(_hardware.Identity, policyMode, out string? policyDetail))
        {
            detail = policyDetail;
            return false;
        }

        if (wantsFullSpeed)
        {
            if (!fullSpeed.Available || !fullSpeed.Writable)
            {
                RestorePreviousPolicy(previousProfile, baseMode, out string? rollback);
                detail = "Max cooling needs Lenovo Other Mode full-speed feature 0x04020000, but this X9 did not expose a safe live/readback contract. " +
                         fullSpeed.Detail + (string.IsNullOrWhiteSpace(rollback) ? string.Empty : $" · rollback: {rollback}");
                return false;
            }

            if (!LenovoOtherModeFullSpeedService.TrySet(_hardware.Identity, enabled: true, out bool changed, out string? fullSpeedDetail))
            {
                RestorePreviousPolicy(previousProfile, baseMode, out string? rollback);
                detail = (fullSpeedDetail ?? "Lenovo full-speed could not be enabled.") +
                         (string.IsNullOrWhiteSpace(rollback) ? string.Empty : $" · rollback: {rollback}");
                return false;
            }

            lock (_gate)
            {
                if (changed)
                    _fullSpeedOwned = true;
                _overrideProfile = profile;
                _overrideProfileId = profileId;
                _status = changed || _fullSpeedOwned
                    ? "Max cooling · Lenovo Other Mode full-speed + Performance thermal policy · live boolean readback verified"
                    : "Max cooling · Lenovo full-speed was already active externally + Performance thermal policy";
            }
            detail = $"{policyDetail} · {fullSpeedDetail}";
            return true;
        }

        lock (_gate)
        {
            _overrideProfile = profile;
            _overrideProfileId = profileId;
            _status = $"{profile} · Lenovo firmware {policyMode} cooling policy · OEM closed-loop fan ownership";
        }
        detail = policyDetail;
        return true;
    }

    internal bool ClearProfileOverride(out string? detail)
    {
        detail = null;
        if (!Supported)
            return true;

        string? baseMode;
        string? activeProfile;
        bool fullSpeedOwned;
        lock (_gate)
        {
            baseMode = _basePowerMode;
            activeProfile = _overrideProfile;
            fullSpeedOwned = _fullSpeedOwned;
        }

        if (fullSpeedOwned)
        {
            if (!LenovoOtherModeFullSpeedService.TrySet(_hardware.Identity, enabled: false, out _, out string? fullSpeedDetail))
            {
                detail = fullSpeedDetail ?? "ThinkControl-owned Lenovo full-speed state could not be released.";
                return false;
            }
            lock (_gate)
                _fullSpeedOwned = false;
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

    /// <summary>
    /// Explicit user/safety Auto request. Unlike automatic disposal cleanup, this may
    /// clear a live full-speed bit that was left behind by an earlier ThinkControl
    /// service instance. It still touches only the exact known boolean feature and
    /// verifies 0 by readback; arbitrary OEM state is never modified.
    /// </summary>
    internal bool RequestFirmwareAuto(out string? detail)
    {
        detail = null;
        if (!Supported)
            return true;

        LenovoOtherModeFullSpeedStatus fullSpeed = LenovoOtherModeFullSpeedService.Read(_hardware.Identity);
        if (fullSpeed.Available && fullSpeed.Enabled)
        {
            if (!LenovoOtherModeFullSpeedService.TrySet(_hardware.Identity, enabled: false, out _, out string? fullSpeedDetail))
            {
                detail = fullSpeedDetail ?? "Lenovo full-speed could not be returned to Auto.";
                return false;
            }
            lock (_gate)
                _fullSpeedOwned = false;
        }

        string? baseMode;
        string? activeProfile;
        lock (_gate)
        {
            baseMode = _basePowerMode;
            activeProfile = _overrideProfile;
        }

        // After a service restart there may be no in-memory profile/baseline. The
        // important recovery in that case is clearing the verified full-speed bit;
        // Windows/Lenovo already owns its current base thermal policy.
        if (activeProfile is null)
        {
            lock (_gate)
            {
                _overrideProfile = null;
                _overrideProfileId = null;
                _status = "Lenovo firmware Auto reasserted";
            }
            detail ??= fullSpeed.Available
                ? "Lenovo firmware Auto reasserted; global full-speed is off."
                : "Lenovo firmware Auto remains active.";
            return true;
        }

        if (baseMode is null)
        {
            detail = "The Lenovo cooling override cannot restore its power-mode baseline because that baseline is unknown.";
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

    private void RestorePreviousPolicy(string? previousProfile, string baseMode, out string? detail)
    {
        string restoreMode = baseMode;
        if (!string.IsNullOrWhiteSpace(previousProfile) &&
            TryMapProfile(previousProfile, out _, out _, out string previousMode))
        {
            restoreMode = previousMode;
        }
        _ = LenovoThermalPolicyService.TrySetX9Policy(_hardware.Identity, restoreMode, out detail);
    }

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
