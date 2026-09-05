using System.Globalization;
using System.IO;

namespace ThinkControl.UI.Services;

internal static class UpdateCheckHistoryService
{
    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ThinkControl",
        "last-update-check.txt");

    internal static DateTimeOffset? Read()
    {
        try
        {
            if (!File.Exists(HistoryPath))
                return null;

            string raw = File.ReadAllText(HistoryPath).Trim();
            if (!DateTimeOffset.TryParseExact(
                    raw,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset value))
            {
                return null;
            }

            return value <= DateTimeOffset.UtcNow.AddMinutes(5) ? value : null;
        }
        catch
        {
            return null;
        }
    }

    internal static void Record(DateTimeOffset timestamp)
    {
        try
        {
            string? folder = Path.GetDirectoryName(HistoryPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            string temporary = HistoryPath + ".tmp";
            File.WriteAllText(temporary, timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            File.Move(temporary, HistoryPath, overwrite: true);
        }
        catch
        {
            // Update history is convenience state. A failed write must never make
            // the updater itself fail.
        }
    }

    internal static string Format(DateTimeOffset? timestamp) =>
        Format(timestamp, DateTimeOffset.Now);

    internal static string Format(DateTimeOffset? timestamp, DateTimeOffset now)
    {
        if (timestamp is null)
            return "Last checked · Never";

        DateTimeOffset local = timestamp.Value.ToLocalTime();
        DateTimeOffset localNow = now.ToLocalTime();
        TimeSpan age = localNow - local;
        if (age >= TimeSpan.Zero && age < TimeSpan.FromMinutes(2))
            return "Last checked · Just now";

        DateTime today = localNow.Date;
        string day = local.Date == today
            ? "Today"
            : local.Date == today.AddDays(-1)
                ? "Yesterday"
                : local.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

        return $"Last checked · {day}, {local:HH:mm}";
    }
}
