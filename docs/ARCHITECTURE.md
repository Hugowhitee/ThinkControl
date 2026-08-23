# Architecture

This document describes the architecture used by ThinkControl `v0.1.0-alpha.2`.

ThinkControl separates the desktop interface from privileged hardware access.

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
            |-- Windows APIs and WMI
            |-- read-only sensor providers
            |-- Lenovo PM / EnergyDrv providers
            |-- verified X9 Lenovo thermal policy
            `-- verified X9 EC provider
```

## Projects

### ThinkControl.Core

Contains shared IPC, capability, telemetry, diagnostics and compatibility contracts. It does not depend on WPF or direct hardware I/O.

### ThinkControl.DeviceProfiles

Contains bundled profile metadata used to identify device families and exact verified models. Profiles select provider candidates; they do not authorize arbitrary low-level writes.

### ThinkControl.Hardware

Owns hardware implementations including CPU temperature readers, machine identity, PawnIO-backed ThinkPad EC transport, the verified X9 fan backend, Lenovo keyboard contracts, read-only Lenovo telemetry and the verified X9 Lenovo Intelligent Cooling policy bridge.

### ThinkControl.Service

The Windows service owns elevated/restricted hardware operations and provider lifetime. Its semantic operations include:

```text
Ping
GetStatus
SetFanLevel
ReturnFanToAuto
SetKeyboardBacklight
SetThermalMode
```

It never exposes generic EC, port or IOCTL passthrough.

`SetThermalMode` is gated by the service-side machine identity and only invokes the observed Lenovo Intelligent Cooling commands on verified X9 machine types `21Q6` and `21Q7`.

### ThinkControl.UI

The WPF application owns tray integration, Windows power mode, display controls, battery telemetry, keyboard effects, themes, startup behavior, update checks and local diagnostics.

The two window surfaces intentionally use different chrome:

- **Compact** is borderless, non-resizable, fixed above the notification area and has no draggable caption region.
- **Advanced** is a standard Windows application window with the native title bar, v3 app icon, minimize/maximize/restore/close buttons, taskbar presence, system menu and Windows 11 Snap Layouts.

Keyboard effects stay in the interactive user session because they depend on idle state, keyboard activity or local audio level. The service receives only semantic hardware-level requests.

## IPC boundary

The current pipe is `ThinkControl.Service.v1`. Access is restricted to appropriate local Windows identities and requests are versioned, length-bounded JSON messages.

Raw operations such as these are intentionally absent:

```text
WriteEc(register, value)
WritePort(port, value)
RawIoctl(...)
```

## Capability resolution

Windows-level features can use supported OS APIs without a Lenovo-specific profile. Low-level writes require a provider with its own authorization and validation rules.

Machine-type parsing prioritizes verified X9 codes `21Q6` and `21Q7` before generic token matching so a Lenovo SKU cannot be misclassified as another four-character token.

## X9 fan backend

The verified mapping is:

```text
Lenovo Auto  0x80
Level 1      0x01
...
Level 7      0x07
```

The backend blocks fan-off `0x00` and the unverified `0x40` family, suppresses duplicate writes, uses readback, shares the standard EC mutexes, polls RPM conservatively and attempts to return manual ownership to Lenovo Auto during normal service disposal.

RPM telemetry is not used as a high-frequency control-loop clock.

## X9 performance / thermal policy

Windows Quiet, Balanced and Performance map to Best efficiency, Balanced and Best performance first. After a successful Windows mode change, the verified X9 profile asynchronously coordinates Lenovo Intelligent Cooling through `LITSSvc`:

```text
AC Quiet        502
AC Balanced     503
AC Performance  504
DC Quiet        507
DC Balanced     508
DC Performance  509
```

The Lenovo contract writes one UInt32 command and reads one Int32 response. Because Lenovo has not published response-value semantics, ThinkControl treats a complete response as the protocol readback boundary rather than inventing a 0/nonzero success rule.

This is thermal-policy coordination, not direct fan PWM/RPM control.

## Keyboard control

The privileged keyboard path probes known Lenovo contracts before any write. `IBMPmDrv` and known `EnergyDrv` encodings are accepted only after a recognized read state, and writes are read back.

When the direct driver path is unavailable, ThinkControl can probe the installed Lenovo Vantage ThinkKeyboard component as a local fallback. Presence alone is not sufficient; the invocation must validate.

## Installation architecture

`v0.1.0-alpha.2` separates the web bootstrapper from the application payload:

```text
ThinkControl-Setup-<version>.exe
        |
        | HTTPS + pinned SHA-256
        v
ThinkControl-Payload-<version>.zip
        |-- ui/
        `-- service/
```

UI and service are framework-dependent `win-x64` applications, preventing duplicate .NET runtime copies. Setup downloads the pinned Microsoft .NET 10 Desktop Runtime only when missing and offers pinned PawnIO 2.2.0 only on verified X9 hardware.

For CI smoke testing, the same installer accepts a local payload override, verifies it against the exact compile-time SHA-256 and runs the same extraction/service lifecycle without requiring a temporary public release.

Uninstall removes the extracted `ui/` and `service/` directories and unregisters the service. Shared PawnIO and vendor software are not removed.

## Branding architecture

`assets/brand/v3` is the canonical branding source. The production app icon, tray icon and README wordmarks are byte-for-byte copies of their canonical v3 assets. The WPF `BrandMark` uses the exact traced 1536×1536 master geometry.

Packaging CI rejects branding drift and explicitly fails if the legacy hand-drawn 64×64 TC geometry reappears.

## Updates and release publication

`version.json` is the release version source. A release-ready commit on `main` creates the exact version tag, dispatches the package workflow and waits for all three release assets:

```text
ThinkControl-Setup-<version>.exe
ThinkControl-Payload-<version>.zip
SHA256SUMS.txt
```

A verified release marker is then written back to `.github/release-status.json`. ThinkControl itself does not install a permanent updater service.

## Diagnostics

Local diagnostics use bounded retention and an allowlisted schema. Data can be previewed, exported and deleted. Automatic private diagnostics upload is not enabled.

## Dependency direction

```text
UI             -> Core
Service        -> Core, Hardware, DeviceProfiles
Hardware       -> Core
DeviceProfiles -> Core
Core           -> no ThinkControl project
```

## Planned work

Later releases may add broader Lenovo model validation, additional provider states, autonomous fan curves with lifecycle recovery, private opt-in diagnostics submission, Authenticode signing and mature update/rollback handling.
