namespace ThinkControl.Core.Touchpad;

/// <summary>
/// Platform-independent recognizer for one-finger edge and launch-corner gestures.
/// It deliberately knows nothing about WPF, Raw Input, cursor capture or the action
/// backend so the recognition contract can be replay-tested in CI.
/// </summary>
public sealed class EdgeGestureRecognizer
{
    private const double CornerActivationMm = 6.5;
    private const double CornerMinimumAxisMm = 2.5;
    private const double CornerMinimumAxisRatio = 0.34;

    private TouchpadGestureConfiguration _configuration;
    private TouchpadGeometry? _geometry;
    private bool _lockoutUntilAllLift;
    private int? _contactId;
    private int _startX;
    private int _startY;
    private int _lastX;
    private int _lastY;
    private TouchpadEdge[] _candidateEdges = [];
    private TouchpadCorner? _candidateCorner;
    private TouchpadEdge? _claimedEdge;
    private TouchpadCorner? _claimedCorner;
    private GestureActionKind _claimedAction;
    private GesturePhase? _phase;
    private double _lastTotalTravelMm;

    public EdgeGestureRecognizer(TouchpadGestureConfiguration? configuration = null)
    {
        _configuration = (configuration ?? TouchpadGestureConfiguration.Default).Sanitize();
    }

