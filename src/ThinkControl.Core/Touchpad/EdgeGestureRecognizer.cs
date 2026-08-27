namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Platform-independent recognizer for one-finger edge gestures. It deliberately
/// knows nothing about WPF, Raw Input, cursor capture or the action backend so the
/// recognition contract can be replay-tested in CI.
/// </summary>
public sealed class EdgeGestureRecognizer
{
    private TouchpadGestureConfiguration _configuration;
    private TouchpadGeometry? _geometry;
    private bool _lockoutUntilAllLift;
    private int? _contactId;
    private int _startX;
    private int _startY;
    private int _lastX;
    private int _lastY;
    private TouchpadEdge[] _candidateEdges = [];
    private TouchpadEdge? _claimedEdge;
    private GestureActionKind _claimedAction;
    private GesturePhase? _phase;
    private double _lastTotalTravelMm;

    public EdgeGestureRecognizer(TouchpadGestureConfiguration? configuration = null)
    {
        _configuration = (configuration ?? TouchpadGestureConfiguration.Default).Sanitize();
    }

    public bool HasCandidateOrActiveGesture => _phase is not null;
    public TouchpadEdge? ActiveEdge => _claimedEdge;

    public void SetConfiguration(TouchpadGestureConfiguration configuration)
    {
        _configuration = configuration.Sanitize();
        if (!_configuration.Enabled)
            Reset();
    }

    public void SetGeometry(TouchpadGeometry geometry) => _geometry = geometry;

    public GestureSignal? ProcessFrame(
        IReadOnlyList<TouchContact> contacts,
        TouchpadGeometry? geometry = null)
    {
        if (geometry is not null)
            _geometry = geometry;

        if (!_configuration.Enabled || _geometry is null)
        {
            if (_phase is not null)
                return Cancel("Gestures disabled or touchpad geometry unavailable");
            return null;
        }

        TouchContact[] down = contacts.Where(static c => c.IsDown).ToArray();

        if (down.Length == 0)
        {
            _lockoutUntilAllLift = false;
            if (_phase is GesturePhase.Claimed or GesturePhase.Active)
            {
                GestureSignal released = new(
                    GesturePhase.Released,
                    _claimedEdge,
                    _claimedAction,
                    TotalTravelMm: _lastTotalTravelMm,
                    ContactId: _contactId);
                Reset();
                return released;
            }

            // Previous/Next optionally owns a deliberate hold-and-release action.
            // Before alpha.26 a stationary contact never left Candidate, so lifting
            // simply reset the recognizer and Play/Pause could never fire reliably.
            // Emit a release for an unambiguous candidate; the action router owns the
            // hold duration and travel threshold, so ordinary taps remain no-ops.
            if (_phase == GesturePhase.Candidate && _candidateEdges.Length == 1)
            {
                TouchpadEdge edge = _candidateEdges[0];
                GestureActionKind action = _configuration.BindingFor(edge).Action;
                GestureSignal released = new(
                    GesturePhase.Released,
                    edge,
                    action,
                    TotalTravelMm: _lastTotalTravelMm,
                    ContactId: _contactId);
                Reset();
                return released;
            }

            Reset();
            return null;
        }

        if (_lockoutUntilAllLift)
            return null;

        if (down.Length != 1)
        {
            _lockoutUntilAllLift = true;
            return _phase is not null ? Cancel("Second finger detected", preserveLockout: true) : null;
        }

        TouchContact contact = down[0];
        if (!contact.Confidence)
        {
            _lockoutUntilAllLift = true;
            return _phase is not null ? Cancel("Low-confidence contact", preserveLockout: true) : null;
        }

        if (_contactId is int trackedId && contact.ContactId != trackedId)
        {
            _lockoutUntilAllLift = true;
            return Cancel("Contact changed", preserveLockout: true);
        }

        if (_phase is null)
            return BeginCandidate(contact);

        _contactId ??= contact.ContactId;

        if (_phase == GesturePhase.Candidate)
            return ResolveCandidate(contact);

        if (_phase is GesturePhase.Claimed or GesturePhase.Active)
            return UpdateActive(contact);

        return null;
    }

    public GestureSignal? CancelCurrent(string reason)
    {
        if (_phase is null)
            return null;
        return Cancel(reason);
    }

