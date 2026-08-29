# Device support

This document describes the support model at **v0.1.0-alpha.35**. ThinkControl is intentionally capability-driven: a laptop model name alone does not grant write access.

## Support levels

### Windows-generic

Available without vendor-specific write access where Windows exposes the information/action safely:

- shell/navigation and settings;
- update/install flow;
- Windows power-policy integration;
- display/audio pages backed by Windows-visible capabilities;
- battery and generic telemetry that Windows/providers expose;
- diagnostics/report preview and explicit sharing controls.

Unsupported vendor controls stay visible as unavailable rather than disappearing or pretending to work.

### Provider-backed read-only

ThinkControl can expose telemetry from a reviewed provider without implying that writes are safe. Examples include temperature/fan/sensor discovery where the provider produces credible values but no verified write contract exists.

### Verified write support

A write control is enabled only when the active provider advertises the exact semantic capability and passes its provider/device validation gate. A failed or unknown write path must fall back to safe firmware/OEM ownership rather than guessing addresses, EC commands or vendor APIs.

## ThinkPad X9 15 Gen 1

Machine types `21Q6` / `21Q7` are the current verified X9 development path. That identity is only one part of the gate: the relevant low-level provider must also initialize and validate the expected hardware behavior before EC writes become available.

Current X9-oriented areas include:

- sensor discovery and CPU/control temperature sources;
- fan RPM/telemetry where available;
- verified fan EC control with Lenovo Auto fallback;
- Lenovo keyboard backlight provider/readback;
- Lenovo/OEM keyboard Auto where verified;
- haptic/raw-touchpad discovery and the shared Touchpad gesture editor.

If PawnIO is missing, stale or inaccessible, ThinkControl should present the existing repair path instead of silently attempting another low-level write backend.

## Fan semantics

Fan features are kept semantically distinct:

- **Lenovo Auto / firmware Auto**: firmware owns cooling;
- **named fan curves**: ThinkControl's current graph-based curve model;
- **manual percent/output**: only where the provider explicitly supports that meaning;
- **telemetry-only**: RPM/state can be shown without enabling writes.

The current UI uses `SetCoolingCurve`, `SetFanPercent` and `ReturnFanToAuto`. The service still accepts a small set of older cooling IPC operations for installed-client compatibility; those are not evidence of current UI features and should not be exposed as new controls. Alpha.35 removes the obsolete current-client wrappers while intentionally preserving those server-side compatibility handlers and the legacy updater fixture.

## Keyboard semantics

- Off / Low / High are static hardware states when available.
- Auto means a verified Lenovo/OEM firmware mode. ThinkControl no longer substitutes a software idle-dimming loop and calls it Auto.
- Breathing / Reactive / Audio are separate ThinkControl user-session effects.
- Effects require the direct keyboard provider; the Vantage fallback is intentionally excluded from repeated effect writes so Lenovo brightness pop-ups are not generated continuously.

A machine with only basic/Vantage-mediated brightness support may therefore expose static/firmware behavior without exposing Effects.

## Touchpad semantics

The Touchpad editor exposes six selectable zones: Top, Bottom, Left, Right, Top-left and Top-right. Edges and corners share one selection/rendering system, while runtime recognition remains deliberately strict and vendor-neutral.

An enabled top-corner launch uses one canonical physical **guard → diagonal lane → rounded end-cap** shape. The visible quarter-circle corner guard is also the recognizer's real first-frame priority area: a finger that begins there belongs to the enabled corner before the adjacent top/side edge can claim it. The lane and rounded cap are real usable areas too, not decorative hit targets. Disabled corner launches do not reserve that runtime input, so normal edge gestures remain available.

Both corner visuals are generated from the same left-local physical geometry; the right corner is an exact horizontal mirror. The center visual is a directional arrow rather than a neutral divider, the end is a semicircular arc rather than a flat 90-degree cross-line, and an enabled action shows its Compact/Advanced semantic icon and label.

Per corner, **Reverse swipe closes ThinkControl** can be enabled independently. With that option on, starting in the rounded inner cap and swiping deliberately back toward the physical corner is classified as an outward corner gesture and hides whichever ThinkControl surface is visible. With it off, the same end-cap remains part of the normal inward launch area. Wrong-direction/rejected corner candidates stay locked out until lift and never fall through into a nearby edge gesture.

Visualized live input is coalesced for WPF, while recognition still receives the full raw frame stream. Track-center behavior remains a separate optional affordance and does not create a second corner-selection system.

## Unknown/new hardware

Unknown hardware should remain safe by default:

1. collect passive, non-sensitive identity/capability evidence;
2. expose read-only features that have credible generic/provider support;
3. keep risky writes unavailable;
4. allow an explicit sanitized compatibility report;
5. promote write support only after reviewed evidence and an explicit provider/profile change.

ThinkControl should never learn a new device by experimentally writing arbitrary EC/IOCTL/BIOS values merely for diagnostics.

## Physical validation

Hosted CI can prove source/build/lifecycle behavior but not physical hardware feel or firmware response. Real-device validation is still required for:

- actual X9 fan RPM/control and Auto recovery;
- Lenovo keyboard Auto/Fn+Space/readback agreement;
- direct-provider effect behavior without Lenovo pop-ups;
- haptic Touchpad corner sensitivity/symmetry and high-rate responsiveness;
- corner guard reliability against nearby top/side gestures on real finger contact;
- reverse-close feel and accidental-trigger rate for both mirrored corners;
- Audio volume/microphone behavior across real navigation during a drag;
- provider repair/restart behavior after real PawnIO/service failure states.

These checks belong in `docs/ALPHA-TESTING.md` and release-readiness notes; they must not be marked complete from screenshots alone.