    public bool HasCandidateOrActiveGesture => _phase is not null;
    public TouchpadEdge? ActiveEdge => _claimedEdge;
    public TouchpadCorner? ActiveCorner => _claimedCorner;

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
                TouchpadEdge? edge = _claimedEdge;
                TouchpadCorner? corner = _claimedCorner;
                GestureSignal released = new(
                    GesturePhase.Released,
                    edge,
                    _claimedAction,
                    TotalTravelMm: _lastTotalTravelMm,
                    ContactId: _contactId,
                    EdgePosition01: edge is TouchpadEdge resolved ? AlongEdgePosition01(resolved, _startX, _startY) : null,
                    Corner: corner);
                Reset();
                return released;
            }

            // Track control optionally owns a deliberate center hold-and-release.
            // Emit a release for an unambiguous edge candidate; launch corners never
            // commit on lift alone and therefore cannot become glorified corner taps.
            if (_phase == GesturePhase.Candidate && _candidateCorner is null && _candidateEdges.Length == 1)
            {
                TouchpadEdge edge = _candidateEdges[0];
                GestureActionKind action = _configuration.BindingFor(edge).Action;
                GestureSignal released = new(
                    GesturePhase.Released,
                    edge,
                    action,
                    TotalTravelMm: _lastTotalTravelMm,
                    ContactId: _contactId,
                    EdgePosition01: AlongEdgePosition01(edge, _startX, _startY));
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

        // Configured launch corners get first refusal only when the finger actually
        // starts inside the same diagonal lane that the UI draws. A disabled corner
        // is invisible to recognition, so the top/side edge remains available.
        TouchpadCorner? corner = DetectConfiguredCorner(contact);
        if (corner is TouchpadCorner launchCorner)
        {
            _candidateCorner = launchCorner;
            _candidateEdges = [];
            _contactId = contact.ContactId;
            _startX = _lastX = contact.X;
            _startY = _lastY = contact.Y;
            _phase = GesturePhase.Candidate;
            _lastTotalTravelMm = 0;
            return new GestureSignal(
                GesturePhase.Candidate,
                Edge: null,
                Action: _configuration.LaunchFor(launchCorner),
                ContactId: contact.ContactId,
                Corner: launchCorner);
        }

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
        _candidateCorner = null;
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
            Reason: candidates.Count > 1 ? "Edge-corner candidate" : null,
            ContactId: contact.ContactId,
            EdgePosition01: candidates.Count == 1 ? AlongEdgePosition01(candidates[0], contact.X, contact.Y) : null);
    }

    private GestureSignal? ResolveCandidate(TouchContact contact)
    {
        if (_candidateCorner is TouchpadCorner corner)
            return ResolveCornerCandidate(corner, contact);

        TouchpadGeometry geometry = _geometry!;
        double dx = geometry.DeltaXToMm(contact.X - _startX);
        double dy = geometry.DeltaYToMm(contact.Y - _startY);
        double absX = Math.Abs(dx);
        double absY = Math.Abs(dy);
        double activation = _configuration.ActivationDistanceMm;
        double dominance = _configuration.DirectionDominance;

        if (Math.Max(absX, absY) < activation)
        {
            _lastTotalTravelMm = Math.Sqrt(dx * dx + dy * dy);
            return null;
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

        double total = AxisTravelMm(chosen.Value, contact.X, contact.Y);
        TouchpadEdgeBinding binding = _configuration.BindingFor(chosen.Value);
        if (binding.Inverted)
            total = -total;
        total *= binding.Sensitivity;
        return ClaimEdge(chosen.Value, contact, total);
    }

    private GestureSignal? ResolveCornerCandidate(TouchpadCorner corner, TouchContact contact)
    {
        TouchpadGeometry geometry = _geometry!;
        double dx = geometry.DeltaXToMm(contact.X - _startX);
        double dy = geometry.DeltaYToMm(contact.Y - _startY);
        double inwardX = corner == TouchpadCorner.TopLeft ? dx : -dx;
        double inwardY = dy;
        double combined = Math.Sqrt(inwardX * inwardX + inwardY * inwardY);
        _lastTotalTravelMm = combined;

        // A launch is intentionally stricter than an edge adjustment. Both axes
        // must move inward by several millimetres and neither axis may dominate the
        // other completely. Normal vertical scrolling or horizontal pointer travel
        // from a corner therefore stays a no-op.
        if (inwardX < -1.0 || inwardY < -1.0)
        {
            if (combined >= CornerActivationMm)
                return Cancel("Corner gesture moved outward");
            return null;
        }

        if (combined < CornerActivationMm)
            return null;

        double minAxis = Math.Min(inwardX, inwardY);
        double maxAxis = Math.Max(inwardX, inwardY);
        if (minAxis < CornerMinimumAxisMm || maxAxis <= 0 || minAxis / maxAxis < CornerMinimumAxisRatio)
        {
            if (combined >= CornerActivationMm * 1.55)
                return Cancel("Corner launch requires diagonal inward motion");
            return null;
        }

        return ClaimCorner(corner, contact, combined);
    }

    private GestureSignal ClaimEdge(TouchpadEdge edge, TouchContact contact, double total)
    {
        _claimedEdge = edge;
        _claimedCorner = null;
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
            ContactId: contact.ContactId,
            EdgePosition01: AlongEdgePosition01(edge, _startX, _startY));
    }

    private GestureSignal ClaimCorner(TouchpadCorner corner, TouchContact contact, double total)
    {
        _claimedEdge = null;
        _claimedCorner = corner;
        _claimedAction = _configuration.LaunchFor(corner);
        _phase = GesturePhase.Claimed;
        _lastX = contact.X;
        _lastY = contact.Y;
        _lastTotalTravelMm = total;

        return new GestureSignal(
            GesturePhase.Claimed,
            Edge: null,
            Action: _claimedAction,
            TotalTravelMm: total,
            DeltaMm: total,
            ContactId: contact.ContactId,
            Corner: corner);
    }

    private GestureSignal? UpdateActive(TouchContact contact)
    {
        if (_claimedCorner is TouchpadCorner corner)
        {
            TouchpadGeometry geometry = _geometry!;
            double dx = geometry.DeltaXToMm(contact.X - _startX);
            double dy = geometry.DeltaYToMm(contact.Y - _startY);
            double inwardX = corner == TouchpadCorner.TopLeft ? dx : -dx;
            double inwardY = dy;
            double positiveX = Math.Max(0, inwardX);
            double positiveY = Math.Max(0, inwardY);
            double total = Math.Sqrt(positiveX * positiveX + positiveY * positiveY);
            double previous = _lastTotalTravelMm;
            _lastTotalTravelMm = total;
            _lastX = contact.X;
            _lastY = contact.Y;
            _phase = GesturePhase.Active;
            return new GestureSignal(
                GesturePhase.Active,
                Edge: null,
                Action: _claimedAction,
                TotalTravelMm: total,
                DeltaMm: total - previous,
                ContactId: contact.ContactId,
                Corner: corner);
        }

        TouchpadEdge edge = _claimedEdge!.Value;
        TouchpadGeometry edgeGeometry = _geometry!;
        TouchpadEdgeBinding binding = _configuration.BindingFor(edge);

        if (edgeGeometry.DistanceToEdgeMm(edge, contact.X, contact.Y) > _configuration.ContinuationToleranceMm)
            return Cancel("Gesture left edge tolerance");

        double axisTotal = AxisTravelMm(edge, contact.X, contact.Y);
        double delta = edge is TouchpadEdge.Left or TouchpadEdge.Right
            ? edgeGeometry.DeltaYToMm(contact.Y - _lastY)
            : edgeGeometry.DeltaXToMm(contact.X - _lastX);

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
            ContactId: contact.ContactId,
            EdgePosition01: AlongEdgePosition01(edge, _startX, _startY));
    }

    private TouchpadCorner? DetectConfiguredCorner(TouchContact contact)
    {
        TouchpadGeometry geometry = _geometry!;

        if (_configuration.LaunchFor(TouchpadCorner.TopLeft) != GestureActionKind.Disabled &&
            TouchpadCornerZonePolicy.ContainsStart(TouchpadCorner.TopLeft, geometry, contact.X, contact.Y))
        {
            return TouchpadCorner.TopLeft;
        }

        if (_configuration.LaunchFor(TouchpadCorner.TopRight) != GestureActionKind.Disabled &&
            TouchpadCornerZonePolicy.ContainsStart(TouchpadCorner.TopRight, geometry, contact.X, contact.Y))
        {
            return TouchpadCorner.TopRight;
        }

        return null;
    }

    private double AxisTravelMm(TouchpadEdge edge, int x, int y)
    {
        TouchpadGeometry geometry = _geometry!;
        return edge is TouchpadEdge.Left or TouchpadEdge.Right
            ? geometry.DeltaYToMm(y - _startY)
            : geometry.DeltaXToMm(x - _startX);
    }

    private double AlongEdgePosition01(TouchpadEdge edge, int x, int y)
    {
        TouchpadGeometry geometry = _geometry!;
        return edge is TouchpadEdge.Left or TouchpadEdge.Right
            ? Math.Clamp((y - geometry.YLogicalMin) / (double)geometry.YRange, 0.0, 1.0)
            : Math.Clamp((x - geometry.XLogicalMin) / (double)geometry.XRange, 0.0, 1.0);
    }

    private GestureSignal Cancel(string reason, bool preserveLockout = false)
    {
        TouchpadEdge? edge = _claimedEdge ?? (_candidateEdges.Length == 1 ? _candidateEdges[0] : null);
        TouchpadCorner? corner = _claimedCorner ?? _candidateCorner;
        GestureActionKind action = _claimedAction != GestureActionKind.Disabled
            ? _claimedAction
            : corner is TouchpadCorner candidateCorner
                ? _configuration.LaunchFor(candidateCorner)
                : edge is TouchpadEdge candidateEdge
                    ? _configuration.BindingFor(candidateEdge).Action
                    : GestureActionKind.Disabled;

        GestureSignal signal = new(
            GesturePhase.Cancelled,
            edge,
            action,
            TotalTravelMm: _lastTotalTravelMm,
            Reason: reason,
            ContactId: _contactId,
            EdgePosition01: edge is TouchpadEdge resolved ? AlongEdgePosition01(resolved, _startX, _startY) : null,
            Corner: corner);

        bool lockout = preserveLockout || _lockoutUntilAllLift;
        Reset();
        _lockoutUntilAllLift = lockout;
        return signal;
    }

    private void Reset()
    {
        _contactId = null;
        _candidateEdges = [];
        _candidateCorner = null;
        _claimedEdge = null;
        _claimedCorner = null;
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