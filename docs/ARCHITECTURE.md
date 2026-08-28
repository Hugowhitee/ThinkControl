# ThinkControl architecture

This document describes the current architecture at **v0.1.0-alpha.34**. `docs/RELEASE_READINESS.md` is the persistent release/commercial handoff; this file explains runtime boundaries and intentional compatibility debt.

## Process boundary

ThinkControl is split into a normal-user WPF application and a privileged Windows service.

- `ThinkControl.UI` owns Compact/Advanced windows, settings, presentation state, user-session effects and orchestration.
- `ThinkControl.Service` owns privileged hardware access and exposes a narrow named-pipe IPC contract.
- `ThinkControl.Core` contains shared models, policies and protocol types that do not depend on WPF or hardware implementations.
- `ThinkControl.Hardware` contains provider implementations and verified low-level device behavior.
- `ThinkControl.DeviceProfiles` is the architectural boundary for device/profile data. It intentionally remains small while capability logic is moved out of UI assumptions.

The UI must remain `asInvoker`. Hardware operations that need elevated/device access belong in the service rather than causing repeated UAC prompts from the desktop process.

## Hardware safety model

Hardware support is capability-driven. Unknown hardware remains read-only/safe until an operation has a reviewed provider and validation gate.

The verified ThinkPad X9 path currently uses machine-type and provider validation before EC writes. Runtime fan safety remains separate from UI presentation: firmware Auto is the fallback, unsupported/failed control does not silently become a different write path, and the application attempts to return fan control to firmware when exiting.

Repeated provider discovery is avoided where possible. The service keeps provider state; the UI consumes bounded status snapshots and uses targeted refresh operations for sensors, keyboard and full provider recovery.

## Cooling model

The current UI has one fan-curve model:

- named `FanCurveDefinition` profiles;
- `SetCoolingCurve` for current curve writes;
- `SetFanPercent` for deliberate manual output where supported;
- `ReturnFanToAuto` for firmware/OEM ownership;
- characterization operations for supported fan-provider calibration workflows.

The service still accepts older IPC operations such as `SetCoolingProfile`, `SetCustomCoolingCurve` and `MarkFanLevelAudible` for the supported installed-client compatibility floor. Those are **legacy server compatibility endpoints**, not current UI APIs. Do not remove them merely because the current `HardwareServiceClient` no longer calls them; removal requires an explicit updater/client-floor decision and compatibility test update.

## Keyboard model

Keyboard brightness and effects deliberately have different ownership.

- Off / Low / High are direct/static hardware states when the active provider supports them.
- Auto means **verified Lenovo/OEM firmware Auto**. ThinkControl does not emulate Auto with a High → Low → Off idle loop.
- Breathing / Reactive / Audio are ThinkControl user-session effects and require the direct backlight provider.
- The Lenovo Vantage fallback is excluded from repeated effect writes so effects do not spam Lenovo keyboard-brightness pop-ups.

Keyboard writes are serialized by the keyboard/effect coordinator so firmware/static ownership and user-session animation do not fight each other.

## Touchpad model

The Advanced Touchpad editor exposes one six-zone selection model: Top, Bottom, Left, Right, Top-left and Top-right.

`TouchpadVisualizer` owns edge/corner rendering, selection and hit-testing. Corner geometry comes from one canonical source and the right side mirrors the left. The auxiliary overlay is non-interactive except for its separate track-center affordance.

Runtime gesture recognition remains intentionally stricter than editor selection. Corner recognizers retain first-candidate ownership, reject-until-lift behavior and their deliberate diagonal lane so a corner launch cannot steal a normal edge gesture after the gesture has begun.

Raw HID input stays available to recognition at full rate. WPF visualization is coalesced and page listeners only remain attached while the Touchpad page is visible, preventing rendering work from becoming an application-wide input tax.

## Audio lifecycle

Audio volume/microphone writes are debounced in the WPF page. Transient debounce timers and drag state are page-lifecycle state, not durable application state; they must be cleared when the Audio page becomes hidden so a navigation during a drag cannot suppress later refreshes or apply a stale microphone write off-page.

## Status, diagnostics and discovery

`HardwareServiceClient` caches a short last-known-good status snapshot and backs off after a confirmed offline service state. It publishes bounded status/operation events to the application instead of letting individual pages create competing service polling loops.

Diagnostics are local-first. Compatibility sharing remains explicit, sanitized and separate from hardware control. Raw touch coordinates, personal file content, usernames, serial numbers and arbitrary memory/log dumps are outside the intended upload schema.

## Release and compatibility boundaries

The installer is a small bootstrapper plus a separately versioned payload. CI exercises build/tests, real WPF shell smoke, visual snapshots, package size, installer/service lifecycle and the legacy updater fixture.

A cleanup is not permission to remove compatibility code blindly. Current-client dead code should be deleted; server-side legacy protocol handlers stay until the minimum supported installed client no longer needs them and the updater compatibility fixture is intentionally advanced.
