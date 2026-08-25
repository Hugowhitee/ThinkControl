# Architecture

This document describes the architecture used by ThinkControl `v0.1.0-alpha.15.1` and the compatibility boundaries new provider work must preserve.

ThinkControl is a general Windows laptop-control product. The current low-level reference implementation is Lenovo/ThinkPad/X9, but the core/UI architecture is intentionally vendor-neutral.

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
            |-- family/model provider specializations
            `-- verified model-specific low-level backends
```

## Projects

### ThinkControl.Core

Shared capability contracts, telemetry models, diagnostics, compatibility state and platform-independent behavior. Core does not depend on WPF or direct hardware I/O and must not contain OEM-specific register/IOCTL knowledge.

### ThinkControl.DeviceProfiles

Device-profile schema/resolution boundary. Profiles select reasonable provider candidates using generic → OEM → family → exact-model scope. Profiles are data; they do not implement hardware access or independently authorize arbitrary writes.

The current assembly remains intentionally small while profile composition/resolution rules are finalized. The on-disk hierarchy is defined in [`devices/README.md`](../devices/README.md).

### ThinkControl.Hardware

Hardware/provider implementations and probes. This layer owns provider lifecycle, validation/readback, backoff, low-level safety and write allowlists.

Current implementations include Windows/LHM/PawnIO sensor routes and Lenovo/X9 providers. Future OEM providers should fit behind the same capability contracts rather than adding vendor-specific product shells.

### ThinkControl.Service

The Windows service owns elevated hardware operations and provider lifetime. Its IPC listener is established before slow hardware discovery, and normal `GetStatus` requests consume a cached hardware snapshot rather than synchronously rebuilding providers. The status provider is demand-driven: idle telemetry sleeps until a client requests state, while active fan supervision owns its own safety cadence. Public operations are semantic, for example:

```text
Ping
GetStatus
RefreshProviders
SetFanLevel
ReturnFanToAuto
SetCoolingProfile
SetKeyboardBacklight
SetThermalMode
```

The service never exposes generic EC, port or IOCTL passthrough.

### ThinkControl.UI

The WPF application owns Compact, Advanced, Windows-safe controls, telemetry/history, touchpad/haptics, user-session effects, updates and local diagnostics.

The UI is capability-first. Fans, Sensors, Battery, Audio, Keyboard, Display and Touchpad do not become separate Lenovo/ASUS/Dell/etc. page families.

## Device/provider hierarchy

ThinkControl expands hardware support from broad to specific:

```text
Windows generic capability
        ↓
OEM generic provider/profile
        ↓
product family
        ↓
exact model
```

Example today:

```text
Windows
→ Lenovo
→ ThinkPad
→ X9-15 Gen 1
```

A future ASUS device should resolve through an ASUS branch, a Dell device through Dell, and so on. The product shell should not care which provider ultimately satisfies `FanTelemetry`, `FanControl`, `KeyboardBacklight` or another capability.

A more specific profile may add/narrow providers, but may not silently weaken safety inherited from a broader scope.

## Window model

ThinkControl has two main user surfaces.

- **Compact** is a fixed tray flyout for quick controls/status.
- **Advanced** is a normal resizable Windows window. Every page uses one shared left content rail and responsive width rule; wide-screen spare space grows on the right instead of recentering pages independently.

High-value overlays such as Notifications, Hardware Setup and telemetry detail use the same theme/resources and are included in deterministic visual QA.

## IPC boundary

The current pipe is `ThinkControl.Service.v1`. Requests are versioned and length-bounded, and the local pipe ACL restricts access to the intended Windows identities.

Service readiness and provider readiness remain separate. A repair is only reported successful after SCM reaches Running **and** the real ThinkControl `Ping` request succeeds through the named pipe.

Longer semantic hardware operations use bounded per-operation client timeouts so a valid readback/recovery path can finish without the UI declaring failure while the service is still completing the command. Fast `Ping` and cached `GetStatus` remain short.

These operations are intentionally absent:

```text
WriteEc(register, value)
WritePort(port, value)
RawIoctl(...)
```

## Capability/readiness model

ThinkControl does not represent hardware as one global supported/unsupported boolean. It distinguishes:

```text
service installed/running
→ IPC reachable
→ dependency/device accessible
→ provider initialized
→ telemetry available
→ reversible write/readback available
→ model-verified low-level write available
```

This prevents a missing sensor provider from making a healthy service look offline and prevents an installed driver from being treated as proof that a low-level provider actually works.

Unsupported controls remain visible where useful for context, but show an actionable unavailable/repair state.

## Sensors and control temperature

Sensor values must be real provider readings.

LibreHardwareMonitor/PawnIO is a generic Windows provider route where supported. CPU Package is labelled CPU Package only when the provider actually identifies it as such. Generic ACPI thermal zones are not silently promoted to CPU temperature.

The always-on sensor set is deliberately narrow. CPU/GPU/motherboard data is enabled where useful; storage, battery, network, controller, PSU and power-monitor providers are not continuously opened merely to populate more rows. Failed providers back off and recover through bounded retry/recycle or an explicit user retry.

Model-specific read-only temperature fallbacks may be exposed with honest source labels. A value may become a fan-control temperature only when its provider/safety policy explicitly permits that use.

## Current X9 provider

Direct X9 EC writes are restricted to machine types `21Q6` and `21Q7`.

Verified fan states:

```text
Lenovo Auto  0x80
Level 1      0x01
...
Level 7      0x07
```

The backend blocks fan-off `0x00` and the unverified `0x40` override family. EC transport probes modern ThinkPad ports `0x1604/0x1600` first with `0x66/0x62` fallback, under shared ThinkPad EC mutexes.

