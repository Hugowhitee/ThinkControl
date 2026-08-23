# Hardware safety

This policy applies to privileged and low-level hardware features in ThinkControl. The first alpha implements manual X9 fan control and Lenovo keyboard backlight writes; future autonomous control requires additional safeguards before release.

## Current requirements

### Device-specific writes are model-gated

The X9 EC backend is authorized only when the detected system is Lenovo machine type `21Q6` or `21Q7`.

Other laptops may use Windows-level features or their own validated providers. They do not inherit the X9 register map.

### No generic raw-write interface

The UI and IPC protocol expose semantic operations only. The desktop process cannot request arbitrary EC registers, I/O ports, ACPI methods or IOCTL payloads.

Low-level addresses and payloads remain inside compiled hardware-provider code.

### X9 fan allowlist

The verified X9 fan contract is:

```text
Control register   0x2F
Lenovo Auto        0x80
Manual levels      0x01 to 0x07
Fan off            0x00, blocked
Override family    0x40, unverified and blocked
```

ThinkControl never converts a percentage into an arbitrary EC value.

### Readback verification

Manual fan writes and return-to-Auto operations are checked against the EC state. A write that cannot be verified is treated as a failure.

Supported Lenovo keyboard writes are also read back after a change.

### Failed manual writes prefer Lenovo Auto

If a manual X9 fan write fails after ThinkControl has attempted direct control, the backend attempts to restore Lenovo Auto before reporting the failure.

### Normal shutdown releases manual fan control

During normal controller or service disposal, ThinkControl attempts to return an active manual X9 fan state to Lenovo Auto.

This covers normal service stop, replacement and uninstall paths where cleanup code is able to run. It does not guarantee recovery from sudden power loss, kernel failure or forced process termination.

### Duplicate writes are suppressed

An unchanged manual fan level is not continuously rewritten. Telemetry refresh must not produce fan-control writes.

### RPM polling is conservative

X9 testing found that aggressive tachometer access could affect audible fan behavior. RPM is therefore polled conservatively and remains separate from any future high-frequency control loop.

### Shared EC locks are respected

The X9 transport uses the established ThinkPad EC mutex names:

```text
Access_Thinkpad_EC
Global\Access_EC
```

If the required lock cannot be acquired, the operation fails rather than bypassing coordination.

### Remote metadata cannot define hardware writes

Remote support metadata, diagnostics responses and downloaded catalogs cannot introduce executable EC addresses, raw IOCTL payloads or arbitrary write instructions.

New low-level write support must ship as reviewed provider code in a normal ThinkControl release.

### Privilege is limited to the service

The WPF application runs as the signed-in user. The Windows service owns privileged hardware operations. Normal UI, update checks, graphs, display controls and user-session keyboard effects do not require an elevated desktop process.

### Diagnostics use an allowlist

Diagnostics exclude unique device identifiers and personal activity data. See [Diagnostics and Privacy](DIAGNOSTICS.md).

## Requirements for future autonomous fan control

The current alpha does not claim to solve every lifecycle case required by an autonomous fan curve.

Before a custom curve engine is enabled, it should include:

- conflict detection for known direct EC fan controllers;
- explicit sleep and hibernate handoff;
- provider reopening and state validation after resume;
- immediate upward cooling transitions;
- delayed downward transitions;
- hysteresis;
- minimum hold times;
- write deduplication;
- safe return to Lenovo Auto;
- recovery from an ungraceful service failure where practical.

A separate recovery mechanism may be required for failures in which the service cannot run its normal disposal path.

## Release rule

Compilation and CI are not evidence that a low-level provider is verified on hardware. A writable provider is marked verified only after the relevant device and operation have been tested on real hardware.

See [Release Checklist](RELEASE-CHECKLIST.md) and [Alpha Testing](ALPHA-TESTING.md) for the current X9 validation process.
