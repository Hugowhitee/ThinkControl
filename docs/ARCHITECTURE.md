# Architecture

> **Current architecture for `v0.1.0-alpha.1`.** Future architecture is called out explicitly as roadmap; this file does not present planned components as already running.

ThinkControl separates the normal-user WPF interface from the privileged operations required for low-level ThinkPad hardware access.

```text
ThinkControl.UI (normal user)
        |
        | semantic Windows named pipe
        v
ThinkControl.Service (LocalSystem Windows service)
        |
        +-- ThinkControl.Core
        +-- ThinkControl.DeviceProfiles
        +-- ThinkControl.Hardware
              +-- Windows APIs / WMI
              +-- LibreHardwareMonitor read-only sensors
              +-- Lenovo PM keyboard provider
              +-- PawnIO-backed X9 EC provider
```

## Projects in alpha.1

### ThinkControl.Core

Contains shared contracts and types, including:

- semantic IPC request/response DTOs;
- hardware capability snapshots;
- telemetry contracts;
- diagnostics contracts and compatibility-state types.

Core does not own WPF or direct device I/O.

### ThinkControl.DeviceProfiles

Contains bundled device-profile metadata. In alpha.1 the privileged X9 hardware implementation still performs an explicit `21Q6` / `21Q7` machine-type gate before X9-specific writes.

Profiles do not contain remotely executable EC/register instructions.

### ThinkControl.Hardware

Current implementations include:

- CPU temperature reader with LibreHardwareMonitor and safe ACPI fallback;
- X9 machine identity gate;
- PawnIO-backed ThinkPad EC transport;
- X9 fan state/RPM/manual-level backend;
- Lenovo PM keyboard backlight backend.

The current hardware assembly does **not** contain a universal ThinkPad fan controller or a generic writable Experimental-provider engine.

### ThinkControl.Service

The Windows service currently owns:

- the `X9HardwareController` lifetime;
- X9 machine-type authorization for low-level writes;
- semantic named-pipe operations;
- service-process privilege boundary;
- normal service-stop disposal and return-to-Lenovo-Auto behavior.

It currently exposes semantic operations such as:

```text
Ping
GetStatus
SetFanLevel
ReturnFanToAuto
SetKeyboardBacklight
```

It does not expose raw EC, port or arbitrary IOCTL commands.

### ThinkControl.UI

The WPF process owns:

- tray icon and compact popup;
- Advanced window/navigation;
- Windows power mode control;
- display refresh/brightness/adaptive-brightness controls;
- battery watts/Wh/health/ETA calculation;
- keyboard Auto/Breathing/Reactive/Audio policies;
- settings/themes/startup;
- update checks;
- local compatibility diagnostics;
- user-facing status and support links.

Keyboard effect logic intentionally stays in the signed-in user session because it needs interactive idle/input/audio state. The privileged service receives only semantic Off/Low/High level requests.

## IPC boundary

The implemented protocol is versioned and uses the pipe name defined by `ThinkControlProtocol.PipeName` (`ThinkControl.Service.v1`).

The service creates a restrictive Windows pipe ACL for LocalSystem, administrators and local interactive users. Requests are length-bounded JSON messages and are validated against the protocol version.

Allowed operations are semantic. Explicitly unavailable operations include:

```text
WriteEc(register, value)
WritePort(port, value)
RawIoctl(...)
```

This keeps low-level address knowledge inside compiled hardware providers.

## Current capability resolution

In alpha.1 there are two practical classes of capability:

### Windows/read-only capabilities

The UI can use Windows-supported or safe read-only providers when they are present, for example display modes, brightness, battery telemetry and temperature sensors.

### X9 low-level capabilities

Fan and Lenovo keyboard writes are currently enabled only when the hardware layer recognizes Lenovo X9 machine type `21Q6` or `21Q7` and the required provider opens successfully.

The core contains richer compatibility-state types (`Verified`, `Experimental`, `Not validated`) for future expansion, but alpha.1 does **not** automatically turn an unknown ThinkPad into a writable Experimental EC device.

See [Device Support](DEVICE-SUPPORT.md).

## X9 fan backend

The current fan backend exposes only hardware states that have an explicit X9 mapping:

```text
Lenovo Auto   -> 0x80
Level 1       -> 0x01
...
Level 7       -> 0x07
```

Safety invariants in the current implementation:

- fan-off `0x00` is never offered as a write;
- `0x40` override-family states are never written;
- manual writes are deduplicated;
- writes are verified with read-back;
- EC access uses shared ThinkPad/EC mutexes;
- tachometer polling is deliberately conservative;
- normal service disposal attempts to return manual fan ownership to Lenovo Auto.

RPM is telemetry, not a control-loop clock.

## What the current fan backend is not

Alpha.1 does not yet contain the autonomous custom fan-curve controller originally designed for later versions. The following are roadmap items:

- temperature-to-level curve evaluation;
- immediate-up / delayed-down logic;
- hysteresis;
- minimum hold timers;
- full third-party EC-controller conflict arbitration;
- a separate guardian capable of recovering from an ungraceful service process crash.

Manual levels and Lenovo Auto are real alpha.1 features; the autonomous curve engine is not.

## Performance coordination

Alpha.1 uses the supported Windows user-configured AC/DC power mode API for Quiet / Balanced / Performance.

Lenovo Intelligent Thermal Solution (LITS) coordination has been researched, but a production `PerformanceCoordinator` that combines Windows mode, LITS and a future custom fan policy is still roadmap work.

## Sleep / resume

The service is structured so provider state is owned by one hardware-controller lifetime and normal service shutdown disposes that state safely.

A complete sleep/resume reinitialization and recovery sequence still requires physical validation and additional lifecycle work before it should be claimed as complete. See [Release Checklist](RELEASE-CHECKLIST.md).

## Installer architecture in alpha.1

The current installer is **not** the earlier proposed 1–3 MB bootstrapper. The first alpha intentionally uses a self-contained x64 package for reliability:

```text
Inno Setup
   |
   +-- self-contained ThinkControl.UI
   +-- self-contained ThinkControl.Service
   +-- service registration / start
   +-- uninstall service cleanup
```

The resulting development installer is roughly tens of megabytes because it carries the .NET runtime. CI performs a real silent install/service-start/uninstall smoke test.

A smaller runtime/bootstrap distribution can be revisited later once the hardware/product path is stable.

PawnIO prerequisite installation is not yet automated in alpha.1.

## Updates and releases

`version.json` is the release source of truth. The release-ready workflow creates the exact `v<version>` tag from `main`, and that tag triggers the tested packaging workflow.

Tagged releases produce a versioned installer and SHA-256 checksum. ThinkControl does not run a permanent updater service.

## Diagnostics

The current UI implements bounded local compatibility diagnostics under the user's local app-data directory. Data is allowlisted/redacted and can be previewed, exported and deleted.

The planned private upload endpoint is not deployed yet; network submission remains disabled and no GitHub PAT is embedded in the application.

## Dependency direction

```text
UI ------------> Core
Service -------> Core, Hardware, DeviceProfiles
Hardware ------> Core
DeviceProfiles -> Core
Core ----------> no ThinkControl project
```

## Roadmap architecture

Later releases may add, only after validation:

- a generic provider registry with safe read-only discovery on more ThinkPads;
- per-capability Experimental promotion;
- Lenovo LITS coordination;
- autonomous custom fan-curve state machine;
- stronger sleep/resume and ungraceful-crash recovery;
- private opt-in diagnostics submission;
- a smaller bootstrap-style installer.

Those are target architecture, not hidden alpha.1 behavior.
