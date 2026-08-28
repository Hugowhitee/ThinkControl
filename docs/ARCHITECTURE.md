# Architecture

This document describes the architecture used by ThinkControl `v0.1.0-alpha.31` and the compatibility/safety boundaries future provider work must preserve.

ThinkControl is a general Windows laptop-control product. The current physically reviewed low-level reference is Lenovo/ThinkPad/X9, but the product/core/UI boundaries are capability-driven rather than vendor-specific.

```text
ThinkControl.UI
      |
      | versioned semantic named pipe
      v
ThinkControl.Service
      |
      +-- ThinkControl.Core
      +-- ThinkControl.DeviceProfiles
      `-- ThinkControl.Hardware
            |-- Windows-safe providers
            |-- generic read-only sensors
            |-- OEM provider contracts
            |-- family/model specializations
            `-- verified model-specific low-level backends
```

## Project boundaries

### ThinkControl.Core

Platform-independent contracts and behavior: capability/telemetry models, cooling policies/output mapping/calibration validation, diagnostics models, compatibility state and the pure touchpad gesture recognizer. Core does not depend on WPF or direct hardware I/O and must not contain OEM-specific register/IOCTL implementation details.

### ThinkControl.DeviceProfiles

Profile schema/resolution boundary. Profiles identify reasonable provider candidates through generic → OEM → family → exact-model scope. Profiles are data; they do not implement hardware access or independently authorize arbitrary writes.

### ThinkControl.Hardware

Provider implementations/probes. Hardware owns provider lifetime, concrete Windows/OEM interfaces, low-level transport, model gating, readback, bounded backoff and write allowlists.

### ThinkControl.Service

The Windows service owns privileged hardware providers and fan-write ownership. Its IPC listener is available independently from slow provider discovery. Runtime status is cached/refreshed by the service; a UI `GetStatus` request is not allowed to reconstruct the complete hardware stack synchronously.

Semantic IPC operations include, as applicable:

```text
Ping
GetStatus
RefreshProviders
RefreshSensorProviders
RefreshKeyboardProvider
SetFanLevel
SetFanPercent
ReturnFanToAuto
SetCoolingProfile
SetCoolingCurve
StartFanCharacterization
StopFanCharacterization
SetKeyboardBacklight
SetThermalMode
```

The service never exposes generic EC register writes, port I/O or raw IOCTL passthrough.

### ThinkControl.UI

The unprivileged WPF application owns Compact/Advanced windows, Windows-safe controls, battery/history, touchpad/haptics, user-session keyboard/audio behavior, updates and local diagnostics. Hardware-specific UI is capability-gated; it does not become a separate Lenovo/ASUS/Dell page hierarchy.

## Dependency direction

```text
UI             -> Core
Service        -> Core, Hardware, DeviceProfiles
Hardware       -> Core
DeviceProfiles -> Core
Core           -> no ThinkControl project
```

Provider expansion must preserve this direction. In particular, Core/UI must not import OEM transport details just to support another model.

## Device/provider hierarchy

```text
Windows generic capability
        ↓
OEM generic provider/profile
        ↓
product family
        ↓
exact model
```

A more specific profile can narrow/prioritize provider candidates but cannot weaken the safety contract inherited from a broader layer. Model-specific writes require the concrete provider to verify the identity/capability contract; profile metadata alone is not permission to write hardware.

## Service readiness versus provider readiness

ThinkControl deliberately models several states instead of one global “hardware online” boolean:

```text
service installed/running
→ IPC reachable
→ prerequisite/device accessible
→ provider initialized
→ telemetry capability available
→ reversible write/readback available
→ model-verified low-level write available
```

A missing sensor provider must not make a healthy Windows service look offline. Likewise, an installed driver is not proof that a low-level provider is writable.

Provider refreshes are explicit operations. Refreshing providers while ThinkControl owns fan output first requires a safe handoff to firmware Auto; characterization blocks provider recycling until it stops.

