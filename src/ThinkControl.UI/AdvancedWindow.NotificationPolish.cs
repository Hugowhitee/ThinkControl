using System.Windows;
using System.Windows.Controls;

namespace ThinkControl.UI;

public partial class AdvancedWindow
{
    private bool _notificationPolishConfigured;
    private bool _notificationPolishBusy;

    /// <summary>
    /// Keeps the notification sheet focused on root causes instead of showing several
    /// cards that all lead to the same repair. The runtime sheet is built asynchronously,
    /// so polish the final visual list whenever its children change.
    /// </summary>
    private void ConfigureNotificationMessagePolish()
    {
        if (_notificationPolishConfigured)
            return;

        EnsureNotificationSheet();
        if (_notificationMessages is null)
            return;

        _notificationMessages.LayoutUpdated += NotificationMessages_LayoutUpdated;
        _notificationPolishConfigured = true;
    }

    private void NotificationMessages_LayoutUpdated(object? sender, EventArgs e)
    {
        if (_notificationPolishBusy || _notificationMessages is null)
            return;

        _notificationPolishBusy = true;
        try
        {
            var cards = _notificationMessages.Children
                .OfType<FrameworkElement>()
                .Select(card => (Card: card, Title: GetNotificationCardTitle(card)))
                .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                .ToList();

            if (cards.Count > 1)
            {
                // A previous repair result is useful as transient feedback, but once a
                // fresh status card exists it duplicates the current state and makes a
                // single fault look like two separate notifications.
                RemoveNotificationCards(cards, title => title == "Last hardware action");
                cards = SnapshotNotificationCards();
            }

            bool serviceIssue = cards.Any(item => item.Title == "Hardware service");
            bool lowLevelIssue = cards.Any(item => item.Title == "Low-level hardware access");

            if (serviceIssue)
            {
                // Service reachability is the prerequisite for every provider check.
                // Repair that single root cause first instead of showing multiple
                // identical "Fix required components" actions at once.
                RemoveNotificationCards(cards, title =>
                    title is "Low-level hardware access" or "Sensors" or "Fans" or "Keyboard");
            }
            else if (lowLevelIssue)
            {
                // Sensors and X9 fan control depend on low-level access. Keyboard is a
                // separate Lenovo provider and remains visible when it independently fails.
                RemoveNotificationCards(cards, title => title is "Sensors" or "Fans");
            }

            RefreshNotificationSummaryFromVisibleCards();
        }
        finally
        {
            _notificationPolishBusy = false;
        }
    }

    private List<(FrameworkElement Card, string Title)> SnapshotNotificationCards()
    {
        if (_notificationMessages is null)
            return [];

        return _notificationMessages.Children
            .OfType<FrameworkElement>()
            .Select(card => (Card: card, Title: GetNotificationCardTitle(card)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .ToList();
    }

    private static void RemoveNotificationCards(
        IEnumerable<(FrameworkElement Card, string Title)> cards,
        Func<string, bool> remove)
    {
        foreach ((FrameworkElement card, string title) in cards.Where(item => remove(item.Title)).ToArray())
        {
            if (card.Parent is Panel panel)
                panel.Children.Remove(card);
        }
    }

    private static string GetNotificationCardTitle(FrameworkElement card) =>
        FindVisualChildren<TextBlock>(card)
            .Select(text => text.Text?.Trim() ?? string.Empty)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? string.Empty;

    private void RefreshNotificationSummaryFromVisibleCards()
    {
        if (_notificationSummary is null || _notificationMessages is null)
            return;

        string[] attentionTitles =
        [
            "ThinkControl update available",
            "Hardware service",
            "Low-level hardware access",
            "Sensors",
            "Fans",
            "Keyboard"
        ];

        int attention = _notificationMessages.Children
            .OfType<FrameworkElement>()
            .Select(GetNotificationCardTitle)
            .Count(title => attentionTitles.Contains(title, StringComparer.Ordinal));

        _notificationSummary.Text = attention > 0
            ? $"{attention} item{(attention == 1 ? string.Empty : "s")} need attention"
            : "You're all caught up";
    }
}
