using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ThinkControl.UI.Services;

public sealed record BatteryHistoryView(
    IReadOnlyList<double> ChargePowerWatts,
    IReadOnlyList<double> HealthTrendPercent,
    IReadOnlyList<string> RecentSessions,
    string ChargeCurveLabel,
    string CurrentSessionText,
    string TypicalChargeText,
    string HealthTrendText,
    double? TypicalChargePowerWatts);

/// <summary>
/// Keeps a local-only charging history. Recent sessions retain sparse curve points;
/// older sessions retain only compact summaries so long-term averages and health
/// trends remain useful without allowing the history file to grow indefinitely.
/// </summary>
public sealed class BatteryHistoryService
{
    private const int MaximumSessionSummaries = 240;
    private const int MaximumDetailedSessions = 20;
    private const int MaximumPointsPerSession = 720;
    private const long MaximumHistoryBytes = 1024 * 1024;
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(365);
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

    public BatteryHistoryView Record(
        bool charging,
        int percent,
        double? watts,
        double? remainingWh,
        double? fullChargeWh,
        double? designWh)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool changed = false;
        ChargeSession? active = _document.ActiveSession;

        if (charging)
        {
            if (active is not null && active.Points.Count > 0 &&
                now - active.Points[^1].At > ResumeGap)
            {
                FinalizeActive(active.Points[^1].At, active.EndPercent, active.EndRemainingWh);
                active = null;
                changed = true;
            }

            if (active is null)
            {
                active = new ChargeSession
                {
                    StartedAt = now,
                    StartPercent = percent,
                    EndPercent = percent,
                    StartRemainingWh = remainingWh,
                    EndRemainingWh = remainingWh,
                    FullChargeCapacityWh = fullChargeWh,
                    DesignCapacityWh = designWh
                };
                _document.ActiveSession = active;
                changed = true;
            }

            active.EndPercent = percent;
            active.EndRemainingWh = remainingWh ?? active.EndRemainingWh;
            active.FullChargeCapacityWh = fullChargeWh ?? active.FullChargeCapacityWh;
            active.DesignCapacityWh = designWh ?? active.DesignCapacityWh;

            ChargePoint? last = active.Points.Count > 0 ? active.Points[^1] : null;
            if (last is null || now - last.At >= SampleInterval)
            {
                active.Points.Add(new ChargePoint
                {
                    At = now,
                    Percent = percent,
                    Watts = watts is > 0 and < 500 ? watts.Value : null,
                    RemainingWh = remainingWh
                });
                if (active.Points.Count > MaximumPointsPerSession)
                    active.Points.RemoveRange(0, active.Points.Count - MaximumPointsPerSession);
                changed = true;
            }
        }
        else if (active is not null)
        {
            FinalizeActive(now, percent, remainingWh);
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
            string corrupt = _path + ".corrupt";
            if (File.Exists(corrupt))
                File.Delete(corrupt);
        }
        catch
        {
            // In-memory state is already empty; a locked file can be replaced later.
        }

