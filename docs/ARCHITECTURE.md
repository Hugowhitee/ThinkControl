# Architecture

This document describes the architecture used by ThinkControl `v0.1.0-alpha.3`.

ThinkControl separates the signed-in desktop application from privileged hardware access.

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
            |-- Lenovo provider contracts
            |-- verified X9 thermal policy
            `-- verified X9 EC provider
```

## Projects

### ThinkControl.Core

Shared contracts, telemetry models, diagnostics, compatibility state and platform-independent gesture recognition. Core does not depend on WPF or direct hardware I/O.

### ThinkControl.DeviceProfiles

Device-family and verified-model metadata. Profiles select provider candidates and verified low-level behavior. A profile does not authorize arbitrary hardware writes.

### ThinkControl.Hardware

Hardware implementations and provider probes. This includes machine identity, CPU temperature readers, Lenovo telemetry, keyboard contracts, the X9 thermal-policy bridge and the verified X9 EC backend.

### ThinkControl.Service

The Windows service owns elevated hardware operations and provider lifetime. Its public operations are semantic:

```text
Ping
GetStatus
SetFanLevel
ReturnFanToAuto
SetKeyboardBacklight
SetThermalMode
```

The service does not expose generic EC, port or IOCTL passthrough.

### ThinkControl.UI

The WPF application owns the tray interface, Advanced window, Windows power and display controls, battery telemetry, keyboard effects, Precision Touchpad gestures, haptic settings, startup behavior, update checks and local diagnostics.

Interactive input stays in the signed-in user session. The service receives only semantic hardware requests.

## Window model

ThinkControl has two user surfaces.

- **Compact** is a fixed tray flyout for daily controls.
- **Advanced** is a normal resizable Windows window with native caption controls, Snap Layouts and responsive content widths.

Both use the shared ThinkControl wordmark and design resources.

## IPC boundary

The current pipe is `ThinkControl.Service.v1`. Requests are versioned and length-bounded. Access is restricted to appropriate local Windows identities.

These operations are intentionally absent:

```text
WriteEc(register, value)
WritePort(port, value)
RawIoctl(...)
```

## Capability resolution

ThinkControl resolves capabilities independently instead of treating a laptop as either fully supported or unsupported.

Windows-level features can work without a Lenovo-specific profile. Lenovo providers activate only after their own probes succeed. Direct EC writes require an exact verified device profile.

Machine-type parsing prioritizes the verified X9 codes `21Q6` and `21Q7` before generic token matching.

Unsupported controls stay visible when useful for context, but remain disabled and report why the provider is unavailable.

## ThinkPad X9-15 Gen 1

Direct EC fan writes are restricted to machine types `21Q6` and `21Q7`.

Verified fan values are:

```text
Lenovo Auto  0x80
Level 1      0x01
...
Level 7      0x07
```

The backend blocks fan-off `0x00` and the unverified `0x40` family. It uses readback, suppresses duplicate writes, shares the standard EC mutexes and polls RPM conservatively.

Normal controller disposal attempts to return an active manual fan state to Lenovo Auto.

## X9 performance policy

Windows Quiet, Balanced and Performance map to the corresponding Windows power mode first. On the verified X9 profile, ThinkControl can then coordinate Lenovo Intelligent Cooling through `LITSSvc`.

```text
AC Quiet        502
AC Balanced     503
AC Performance  504
DC Quiet        507
DC Balanced     508
DC Performance  509
```

This is thermal-policy coordination. It is not direct PWM or RPM control.

## Keyboard control

The privileged keyboard path probes known Lenovo contracts before writing. Recognized states are read back after a change.

When a direct driver path is unavailable, ThinkControl may probe installed Lenovo components as a local fallback. Presence alone is not considered support.

## Precision Touchpad gestures

Precision Touchpad Raw HID input is handled in `ThinkControl.UI` because it belongs to the interactive user session.

The pure recognizer lives in Core and is replay-tested without WPF or hardware dependencies. Windows-specific HID parsing, cursor capture, media actions and haptic settings stay in the UI platform layer.

The default gesture preset is left Volume, right Brightness and top relative Media Seek. A second contact cancels an edge gesture. Precision Touchpad Confidence is used when the device reports it.

## Installation

The installer is device-neutral.

```text
ThinkControl-Setup-<version>.exe
        |
        | HTTPS + pinned SHA-256
        v
ThinkControl-Payload-<version>.zip
        |-- ui/
        `-- service/
```

UI and service are framework-dependent `win-x64` applications. This avoids embedding duplicate .NET runtimes.

Setup installs ThinkControl and `ThinkControlService`, and installs the Microsoft .NET 10 Desktop Runtime only when it is missing. Device-specific low-level prerequisites are handled later by the in-app Hardware Setup flow and are only offered when the detected verified profile requires them.

Packaging CI enforces size budgets for the bootstrapper, compressed payload and installed application payload.

## Branding

`assets/brand/v3` is the canonical branding source. Packaging CI verifies the production app icon, tray icon and wordmark alignment against those assets.

The special C geometry is shared across the product. The `ontrol` suffix uses the approved optical spacing in both the SVG and WPF wordmark.

## Release publication

`version.json` is the release version source.

A release-ready change is merged to `main`, normal CI validates that exact commit, and `publish-release.yml` creates or resumes the matching version tag only after CI succeeds. The tagged `release.yml` workflow builds and publishes:

```text
ThinkControl-Setup-<version>.exe
ThinkControl-Payload-<version>.zip
SHA256SUMS.txt
```

GitHub Releases and those published assets are the release source of truth. Release verification does not write status commits back to `main`.

## Branch hygiene

Merged feature branches are deleted automatically. The hygiene workflow also removes abandoned branches that contain no commits not already present in `main`.

Tags are not branches and are retained as immutable release references.

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