EC reads drain stale output before a new transaction, prefer a fresh Output Buffer Full indication, then use the same bounded Input Buffer Full-clear compatibility fallback used by LibreHardwareMonitor when firmware has completed the read without reliably asserting OBF. Writes still require explicit register/value acceptance followed by readback; this fallback does not widen the set of writable registers or states.

Fan telemetry prefers real LibreHardwareMonitor/PawnIO readings when that provider exposes them. The verified `0x84/0x85` EC tachometer remains a conservative fallback. Fan-control register state is validated during provider setup and explicit write/readback paths rather than continuously polled in the normal status loop.

The physical X9 chassis has two fans, but ThinkControl does not use an unverified selector write to fabricate separate per-fan telemetry.

Normal controller disposal attempts to return active manual fan ownership to Lenovo Auto. Provider refresh also refuses to tear down an actively owned fan path until ownership has been safely returned to firmware.

## Cooling profiles

Cooling ownership is separate from Windows power policy. Firmware/OEM Auto returns ownership to the platform. Silent, Normal and Cool use the supervised controller only when a writable provider and valid control-temperature input are both available.

The supervisor uses bounded smoothing, hysteresis/downshift dwell and discrete provider levels; it is not a second PWM implementation.

Other laptop families should supply their own `FanControl` provider semantics rather than inheriting X9 registers.

## Performance/thermal policy

Windows power mode is the generic baseline.

On the verified X9 profile, ThinkControl additionally coordinates the reviewed Lenovo LITSSvc semantic commands after applying the Windows mode. That provider is profile-specific and is never reused for another OEM/model merely because the UI uses the same Quiet/Balanced/Performance labels.

The always-on UI scheduler reads battery state through the Windows power manager and current refresh rate through direct display APIs. WMI/`powercfg` capability discovery remains cached or explicit instead of running every two seconds.

## Keyboard

Keyboard backlight is a capability, not a Lenovo page. The current Lenovo implementation probes established `IBMPmDrv`/`EnergyDrv` contracts and requires readback before writes. It can also reuse the installed Lenovo Vantage ThinkKeyboard component after validating Lenovo metadata, loading its adjacent contract dependency when present and marshalling reflected enum parameters using the target type. Failed probes are backed off; future OEM providers should implement the same capability contract behind the shared UI.

User-session effects (Auto/Breathing/Reactive/Audio) remain separate from the privileged hardware-level setter. Their hooks/timers are demand-driven. Explicit static Off/Low/High changes wait for an in-flight effect write rather than being discarded.

## Audio

Windows output volume stays in the user session. Dolby DAX direct control is vendor-neutral to the laptop brand and only uses semantic operations accepted/read back by the installed DAX build. Guessed numeric profile/IEQ mappings are not part of the architecture.

## Battery

Always-on battery status comes from the Windows power manager rather than fixed-cadence battery WMI. Charge/discharge history is local and bounded; slow/static metadata is cached or read explicitly. Session detail supports aligned percentage and power timelines.

OEM charge-threshold/control providers can be added later behind a battery capability without changing the main Battery page.

## Precision Touchpad gestures

Precision Touchpad Raw HID input is handled in `ThinkControl.UI` because it belongs to the interactive user session. The pure recognizer lives in Core. OS/media writes are coalesced and bounded so per-frame input cannot create unbounded async/provider work.

Media seeking accumulates a stable gesture target. Browser media can use a responsive bounded cadence while Spotify/Apple Music use a more conservative cadence to avoid flooding fragile GSMTC bridges.

## Installation

The installer is device-neutral:

```text
ThinkControl-Setup-<version>.exe
        |
        | HTTPS + pinned SHA-256
        v
ThinkControl-Payload-<version>.zip
        |-- ui/
        `-- service/
```

Device-specific prerequisites belong to in-app Hardware Setup and are offered only when the resolved provider actually requires them. A new OEM must not turn Setup into a giant bundle of every vendor driver.

## Visual QA

The real WPF interface is rendered in CI. Every Advanced page is checked at normal, minimum and wide sizes. High-value provider-unavailable states, Notifications, Hardware Setup, Audio and telemetry detail are rendered deterministically so CI runner hardware does not masquerade as product state.

Generated screenshots live in Actions artifacts and are inspected as visual QA. They are not published as managed release downloads.

## Release publication

`version.json` is the build and release version source. Normal builds read it directly; package CI may temporarily append a development build suffix through `APP_VERSION`, while a tagged build must match the exact `v<version>` tag.

A release-ready change merges to `main`, the exact commit is tagged and the tagged packaging workflow repeats build/visual QA/payload/installer/service-lifecycle checks before publication. Pull requests that touch runtime or installer code additionally run a Windows reliability gate covering clean install, service start, `Ping`, `GetStatus`, in-place reinstall/update and uninstall cleanup.

Published release assets are intentionally limited to:

```text
ThinkControl-Setup-<version>.exe
ThinkControl-Payload-<version>.zip
SHA256SUMS.txt
```

## Branch hygiene

Merged same-repository release/feature branches are disposable and cleaned automatically. Tags remain immutable release references. Ephemeral Actions artifacts are bounded separately and are never confused with immutable GitHub Release assets.

## Diagnostics

Local diagnostics use bounded retention and an allowlisted schema. Serial numbers, usernames, MAC addresses, disk identifiers and personal paths are not required for provider/profile matching.

## Dependency direction

```text
UI             -> Core
Service        -> Core, Hardware, DeviceProfiles
Hardware       -> Core
DeviceProfiles -> Core
Core           -> no ThinkControl project
```

Provider expansion must preserve this direction and keep OEM-specific implementation details out of Core/UI.
