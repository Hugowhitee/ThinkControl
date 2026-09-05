# ThinkControl architecture

This document describes the current architecture at **v0.1.0-alpha.37**. `docs/RELEASE_READINESS.md` is the persistent release/commercial handoff; this file explains runtime boundaries and intentional compatibility debt.

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

The ThinkPad X9 path currently prefers Lenovo-native fan semantics over direct EC writes. `LENOVO_OTHER_METHOD` target-RPM control is writable only when exact-X9 identity, per-channel VALID+GET+SET capability data, sane Lenovo fan constraints and at least two live fan channels all agree. Lenovo `EnergyDrv` is used only for its recovered read-only fan-speed query until a matching X9 write contract is proven. The seven-step ThinkPad EC writer remains a fallback/investigation path and is not promoted over proven native two-fan telemetry.

Fan ownership is explicit. ThinkControl records the provider/channels it actually takes over, returns those owned channels to Lenovo/OEM Auto on handoff/failure/disposal where supported, and does not infer ownership merely from reading an external manual-looking state. Native two-fan evidence is latched for the current service lifetime so a transient OEM telemetry miss cannot silently re-enable the known-inferior EC writer.

Repeated provider discovery is avoided where possible. The service keeps provider state; the UI consumes bounded status snapshots and uses targeted refresh operations for sensors, keyboard and full provider recovery.

## Cooling model

The current UI has one fan-curve model:

- named `FanCurveDefinition` profiles;
- `SetCoolingCurve` for current curve writes;
- `SetFanPercent` for deliberate manual output where supported;
- `ReturnFanToAuto` for firmware/OEM ownership;
- characterization operations for supported discrete-provider calibration workflows.

`FanSupervisor` is the sole owner of ThinkControl fan writes. A continuous OEM target-RPM provider receives percentages directly; a discrete EC provider maps those same semantic targets through its verified output-state mapping. Raw EC steps/calibration remain provider-specific diagnostics rather than a generic fan-control assumption.

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

Runtime gesture recognition remains intentionally stricter than editor selection. Enabled corner launches use the same visible guard → diagonal lane → rounded end-cap geometry as the recognizer. A corner candidate owns the contact from the first eligible frame and rejected corner input remains locked out until lift instead of falling through into a neighboring edge gesture. Optional reverse-close starts from the rounded inner cap and uses the same ownership/intent rules rather than a second gesture worker.

Reverse-close routes into the canonical application hide-to-tray transition. Compact uses the transition-owned synchronous hide before final shell-state verification; normal user-triggered tray toggling keeps its separate animation path. Visual-QA reverse fixtures are built from a clean non-live corner baseline so an outward trail cannot inherit an earlier inward contact segment.

Raw HID input stays available to recognition at full rate. WPF visualization is coalesced and page listeners only remain attached while the Touchpad page is visible, preventing rendering work from becoming an application-wide input tax.

## Audio lifecycle

Audio volume/microphone writes are debounced in the WPF page. Transient debounce timers and drag state are page-lifecycle state, not durable application state; they are cleared when the Audio page becomes hidden so navigation during a drag cannot suppress later refreshes or apply a stale microphone write off-page.

## Status, diagnostics and discovery

`HardwareServiceClient` caches a short last-known-good status snapshot and backs off after a confirmed offline service state. It publishes bounded status/operation events to the application instead of letting individual pages create competing service polling loops.

Diagnostics are local-first. Compatibility sharing remains explicit, sanitized and separate from hardware control. Raw touch coordinates, personal file content, usernames, serial numbers and arbitrary memory/log dumps are outside the intended upload schema.

Current fan-percent and fan-curve writes are classified as fan-control diagnostic operations rather than falling through to generic hardware events. Bounded X9 fan samples reuse already-observed service status and preserve provider/source distinctions without starting a second hardware polling loop. Removed current-client cooling wrappers are guarded by source tests so the service-only legacy compatibility surface cannot silently leak back into the modern UI client.

## Release and compatibility boundaries

The installer is a small bootstrapper plus a separately versioned payload. CI exercises build/tests, real WPF shell smoke, visual snapshots, package size, installer/service lifecycle and the legacy updater fixture.

PR CI and package workflows cancel superseded runs for the same PR/ref so stale branch commits do not waste Windows runners. Immutable/tag release packaging remains outside that cancellation behavior.

A cleanup is not permission to remove compatibility code blindly. Current-client dead code should be deleted; server-side legacy protocol handlers stay until the minimum supported installed client no longer needs them and the updater compatibility fixture is intentionally advanced.
