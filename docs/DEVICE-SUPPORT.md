# Device support

This document describes the support model at **v0.1.0-alpha.38**. ThinkControl is intentionally capability-driven: a laptop model name alone does not grant write access or decide which setup/calibration/effect workflows appear.

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

Machine types `21Q6` / `21Q7` are the current verified X9 development path. That identity is only one part of the gate: the relevant low-level provider must also initialize and validate the expected hardware behavior before writes become available. The X9 is a reference implementation, not the product boundary.

Current X9-oriented areas include:

- sensor discovery and CPU/control temperature sources;
- independent Fan 1 / Fan 2 telemetry where Lenovo-native or reviewed EC providers expose it;
- Lenovo `LENOVO_OTHER_METHOD` direct target-RPM control when at least two exact-X9 channels independently pass VALID+GET+SET, safe-range and live-read gates;
- read-only Lenovo `EnergyDrv` `QueryFanSpeed` telemetry where the matching write contract is not verified;
- seven-step ThinkPad EC fan control only as the explicitly gated fallback/investigation provider, with Lenovo Auto recovery;
- Lenovo keyboard backlight provider/readback;
- Lenovo/OEM keyboard Auto where verified;
- haptic/raw-touchpad discovery and the shared Touchpad gesture editor.

If two native Lenovo fan channels have been proven during a hardware-service lifetime, a transient native read failure does not silently re-authorize the EC writer. If PawnIO is missing, stale or inaccessible, ThinkControl presents the existing repair path rather than treating provider failure as permission to guess another low-level backend.

## Fan semantics

Fan features are kept semantically distinct:

- **Firmware/OEM Auto**: firmware owns cooling;
- **OEM target RPM**: a provider accepts a real per-fan RPM target and exposes its own capability/range contract; target `0` is reserved for Auto on Lenovo Other Mode;
- **named fan curves**: ThinkControl's graph-based curve model, routed through the active provider's semantic output contract;
- **discrete output**: provider/model-specific states, not fake continuous PWM;
- **calibration**: a provider-advertised mapping workflow used only when that provider requires measured evidence before translating semantic percentages;
- **telemetry-only**: RPM/state can be shown without enabling writes.

The generic service/UI contract carries `FanCalibrationSupported` and `FanCalibrationRequired`. The Fans page must not recreate those decisions from `21Q6`, `21Q7`, X9, Lenovo or provider-detail strings. A future fan provider can advertise no calibration, optional calibration or a required mapping without adding a model-specific page branch.

On Lenovo Other Mode, the known fan attributes are `0x04030001` onward. ThinkControl requires at least two independently live, constrained writable channels before exposing direct target-RPM control; extra/phantom firmware records do not make the whole provider all-or-nothing. ThinkControl records which channels it actually owns and returns those owned channels to Auto on handoff/failure where the provider remains reachable.

`EnergyDrv` `QueryFanSpeed 0x83102570` is currently read-only evidence. The separate `ChangeFanSpeed 0x8310257C` writer remains blocked until its exact X9 command encoding and rollback semantics are recovered; maintenance/high-speed IOCTL families are not substituted for smooth percentage control.

The current UI uses `SetCoolingCurve`, `SetFanPercent` and `ReturnFanToAuto`. The service still accepts a small set of older cooling IPC operations for installed-client compatibility; those are not evidence of current UI features and should not be exposed as new controls.

## Keyboard semantics

- Off / Low / High are static hardware states when available.
- Auto means a verified firmware/OEM mode where supported. ThinkControl does not substitute a software idle-dimming loop and call it Auto.
- Breathing / Reactive / Audio are separate ThinkControl user-session effects.
- Effects appear only when the active provider advertises `KeyboardEffects`; generic UI does not infer support from a Lenovo/Vantage/backend-name string.
- A saved effect is restored only after that capability has been observed.

The current Lenovo Vantage fallback intentionally does not advertise repeated user-session effects because repeated writes can show Lenovo brightness pop-ups. A machine may therefore expose static/firmware behavior without exposing Effects. Other OEMs can advertise the same semantic capability from their own provider without creating vendor-specific Keyboard pages.

## Touchpad semantics

The Touchpad editor exposes six selectable zones: Top, Bottom, Left, Right, Top-left and Top-right. Edges and corners share one selection/rendering system, while runtime recognition remains deliberately strict and vendor-neutral.

An enabled top-corner launch uses one canonical physical **guard → diagonal lane → rounded end-cap** shape. The visible quarter-circle corner guard is also the recognizer's real first-frame priority area: a finger that begins there belongs to the enabled corner before the adjacent top/side edge can claim it. The lane and rounded cap are real usable areas too, not decorative hit targets. Disabled corner launches do not reserve that runtime input, so normal edge gestures remain available.

Both corner visuals are generated from the same left-local physical geometry; the right corner is an exact horizontal mirror. Edge visual bands are clipped around corner geometry and the same fill/boundary state grammar is used for edges and corners, so a corner does not behave or look like a separate overlay. The center visual is a directional arrow, the end is a semicircular arc, and an enabled action shows its Compact/Advanced semantic icon and label.

Per corner, **Reverse swipe closes ThinkControl** can be enabled independently. With that option on, starting in the rounded inner cap and swiping deliberately back toward the physical corner is classified as an outward corner gesture and hides whichever ThinkControl surface is visible. With it off, the same end-cap remains part of the normal inward launch area. Wrong-direction/rejected corner candidates stay locked out until lift and never fall through into a nearby edge gesture.

The reverse-close action reuses the canonical application hide-to-tray transition. Compact completes the transition-owned synchronous hide before shell-state verification; this does not change the separate animated tray-toggle path. The mirrored reverse visual fixture is built from a clean non-live corner baseline so its trail contains only the outward gesture being validated.

Track control can optionally expose a small visible center Play/Pause target inside the active edge lane. That target accepts only a short low-travel tap; the surrounding edge remains Previous/Next swipe space. It does not add a second overlay/recognizer or hidden hold gesture.

Visualized live input is coalesced for WPF, while recognition still receives the full raw frame stream.

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

- actual X9 direct Lenovo target-RPM activation/range, two-fan response and repeated Auto recovery on the installed firmware/software stack;
- EnergyDrv/native telemetry correlation when Other Mode does not expose a writable contract;
- final maximum-cooling comparison with naturally hot Lenovo Auto;
- provider-driven fan calibration behavior on the discrete X9 fallback and future devices;
- Lenovo keyboard Auto/Fn+Space/readback agreement;
- direct-provider effect behavior without Lenovo pop-ups;
- haptic Touchpad corner sensitivity/symmetry and high-rate responsiveness;
- corner guard reliability against nearby top/side gestures on real finger contact;
- center Play/Pause tap reliability versus surrounding Previous/Next swipes;
- reverse-close feel and accidental-trigger rate for both mirrored corners;
- Audio volume/microphone behavior across real navigation during a drag;
- provider repair/restart behavior after real PawnIO/service failure states.

These checks belong in `docs/ALPHA-TESTING.md` and release-readiness notes; they must not be marked complete from screenshots alone.
