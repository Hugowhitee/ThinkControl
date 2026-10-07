using ThinkControl.Core.Audio;
using ThinkControl.Core.Ipc;

namespace ThinkControl.UI.Services;

internal sealed class ThinkControlModeCoordinator
{
    private readonly App _app;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<ThinkControlModeFacet> _owned = [];
    // A later sparse/no-op mode must not certify settings left uncertain by rollback.
    private readonly HashSet<ThinkControlModeFacet> _uncertainFacets = [];

    private string? _coolingBaseline;
    private RefreshModeSnapshot? _refreshBaseline;
    private AudioSafetyMode? _audioBaseline;
    private bool? _touchpadBaseline;
    private KeyboardModeSnapshot? _keyboardBaseline;
    private bool _applying;
    private ThinkControlModeDefinition _activeDefinition = ThinkControlModeCatalog.NoMode;

    internal sealed record Session(ThinkControlModeDefinition Definition, bool Modified);
    internal static ThinkControlModeDefinition WithoutFacet(ThinkControlModeDefinition definition, ThinkControlModeFacet facet) => facet switch
    {
        ThinkControlModeFacet.PerformanceMode => definition with { PerformanceMode = null },
        ThinkControlModeFacet.CoolingProfile => definition with { CoolingProfile = null },
        ThinkControlModeFacet.RefreshRate => definition with { RefreshRate = null },
        ThinkControlModeFacet.AudioSafety => definition with { AudioSafety = null },
        ThinkControlModeFacet.TouchpadGestures => definition with { TouchpadGesturesEnabled = null },
        ThinkControlModeFacet.KeyboardLight => definition with { KeyboardLight = null },
        _ => definition
    };

    internal Session CaptureSession() => new(_activeDefinition with
    {
        PerformanceMode = _owned.Contains(ThinkControlModeFacet.PerformanceMode) ? _activeDefinition.PerformanceMode : null,
        CoolingProfile = _owned.Contains(ThinkControlModeFacet.CoolingProfile) ? _activeDefinition.CoolingProfile : null,
        RefreshRate = _owned.Contains(ThinkControlModeFacet.RefreshRate) ? _activeDefinition.RefreshRate : null,
        AudioSafety = _owned.Contains(ThinkControlModeFacet.AudioSafety) ? _activeDefinition.AudioSafety : null,
        TouchpadGesturesEnabled = _owned.Contains(ThinkControlModeFacet.TouchpadGestures) ? _activeDefinition.TouchpadGesturesEnabled : null,
        KeyboardLight = _owned.Contains(ThinkControlModeFacet.KeyboardLight) ? _activeDefinition.KeyboardLight : null
    }, IsModified);

    internal async Task<bool> RestoreSessionAsync(Session session)
    {
        var definition = ThinkControlModeCatalog.Find(session.Definition.Id, _app.UserSettings.Current.CustomModes) is null
            ? ThinkControlModeCatalog.NoMode : session.Definition;
        if (!await ActivateAsync(definition.Id, ThinkControlModeActivationOrigin.Restore, definition)) return false;
        IsModified = session.Modified && definition.Id != ThinkControlModeCatalog.NormalId;
        Publish();
        return true;
    }

    internal ThinkControlModeCoordinator(App app)
    {
        _app = app;
        Publish();
    }

    internal event Action? Changed;

    internal string ActiveModeId { get; private set; } = ThinkControlModeCatalog.NormalId;
    internal string ActiveModeName { get; private set; } = "No mode";
    internal bool ActiveModeAutomatic { get; private set; }
    internal bool IsModified { get; private set; }
    internal string? LastTransitionError { get; private set; }
    internal string? FailedModeId { get; private set; }
    internal string? FailedModeName { get; private set; }
    internal bool SettingsNeedChecking { get; private set; }
    internal string? TransitionModeId { get; private set; }
    internal string? TransitionModeName { get; private set; }
    internal bool IsTransitioning => TransitionModeId is not null;
    internal string VisibleModeId => TransitionModeId ?? ActiveModeId;
    internal string VisibleModeName => TransitionModeName ?? ActiveModeName;

    internal IReadOnlyList<ThinkControlModeDefinition> GetModes() =>
        ThinkControlModeCatalog.VisibleModes(_app.UserSettings.Current.CustomModes);

    internal bool OwnsFacet(ThinkControlModeFacet facet) => _owned.Contains(facet);