## Runtime status and performance

Normal UI refresh must avoid fixed-cadence expensive discovery.

- Windows power-source changes use OS events.
- Cheap/cached service and battery state may update at runtime cadence appropriate to the visible surface.
- WMI, full display capability discovery, `powercfg`-style inspection and provider reconstruction belong to startup, explicit refresh or bounded human-scale cache refreshes.
- Failed low-level/read-only providers use bounded retry/backoff rather than being reprobed by every UI refresh.
- Per-frame touch input/media actions use bounded/coalesced state owners rather than spawning unbounded OS calls.

## Window and startup model

ThinkControl has two main surfaces:

- **Compact** — fixed utility surface near the notification area.
- **Advanced** — normal resizable Windows application window with native caption/Snap/taskbar behavior and one shared page rail.

Window ownership is explicit:

1. `BootstrapWindow` is painted before synchronous startup preflight when the launch is interactive.
2. The loader stays above the destination until the real Compact/Advanced surface has completed a WPF render pass.
3. Compact ↔ Advanced switching has one app-level transition owner.
4. Transition feedback paints before expensive destination construction/navigation.
5. Destination state is established before the old surface disappears; exceptions restore a usable surface rather than leaving ThinkControl hidden/transparent.
6. Compact remains a topmost utility while visible, but owned ThinkControl popups are allowed to remain above their owner.
7. Whole-window opacity tricks are avoided where they can expose an uncomposed black WPF frame.

`ThinkControl.ShellSmoke` runs the actual Compact → Advanced → Compact lifecycle in CI. Static screenshots are not sufficient coverage for window activation/ownership regressions.

## Advanced UI composition

All Advanced pages share one left content rail, maximum readable width, typography scale, scrollbar/style system and capability-state language. Dynamic pages such as Touchpad/Audio/Sensors must enter the same layout contract rather than creating their own screen geometry.

Runtime visual-tree mutation is reserved for genuinely dynamic/capability-driven content. Release-specific one-off “polish” layers are not permanent architecture; stable rules belong to canonical feature/layout owners or shared XAML resources.

## Sensors and control temperature

Telemetry values must be real provider readings. Generic providers such as LibreHardwareMonitor/PawnIO may supply read-only sensors where supported. CPU/control-temperature labels reflect the provider identity; generic ACPI thermal zones are not silently renamed to CPU Package.

The cooling control temperature is a canonical thermal input, not an arbitrary average of unrelated SSD/battery zones. Safety decisions use the raw control temperature; normal curve decisions may use bounded smoothing.

## Cooling ownership

Cooling is independent from Windows power preference.

- **Auto** returns fan ownership to OEM/firmware.
- Supervised curves exist only while a verified writable provider and valid control-temperature source are available.
- Discrete providers expose discrete states; percentage targets are mapped to verified/calibrated output rather than presented as fake continuous PWM.
- Meaningful hot/upward transitions can happen promptly; downshifts use hysteresis/dwell to avoid audible state hunting.
- Loss of required telemetry/provider state and the high-temperature safety boundary hand ownership back to firmware.
- Service/app shutdown and temporary manual-test exit paths restore the previous safe owner/profile, with firmware Auto as the fallback.

The verified X9 provider-specific transport/EC evidence lives in [Lenovo provider research](research/lenovo-providers.md), [Hardware Safety](HARDWARE-SAFETY.md) and [X9 research](research/x9-15-gen1.md), not duplicated here.

## Fan calibration

X9 discrete-output calibration is a supervised service operation, not a generic UI calculation.

1. The service verifies the exact X9 provider plus fan write, control-temperature and real tachometer capabilities.
2. A new run uses a separate candidate set; the previous known-good calibration remains untouched.
3. Each EC state is settled and sampled several times.
4. Core `FanCalibrationPolicy` validates the complete seven-state evidence, including credible positive RPM and a plausible state-7 maximum.
5. Only a fully valid candidate replaces persisted calibration.
6. Cancel/failure/provider loss/thermal safety handoff returns to firmware Auto and cannot persist partial evidence.

