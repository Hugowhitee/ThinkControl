# Architecture

## Goals

ThinkControl must stay small, testable and safe while crossing a privilege boundary for low-level ThinkPad access. The architecture therefore separates user experience, orchestration, device matching and hardware I/O.

```text
ThinkControl.UI (normal user)
        |
        | Windows-authenticated named pipe
        v
ThinkControl.Service (privileged)
        |
        +-- ThinkControl.Core
        +-- ThinkControl.DeviceProfiles
        +-- ThinkControl.Hardware
              +-- Windows APIs
              +-- Lenovo services/APIs
              +-- PawnIO (when installed)
              +-- verified ThinkPad EC backend
```

## Project responsibilities

### ThinkControl.Core

Platform-neutral contracts and domain logic:

- device identity
- capability model
- fan states and telemetry DTOs
- profile definitions
- IPC request/response contracts
- coordinator state machines
- redaction rules that can be unit tested without hardware

Core must not call WMI, Win32, ACPI, PawnIO, Lenovo drivers or UI frameworks.

### ThinkControl.DeviceProfiles

Matches an observed device to bundled, versioned support metadata. It answers what has been verified, not how to perform an operation.

A profile may describe identifiers and verified capability facts. Remote metadata must never be able to introduce new hardware-write addresses or methods. New write support ships only in a normal application release.

### ThinkControl.Hardware

Contains Windows and Lenovo provider implementations behind Core contracts:

- Windows display/power/battery providers
- Lenovo Intelligent Thermal Solution integration
- Lenovo Power Management integration when a command is proven
- PawnIO-backed sensor/EC transport
- ThinkPad EC fan backend for validated devices
- conflict detection and platform diagnostics

Provider implementations may expose semantic operations such as `SetFanLevel(4)`. They must not expose arbitrary port/register writes to the UI or IPC layer.

### ThinkControl.Service

The only privileged process. It owns:

- provider discovery
- capability resolution
- hardware-write authorization
- fan-control state machine
- profile enforcement
- EC mutex ownership
- sleep/resume handling
- rollback to Lenovo Auto
- privileged diagnostics

Closing the UI does not stop a running hardware profile.

### ThinkControl.UI

Normal-user WPF process. It owns:

- tray icon
- compact popup
- Advanced window
- graphs and presentation
- theme/settings
- update UX
- support links

The UI never opens raw hardware devices directly.

## IPC boundary

Planned pipe name: `\\.\pipe\ThinkControl.Service.v1`.

Authentication should rely on Windows identities and a restrictive pipe ACL, not an application secret embedded in the executable. The service validates the connecting Windows token and protocol version before accepting commands.

The protocol must be:

- versioned
- length bounded
- cancellation aware
- semantic rather than raw-hardware oriented
- explicit about capability errors and conflicts

Examples of acceptable operations:

- `GetDeviceState`
- `SetPerformanceProfile(Quiet)`
- `SetFanState(Level4)`
- `ReturnFanToAuto`

Explicitly forbidden IPC operations:

- `WriteEc(register, value)`
- `WritePort(port, value)`
- arbitrary IOCTL passthrough

## Capability resolution

```text
SMBIOS / Windows identity
        +
ACPI/services/drivers present
        +
bundled verified device profile
        +
provider self-check
        +
conflict check
        =
CapabilitySet
```

The UI renders from `CapabilitySet`. Unsupported controls are omitted or clearly explained; no inert placeholder buttons.

Suggested states:

- Unavailable
- SafeReadOnly
- Verified
- BlockedByConflict
- ExperimentalReadOnly

A write-capable feature requires all of:

1. exact profile match
2. verified backend compiled into this release
3. provider health check passes
4. no conflicting controller owns the hardware
5. safety policy allows the requested state

## Performance coordination

One `PerformanceCoordinator` owns the combined user profile. It may coordinate:

- Windows Energy Mode
- Lenovo Intelligent Thermal policy through LITS when verified
- fan policy
- AC/battery-specific display behavior later

No independent timer should repeatedly fight Windows, LITS or another ThinkControl component. Writes occur on intentional state transitions or recovery, not on every telemetry tick.

## Fan engine

The fan engine is a state machine over semantic states:

```text
LenovoAuto
ManualLevel1
ManualLevel2
...
ManualLevel7
```

For the X9 backend the mapping to EC values lives only inside the validated backend/profile boundary.

The engine separates:

- temperature sampling
- desired-state calculation
- transition filtering (hysteresis/hold/down-delay)
- hardware commit
- RPM telemetry scheduling

This is critical because repeated EC writes or tachometer reads can themselves influence observed fan behavior.

## RPM scheduling

RPM is telemetry, not the control loop clock.

For the X9 manual path, the initial design is event-driven: after a fan-level change, wait for the fan to settle, take one RPM reading, then avoid continuous tachometer polling until a later state change or explicit refresh. Lenovo Auto may use a conservative periodic interval only after A/B validation shows the reads do not create an audible cadence.

## Sleep, resume and shutdown

Before service stop, shutdown, sleep or hibernate, a direct-control backend must attempt `LenovoAuto` and release EC resources.

On resume:

1. wait for platform/EC readiness
2. reopen providers
3. re-identify the device
4. re-run capability/conflict checks
5. read current state
6. reapply the selected profile only if still safe

No stale PawnIO/EC handle may be reused across sleep.

## Conflicts

Direct EC fan control is mutually exclusive with known tools such as FanControl ThinkPad plugins, TPFanControl/TPFanCtrl2 and NBFC-style EC controllers. When a conflict is detected, only the direct fan-control capability is blocked; safe display/performance functions may remain available.

## Installer and updates

The intended installer is a small bootstrapper rather than a self-contained 50+ MB application bundle. It should:

1. validate OS/architecture
2. install/detect .NET Desktop Runtime
3. fetch a release manifest from GitHub Releases
4. download the payload
5. verify SHA-256 (and later Authenticode/signed manifest)
6. install UI + service
7. optionally install verified PawnIO hardware access

The UI checks GitHub Releases at a low frequency. Updating is explicit. The same bootstrapper can perform the elevated service replacement so there is no permanent updater service.

## Logging and diagnostics

- UI logs: `%LocalAppData%\ThinkControl\Logs`
- service logs: `%ProgramData%\ThinkControl\Logs`
- bounded rolling files
- no serial/MAC/user name in normal logs
- diagnostics export shows a preview before submission
- service failures log the attempted semantic operation and provider, not secrets or arbitrary memory

## Dependency rule

```text
UI ------------> Core
Service -------> Core, Hardware, DeviceProfiles
Hardware ------> Core
DeviceProfiles -> Core
Core ----------> nothing project-specific
```

A later architecture test should enforce these references.
