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
        if (modes.SettingsNeedChecking)
            return new("Settings need checking", "Recovery was incomplete. Check affected settings before retrying.", true);
        var rule = app.UserSettings.Current.AutomationRules?.FirstOrDefault(rule => rule.Id == app.ModeAutomation.ActiveRuleId);
        string source = app.ModeAutomation.Paused
            ? "Triggers paused. Turn on to resume enabled rules."
            : modes.ActiveModeAutomatic
                ? rule is null ? "Activated by automation." : $"By {rule.Name}. {ThinkControlAutomationRules.ConditionsSummary(rule)}"
                : "Selected manually. Triggers are ready.";
        if (modes.ActiveModeId == ThinkControlModeCatalog.NormalId && !app.ModeAutomation.Paused)
            source = app.UserSettings.Current.AutomationRules?.Any(rule => rule.Enabled) == true
                ? "Triggers are ready."
                : "No enabled rules.";
        if (modes.IsModified)
            source = "Settings changed since this mode was applied. " + source;
        return new(title, source, false);
    }
}
