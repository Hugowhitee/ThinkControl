using ThinkControl.Core.Cooling;

namespace ThinkControl.UI.Services;

public sealed class FanProfileCatalog
{
    public const int MaxCustomProfiles = 6;
    private readonly UserSettingsService _settings;

    public FanProfileCatalog(UserSettingsService settings) => _settings = settings;

    public IReadOnlyList<FanCurveDefinition> GetProfiles()
    {
        ThinkControlUserSettings settings = _settings.Current;
        var result = new List<FanCurveDefinition>(3 + MaxCustomProfiles);
        foreach (FanCurveDefinition factory in FanCurveDefaults.BuiltIns)
        {
            FanCurveDefinition? saved = settings.FanProfileOverrides?.FirstOrDefault(profile =>
                string.Equals(profile.Id, factory.Id, StringComparison.OrdinalIgnoreCase));
            result.Add(saved ?? factory);
        }

        if (settings.CustomFanProfiles is not null)
            result.AddRange(settings.CustomFanProfiles);
        return result;
    }

    public FanCurveDefinition? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        return GetProfiles().FirstOrDefault(profile =>
            string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsBuiltIn(string? id) => id is
        FanCurveDefaults.QuietId or FanCurveDefaults.BalancedId or FanCurveDefaults.MaxCoolingId;

    public bool SaveCurve(FanCurveDefinition definition, out string? error)
    {
        error = null;
        if (!FanCurveGraphPolicy.TryNormalize(definition.Points, out FanCurvePoint[] normalized, out error))
            return false;

        if (IsBuiltIn(definition.Id))
        {
            FanCurveDefinition factory = FanCurveDefaults.ById(definition.Id);
            var replacement = new FanCurveDefinition(factory.Id, factory.Name, normalized);
            _settings.Update(settings => settings with
            {
                FanProfileOverrides = Upsert(settings.FanProfileOverrides ?? [], replacement, 3)
            });
            return true;
        }

        string id = definition.Id?.Trim() ?? string.Empty;
        string name = NormalizeName(definition.Name);
        if (!id.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(name))
        {
            error = "Custom fan profile metadata is invalid.";
            return false;
        }

        FanCurveDefinition[] existing = _settings.Current.CustomFanProfiles ?? [];
        bool alreadyExists = existing.Any(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
        if (!alreadyExists && existing.Length >= MaxCustomProfiles)
        {
            error = $"ThinkControl supports up to {MaxCustomProfiles} custom fan profiles.";
            return false;
        }

        var saved = new FanCurveDefinition(id, name, normalized);
        _settings.Update(settings => settings with
        {
            CustomFanProfiles = Upsert(settings.CustomFanProfiles ?? [], saved, MaxCustomProfiles)
        });
        return true;
    }

    public FanCurveDefinition? CreateCustom(string? cloneId, out string? error)
    {
        error = null;
        FanCurveDefinition source = Find(cloneId) ?? FanCurveDefaults.Balanced;
        FanCurveDefinition[] existing = _settings.Current.CustomFanProfiles ?? [];
        if (existing.Length >= MaxCustomProfiles)
        {
            error = $"ThinkControl supports up to {MaxCustomProfiles} custom fan profiles.";
            return null;
        }

        int number = 1;
        string name;
        do { name = $"Custom {number++}"; }
        while (existing.Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)));

        var created = new FanCurveDefinition(
            $"custom:{Guid.NewGuid():N}",
            name,
            source.Points.Select(point => point with { }).ToArray());
        if (!SaveCurve(created, out error))
            return null;
        return created;
    }

    public bool Rename(string id, string name, out string? error)
    {
        error = null;
        if (IsBuiltIn(id))
        {
            error = "Built-in fan profiles keep their standard names.";
            return false;
        }

        string normalized = NormalizeName(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "Profile name cannot be empty.";
            return false;
        }
        FanCurveDefinition? existing = Find(id);
        if (existing is null)
        {
            error = "Fan profile no longer exists.";
            return false;
        }
        if (GetProfiles().Any(profile => !string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase) &&
                                        string.Equals(profile.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Another fan profile already uses that name.";
            return false;
        }

        return SaveCurve(existing with { Name = normalized }, out error);
    }

    public bool Delete(string id, out string? error)
    {
        error = null;
        if (IsBuiltIn(id))
        {
            error = "Built-in fan profiles can be reset, not deleted.";
            return false;
        }

        FanCurveDefinition[] existing = _settings.Current.CustomFanProfiles ?? [];
        FanCurveDefinition[] remaining = existing
            .Where(profile => !string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (remaining.Length == existing.Length)
        {
            error = "Fan profile no longer exists.";
            return false;
        }

        _settings.Update(settings => settings with
        {
            CustomFanProfiles = remaining,
            CoolingProfile = string.Equals(settings.CoolingProfile, id, StringComparison.OrdinalIgnoreCase)
                ? "Lenovo Auto"
                : settings.CoolingProfile
        });
        return true;
    }

    public FanCurveDefinition ResetBuiltIn(string id)
    {
        FanCurveDefinition factory = FanCurveDefaults.ById(id);
        _settings.Update(settings => settings with
        {
            FanProfileOverrides = (settings.FanProfileOverrides ?? [])
                .Where(profile => !string.Equals(profile.Id, factory.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray()
        });
        return factory;
    }

    public void ResetAllCurves()
    {
        _settings.Update(settings => settings with
        {
            FanProfileOverrides = [],
            CustomFanProfiles = [],
            CoolingProfile = "Lenovo Auto"
        });
    }

    private static FanCurveDefinition[] Upsert(
        IReadOnlyList<FanCurveDefinition> existing,
        FanCurveDefinition replacement,
        int max)
    {
        var result = existing
            .Where(profile => !string.Equals(profile.Id, replacement.Id, StringComparison.OrdinalIgnoreCase))
            .Take(max - 1)
            .ToList();
        result.Add(replacement);
        return result.ToArray();
    }

    private static string NormalizeName(string? value)
    {
        string name = (value ?? string.Empty).Trim();
        if (name.Length > 32)
            name = name[..32].Trim();
        return name;
    }
}
