using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using ThinkControl.Core.Battery;
using ThinkControl.UI.Controls;

namespace ThinkControl.UI.Services;

public sealed record BatteryHistoryView(
    IReadOnlyList<TimeSeriesPoint> ChargePowerTimeline,
    IReadOnlyList<TimeSeriesPoint> ChargePercentTimeline,
    IReadOnlyList<TimeSeriesPoint> HealthTrendTimeline,
    IReadOnlyList<string> RecentSessions,
    string ChargeCurveLabel,
    string CurrentSessionText,
    string TypicalChargeText,
    string HealthTrendText,
    double? TypicalChargePowerWatts)
{
    public IReadOnlyList<TimeSeriesPoint> CycleCountTimeline { get; init; } = [];
    public string CycleTrendText { get; init; } = "Waiting for firmware cycle history";
}

public sealed record BatterySessionDetail(
    string Id,
    string Kind,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int StartPercent,
    int EndPercent,
    double? AveragePowerWatts,
    double? PeakPowerWatts,
    double? EnergyWh,
    double? PercentPerHour,
    IReadOnlyList<TimeSeriesPoint> PowerTimeline,
    IReadOnlyList<TimeSeriesPoint> PercentTimeline,
    string Summary,
    bool IsActive = false)
{
    public TimeSpan Duration => (EndedAt ?? DateTimeOffset.UtcNow) - StartedAt;
}

public sealed record BatteryDaySummary(
    DateOnly Day,
    string Label,
    int ChargedPercent,
    int DischargedPercent,
    TimeSpan ChargingTime,
    TimeSpan UsageTime,
    IReadOnlyList<BatterySessionDetail> Sessions,
    bool HasActiveSession);

/// <summary>
/// Keeps a tiny local-only charge/discharge history. Recent sessions retain sparse
/// curves; older sessions keep summaries only. No raw system identifiers are stored.
/// </summary>
public sealed class BatteryHistoryService
{
    private const int MaximumSessionSummaries = 240;
    private const int MaximumHealthSamples = 400;
    private const int MaximumPointsPerSession = 900;
    private const long MaximumHistoryBytes = 1024 * 1024;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MinimumSignificantSampleInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ResumeGap = TimeSpan.FromMinutes(20);

    private readonly string _path;
    private HistoryDocument _document;
    private int _detailedRetentionDays = 7;

    public BatteryHistoryService(int detailedRetentionDays = 7)
    {
        _detailedRetentionDays = BatteryHistoryRetentionPolicy.NormalizeDetailedDays(detailedRetentionDays);
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThinkControl");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "battery-history.json");
        _document = Load();
        TrimDocument(DateTimeOffset.UtcNow);
        RefreshPriors();
    }

    internal BatteryHistoryService(string path)
    {
        _path = path;
        _document = Load();
    }

    public BatteryHistoryView Record(
        bool charging,
        int percent,
        double? watts,
        double? remainingWh,
        double? fullChargeWh,
        double? designWh,
        int? cycleCount = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool onBattery = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus !=
                         System.Windows.Forms.PowerLineStatus.Online;
        return Record(charging, onBattery, now, percent, watts, remainingWh, fullChargeWh, designWh, cycleCount);
    }

    internal BatteryHistoryView Record(
        bool charging, bool onBattery, DateTimeOffset now, int percent,
        double? watts, double? remainingWh, double? fullChargeWh, double? designWh, int? cycleCount = null)
    {
        // Some laptop firmware exposes battery percentage but no ChargeRate/
        // DischargeRate. Session ownership must follow the actual AC state so
        // percentage history remains useful even when power telemetry is absent.
        bool discharging = !charging && onBattery;
        // Battery health is derived from firmware-reported full-charge capacity versus
        // design capacity. Sample that independently of charge-session completion so
        // a deliberate 80–90% charge limit does not prevent the health trend learning.
        bool changed = RecordHealthSample(now, fullChargeWh, designWh);
        changed |= RecordCycleSample(now, cycleCount);

        // A source change after sleep/restart must close the previous session at
        // its last observation, not count the unobserved gap as charging/usage.
        if (_document.ActiveSession is { Points.Count: > 0 } previousCharge &&
            now - previousCharge.Points[^1].At > ResumeGap)
        {
            FinalizeCharge(previousCharge.Points[^1].At, previousCharge.EndPercent, previousCharge.EndRemainingWh);
            changed = true;
        }
        if (_document.ActiveDischargeSession is { Points.Count: > 0 } previousDischarge &&
            now - previousDischarge.Points[^1].At > ResumeGap)
        {
            FinalizeDischarge(previousDischarge.Points[^1].At, previousDischarge.EndPercent, previousDischarge.EndRemainingWh);
            changed = true;
        }

        if (charging)
        {
            if (_document.ActiveDischargeSession is not null)
            {
                FinalizeDischarge(now, percent, remainingWh);
                changed = true;
            }
            changed |= RecordCharge(now, percent, watts, remainingWh, fullChargeWh, designWh);
        }
        else if (discharging)
        {
            if (_document.ActiveSession is not null)
            {
                FinalizeCharge(now, percent, remainingWh);
                changed = true;
            }
            changed |= RecordDischarge(now, percent, watts, remainingWh, fullChargeWh, designWh);
        }
        else
        {
            if (_document.ActiveSession is not null)
            {
                FinalizeCharge(now, percent, remainingWh);
                changed = true;
            }
            if (_document.ActiveDischargeSession is not null)
            {
                FinalizeDischarge(now, percent, remainingWh);
                changed = true;
            }
        }

        if (changed)
        {
            TrimDocument(now);
            RefreshPriors();
            Save();
        }

        return BuildView();
    }

