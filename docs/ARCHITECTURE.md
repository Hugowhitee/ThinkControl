# Architecture

This document describes the architecture used by ThinkControl `v0.1.0-alpha.1`.

ThinkControl separates the desktop interface from privileged hardware access.

```text
ThinkControl.UI
      |
      | versioned named pipe
      v
ThinkControl.Service
      |
      +-- ThinkControl.Core
      +-- ThinkControl.DeviceProfiles
      `-- ThinkControl.Hardware
            |-- Windows APIs and WMI
            |-- read-only sensor providers
            |-- Lenovo PM providers
            `-- verified device-specific providers
```

## Projects

### ThinkControl.Core

Contains shared contracts and types:

- IPC request and response models;
- capability and telemetry models;
- diagnostics contracts;
- compatibility states.

It does not depend on WPF or direct hardware I/O.

### ThinkControl.DeviceProfiles

Contains bundled profile metadata used to identify device families and exact verified models.

Profiles select provider candidates. They do not contain remotely executable EC register writes or arbitrary hardware commands.

### ThinkControl.Hardware

Contains hardware implementations such as:

- CPU temperature readers;
- machine identity checks;
- PawnIO-backed ThinkPad EC transport;
- the verified X9 fan backend;
- Lenovo PM keyboard backlight providers.

This project owns low-level device knowledge. The current alpha does not contain a universal writable ThinkPad fan backend.

### ThinkControl.Service

The Windows service owns operations that require elevated hardware access. Current responsibilities include:

- hardware-controller lifetime;
- authorization of X9-specific writes;
- semantic named-pipe operations;
- service privilege boundary;
- cleanup during normal service stop.

The protocol exposes operations such as:

```text
Ping
GetStatus
SetFanLevel
ReturnFanToAuto
SetKeyboardBacklight
```

It does not expose generic EC, port or IOCTL operations.

### ThinkControl.UI

The WPF application owns:

- notification-area integration and compact window;
- Advanced window and navigation;
- Windows power mode controls;
- display refresh and brightness controls;
- battery telemetry and time estimates;
- keyboard effects that depend on the interactive user session;
- themes, startup settings and update checks;
- local diagnostics and support actions.

Keyboard effects remain in the user session because they depend on idle state, keyboard activity or local audio level. The service receives only semantic hardware-level requests.

## IPC boundary

The current pipe name is defined by `ThinkControlProtocol.PipeName` as `ThinkControl.Service.v1`.

The service restricts pipe access to appropriate local Windows identities. Messages are versioned, length-bounded JSON requests.

Raw operations are intentionally absent:

```text
WriteEc(register, value)
WritePort(port, value)
RawIoctl(...)
```

This prevents the UI from becoming a generic privileged hardware-write client.

## Capability resolution

ThinkControl resolves capabilities independently.

Windows-level features can use supported operating-system APIs without a Lenovo-specific profile. Examples include display modes, brightness and battery telemetry.

Low-level writes require a provider with its own authorization and validation rules. The current X9 fan backend requires Lenovo machine type `21Q6` or `21Q7` and a working low-level transport.

See [Device Support](DEVICE-SUPPORT.md) for the current support model.

## X9 fan backend

The verified X9 mapping is:

```text
Lenovo Auto  0x80
Level 1      0x01
Level 2      0x02
...
Level 7      0x07
```

The backend enforces the following rules:

- fan-off `0x00` is not exposed;
- the unverified `0x40` family is never written;
- duplicate writes are suppressed;
- writes are verified with readback;
- shared EC mutexes are used;
- RPM polling is conservative;
- normal service disposal attempts to return manual control to Lenovo Auto.

RPM telemetry is not used as a high-frequency control-loop clock.

## Performance control

The current release uses Windows power mode APIs for Quiet, Balanced and Performance.

Lenovo thermal-policy integration has been researched, but it is not part of the completed alpha architecture.

## Sleep and resume

The service owns hardware-provider lifetime and performs cleanup during normal shutdown. More complete sleep, resume and ungraceful-crash recovery remains work for future autonomous hardware-control features.

## Installation

The current release uses a self-contained x64 Inno Setup package containing the UI, service and .NET runtime.

```text
Inno Setup
   |-- ThinkControl.UI
   |-- ThinkControl.Service
   |-- service registration and start
   `-- uninstall cleanup
```

PawnIO installation is not automated in the first alpha.

## Updates

`version.json` is the repository version source. Tagged releases build a versioned installer and SHA-256 checksum. ThinkControl does not install a permanent updater service.

## Diagnostics

Local diagnostics are stored under the user profile with bounded retention and an allowlisted schema. Data can be previewed, exported and deleted from the application.

Automatic private diagnostics upload is not enabled in the current release.

## Dependency direction

```text
UI             -> Core
Service        -> Core, Hardware, DeviceProfiles
Hardware       -> Core
DeviceProfiles -> Core
Core           -> no ThinkControl project
```

## Planned architecture work

Later releases may add:

- broader provider discovery across Lenovo families;
- additional per-capability validation states;
- Lenovo thermal-policy coordination;
- autonomous fan curves;
- stronger sleep/resume and crash recovery;
- private opt-in diagnostics submission;
- a smaller installer bootstrap path.

These are roadmap items, not active alpha behavior.
