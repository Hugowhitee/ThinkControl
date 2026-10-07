namespace ThinkControl.UI.Services;

// Presentation only: the coordinator and automation service remain the state owners.
internal sealed record ModeStatusPresentation(string Title, string Detail, bool Failed)
{
    internal static ModeStatusPresentation From(App app)
    {
        var modes = app.Modes;
        string title = modes.ActiveModeId == ThinkControlModeCatalog.NormalId ? "Regular settings" : $"Active mode: {modes.ActiveModeName}";
        if (modes.IsTransitioning)
            return new(title, $"Applying {modes.VisibleModeName}…", false);
        if (!string.IsNullOrWhiteSpace(modes.LastTransitionError))
            return new(title, $"The last change failed. {modes.LastTransitionError}", true);
        var rule = app.UserSettings.Current.AutomationRules?.FirstOrDefault(rule => rule.Id == app.ModeAutomation.ActiveRuleId);
        string source = app.ModeAutomation.Paused
            ? "Selected manually. Automation is paused until you resume it or a different rule wins."
            : modes.ActiveModeAutomatic
                ? rule is null ? "Activated by automation." : $"Activated by {rule.Name}. {ThinkControlAutomationRules.ConditionsSummary(rule)}"
                : "Selected manually. Automation can change it when a rule matches.";
        if (modes.ActiveModeId == ThinkControlModeCatalog.NormalId && !app.ModeAutomation.Paused)
            source = app.UserSettings.Current.AutomationRules?.Any(rule => rule.Enabled) == true
                ? "No mode selected. " + app.ModeAutomation.Status
                : "No mode selected. Automation has no enabled rules.";
        if (modes.IsModified)
            source = "Settings changed since this mode was applied. " + source;
        return new(title, source, false);
    }
}
