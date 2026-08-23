using System.Globalization;
using System.Text;
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
    private const long MaximumHistoryBytes = 512 * 1024;
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(90);
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
        TrimDocument(DateTimeOffset.UtcNow);
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
        {
            TrimDocument(now);
            Save();
        }

        return BuildView();
    }

    public BatteryHistoryView GetView() => BuildView();

    public BatteryHistoryView Clear()
    {
        _document = new HistoryDocument();
        try
        {
            if (File.Exists(_path))
                File.Delete(_path);
            string temp = _path + ".tmp";
            if (File.Exists(temp))
                File.Delete(temp);
        }
        catch
        {
            // The in-memory history is already cleared. A locked file can be
            // overwritten on the next successful save rather than blocking the UI.
        }

        return BuildView();
    }

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
            _document.Sessions.Insert(0, active);
    }

    private void TrimDocument(DateTimeOffset now)
    {
        DateTimeOffset oldestAllowed = now - HistoryRetention;
        _document.Sessions.RemoveAll(session =>
            (session.EndedAt ?? session.StartedAt) < oldestAllowed);

        foreach (ChargeSession session in _document.Sessions)
            TrimPoints(session);
        if (_document.ActiveSession is not null)
            TrimPoints(_document.ActiveSession);

        _document.Sessions = _document.Sessions
            .OrderByDescending(session => session.EndedAt ?? session.StartedAt)
            .Take(MaximumSessions)
            .ToList();
    }

    private static void TrimPoints(ChargeSession session)
    {
        if (session.Points.Count > MaximumPointsPerSession)
            session.Points.RemoveRange(0, session.Points.Count - MaximumPointsPerSession);
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

            var info = new FileInfo(_path);
            if (info.Length > MaximumHistoryBytes)
            {
                File.Delete(_path);
                return new HistoryDocument();
            }

            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<HistoryDocument>(json) ?? new HistoryDocument();
        }
        catch
        {
            TryQuarantineCorruptFile();
            return new HistoryDocument();
        }
    }

    private void Save()
    {
        try
        {
            TrimDocument(DateTimeOffset.UtcNow);
            string json = JsonSerializer.Serialize(_document, new JsonSerializerOptions { WriteIndented = false });
            if (Encoding.UTF8.GetByteCount(json) > MaximumHistoryBytes)
            {
                // This should be unreachable with the normal caps, but keep a final
                // disk-growth guard in case the schema expands in a future release.
                _document.Sessions = _document.Sessions.Take(5).ToList();
                foreach (ChargeSession session in _document.Sessions)
                {
                    if (session.Points.Count > 180)
                        session.Points.RemoveRange(0, session.Points.Count - 180);
                }
                json = JsonSerializer.Serialize(_document, new JsonSerializerOptions { WriteIndented = false });
            }

            string temp = _path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _path, true);
        }
        catch
        {
            // History is optional UI context; never let persistence affect charging telemetry.
        }
    }

    private void TryQuarantineCorruptFile()
    {
        try
        {
            if (!File.Exists(_path))
                return;
            string quarantine = _path + ".corrupt";
            if (File.Exists(quarantine))
                File.Delete(quarantine);
            File.Move(_path, quarantine);
        }
        catch
        {
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