    public BatteryHistoryView GetView() => BuildView();

    public int DetailedRetentionDays => _detailedRetentionDays;

    public void ConfigureDetailedRetentionDays(int days)
    {
        _detailedRetentionDays = BatteryHistoryRetentionPolicy.NormalizeDetailedDays(days);
        TrimDocument(DateTimeOffset.UtcNow);
        Save();
    }

    public IReadOnlyList<BatteryDaySummary> GetRecentDays(int maximum = 14)
    {
        maximum = Math.Clamp(maximum, 1, 60);
        var days = new Dictionary<DateOnly, DayTotals>();
        foreach (BatterySessionDetail session in EnumerateSessionDetails())
        {
            // Live time ends at the last observation: an open file after sleep
            // must not manufacture usage while the app was not observing it.
            DateTimeOffset end = session.EndedAt ?? session.PercentTimeline.LastOrDefault()?.At ?? session.StartedAt;
            if (end < session.StartedAt) continue;
            DateTimeOffset cursor = session.StartedAt;
            do
            {
                DateOnly day = LocalDay(cursor);
                DateTime nextDate = day.AddDays(1).ToDateTime(TimeOnly.MinValue);
                var midnight = new DateTimeOffset(nextDate, TimeZoneInfo.Local.GetUtcOffset(nextDate));
                DateTimeOffset until = end < midnight ? end : midnight;
                DayTotals totals = GetDay(day);
                totals.Sessions.Add(session);
                TimeSpan elapsed = until - cursor;
                if (session.Kind == "Charge") totals.ChargingTime += elapsed;
                else totals.UsageTime += elapsed;
                cursor = until;
            } while (cursor < end);

            // Assign measured percentage changes to the day they were observed.
            // Do not invent a midnight percentage by interpolating across samples.
            // Summary-only older sessions have no day-level samples; their net
            // change belongs to the recorded end day and retains its full detail.
            int remaining = session.Kind == "Charge"
                ? Math.Max(0, session.EndPercent - session.StartPercent)
                : Math.Max(0, session.StartPercent - session.EndPercent);
            double observed = session.StartPercent;
            foreach (TimeSeriesPoint point in session.PercentTimeline.OrderBy(point => point.At))
            {
                if (point.At < session.StartedAt || point.At > end) continue;
                double next = session.Kind == "Charge" ? Math.Max(observed, point.Value) : Math.Min(observed, point.Value);
                int delta = Math.Min(remaining, (int)Math.Abs(next - observed));
                if (delta > 0) AddPercent(GetDay(LocalDay(point.At)), session, delta);
                observed = next;
                remaining -= delta;
            }
            if (remaining > 0) AddPercent(GetDay(LocalDay(end)), session, remaining);
        }
        return days.OrderByDescending(pair => pair.Key)
            .Take(maximum)
            .Select(pair =>
            {
                BatterySessionDetail[] sessions = pair.Value.Sessions
                    .OrderByDescending(session => session.StartedAt)
                    .ToArray();
                DateOnly today = DateOnly.FromDateTime(DateTime.Now);
                string label = pair.Key == today ? "Today" : pair.Key == today.AddDays(-1)
                    ? "Yesterday"
                    : pair.Key.ToString("ddd, d MMM", CultureInfo.CurrentCulture);
                return new BatteryDaySummary(pair.Key, label, pair.Value.ChargedPercent, pair.Value.DischargedPercent,
                    pair.Value.ChargingTime, pair.Value.UsageTime, sessions,
                    sessions.Any(session => session.IsActive && LocalDay(EndOf(session)) == pair.Key));
            })
            .ToArray();

        DayTotals GetDay(DateOnly day)
        {
            if (!days.TryGetValue(day, out DayTotals? totals)) days[day] = totals = new DayTotals();
            return totals;
        }
        static DateTimeOffset EndOf(BatterySessionDetail session) =>
            session.EndedAt ?? session.PercentTimeline.LastOrDefault()?.At ?? session.StartedAt;
        static void AddPercent(DayTotals totals, BatterySessionDetail session, int delta)
        {
            if (delta == 0) return;
            totals.Sessions.Add(session);
            if (session.Kind == "Charge") totals.ChargedPercent += delta;
            else totals.DischargedPercent += delta;
        }
    }