    private GestureSignal? BeginCandidate(TouchContact contact)
    {
        TouchpadGeometry geometry = _geometry!;
        var candidates = new List<TouchpadEdge>(2);

        foreach (TouchpadEdge edge in Enum.GetValues<TouchpadEdge>())
        {
            if (_configuration.BindingFor(edge).Action == GestureActionKind.Disabled)
                continue;

            if (geometry.DistanceToEdgeMm(edge, contact.X, contact.Y) <= _configuration.EdgeWidthMm)
                candidates.Add(edge);
        }

        if (candidates.Count == 0)
        {
            _lockoutUntilAllLift = true;
            return null;
        }

        _candidateEdges = candidates.ToArray();
        _contactId = contact.ContactId;
        _startX = _lastX = contact.X;
        _startY = _lastY = contact.Y;
        _phase = GesturePhase.Candidate;
        _lastTotalTravelMm = 0;

        GestureActionKind candidateAction = candidates.Count == 1
            ? _configuration.BindingFor(candidates[0]).Action
            : GestureActionKind.Disabled;
        return new GestureSignal(
            GesturePhase.Candidate,
            candidates.Count == 1 ? candidates[0] : null,
            candidateAction,
            Reason: candidates.Count > 1 ? "Corner candidate" : null,
            ContactId: contact.ContactId);
    }

    private GestureSignal? ResolveCandidate(TouchContact contact)
    {
        TouchpadGeometry geometry = _geometry!;
        double dx = geometry.DeltaXToMm(contact.X - _startX);
        double dy = geometry.DeltaYToMm(contact.Y - _startY);
        double absX = Math.Abs(dx);
        double absY = Math.Abs(dy);
        double activation = _configuration.ActivationDistanceMm;
        double dominance = _configuration.DirectionDominance;

        if (Math.Max(absX, absY) < activation)
            return null;

        // Open ThinkControl follows the ASUS-style mental model: start at an edge
        // and move into the touchpad, perpendicular to that edge. It intentionally
        // differs from continuous controls, which travel along the edge. Give an
        // assigned inward action first refusal at a corner so a diagonal inward
        // swipe is deterministic instead of depending on enum order.
        TouchpadEdge? inward = _candidateEdges
            .Where(edge => _configuration.BindingFor(edge).Action == GestureActionKind.OpenThinkControl)
            .FirstOrDefault(edge => IsInwardIntent(edge, dx, dy, activation, dominance));
        if (inward is TouchpadEdge inwardEdge &&
            _configuration.BindingFor(inwardEdge).Action == GestureActionKind.OpenThinkControl &&
            IsInwardIntent(inwardEdge, dx, dy, activation, dominance))
        {
            return Claim(inwardEdge, contact, InwardTravelMm(inwardEdge, dx, dy));
        }

        TouchpadEdge? chosen = null;
        bool horizontalIntent = absX >= activation && absX >= absY * dominance;
        bool verticalIntent = absY >= activation && absY >= absX * dominance;

        if (horizontalIntent)
            chosen = _candidateEdges.FirstOrDefault(IsHorizontalEdge);
        else if (verticalIntent)
            chosen = _candidateEdges.FirstOrDefault(IsVerticalEdge);
        else if (Math.Max(absX, absY) < activation * 2.0)
            return null;

        if (chosen is null || !_candidateEdges.Contains(chosen.Value))
            return Cancel("Wrong direction");

        if (horizontalIntent && !_candidateEdges.Any(IsHorizontalEdge))
            return Cancel("Wrong direction");
        if (verticalIntent && !_candidateEdges.Any(IsVerticalEdge))
            return Cancel("Wrong direction");

        // OpenThinkControl is perpendicular-only. If its edge was selected by the
        // normal along-edge resolver, do not accidentally treat that as an opener.
        if (_configuration.BindingFor(chosen.Value).Action == GestureActionKind.OpenThinkControl)
            return Cancel("Swipe inward to open ThinkControl");

        double total = AxisTravelMm(chosen.Value, contact.X, contact.Y);
        TouchpadEdgeBinding binding = _configuration.BindingFor(chosen.Value);
        if (binding.Inverted)
            total = -total;
        total *= binding.Sensitivity;
        return Claim(chosen.Value, contact, total);
    }

    private GestureSignal Claim(TouchpadEdge edge, TouchContact contact, double total)
    {
        _claimedEdge = edge;
        _claimedAction = _configuration.BindingFor(edge).Action;
        _phase = GesturePhase.Claimed;
        _lastX = contact.X;
        _lastY = contact.Y;
        _lastTotalTravelMm = total;

        return new GestureSignal(
            GesturePhase.Claimed,
            edge,
            _claimedAction,
            total,
            total,
            ContactId: contact.ContactId);
    }