    internal string? AvailabilityError(ThinkControlModeDefinition target)
    {
        if (target.CoolingProfile is not null && !_app.State.CanFanControl)
            return "Cooling is unavailable. Remove Cooling to use the other settings.";
        if (target.CoolingProfile is not null && !FanControlKinds.SupportsProfile(_app.State.FanControlKind, target.CoolingProfile))
            return "Cooling supports Auto or Max cooling. Change or remove this setting.";
        if (target.KeyboardLight is not null && !_app.State.CanKeyboardBacklight)
            return "Keyboard control is unavailable.";
        return null;
    }

    internal async Task<bool> ActivateAsync(
        string id,
        ThinkControlModeActivationOrigin origin = ThinkControlModeActivationOrigin.Manual,
        ThinkControlModeDefinition? sessionDefinition = null)
    {
        await _gate.WaitAsync();
        try
        {
            ThinkControlModeDefinition? target = sessionDefinition ?? ThinkControlModeCatalog.Find(
                id,
                _app.UserSettings.Current.CustomModes);
            FailedModeId = id;
            FailedModeName = target?.Name;
            if (target is null)
            {
                LastTransitionError = "This mode is no longer available.";
                Publish();
                return false;
            }

            LastTransitionError = null;
            FailedModeId = target.Id;
            FailedModeName = target.Name;
            if (AvailabilityError(target) is string availabilityError)
            {
                LastTransitionError = availabilityError;
                Publish();
                return false;
            }
            string previousId = ActiveModeId;
            string previousName = ActiveModeName;
            bool previousAutomatic = ActiveModeAutomatic;
            bool previousModified = IsModified;
            bool previouslyUncertain = SettingsNeedChecking;
            HashSet<ThinkControlModeFacet> previousOwned = [.. _owned];
            string? previousCoolingBaseline = _coolingBaseline;
            RefreshModeSnapshot? previousRefreshBaseline = _refreshBaseline;
            AudioSafetyMode? previousAudioBaseline = _audioBaseline;
            bool? previousTouchpadBaseline = _touchpadBaseline;
            KeyboardModeSnapshot? previousKeyboardBaseline = _keyboardBaseline;
            ThinkControlModeDefinition previousDefinition = _activeDefinition;

            HashSet<ThinkControlModeFacet> targetFacets =
                [.. ThinkControlModeCatalog.Facets(target)];
            // Rollback only facets that were actually attempted. Replaying
            // every planned facet can otherwise clear an external Lenovo
            // full-speed override even when the *first* Windows power action
            // failed and cooling was never touched.
            HashSet<ThinkControlModeFacet> attemptedFacets = [];

            TransitionModeId = target.Id;
            TransitionModeName = target.Name;
            Publish();

            _applying = true;
            try
            {
                foreach (ThinkControlModeFacet facet in targetFacets)
                {
                    if (!previousOwned.Contains(facet))
                        CaptureBaseline(facet);
                }

                foreach (ThinkControlModeFacet facet in OrderedFacets(targetFacets))
                {
                    attemptedFacets.Add(facet);
                    if (!await TryApplyFacetAsync(target, facet))
                    {
                        string failure = DescribeFacetFailure(facet);
                        // Failed Quiet/Balanced when an unrelated Lenovo utility
                        // owns full-speed must not cause an implicit Auto write.
                        if (facet == ThinkControlModeFacet.CoolingProfile &&
                            App.IsExternalCoolingOwnerConflict(_app.LastCoolingError))
                            attemptedFacets.Remove(facet);
                        bool recovered = await RollBackAsync(previousDefinition, previousOwned, attemptedFacets);
                        if (!recovered) _uncertainFacets.UnionWith(attemptedFacets);
                        SettingsNeedChecking = previouslyUncertain || !recovered;
                        LastTransitionError = failure + (recovered
                            ? " Previous settings were requested again."
                            : " Recovery was incomplete; check the affected settings.");
                        RestoreCoordinatorState(
                            previousId,
                            previousName,
                            previousAutomatic,
                            previousModified || !recovered,
                            previousOwned,
                            previousCoolingBaseline,
                            previousRefreshBaseline,
                            previousAudioBaseline,
                            previousTouchpadBaseline,
                            previousKeyboardBaseline);
                        return false;
                    }
                }

                foreach (ThinkControlModeFacet facet in OrderedFacets(previousOwned.Except(targetFacets)))
                {
                    attemptedFacets.Add(facet);
                    if (!await TryRestoreBaselineAsync(facet))
                    {
                        string failure = DescribeFacetFailure(facet);
                        bool recovered = await RollBackAsync(previousDefinition, previousOwned, attemptedFacets);
                        if (!recovered) _uncertainFacets.UnionWith(attemptedFacets);
                        SettingsNeedChecking = previouslyUncertain || !recovered;
                        LastTransitionError = failure + (recovered
                            ? " Previous settings were requested again."
                            : " Recovery was incomplete; check the affected settings.");
                        RestoreCoordinatorState(
                            previousId,
                            previousName,
                            previousAutomatic,
                            previousModified || !recovered,
                            previousOwned,
                            previousCoolingBaseline,
                            previousRefreshBaseline,
                            previousAudioBaseline,
                            previousTouchpadBaseline,
                            previousKeyboardBaseline);
                        return false;
                    }

                    ClearBaseline(facet);
                }

                _owned.Clear();
                foreach (ThinkControlModeFacet facet in targetFacets)
                    _owned.Add(facet);

                ActiveModeId = target.Id;
                _activeDefinition = target;
                ActiveModeName = target.Name;
                ActiveModeAutomatic =
                    origin == ThinkControlModeActivationOrigin.Automatic &&
                    target.Id != ThinkControlModeCatalog.NormalId;
                IsModified = false;
                _uncertainFacets.ExceptWith(attemptedFacets);
                SettingsNeedChecking = _uncertainFacets.Count > 0;
                FailedModeId = null;
                FailedModeName = null;
                TransitionModeId = null;
                TransitionModeName = null;
                Publish();
                if (origin == ThinkControlModeActivationOrigin.Manual)
                    _app.NotifyManualModeSelection();
                return true;
            }
            finally
            {
                _applying = false;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal Task<bool> ReapplyAsync() => ActivateAsync(
        ActiveModeId,
        ActiveModeAutomatic
            ? ThinkControlModeActivationOrigin.Automatic
            : ThinkControlModeActivationOrigin.Manual);

    internal async Task<bool> ReapplyOwnedFacetAsync(ThinkControlModeFacet facet)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_owned.Contains(facet))
                return false;

            ThinkControlModeDefinition? active = ThinkControlModeCatalog.Find(
                ActiveModeId,
                _app.UserSettings.Current.CustomModes);
            if (active is null)
                return false;

            _applying = true;
            try
            {
                return await ApplyFacetAsync(active, facet);
            }
            finally
            {
                _applying = false;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void ReleaseFacet(ThinkControlModeFacet facet)
    {
        if (_applying || ActiveModeId == ThinkControlModeCatalog.NormalId || !_owned.Remove(facet))
            return;

        ClearBaseline(facet);
        if (ActiveModeAutomatic) _app.ModeAutomation.ReleaseRestoreFacet(facet);
        IsModified = true;
        Publish();
    }

    internal bool SaveCustomMode(ThinkControlModeDefinition mode)
    {
        ThinkControlModeDefinition? sanitized = ThinkControlModeCatalog.SanitizeCustomMode(mode);
        if (sanitized is null)
            return false;

        var modes = (_app.UserSettings.Current.CustomModes ?? []).ToList();
        if (modes.Any(existing =>
                !existing.Id.Equals(sanitized.Id, StringComparison.OrdinalIgnoreCase) &&
                existing.Name.Equals(sanitized.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        int index = modes.FindIndex(existing =>
            existing.Id.Equals(sanitized.Id, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
            modes[index] = sanitized;
        else if (modes.Count < ThinkControlModeCatalog.MaxCustomModes)
            modes.Add(sanitized);
        else
            return false;

        _app.UserSettings.Update(settings => settings with { CustomModes = modes.ToArray() });

        if (ActiveModeId.Equals(sanitized.Id, StringComparison.OrdinalIgnoreCase))
        {
            ActiveModeName = sanitized.Name;
            IsModified = true;
            Publish();
        }
        else
        {
            Changed?.Invoke();
        }

        _app.RequestModeAutomationEvaluation();
        return true;
    }

    internal bool DeleteCustomMode(string id)
    {
        if (ActiveModeId.Equals(id, StringComparison.OrdinalIgnoreCase))
            return false;

        ThinkControlModeDefinition[] current = _app.UserSettings.Current.CustomModes ?? [];
        ThinkControlModeDefinition[] next = current
            .Where(mode => !mode.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (next.Length == current.Length)
            return false;

        _app.UserSettings.Update(settings => settings with { CustomModes = next });
        Changed?.Invoke();
        _app.RequestModeAutomationEvaluation();
        return true;
    }

    private void CaptureBaseline(ThinkControlModeFacet facet)
    {
        switch (facet)
        {
            case ThinkControlModeFacet.CoolingProfile:
                _coolingBaseline = _app.State.CoolingProfile;
                break;
            case ThinkControlModeFacet.RefreshRate:
                _refreshBaseline = _app.CaptureRefreshModeSnapshot();
                break;
            case ThinkControlModeFacet.AudioSafety:
                _audioBaseline = _app.AudioSafety.Mode;
                break;
            case ThinkControlModeFacet.TouchpadGestures:
                _touchpadBaseline = _app.GetEffectiveTouchpadGesturesEnabled();
                break;
            case ThinkControlModeFacet.KeyboardLight:
                _keyboardBaseline = _app.CaptureKeyboardModeSnapshot();
                break;
        }
    }

    private async Task<bool> ApplyFacetAsync(
        ThinkControlModeDefinition mode,
        ThinkControlModeFacet facet)
    {
        switch (facet)
        {
            case ThinkControlModeFacet.PerformanceMode:
                return mode.PerformanceMode is null ||
                       _app.ApplyPowerModeOverride(mode.PerformanceMode);

            case ThinkControlModeFacet.CoolingProfile:
                return mode.CoolingProfile is null ||
                       await _app.ApplyCoolingModeOverrideAsync(mode.CoolingProfile);

            case ThinkControlModeFacet.RefreshRate:
                return mode.RefreshRate is null ||
                       _app.ApplyRefreshModeOverride(mode.RefreshRate);

            case ThinkControlModeFacet.AudioSafety:
                if (mode.AudioSafety is null)
                    return true;
                AudioSafetyTransitionResult audio = await _app.ApplyAudioSafetyModeFromModeAsync(
                    ThinkControlModeCatalog.ParseAudioSafety(mode.AudioSafety));
                return audio.Success;

            case ThinkControlModeFacet.TouchpadGestures:
                if (mode.TouchpadGesturesEnabled is not bool gestures)
                    return true;
                _app.ApplyTouchpadGestureModeOverride(gestures);
                return true;

            case ThinkControlModeFacet.KeyboardLight:
                return mode.KeyboardLight is null ||
                       await _app.ApplyKeyboardLightModeOverrideAsync(mode.KeyboardLight);

            default:
                return true;
        }
    }

    private async Task<bool> RestoreBaselineAsync(ThinkControlModeFacet facet)
    {
        switch (facet)
        {
            case ThinkControlModeFacet.PerformanceMode:
                return _app.RestorePowerModeOverride();

            case ThinkControlModeFacet.CoolingProfile:
                return _coolingBaseline is null ||
                       await _app.RestoreCoolingModeBaselineAsync(_coolingBaseline);

            case ThinkControlModeFacet.RefreshRate:
                return _refreshBaseline is null ||
                       _app.RestoreRefreshModeSnapshot(_refreshBaseline);

            case ThinkControlModeFacet.AudioSafety:
                if (_audioBaseline is not AudioSafetyMode audio)
                    return true;
                AudioSafetyTransitionResult result = await _app.ApplyAudioSafetyModeFromModeAsync(audio);
                return result.Success;

            case ThinkControlModeFacet.TouchpadGestures:
                _app.ClearTouchpadGestureModeOverride();
                return true;

            case ThinkControlModeFacet.KeyboardLight:
                return _keyboardBaseline is null ||
                       await _app.RestoreKeyboardModeSnapshotAsync(_keyboardBaseline);

            default:
                return true;
        }
    }

    private string? _lastFacetException;

    private async Task<bool> TryApplyFacetAsync(ThinkControlModeDefinition mode, ThinkControlModeFacet facet)
    {
        _lastFacetException = null;
        try { return await ApplyFacetAsync(mode, facet); }
        catch (Exception ex)
        {
            _lastFacetException = ex.Message;
            return false;
        }
    }

    private async Task<bool> TryRestoreBaselineAsync(ThinkControlModeFacet facet)
    {
        _lastFacetException = null;
        try { return await RestoreBaselineAsync(facet); }
        catch (Exception ex)
        {
            _lastFacetException = ex.Message;
            return false;
        }
    }

    private string DescribeFacetFailure(ThinkControlModeFacet facet)
    {
        string? detail = _lastFacetException ??
            (facet == ThinkControlModeFacet.CoolingProfile ? _app.LastCoolingError :
             facet == ThinkControlModeFacet.PerformanceMode ? _app.LastPowerModeError : null);
        string label = facet switch
        {
            ThinkControlModeFacet.CoolingProfile => "Cooling",
            ThinkControlModeFacet.PerformanceMode => "Performance",
            ThinkControlModeFacet.RefreshRate => "Refresh rate",
            ThinkControlModeFacet.AudioSafety => "Audio safety",
            ThinkControlModeFacet.TouchpadGestures => "Touchpad gestures",
            ThinkControlModeFacet.KeyboardLight => "Keyboard light",
            _ => facet.ToString()
        };
        return string.IsNullOrWhiteSpace(detail)
            ? $"{label} could not be applied or restored."
            : $"{label}: {detail}";
    }

    private async Task<bool> RollBackAsync(
        ThinkControlModeDefinition? previousDefinition,
        IReadOnlySet<ThinkControlModeFacet> previousOwned,
        IReadOnlySet<ThinkControlModeFacet> attemptedFacets)
    {
        bool recovered = true;
        foreach (ThinkControlModeFacet facet in OrderedFacets(attemptedFacets.Where(facet => !previousOwned.Contains(facet))))
            recovered = await TryRestoreBaselineAsync(facet) && recovered;

        if (previousDefinition is not null)
        {
            foreach (ThinkControlModeFacet facet in OrderedFacets(previousOwned.Intersect(attemptedFacets)))
                recovered = await TryApplyFacetAsync(previousDefinition, facet) && recovered;
        }
        return recovered;
    }

    private void RestoreCoordinatorState(
        string id,
        string name,
        bool automatic,
        bool modified,
        IReadOnlySet<ThinkControlModeFacet> owned,
        string? coolingBaseline,
        RefreshModeSnapshot? refreshBaseline,
        AudioSafetyMode? audioBaseline,
        bool? touchpadBaseline,
        KeyboardModeSnapshot? keyboardBaseline)
    {
        _owned.Clear();
        foreach (ThinkControlModeFacet facet in owned)
            _owned.Add(facet);

        _coolingBaseline = coolingBaseline;
        _refreshBaseline = refreshBaseline;
        _audioBaseline = audioBaseline;
        _touchpadBaseline = touchpadBaseline;
        _keyboardBaseline = keyboardBaseline;
        ActiveModeId = id;
        ActiveModeName = name;
        ActiveModeAutomatic = automatic;
        IsModified = modified;
        TransitionModeId = null;
        TransitionModeName = null;
        Publish();
    }

    private static IEnumerable<ThinkControlModeFacet> OrderedFacets(IEnumerable<ThinkControlModeFacet> facets)
    {
        HashSet<ThinkControlModeFacet> set = [.. facets];
        if (set.Contains(ThinkControlModeFacet.PerformanceMode))
            yield return ThinkControlModeFacet.PerformanceMode;
        if (set.Contains(ThinkControlModeFacet.CoolingProfile))
            yield return ThinkControlModeFacet.CoolingProfile;
        if (set.Contains(ThinkControlModeFacet.RefreshRate))
            yield return ThinkControlModeFacet.RefreshRate;
        if (set.Contains(ThinkControlModeFacet.AudioSafety))
            yield return ThinkControlModeFacet.AudioSafety;
        if (set.Contains(ThinkControlModeFacet.TouchpadGestures))
            yield return ThinkControlModeFacet.TouchpadGestures;
        if (set.Contains(ThinkControlModeFacet.KeyboardLight))
            yield return ThinkControlModeFacet.KeyboardLight;
    }

    private void ClearBaseline(ThinkControlModeFacet facet)
    {
        switch (facet)
        {
            case ThinkControlModeFacet.PerformanceMode:
                _app.PowerModeService.ReleaseModePlan();
                break;
            case ThinkControlModeFacet.CoolingProfile:
                _coolingBaseline = null;
                break;
            case ThinkControlModeFacet.RefreshRate:
                _refreshBaseline = null;
                break;
            case ThinkControlModeFacet.AudioSafety:
                _audioBaseline = null;
                break;
            case ThinkControlModeFacet.TouchpadGestures:
                _touchpadBaseline = null;
                break;
            case ThinkControlModeFacet.KeyboardLight:
                _keyboardBaseline = null;
                break;
        }
    }

    private void Publish()
    {
        _app.State.ActiveModeId = ActiveModeId;
        _app.State.ActiveModeName = ActiveModeName;
        _app.State.ActiveModeModified = IsModified;
        _app.State.ActiveModeAutomatic = ActiveModeAutomatic;
        Changed?.Invoke();
    }
}
