# Architecture

This document describes the architecture used by ThinkControl `v0.1.0-alpha.23` and the compatibility/safety boundaries new provider work must preserve.

ThinkControl is a general Windows laptop-control product. The current low-level reference implementation is Lenovo/ThinkPad/X9, but the core and UI architecture are intentionally vendor-neutral.

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

### ThinkControl.Hardware

Hardware/provider implementations and probes. This layer owns provider lifecycle, validation/readback, backoff, low-level safety and write allowlists.

### ThinkControl.Service

The Windows service owns elevated hardware operations and provider lifetime. Its IPC listener is established before slow hardware discovery. Normal `GetStatus` requests consume a cached hardware snapshot rather than rebuilding providers synchronously.

Public operations are semantic, for example:

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

The WPF application owns Compact/full windows, Windows-safe controls, telemetry/history, touchpad/haptics, user-session effects, updates and local diagnostics.

The UI is capability-first. Fans, Sensors, Battery, Audio, Keyboard, Display and Touchpad do not become separate Lenovo/ASUS/Dell page families.

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

A more specific profile may add/narrow providers, but may not silently weaken safety inherited from a broader scope.

## Window and startup model

ThinkControl has two main user surfaces:

- **Compact** — fixed tray flyout for quick controls/status.
- **Full** — normal resizable Windows window with shared page rail and native Windows caption/Snap behavior.

Alpha.23 makes shell ownership explicit:

1. The small `BootstrapWindow` is shown from the earliest `Application.Startup` path, **before** synchronous WMI/SMBIOS preflight.
2. The loader remains painted until the configured Compact/full destination has completed a WPF render pass.
3. Compact ↔ Full switching has one app-level transition owner.
4. The destination surface is shown and rendered before the previous surface is hidden.
5. Whole-window opacity fades are not used for Compact/full switching because a transparent/uncomposed native WPF window can expose a black client area.
6. Transition exceptions restore the prior surface instead of letting ThinkControl disappear.

A dedicated `ThinkControl.ShellSmoke` tool runs the actual Compact → Full → Compact route repeatedly in release CI. Static screenshots are not considered sufficient coverage for shell transitions.

## IPC boundary

The current pipe is `ThinkControl.Service.v1`. Requests are versioned and length-bounded, and the local pipe ACL restricts access to intended Windows identities.

Service readiness and provider readiness remain separate. A repair is only reported successful after SCM reaches Running **and** the real ThinkControl `Ping` request succeeds.

These operations are intentionally absent:

```text
WriteEc(register, value)
WritePort(port, value)
RawIoctl(...)
```

## Capability/readiness model

ThinkControl does not represent hardware as one global supported/unsupported boolean:

```text
service installed/running
→ IPC reachable
→ dependency/device accessible
→ provider initialized
→ telemetry available
→ reversible write/readback available
→ model-verified low-level write available
```

This prevents a missing sensor provider from making a healthy service look offline and prevents an installed driver from being treated as proof that a low-level provider works.

## Sensors and control temperature

Sensor values must be real provider readings. LibreHardwareMonitor/PawnIO is a generic Windows provider route where supported. CPU Package is labelled CPU Package only when the provider actually identifies it as such. Generic ACPI thermal zones are not silently promoted to CPU temperature.

The always-on sensor set is deliberately narrow. Slow or failing providers use bounded retry/backoff rather than being hammered by every UI refresh.

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

Fan telemetry prefers real LibreHardwareMonitor/PawnIO readings. The verified `0x84/0x85` EC tachometer remains a conservative fallback. Normal controller disposal attempts to return active manual fan ownership to Lenovo Auto.

## Cooling profiles

Cooling ownership is separate from Windows power policy. Firmware/OEM Auto returns ownership to the platform. Supervised custom profiles run only when a writable provider and valid control-temperature input are both available.

The supervisor uses bounded smoothing, hysteresis and state dwell with sparse discrete writes. Ordinary threshold noise cannot immediately bounce between adjacent levels; safety-critical/hot upshifts remain prompt.

## Performance/thermal policy

Windows power mode is the generic baseline. User-facing UI terminology is **Efficiency / Balanced / Performance**.

On the verified X9 profile, ThinkControl additionally coordinates reviewed Lenovo LITSSvc semantic commands after applying the Windows mode. That provider is profile-specific and is never reused merely because another device has similarly named UI modes.

## Keyboard

Keyboard backlight is a capability, not a Lenovo page. Current Lenovo implementations probe established providers and require readback before writes. User-session effects remain separate from the privileged hardware-level setter.

## Audio and Dolby

Windows output/microphone control stays in the user session. Dolby DAX direct control only uses semantic operations accepted/read back by the installed DAX build. Compatible OEM DAX3 builds may use a bounded Dolby Access automation bridge for explicit requested actions. Guessed private IDs are not part of the architecture.

## Battery and Windows settings

Always-on battery status comes from Windows power APIs rather than fixed-cadence battery WMI. Charge/discharge history is local and bounded; slow/static metadata is cached or read explicitly.

Windows remains the owner of screen/sleep, Night light and presence-sensing policy. ThinkControl uses documented `ms-settings:` navigation for those surfaces rather than undocumented registry/CloudStore manipulation.

## Precision Touchpad gestures

Precision Touchpad input lives in `ThinkControl.UI` because it belongs to the interactive user session. The pure recognizer lives in Core. OS/media writes are coalesced and bounded.

The visualizer tracks contact lifetime separately from gesture state. A finger lift starts a new trail segment; physically implausible coordinate jumps also break a segment so the UI never draws a false line across empty space.

Transient gesture feedback is edge-local: direction while active, final value/action after release, then a bounded fade. New input replaces old feedback immediately.

## Installation

```text
ThinkControl-Setup-<version>.exe
        |
        | HTTPS + pinned SHA-256
        v
ThinkControl-Payload-<version>.zip
        |-- ui/
        `-- service/
```

Device-specific prerequisites belong to in-app Hardware Setup and are offered only when the resolved provider requires them.

## Visual QA and release publication

The real WPF interface is rendered in CI. Every full-view page is checked at normal, minimum and wide sizes, plus important unavailable/error states and overlays.

The complete visual matrix lives in the temporary Actions artifact. Public releases include one composed `ui-overview.png` instead of a long screenshot list.

`version.json` is the build/release version source. A release-ready change merges to `main`, the exact commit is tagged, and tagged packaging repeats build, tests, shell smoke, visual QA, payload/installer and service-lifecycle checks before publication.

Managed public release assets are:

```text
ThinkControl-Setup-<version>.exe
ThinkControl-Payload-<version>.zip
SHA256SUMS.txt
ui-overview.png
```

Tags are immutable release references. Merged same-repository feature/release branches are disposable.

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