    private GestureSignal? UpdateActive(TouchContact contact)
    {
        TouchpadEdge edge = _claimedEdge!.Value;
        TouchpadGeometry geometry = _geometry!;
        TouchpadEdgeBinding binding = _configuration.BindingFor(edge);

        if (_claimedAction == GestureActionKind.OpenThinkControl)
        {
            double dx = geometry.DeltaXToMm(contact.X - _startX);
            double dy = geometry.DeltaYToMm(contact.Y - _startY);
            double total = InwardTravelMm(edge, dx, dy) * binding.Sensitivity;
            double previous = _lastTotalTravelMm;
            _lastTotalTravelMm = total;
            _lastX = contact.X;
            _lastY = contact.Y;
            _phase = GesturePhase.Active;
            return new GestureSignal(
                GesturePhase.Active,
                edge,
                _claimedAction,
                total,
                total - previous,
                ContactId: contact.ContactId);
        }

        if (geometry.DistanceToEdgeMm(edge, contact.X, contact.Y) > _configuration.ContinuationToleranceMm)
            return Cancel("Gesture left edge tolerance");

        double axisTotal = AxisTravelMm(edge, contact.X, contact.Y);
        double delta = edge is TouchpadEdge.Left or TouchpadEdge.Right
            ? geometry.DeltaYToMm(contact.Y - _lastY)
            : geometry.DeltaXToMm(contact.X - _lastX);

        if (binding.Inverted)
        {
            axisTotal = -axisTotal;
            delta = -delta;
        }

        axisTotal *= binding.Sensitivity;
        delta *= binding.Sensitivity;
        _lastTotalTravelMm = axisTotal;
        _lastX = contact.X;
        _lastY = contact.Y;
        _phase = GesturePhase.Active;

        return new GestureSignal(
            GesturePhase.Active,
            edge,
            _claimedAction,
            axisTotal,
            delta,
            ContactId: contact.ContactId);
    }

    private double AxisTravelMm(TouchpadEdge edge, int x, int y)
    {
        TouchpadGeometry geometry = _geometry!;
        return edge is TouchpadEdge.Left or TouchpadEdge.Right
            ? geometry.DeltaYToMm(y - _startY)
            : geometry.DeltaXToMm(x - _startX);
    }

    private static bool IsInwardIntent(
        TouchpadEdge edge,
        double dx,
        double dy,
        double activation,
        double dominance)
    {
        double inward = InwardTravelMm(edge, dx, dy);
        double parallel = edge is TouchpadEdge.Left or TouchpadEdge.Right ? Math.Abs(dy) : Math.Abs(dx);
        return inward >= activation && inward >= parallel * Math.Min(dominance, 1.35);
    }

    private static double InwardTravelMm(TouchpadEdge edge, double dx, double dy) => edge switch
    {
        TouchpadEdge.Left => dx,
        TouchpadEdge.Right => -dx,
        TouchpadEdge.Top => dy,
        TouchpadEdge.Bottom => -dy,
        _ => 0
    };

    private GestureSignal Cancel(string reason, bool preserveLockout = false)
    {
        GestureSignal signal = new(
            GesturePhase.Cancelled,
            _claimedEdge ?? (_candidateEdges.Length == 1 ? _candidateEdges[0] : null),
            _claimedAction,
            TotalTravelMm: _lastTotalTravelMm,
            Reason: reason,
            ContactId: _contactId);

        bool lockout = preserveLockout || _lockoutUntilAllLift;
        Reset();
        _lockoutUntilAllLift = lockout;
        return signal;
    }

    private void Reset()
    {
        _contactId = null;
        _candidateEdges = [];
        _claimedEdge = null;
        _claimedAction = GestureActionKind.Disabled;
        _phase = null;
        _lastTotalTravelMm = 0;
        _startX = _startY = _lastX = _lastY = 0;
    }

    private static bool IsVerticalEdge(TouchpadEdge edge) =>
        edge is TouchpadEdge.Left or TouchpadEdge.Right;

    private static bool IsHorizontalEdge(TouchpadEdge edge) =>
        edge is TouchpadEdge.Top or TouchpadEdge.Bottom;
}
