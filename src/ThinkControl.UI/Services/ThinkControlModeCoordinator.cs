using ThinkControl.Core.Audio;

namespace ThinkControl.UI.Services;

internal sealed class ThinkControlModeCoordinator
{
    private readonly App _app;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<ThinkControlModeFacet> _owned = [];

    private AudioSafetyMode? _audioBaseline;
    private bool? _touchpadBaseline;
    private KeyboardModeSnapshot? _keyboardBaseline;
    private bool _applying;

    internal ThinkControlModeCoordinator(App app)
    {
        _app = app;
        Publish();
    }

    internal event Action? Changed;

    internal string ActiveModeId { get; private set; } = ThinkControlModeCatalog.NormalId;
    internal string ActiveModeName { get; private set; } = "Normal";
    internal bool IsModified { get; private set; }

    internal IReadOnlyList<ThinkControlModeDefinition> GetModes()
    {
        ThinkControlModeDefinition[] customs =
            _app.UserSettings.Current.CustomModes ?? [];
        return ThinkControlModeCatalog.BuiltIns.Concat(customs).ToArray();
    }

    internal async Task<bool> ActivateAsync(string id)
    {
        await _gate.WaitAsync();
        try
        {
            ThinkControlModeDefinition? target = ThinkControlModeCatalog.Find(
                id,
                _app.UserSettings.Current.CustomModes);
            if (target is null)
                return false;

            string previousId = ActiveModeId;
            string previousName = ActiveModeName;
            bool previousModified = IsModified;
            HashSet<ThinkControlModeFacet> previousOwned = [.. _owned];
            AudioSafetyMode? previousAudioBaseline = _audioBaseline;
            bool? previousTouchpadBaseline = _touchpadBaseline;
            KeyboardModeSnapshot? previousKeyboardBaseline = _keyboardBaseline;
            ThinkControlModeDefinition? previousDefinition = ThinkControlModeCatalog.Find(
                previousId,
                _app.UserSettings.Current.CustomModes);

            HashSet<ThinkControlModeFacet> targetFacets =
                [.. ThinkControlModeCatalog.Facets(target)];

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
                    if (!await ApplyFacetAsync(target, facet))
                    {
                        await RollBackAsync(previousDefinition, previousOwned, targetFacets);
                        RestoreCoordinatorState(
                            previousId,
                            previousName,
                            previousModified,
                            previousOwned,
                            previousAudioBaseline,
                            previousTouchpadBaseline,
                            previousKeyboardBaseline);
                        return false;
                    }
                }

                foreach (ThinkControlModeFacet facet in OrderedFacets(previousOwned.Except(targetFacets)))
                {
                    if (!await RestoreBaselineAsync(facet))
                    {
                        await RollBackAsync(previousDefinition, previousOwned, targetFacets);
                        RestoreCoordinatorState(
                            previousId,
                            previousName,
                            previousModified,
                            previousOwned,
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
                ActiveModeName = target.Name;
                IsModified = false;
                Publish();
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

    internal Task<bool> ReapplyAsync() => ActivateAsync(ActiveModeId);

    internal void ReleaseFacet(ThinkControlModeFacet facet)
    {
        if (_applying || ActiveModeId == ThinkControlModeCatalog.NormalId || !_owned.Remove(facet))
            return;

        ClearBaseline(facet);
        IsModified = true;
        Publish();
    }

    internal bool SaveCustomMode(ThinkControlModeDefinition mode)
    {
        ThinkControlModeDefinition? sanitized = ThinkControlModeCatalog.SanitizeCustomMode(mode);
        if (sanitized is null)
            return false;

        var modes = (_app.UserSettings.Current.CustomModes ?? []).ToList();
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
        return true;
    }

    private void CaptureBaseline(ThinkControlModeFacet facet)
    {
        switch (facet)
        {
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

    private async Task RollBackAsync(
        ThinkControlModeDefinition? previousDefinition,
        IReadOnlySet<ThinkControlModeFacet> previousOwned,
        IReadOnlySet<ThinkControlModeFacet> attemptedFacets)
    {
        foreach (ThinkControlModeFacet facet in OrderedFacets(attemptedFacets.Where(facet => !previousOwned.Contains(facet))))
        {
            try { await RestoreBaselineAsync(facet); }
            catch { }
        }

        if (previousDefinition is null)
            return;

        foreach (ThinkControlModeFacet facet in OrderedFacets(previousOwned))
        {
            try { await ApplyFacetAsync(previousDefinition, facet); }
            catch { }
        }
    }

    private void RestoreCoordinatorState(
        string id,
        string name,
        bool modified,
        IReadOnlySet<ThinkControlModeFacet> owned,
        AudioSafetyMode? audioBaseline,
        bool? touchpadBaseline,
        KeyboardModeSnapshot? keyboardBaseline)
    {
        _owned.Clear();
        foreach (ThinkControlModeFacet facet in owned)
            _owned.Add(facet);

        _audioBaseline = audioBaseline;
        _touchpadBaseline = touchpadBaseline;
        _keyboardBaseline = keyboardBaseline;
        ActiveModeId = id;
        ActiveModeName = name;
        IsModified = modified;
        Publish();
    }

    private static IEnumerable<ThinkControlModeFacet> OrderedFacets(IEnumerable<ThinkControlModeFacet> facets)
    {
        HashSet<ThinkControlModeFacet> set = [.. facets];
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
        Changed?.Invoke();
    }
}