    private static DateOnly LocalDay(DateTimeOffset at) => DateOnly.FromDateTime(at.ToLocalTime().DateTime);

    private sealed class DayTotals
    {
        public int ChargedPercent;
        public int DischargedPercent;
        public TimeSpan ChargingTime;
        public TimeSpan UsageTime;
        public HashSet<BatterySessionDetail> Sessions { get; } = [];
    }

    public IReadOnlyList<BatterySessionDetail> GetRecentSessionDetails(int maximum = 12)
    {
        maximum = Math.Clamp(maximum, 1, 40);
        return EnumerateSessionDetails()
            .OrderByDescending(session => session.EndedAt ?? session.StartedAt)
            .Take(maximum)
            .ToArray();
    }

    private IEnumerable<BatterySessionDetail> EnumerateSessionDetails()
    {
        var sessions = new List<BatterySessionDetail>();
        if (_document.ActiveSession is not null)
            sessions.Add(ToDetail(_document.ActiveSession, active: true));
        if (_document.ActiveDischargeSession is not null)
            sessions.Add(ToDetail(_document.ActiveDischargeSession, active: true));
        sessions.AddRange(_document.Sessions.Select(session => ToDetail(session, active: false)));
        sessions.AddRange(_document.DischargeSessions.Select(session => ToDetail(session, active: false)));
        return sessions;
    }

    public IReadOnlyList<TimeSeriesPoint> GetLatestDischargeTimeline()
    {
        DischargeSession? session = _document.ActiveDischargeSession ??
                                    _document.DischargeSessions.FirstOrDefault(item => item.Points.Count > 0);
        return session?.Points
            .Where(point => point.Watts is > 0)
            .Select(point => new TimeSeriesPoint(point.At, point.Watts!.Value, $"{point.Percent}%"))
            .ToArray() ?? [];
    }

    public IReadOnlyList<TimeSeriesPoint> GetLatestDischargePercentTimeline()
    {
        DischargeSession? session = _document.ActiveDischargeSession ??
                                    _document.DischargeSessions.FirstOrDefault(item => item.Points.Count > 0);
        return session?.Points
            .Select(point => new TimeSeriesPoint(point.At, point.Percent))
            .ToArray() ?? [];
    }

    public string GetLatestDischargeSummary()
    {
        DischargeSession? session = _document.ActiveDischargeSession ?? _document.DischargeSessions.FirstOrDefault();
        return session is null ? "No discharge session recorded yet" : FormatDischargeSession(session, _document.ActiveDischargeSession == session);
    }

    public BatteryHistoryView Clear()
    {
        _document = new HistoryDocument();
        BatteryPowerHistoryPriors.TypicalDischargePowerWatts = null;
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
        }