The old subjective audibility marker is not part of the product mapping contract.

## Power and thermal policy

Windows power preference is the generic baseline. Visible terminology is **Efficiency / Balanced / Performance**.

Battery and AC preferences are stored independently. Compact/Home expose the battery preference as the quick control; the Advanced Performance page configures both sources.

A reviewed OEM thermal-policy provider may coordinate with the Windows preference for the exact verified scope. That semantic OEM integration is not reused merely because another laptop has similarly named performance modes.

## Keyboard

Keyboard backlight is a capability, not a Lenovo page. Hardware-level setters require the provider/readback contract appropriate to the backend. User-session Auto/effects and direct static changes share serialized write ownership so two paths cannot race and silently drop changes.

## Audio and Dolby

Windows output/microphone/volume stay in the user session. Dolby direct control is enabled only when the installed DAX path exposes a semantic operation ThinkControl can verify; private profile IDs are never guessed. Opening official Dolby UI remains a safe fallback when direct semantic control is unavailable.

## Battery

Always-on battery status uses Windows/platform-safe paths with bounded local history. Slow/static metadata is cached or read explicitly. Battery temperature is surfaced only from a credible battery-specific sensor/provider.

Windows remains the owner of screen/sleep/Night light/presence policy. ThinkControl links to supported Windows settings rather than duplicating undocumented CloudStore/registry state.

## Precision Touchpad and haptics

Interactive Precision Touchpad input lives in `ThinkControl.UI`; the pure recognizer/physical policies live in Core.

- Contact lifetime is separate from gesture lifetime; lift/re-touch and implausible physical jumps break visual trail segments.
- Precision edge actions use configurable physical edge bands.
- Optional top-corner launch lanes have one shared millimetre geometry for Core recognition, UI drawing and UI hit-testing.
- Track Previous/Next requires deliberate travel; optional center Play/Pause uses its own visible bounded center zone and low-travel hold/release policy.
- Media/brightness/volume writes are coalesced/bounded.
- Haptic controls reflect the capability actually reported by Windows/provider state; missing one haptic setting is not treated as proof the entire touchpad is absent.

## Diagnostics and privacy

Local diagnostics/crash history use bounded retention and allowlisted/redacted support schemas. Provider/profile matching does not require serial numbers, usernames, hostnames, personal paths, raw touch trails or unrelated user content.

Crash reporting remains durable locally; a report is only considered handled/reported after the relevant explicit workflow/acknowledgement. Optional future cloud diagnostics must reuse the same bounded semantic schema rather than upload raw application state.

## Installation and update architecture

```text
ThinkControl-Setup-<version>.exe
        |
        | HTTPS + SHA-256 verification
        v
ThinkControl-Payload-<version>.zip
        |-- ui/
        `-- service/
```

Device-specific prerequisites belong to in-app Hardware Setup and are offered only when the resolved provider requires them.

`version.json` is the build/release version source of truth. `releaseReady=false` prevents main-push promotion while a release PR is unfinished. Public prerelease tags/assets are immutable; changes made after a published version must advance to a new version rather than attempting to replace old release binaries.

Managed public release assets are exactly:

```text
ThinkControl-Setup-<version>.exe
ThinkControl-Payload-<version>.zip
SHA256SUMS.txt
ui-overview.png
```

The release path repeats build/tests, WPF shell smoke, visual QA, package construction and installer/service lifecycle validation before promotion.

## Repository validation

Normal CI validates repository hygiene before restore/build. The hygiene gate covers broken local Markdown references, tracked generated/runtime output, main-doc version drift and known release-specific UI partials that have been consolidated into permanent owners.

Visual QA renders the real WPF interface at minimum/normal/wide sizes plus light/dark and important active/unavailable/error states. Generated screenshots must still be inspected; successful PNG creation is not a visual correctness proof.
