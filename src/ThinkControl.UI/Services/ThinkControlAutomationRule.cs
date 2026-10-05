namespace ThinkControl.UI.Services;

public sealed record ThinkControlAutomationRule(
    string Id,
    string Name,
    string ModeId,
    ThinkControlModeTrigger[] Conditions,
    bool Enabled = true,
    bool MatchAll = false,
    int Priority = 0);

internal static class ThinkControlAutomationRules
{
    internal const int Maximum = 24;

    internal static ThinkControlAutomationRule[] Migrate(
        IEnumerable<ThinkControlModeDefinition> modes) => modes
        .Where(mode => mode.Triggers?.Length > 0)
        .Select(mode => new ThinkControlAutomationRule("rule:" + mode.Id, mode.Name,
            mode.Id, ThinkControlModeCatalog.SanitizeTriggers(mode.Triggers),
            mode.AutomationEnabled, mode.MatchAllTriggers, mode.AutomationPriority))
        .ToArray();

    internal static ThinkControlAutomationRule[] Sanitize(
        IEnumerable<ThinkControlAutomationRule>? rules)
    {
        var result = new List<ThinkControlAutomationRule>();
        foreach (var rule in rules ?? [])
        {
            string id = rule.Id?.Trim() ?? "";
            string name = rule.Name?.Trim() ?? "";
            string modeId = rule.ModeId?.Trim() ?? "";
            var conditions = ThinkControlModeCatalog.SanitizeTriggers(rule.Conditions);
            if (!id.StartsWith("rule:", StringComparison.Ordinal) || id.Length > 100 ||
                name.Length is 0 or > 48 || modeId.Length is 0 or > 80 ||
                conditions.Length == 0 || result.Any(item => item.Id == id))
                continue;
            result.Add(rule with { Id = id, Name = name, ModeId = modeId,
                Conditions = conditions, Priority = Math.Clamp(rule.Priority, -1, 1) });
            if (result.Count == Maximum) break;
        }
        return result.ToArray();
    }

    internal static ThinkControlModeDefinition AsPolicyMode(ThinkControlAutomationRule rule) =>
        new(rule.Id, rule.Name, Triggers: rule.Conditions, AutomationEnabled: rule.Enabled,
            MatchAllTriggers: rule.MatchAll, AutomationPriority: rule.Priority);

    internal static ThinkControlAutomationRule[] MoveWithinPriority(
        IReadOnlyList<ThinkControlAutomationRule> rules, string id, int direction)
    {
        var result = rules.ToArray();
        int index = Array.FindIndex(result, rule => rule.Id == id);
        if (index < 0 || direction is not (-1 or 1)) return result;
        int adjacent = index + direction;
        while (adjacent >= 0 && adjacent < result.Length && result[adjacent].Priority != result[index].Priority)
            adjacent += direction;
        if (adjacent >= 0 && adjacent < result.Length)
            (result[index], result[adjacent]) = (result[adjacent], result[index]);
        return result;
    }

    internal static string ConditionsSummary(ThinkControlAutomationRule rule) =>
        (rule.MatchAll ? "All: " : "Any: ") +
        string.Join(", ", rule.Conditions.Select(ThinkControlModeCatalog.TriggerSummary));
}
