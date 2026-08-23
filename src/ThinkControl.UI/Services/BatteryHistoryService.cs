using System.Globalization;
using System.Text.Json;

namespace ThinkControl.UI.Services;

public sealed record BatteryHistoryView(
    IReadOnlyList<double> CurrentChargePowerWatts,
    IReadOnlyList<string> RecentSessions,
    string CurrentSessionText);

/// <summary>
/// Keeps a small local-only history of charging sessions. Samples are intentionally
/// sparse so ThinkControl can show useful AccuBattery-like context without adding a
/// database dependency or writing to disk every two-second UI refresh.
/// </summary>
public sealed class BatteryHistoryService
{
    private const int MaximumSessions = 20;
    private const int MaximumPointsPerSession = 720;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ResumeGap = TimeSpan.FromMinutes(20);

    private readonly string _path;
    private HistoryDocument _document;

    public BatteryHistoryService()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThinkControl");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "battery-history.json");
        _document = Load();
    }

    public BatteryHistoryView Record(bool charging, int percent, double? watts)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool changed = false;
        ChargeSession? active = _document.ActiveSession;

        if (charging)
        {
            if (active is not null && active.Points.Count > 0 &&
                now - active.Points[^1].At > ResumeGap)
            {
                FinalizeActive(active.Points[^1].At, active.EndPercent);
                active = null;
                changed = true;
            }

            if (active is null)
            {
                active = new ChargeSession
                {
                    StartedAt = now,
                    StartPercent = percent,
                    EndPercent = percent
                };
                _document.ActiveSession = active;
                changed = true;
            }

            active.EndPercent = percent;
            ChargePoint? last = active.Points.Count > 0 ? active.Points[^1] : null;
            if (last is null || now - last.At >= SampleInterval)
            {
                active.Points.Add(new ChargePoint
                {
                    At = now,
                    Percent = percent,
                    Watts = watts is > 0 and < 500 ? watts.Value : null
                });
                if (active.Points.Count > MaximumPointsPerSession)
                    active.Points.RemoveRange(0, active.Points.Count - MaximumPointsPerSession);
                changed = true;
            }
        }
        else if (active is not null)
        {
            FinalizeActive(now, percent);
            changed = true;
        }

        if (changed)
            Save();

        return BuildView();
    }

    public BatteryHistoryView GetView() => BuildView();

    private void FinalizeActive(DateTimeOffset endedAt, int endPercent)
    {
        ChargeSession? active = _document.ActiveSession;
        if (active is null)
            return;

        active.EndedAt = endedAt;
        active.EndPercent = endPercent;
        _document.ActiveSession = null;

        TimeSpan duration = endedAt - active.StartedAt;
        if (duration >= TimeSpan.FromMinutes(2) || active.EndPercent > active.StartPercent)
        {
            _document.Sessions.Insert(0, active);
            if (_document.Sessions.Count > MaximumSessions)
                _document.Sessions.RemoveRange(MaximumSessions, _document.Sessions.Count - MaximumSessions);
        }
    }

    private BatteryHistoryView BuildView()
    {
        ChargeSession? active = _document.ActiveSession;
        IReadOnlyList<double> currentPower = active?.Points
            .Where(point => point.Watts is > 0)
            .Select(point => point.Watts!.Value)
            .ToArray() ?? [];

        string currentText = active is null
            ? "No active charge session"
            : FormatCurrentSession(active);

        IReadOnlyList<string> sessions = _document.Sessions
            .Take(6)
            .Select(FormatSession)
            .ToArray();

        return new BatteryHistoryView(currentPower, sessions, currentText);
    }

    private static string FormatCurrentSession(ChargeSession session)
    {
        TimeSpan duration = DateTimeOffset.UtcNow - session.StartedAt;
        string average = AveragePower(session) is double watts ? $" · {watts:0.#} W avg" : string.Empty;
        return $"{session.StartPercent}% → {session.EndPercent}% · {FormatDuration(duration)}{average}";
    }

    private static string FormatSession(ChargeSession session)
    {
        DateTimeOffset ended = session.EndedAt ?? session.Points.LastOrDefault()?.At ?? session.StartedAt;
        TimeSpan duration = ended - session.StartedAt;
        DateTimeOffset local = session.StartedAt.ToLocalTime();
        string date = local.Date == DateTimeOffset.Now.Date
            ? "Today"
            : local.ToString("d MMM", CultureInfo.CurrentCulture);
        string average = AveragePower(session) is double watts ? $" · {watts:0.#} W avg" : string.Empty;
        return $"{date} · {session.StartPercent}% → {session.EndPercent}% · {FormatDuration(duration)}{average}";
    }

    private static double? AveragePower(ChargeSession session)
    {
        double[] values = session.Points
            .Where(point => point.Watts is > 0)
            .Select(point => point.Watts!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Average();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
        return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} min";
    }

    private HistoryDocument Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new HistoryDocument();
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<HistoryDocument>(json) ?? new HistoryDocument();
        }
        catch
        {
            return new HistoryDocument();
        }
    }

    private void Save()
    {
        try
        {
            string temp = _path + ".tmp";
            string json = JsonSerializer.Serialize(_document, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(temp, json);
            File.Move(temp, _path, true);
        }
        catch
        {
            // History is optional UI context; never let persistence affect charging telemetry.
        }
    }

    private sealed class HistoryDocument
    {
        public ChargeSession? ActiveSession { get; set; }
        public List<ChargeSession> Sessions { get; set; } = [];
    }

    private sealed class ChargeSession
    {
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public int StartPercent { get; set; }
        public int EndPercent { get; set; }
        public List<ChargePoint> Points { get; set; } = [];
    }

    private sealed class ChargePoint
    {
        public DateTimeOffset At { get; set; }
        public int Percent { get; set; }
        public double? Watts { get; set; }
    }
}