        return BuildView();
    }

    private void FinalizeActive(DateTimeOffset endedAt, int endPercent, double? endRemainingWh)
    {
        ChargeSession? active = _document.ActiveSession;
        if (active is null)
            return;

        active.EndedAt = endedAt;
        active.EndPercent = endPercent;
        active.EndRemainingWh = endRemainingWh ?? active.EndRemainingWh;
        PopulateSummary(active);
        _document.ActiveSession = null;

        TimeSpan duration = endedAt - active.StartedAt;
        if (duration >= TimeSpan.FromMinutes(2) || active.EndPercent > active.StartPercent)
            _document.Sessions.Insert(0, active);
    }

    private static void PopulateSummary(ChargeSession session)
    {
        session.AveragePowerWatts = AveragePointPower(session);
        if (session.StartRemainingWh is double start && session.EndRemainingWh is double end && end > start)
            session.EnergyAddedWh = end - start;

        if (session.FullChargeCapacityWh is > 0 && session.DesignCapacityWh is > 0)
            session.HealthPercent = session.FullChargeCapacityWh.Value / session.DesignCapacityWh.Value * 100d;
    }

    private void TrimDocument(DateTimeOffset now)
    {
        DateTimeOffset oldestAllowed = now - HistoryRetention;
        _document.Sessions.RemoveAll(session =>
            (session.EndedAt ?? session.StartedAt) < oldestAllowed);

        _document.Sessions = _document.Sessions
            .OrderByDescending(session => session.EndedAt ?? session.StartedAt)
            .Take(MaximumSessionSummaries)
            .ToList();

        for (int i = 0; i < _document.Sessions.Count; i++)
        {
            ChargeSession session = _document.Sessions[i];
            PopulateSummary(session);
            if (i < MaximumDetailedSessions)
                TrimPoints(session);
            else
                session.Points.Clear();
        }

        if (_document.ActiveSession is not null)
            TrimPoints(_document.ActiveSession);
    }

    private static void TrimPoints(ChargeSession session)
    {
        if (session.Points.Count > MaximumPointsPerSession)
            session.Points.RemoveRange(0, session.Points.Count - MaximumPointsPerSession);
    }

    private BatteryHistoryView BuildView()
    {
        ChargeSession? active = _document.ActiveSession;
        ChargeSession? curveSession = active ?? _document.Sessions.FirstOrDefault(session => session.Points.Count >= 2);
        IReadOnlyList<double> chargePower = curveSession?.Points
            .Where(point => point.Watts is > 0)
            .Select(point => point.Watts!.Value)
            .ToArray() ?? [];

        string curveLabel = active is not null
            ? "Current charge · full session curve"
            : curveSession is not null
                ? "Last charge · full retained session curve"
                : "Charge curve · learning";

        string currentText = active is null
            ? curveSession is null ? "No charge sessions recorded yet" : FormatSession(curveSession)
            : FormatCurrentSession(active);

        IReadOnlyList<string> sessions = _document.Sessions
            .Take(6)
            .Select(FormatSession)
            .ToArray();

        double[] usefulChargePowers = _document.Sessions
            .Where(IsUsefulChargeSession)
            .Select(session => session.AveragePowerWatts!.Value)
            .Take(40)
            .ToArray();
        double? typicalPower = usefulChargePowers.Length == 0 ? null : Median(usefulChargePowers);
        string typicalText = typicalPower is double typical
            ? $"Typical {typical:0.#} W · {usefulChargePowers.Length} sessions"
            : "Typical charge · learning";

        double[] health = _document.Sessions
            .Where(session => session.HealthPercent is > 0 and <= 130)
            .OrderBy(session => session.EndedAt ?? session.StartedAt)
            .Select(session => session.HealthPercent!.Value)
            .ToArray();
        string healthTrendText = FormatHealthTrend(health);

        return new BatteryHistoryView(
            chargePower,
            health,
            sessions,
            curveLabel,
            currentText,
            typicalText,
            healthTrendText,
            typicalPower);
    }

    private static bool IsUsefulChargeSession(ChargeSession session)
    {
        TimeSpan duration = (session.EndedAt ?? session.StartedAt) - session.StartedAt;
        return duration >= TimeSpan.FromMinutes(5) &&
               session.AveragePowerWatts is > 0.4 and < 200 &&
               session.EndPercent - session.StartPercent >= 3;
    }

    private static string FormatCurrentSession(ChargeSession session)
    {
        TimeSpan duration = DateTimeOffset.UtcNow - session.StartedAt;
        double? averagePower = AveragePointPower(session);
        string average = averagePower is double watts ? $" · {watts:0.#} W avg" : string.Empty;
        string energy = session.StartRemainingWh is double start && session.EndRemainingWh is double end && end > start
            ? $" · +{end - start:0.#} Wh"
            : string.Empty;
        return $"{session.StartPercent}% → {session.EndPercent}% · {FormatDuration(duration)}{average}{energy}";
    }

    private static string FormatSession(ChargeSession session)
    {
        DateTimeOffset ended = session.EndedAt ?? session.StartedAt;
        TimeSpan duration = ended - session.StartedAt;
        DateTimeOffset local = session.StartedAt.ToLocalTime();
        string date = local.Date == DateTimeOffset.Now.Date
            ? "Today"
            : local.ToString("d MMM", CultureInfo.CurrentCulture);
        string average = session.AveragePowerWatts is double watts ? $" · {watts:0.#} W avg" : string.Empty;
        string energy = session.EnergyAddedWh is double wh ? $" · +{wh:0.#} Wh" : string.Empty;
        return $"{date} · {session.StartPercent}% → {session.EndPercent}% · {FormatDuration(duration)}{average}{energy}";
    }

    private static string FormatHealthTrend(IReadOnlyList<double> health)
    {
        if (health.Count == 0)
            return "Health trend · learning";
        if (health.Count == 1)
            return $"Health trend · {health[0]:0.#}%";

        double recent = health[^1];
        int compareIndex = Math.Max(0, health.Count - Math.Min(10, health.Count));
        double baseline = health[compareIndex];
        double delta = recent - baseline;
        string trend = Math.Abs(delta) < 0.15 ? "stable" : delta > 0 ? $"+{delta:0.#} pp" : $"{delta:0.#} pp";
        return $"Health trend · {recent:0.#}% · {trend}";
    }

    private static double? AveragePointPower(ChargeSession session)
    {
        double[] values = session.Points
            .Where(point => point.Watts is > 0)
            .Select(point => point.Watts!.Value)
            .ToArray();
        return values.Length == 0 ? session.AveragePowerWatts : values.Average();
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
            return 0;
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2d
            : sorted[middle];
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
                _document.Sessions = _document.Sessions.Take(80).ToList();
                for (int i = 10; i < _document.Sessions.Count; i++)
                    _document.Sessions[i].Points.Clear();
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
        public int SchemaVersion { get; set; } = 2;
        public ChargeSession? ActiveSession { get; set; }
        public List<ChargeSession> Sessions { get; set; } = [];
    }

    private sealed class ChargeSession
    {
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public int StartPercent { get; set; }
        public int EndPercent { get; set; }
        public double? StartRemainingWh { get; set; }
        public double? EndRemainingWh { get; set; }
        public double? EnergyAddedWh { get; set; }
        public double? AveragePowerWatts { get; set; }
        public double? FullChargeCapacityWh { get; set; }
        public double? DesignCapacityWh { get; set; }
        public double? HealthPercent { get; set; }
        public List<ChargePoint> Points { get; set; } = [];
    }

    private sealed class ChargePoint
    {
        public DateTimeOffset At { get; set; }
        public int Percent { get; set; }
        public double? Watts { get; set; }
        public double? RemainingWh { get; set; }
    }
}