        return BuildView();
    }

    private bool RecordCycleSample(DateTimeOffset now, int? count)
    {
        if (count is not >= 0) return false;
        BatteryCycleSample? latest = _document.CycleSamples.LastOrDefault();
        if (latest is not null && (now <= latest.At ||
            latest.Count == count && latest.At.UtcDateTime.Date == now.UtcDateTime.Date)) return false;
        _document.CycleSamples.Add(new BatteryCycleSample { At = now, Count = count.Value });
        return true;
    }

    private bool RecordHealthSample(DateTimeOffset now, double? fullChargeWh, double? designWh)
    {
        if (fullChargeWh is not > 0 || designWh is not > 0)
            return false;

        double health = fullChargeWh.Value / designWh.Value * 100d;
        if (health is <= 0 or > 130)
            return false;

        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        BatteryHealthSample? latest = _document.HealthSamples
            .OrderByDescending(sample => sample.At)
            .FirstOrDefault();
        if (latest is not null && DateOnly.FromDateTime(latest.At.UtcDateTime) == today)
            return false;

        _document.HealthSamples.Add(new BatteryHealthSample
        {
            At = now,
            FullChargeCapacityWh = fullChargeWh.Value,
            DesignCapacityWh = designWh.Value,
            HealthPercent = health
        });
        return true;
    }

    private bool RecordCharge(
        DateTimeOffset now,
        int percent,
        double? watts,
        double? remainingWh,
        double? fullChargeWh,
        double? designWh)
    {
        bool changed = false;
        ChargeSession? active = _document.ActiveSession;
        if (active is not null && active.Points.Count > 0 && now - active.Points[^1].At > ResumeGap)
        {
            FinalizeCharge(active.Points[^1].At, active.EndPercent, active.EndRemainingWh);
            active = null;
            changed = true;
        }

        if (active is null)
        {
            active = new ChargeSession
            {
                Id = Guid.NewGuid().ToString("N"),
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

        int previousPercent = active.EndPercent;
        active.EndPercent = percent;
        active.EndRemainingWh = remainingWh ?? active.EndRemainingWh;
        active.FullChargeCapacityWh = fullChargeWh ?? active.FullChargeCapacityWh;
        active.DesignCapacityWh = designWh ?? active.DesignCapacityWh;

        if (ShouldSample(active.Points, now, percent, previousPercent, watts))
        {
            active.Points.Add(NewPoint(now, percent, watts, remainingWh));
            TrimPoints(active.Points);
            changed = true;
        }
        return changed;
    }

    private bool RecordDischarge(
        DateTimeOffset now,
        int percent,
        double? watts,
        double? remainingWh,
        double? fullChargeWh,
        double? designWh)
    {
        bool changed = false;
        DischargeSession? active = _document.ActiveDischargeSession;
        if (active is not null && active.Points.Count > 0 && now - active.Points[^1].At > ResumeGap)
        {
            FinalizeDischarge(active.Points[^1].At, active.EndPercent, active.EndRemainingWh);
            active = null;
            changed = true;
        }

        if (active is null)
        {
            active = new DischargeSession
            {
                Id = Guid.NewGuid().ToString("N"),
                StartedAt = now,
                StartPercent = percent,
                EndPercent = percent,
                StartRemainingWh = remainingWh,
                EndRemainingWh = remainingWh,
                FullChargeCapacityWh = fullChargeWh,
                DesignCapacityWh = designWh
            };
            _document.ActiveDischargeSession = active;
            changed = true;
        }

        int previousPercent = active.EndPercent;
        active.EndPercent = percent;
        active.EndRemainingWh = remainingWh ?? active.EndRemainingWh;
        active.FullChargeCapacityWh = fullChargeWh ?? active.FullChargeCapacityWh;
        active.DesignCapacityWh = designWh ?? active.DesignCapacityWh;

        if (ShouldSample(active.Points, now, percent, previousPercent, watts))
        {
            active.Points.Add(NewPoint(now, percent, watts, remainingWh));
            TrimPoints(active.Points);
            changed = true;
        }
        return changed;
    }

    private static bool ShouldSample(
        IReadOnlyList<ChargePoint> points,
        DateTimeOffset now,
        int percent,
        int previousPercent,
        double? watts)
    {
        ChargePoint? last = points.Count > 0 ? points[^1] : null;
        bool validWatts = watts is > 0.4 and < 500;
        bool powerMoved = validWatts && last?.Watts is double lastWatts && Math.Abs(watts!.Value - lastWatts) >= 0.75;
        bool percentMoved = percent != previousPercent;
        TimeSpan age = last is null ? TimeSpan.MaxValue : now - last.At;
        return last is null || age >= SampleInterval ||
               (age >= MinimumSignificantSampleInterval && (powerMoved || percentMoved));
    }

    private static ChargePoint NewPoint(DateTimeOffset at, int percent, double? watts, double? remainingWh) => new()
    {
        At = at,
        Percent = percent,
        Watts = watts is > 0.4 and < 500 ? watts : null,
        RemainingWh = remainingWh
    };

    private void FinalizeCharge(DateTimeOffset endedAt, int endPercent, double? endRemainingWh)
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

    private void FinalizeDischarge(DateTimeOffset endedAt, int endPercent, double? endRemainingWh)
    {
        DischargeSession? active = _document.ActiveDischargeSession;
        if (active is null)
            return;

        active.EndedAt = endedAt;
        active.EndPercent = endPercent;
        active.EndRemainingWh = endRemainingWh ?? active.EndRemainingWh;
        PopulateSummary(active);
        _document.ActiveDischargeSession = null;

        TimeSpan duration = endedAt - active.StartedAt;
        if (duration >= TimeSpan.FromMinutes(3) || active.StartPercent > active.EndPercent)
            _document.DischargeSessions.Insert(0, active);
    }

    private static void PopulateSummary(ChargeSession session)
    {
        session.AveragePowerWatts = AveragePointPower(session.Points, session.AveragePowerWatts);
        session.PeakPowerWatts = PeakPointPower(session.Points, session.PeakPowerWatts);
        if (session.StartRemainingWh is double start && session.EndRemainingWh is double end && end > start)
            session.EnergyAddedWh = end - start;
        if (session.FullChargeCapacityWh is > 0 && session.DesignCapacityWh is > 0)
            session.HealthPercent = session.FullChargeCapacityWh.Value / session.DesignCapacityWh.Value * 100d;
    }

    private static void PopulateSummary(DischargeSession session)
    {
        session.AveragePowerWatts = AveragePointPower(session.Points, session.AveragePowerWatts);
        session.PeakPowerWatts = PeakPointPower(session.Points, session.PeakPowerWatts);
        if (session.StartRemainingWh is double start && session.EndRemainingWh is double end && start > end)
            session.EnergyUsedWh = start - end;
    }

    private void TrimDocument(DateTimeOffset now)
    {
        _document.CycleSamples = _document.CycleSamples
            .Where(sample => sample.Count >= 0 && BatteryHistoryRetentionPolicy.KeepSummary(sample.At, now))
            .OrderByDescending(sample => sample.At).Take(MaximumHealthSamples)
            .OrderBy(sample => sample.At).ToList();
        _document.Sessions.RemoveAll(session =>
            !BatteryHistoryRetentionPolicy.KeepSummary(session.EndedAt ?? session.StartedAt, now));
        _document.DischargeSessions.RemoveAll(session =>
            !BatteryHistoryRetentionPolicy.KeepSummary(session.EndedAt ?? session.StartedAt, now));
        _document.HealthSamples.RemoveAll(sample =>
            !BatteryHistoryRetentionPolicy.KeepSummary(sample.At, now));

        _document.HealthSamples = _document.HealthSamples
            .OrderByDescending(sample => sample.At)
            .Take(MaximumHealthSamples)
            .OrderBy(sample => sample.At)
            .ToList();

        _document.Sessions = _document.Sessions
            .OrderByDescending(session => session.EndedAt ?? session.StartedAt)
            .Take(MaximumSessionSummaries)
            .ToList();
        _document.DischargeSessions = _document.DischargeSessions
            .OrderByDescending(session => session.EndedAt ?? session.StartedAt)
            .Take(MaximumSessionSummaries)
            .ToList();

        for (int i = 0; i < _document.Sessions.Count; i++)
        {
            ChargeSession session = _document.Sessions[i];
            PopulateSummary(session);
            if (BatteryHistoryRetentionPolicy.KeepDetailedSamples(
                    session.EndedAt ?? session.StartedAt, now, _detailedRetentionDays))
                TrimPoints(session.Points);
            else
                session.Points.Clear();
        }
        for (int i = 0; i < _document.DischargeSessions.Count; i++)
        {
            DischargeSession session = _document.DischargeSessions[i];
            PopulateSummary(session);
            if (BatteryHistoryRetentionPolicy.KeepDetailedSamples(
                    session.EndedAt ?? session.StartedAt, now, _detailedRetentionDays))
                TrimPoints(session.Points);
            else
                session.Points.Clear();
        }

        if (_document.ActiveSession is not null) TrimPoints(_document.ActiveSession.Points);
        if (_document.ActiveDischargeSession is not null) TrimPoints(_document.ActiveDischargeSession.Points);
    }

    private static void TrimPoints(List<ChargePoint> points)
    {
        if (points.Count > MaximumPointsPerSession)
            points.RemoveRange(0, points.Count - MaximumPointsPerSession);
    }

    private BatteryHistoryView BuildView()
    {
        ChargeSession? active = _document.ActiveSession;
        ChargeSession? curveSession = active ?? _document.Sessions.FirstOrDefault(session => session.Points.Count >= 1);
        IReadOnlyList<TimeSeriesPoint> chargePower = curveSession?.Points
            .Where(point => point.Watts is > 0)
            .Select(point => new TimeSeriesPoint(point.At, point.Watts!.Value, $"{point.Percent}%"))
            .ToArray() ?? [];
        IReadOnlyList<TimeSeriesPoint> chargePercent = curveSession?.Points
            .Select(point => new TimeSeriesPoint(point.At, point.Percent))
            .ToArray() ?? [];

        string curveLabel = active is not null
            ? "Current charge (live)"
            : curveSession is not null ? "Last charge session" : "Learning charge pattern";
        string currentText = active is null
            ? curveSession is null ? "No charge sessions recorded yet" : FormatChargeSession(curveSession, false)
            : FormatChargeSession(active, true);

        IReadOnlyList<string> sessions = _document.Sessions.Take(6).Select(session => FormatChargeSession(session, false)).ToArray();
        double[] usefulChargePowers = _document.Sessions
            .Where(IsUsefulChargeSession)
            .Select(session => session.AveragePowerWatts!.Value)
            .Take(40)
            .ToArray();
        double? typicalPower = usefulChargePowers.Length == 0 ? null : Median(usefulChargePowers);
        string typicalText = typicalPower is double typical
            ? $"{typical:0.#} W ({usefulChargePowers.Length} sessions)"
            : "Learning typical charge power";

        // Keep legacy session-derived observations for existing installs, then merge
        // them with the new cap-independent daily capacity samples. One point per UTC
        // day prevents dense polling from masquerading as a meaningful trend.
        var healthObservations = _document.Sessions
            .Where(session => session.HealthPercent is > 0 and <= 130)
            .Select(session => (
                At: session.EndedAt ?? session.StartedAt,
                Health: session.HealthPercent!.Value))
            .Concat(_document.HealthSamples
                .Where(sample => sample.HealthPercent is > 0 and <= 130)
                .Select(sample => (At: sample.At, Health: sample.HealthPercent)))
            .OrderBy(observation => observation.At)
            .GroupBy(observation => DateOnly.FromDateTime(observation.At.UtcDateTime))
            .Select(group => group.Last())
            .ToArray();
        double[] health = healthObservations.Select(observation => observation.Health).ToArray();
        IReadOnlyList<TimeSeriesPoint> healthTimeline = healthObservations
            .Select(observation => new TimeSeriesPoint(observation.At, observation.Health))
            .ToArray();

        RefreshPriors();
        return new BatteryHistoryView(
            chargePower,
            chargePercent,
            healthTimeline,
            sessions,
            curveLabel,
            currentText,
            typicalText,
            FormatHealthTrend(health),
            typicalPower)
        {
            CycleCountTimeline = _document.CycleSamples
                .Select(sample => new TimeSeriesPoint(sample.At, sample.Count)).ToArray(),
            CycleTrendText = DescribeCycleTrend(_document.CycleSamples)
        };
    }

    private static string DescribeCycleTrend(IReadOnlyList<BatteryCycleSample> samples)
    {
        if (samples.Count == 0) return "The battery has not exposed a cycle count.";
        if (samples.Count == 1) return "First firmware reading recorded. Changes will appear here over time.";
        if (samples.Zip(samples.Skip(1), (a, b) => b.Count < a.Count).Any(reset => reset))
            return "The counter decreased during this period. A battery replacement or counter reset may explain this.";
        double days = (samples[^1].At - samples[0].At).TotalDays;
        int cycles = samples[^1].Count - samples[0].Count;
        return days >= 1
            ? $"{cycles} cycles added over {days:0.#} days ({cycles / days * 7:0.#} per week). Firmware readings."
            : $"{cycles} cycles added since tracking began. A weekly rate needs at least one day of history.";
    }

    private void RefreshPriors()
    {
        double[] discharge = _document.DischargeSessions
            .Where(IsUsefulDischargeSession)
            .Select(session => session.AveragePowerWatts!.Value)
            .Take(40)
            .ToArray();
        BatteryPowerHistoryPriors.TypicalDischargePowerWatts = discharge.Length == 0 ? null : Median(discharge);
    }

    private static bool IsUsefulChargeSession(ChargeSession session)
    {
        TimeSpan duration = (session.EndedAt ?? session.StartedAt) - session.StartedAt;
        return duration >= TimeSpan.FromMinutes(5) &&
               session.AveragePowerWatts is > 0.4 and < 200 &&
               session.EndPercent - session.StartPercent >= 3;
    }

    private static bool IsUsefulDischargeSession(DischargeSession session)
    {
        TimeSpan duration = (session.EndedAt ?? session.StartedAt) - session.StartedAt;
        return duration >= TimeSpan.FromMinutes(8) &&
               session.AveragePowerWatts is > 0.4 and < 200 &&
               session.StartPercent - session.EndPercent >= 3;
    }

    private static string FormatChargeSession(ChargeSession session, bool active)
    {
        DateTimeOffset ended = active ? DateTimeOffset.UtcNow : session.EndedAt ?? session.StartedAt;
        TimeSpan duration = ended - session.StartedAt;
        string date = FormatDate(session.StartedAt);
        string average = session.AveragePowerWatts is double watts ? $", average power: {watts:0.#} W" : string.Empty;
        string energy = session.EnergyAddedWh is double wh ? $", energy added: {wh:0.#} Wh" : string.Empty;
        return $"{date}: {session.StartPercent}% to {session.EndPercent}% in {FormatDuration(duration)}{average}{energy}";
    }

    private static string FormatDischargeSession(DischargeSession session, bool active)
    {
        DateTimeOffset ended = active ? DateTimeOffset.UtcNow : session.EndedAt ?? session.StartedAt;
        TimeSpan duration = ended - session.StartedAt;
        string date = FormatDate(session.StartedAt);
        string average = session.AveragePowerWatts is double watts ? $", average power: {watts:0.#} W" : string.Empty;
        string energy = session.EnergyUsedWh is double wh ? $", energy used: {wh:0.#} Wh" : string.Empty;
        double hours = Math.Max(duration.TotalHours, 1d / 60d);
        double rate = Math.Max(0, session.StartPercent - session.EndPercent) / hours;
        string rateText = rate > 0 && duration >= TimeSpan.FromMinutes(5) ? $", drain rate: {rate:0.#}%/h" : string.Empty;
        return $"{date}: {session.StartPercent}% to {session.EndPercent}% in {FormatDuration(duration)}{average}{energy}{rateText}";
    }

    private static BatterySessionDetail ToDetail(ChargeSession session, bool active)
    {
        DateTimeOffset? ended = active ? null : session.EndedAt;
        TimeSpan duration = (ended ?? DateTimeOffset.UtcNow) - session.StartedAt;
        double hours = Math.Max(duration.TotalHours, 1d / 60d);
        double? percentPerHour = duration >= TimeSpan.FromMinutes(5) && session.EndPercent > session.StartPercent
            ? (session.EndPercent - session.StartPercent) / hours
            : null;
        return new BatterySessionDetail(
            string.IsNullOrWhiteSpace(session.Id) ? $"charge-{session.StartedAt.UtcTicks}" : session.Id,
            "Charge",
            session.StartedAt,
            ended,
            session.StartPercent,
            session.EndPercent,
            AveragePointPower(session.Points, session.AveragePowerWatts),
            PeakPointPower(session.Points, session.PeakPowerWatts),
            session.EnergyAddedWh,
            percentPerHour,
            session.Points.Where(point => point.Watts is > 0)
                .Select(point => new TimeSeriesPoint(point.At, point.Watts!.Value, $"{point.Percent}%"))
                .ToArray(),
            session.Points
                .Select(point => new TimeSeriesPoint(point.At, point.Percent))
                .ToArray(),
            FormatChargeSession(session, active),
            active);
    }

    private static BatterySessionDetail ToDetail(DischargeSession session, bool active)
    {
        DateTimeOffset? ended = active ? null : session.EndedAt;
        TimeSpan duration = (ended ?? DateTimeOffset.UtcNow) - session.StartedAt;
        double hours = Math.Max(duration.TotalHours, 1d / 60d);
        double? percentPerHour = duration >= TimeSpan.FromMinutes(5) && session.StartPercent > session.EndPercent
            ? (session.StartPercent - session.EndPercent) / hours
            : null;
        return new BatterySessionDetail(
            string.IsNullOrWhiteSpace(session.Id) ? $"discharge-{session.StartedAt.UtcTicks}" : session.Id,
            "Discharge",
            session.StartedAt,
            ended,
            session.StartPercent,
            session.EndPercent,
            AveragePointPower(session.Points, session.AveragePowerWatts),
            PeakPointPower(session.Points, session.PeakPowerWatts),
            session.EnergyUsedWh,
            percentPerHour,
            session.Points.Where(point => point.Watts is > 0)
                .Select(point => new TimeSeriesPoint(point.At, point.Watts!.Value, $"{point.Percent}%"))
                .ToArray(),
            session.Points
                .Select(point => new TimeSeriesPoint(point.At, point.Percent))
                .ToArray(),
            FormatDischargeSession(session, active),
            active);
    }

    private static string FormatDate(DateTimeOffset started)
    {
        DateTimeOffset local = started.ToLocalTime();
        return local.Date == DateTimeOffset.Now.Date ? "Today" : local.ToString("d MMM", CultureInfo.CurrentCulture);
    }

    private static string FormatHealthTrend(IReadOnlyList<double> health)
    {
        if (health.Count == 0) return "Waiting for capacity readings";
        if (health.Count == 1) return $"Health: {health[0]:0.#}% from 1 daily reading";
        double recent = health[^1];
        int compareIndex = Math.Max(0, health.Count - Math.Min(10, health.Count));
        double delta = recent - health[compareIndex];
        string trend = Math.Abs(delta) < 0.15 ? "stable" : delta > 0 ? $"+{delta:0.#} pp" : $"{delta:0.#} pp";
        return $"Health: {recent:0.#}%, {trend}, {health.Count} daily readings";
    }

    private static double? AveragePointPower(IReadOnlyList<ChargePoint> points, double? fallback)
    {
        double[] values = points.Where(point => point.Watts is > 0).Select(point => point.Watts!.Value).ToArray();
        return values.Length == 0 ? fallback : values.Average();
    }

    private static double? PeakPointPower(IReadOnlyList<ChargePoint> points, double? fallback)
    {
        double[] values = points.Where(point => point.Watts is > 0).Select(point => point.Watts!.Value).ToArray();
        return values.Length == 0 ? fallback : values.Max();
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0) return 0;
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2d : sorted[middle];
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
        return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} min";
    }

    private HistoryDocument Load()
    {
        try
        {
            if (!File.Exists(_path)) return new HistoryDocument();
            var info = new FileInfo(_path);
            if (info.Length > MaximumHistoryBytes)
            {
                File.Delete(_path);
                return new HistoryDocument();
            }
            string json = File.ReadAllText(_path);
            HistoryDocument document = JsonSerializer.Deserialize<HistoryDocument>(json) ?? new HistoryDocument();
            document.Sessions ??= [];
            document.DischargeSessions ??= [];
            document.HealthSamples ??= [];
            document.CycleSamples ??= [];
            document.SchemaVersion = 6;
            return document;
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
                _document.DischargeSessions = _document.DischargeSessions.Take(80).ToList();
                for (int i = 10; i < _document.Sessions.Count; i++) _document.Sessions[i].Points.Clear();
                for (int i = 10; i < _document.DischargeSessions.Count; i++) _document.DischargeSessions[i].Points.Clear();
                json = JsonSerializer.Serialize(_document, new JsonSerializerOptions { WriteIndented = false });
            }

            string temp = _path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _path, true);
        }
        catch
        {
        }
    }

    private void TryQuarantineCorruptFile()
    {
        try
        {
            if (!File.Exists(_path)) return;
            string quarantine = _path + ".corrupt";
            if (File.Exists(quarantine)) File.Delete(quarantine);
            File.Move(_path, quarantine);
        }
        catch
        {
        }
    }

    private sealed class HistoryDocument
    {
        public int SchemaVersion { get; set; } = 6;
        public ChargeSession? ActiveSession { get; set; }
        public List<ChargeSession> Sessions { get; set; } = [];
        public DischargeSession? ActiveDischargeSession { get; set; }
        public List<DischargeSession> DischargeSessions { get; set; } = [];
        public List<BatteryHealthSample> HealthSamples { get; set; } = [];
        public List<BatteryCycleSample> CycleSamples { get; set; } = [];
    }

    private sealed class BatteryCycleSample
    {
        public DateTimeOffset At { get; set; }
        public int Count { get; set; }
    }

    private sealed class BatteryHealthSample
    {
        public DateTimeOffset At { get; set; }
        public double FullChargeCapacityWh { get; set; }
        public double DesignCapacityWh { get; set; }
        public double HealthPercent { get; set; }
    }

    private sealed class ChargeSession
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public int StartPercent { get; set; }
        public int EndPercent { get; set; }
        public double? StartRemainingWh { get; set; }
        public double? EndRemainingWh { get; set; }
        public double? EnergyAddedWh { get; set; }
        public double? AveragePowerWatts { get; set; }
        public double? PeakPowerWatts { get; set; }
        public double? FullChargeCapacityWh { get; set; }
        public double? DesignCapacityWh { get; set; }
        public double? HealthPercent { get; set; }
        public List<ChargePoint> Points { get; set; } = [];
    }

    private sealed class DischargeSession
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public int StartPercent { get; set; }
        public int EndPercent { get; set; }
        public double? StartRemainingWh { get; set; }
        public double? EndRemainingWh { get; set; }
        public double? EnergyUsedWh { get; set; }
        public double? AveragePowerWatts { get; set; }
        public double? PeakPowerWatts { get; set; }
        public double? FullChargeCapacityWh { get; set; }
        public double? DesignCapacityWh { get; set; }
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
