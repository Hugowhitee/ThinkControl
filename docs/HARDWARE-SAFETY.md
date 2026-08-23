# Hardware safety

This policy applies to privileged and low-level hardware features in ThinkControl. `v0.1.0-alpha.2` implements manual X9 fan control, Lenovo keyboard backlight writes and X9-gated Lenovo Intelligent Cooling policy coordination. Future autonomous control requires additional safeguards before release.

## Current requirements

### Device-specific writes are model-gated

The X9 EC backend and X9 Lenovo Intelligent Cooling command set are authorized only when the detected Lenovo machine type is `21Q6` or `21Q7`.

Other laptops may use Windows-level features or their own validated providers. They do not inherit the X9 register map or the X9 `502/503/504/507/508/509` thermal-policy commands.

### No generic raw-write interface

The UI and IPC expose semantic operations only. The desktop process cannot request arbitrary EC registers, I/O ports, ACPI methods, raw named-pipe command IDs or IOCTL payloads.

Low-level addresses/command IDs remain inside compiled, model-gated providers.

### X9 fan allowlist

```text
Control register   0x2F
Lenovo Auto        0x80
Manual levels      0x01 to 0x07
Fan off            0x00, blocked
Override family    0x40, unverified and blocked
```

ThinkControl never converts a percentage into an arbitrary EC value.

### X9 Lenovo thermal-policy allowlist

The observed Lenovo Intelligent Cooling contract is limited to:

```text
AC Quiet        502
AC Balanced     503
AC Performance  504
DC Quiet        507
DC Balanced     508
DC Performance  509
```

The service chooses AC/DC from Windows power-source state and accepts only the semantic `Quiet`, `Balanced` or `Performance` operation. The UI cannot submit a raw command ID.

This provider is explicitly a **thermal-policy** interface. It must not be described or used as arbitrary fan RPM/PWM control.

### Readback / response boundaries

Manual fan writes and return-to-Auto operations are checked against EC state. Supported Lenovo keyboard writes are read back after a change.

For the LITSSvc Intelligent Cooling pipe, the observed protocol writes one UInt32 and reads one Int32 response. Lenovo has not published the meaning of that Int32, so ThinkControl treats receipt of the complete response as the protocol boundary and does not invent a `0 == success` or similar rule.

### Failed manual fan writes prefer Lenovo Auto

If a manual X9 fan write fails after ThinkControl has attempted direct control, the backend attempts to restore Lenovo Auto before reporting failure.

### Normal shutdown releases manual fan control

During normal controller or service disposal, ThinkControl attempts to return an active manual X9 fan state to Lenovo Auto. This covers normal service stop, replacement and uninstall paths where cleanup code can run; it cannot guarantee recovery from sudden power loss or kernel failure.

### Duplicate writes are suppressed

An unchanged manual fan level is not continuously rewritten. Telemetry refresh must not produce fan-control writes.

### RPM polling is conservative

X9 testing found that aggressive tachometer access could affect audible fan behavior. RPM is therefore polled conservatively and remains separate from any future high-frequency control loop.

### Shared EC locks are respected

The X9 transport uses established ThinkPad EC mutexes:

```text
Access_Thinkpad_EC
Global\Access_EC
```

If the required lock cannot be acquired, the operation fails rather than bypassing coordination.

### Remote metadata cannot define hardware writes

Remote support metadata, diagnostics responses and downloaded catalogs cannot introduce executable EC addresses, raw LITS command IDs, raw IOCTL payloads or arbitrary write instructions. New low-level support must ship as reviewed provider code in a normal ThinkControl release.

### Privilege is limited to the service

The WPF application runs as the signed-in user. The Windows service owns restricted hardware operations. Normal UI, update checks, display controls and user-session keyboard effects do not require an elevated desktop process.

### Diagnostics use an allowlist

Diagnostics exclude unique device identifiers and personal activity data. See [Diagnostics and Privacy](DIAGNOSTICS.md).

## Requirements for future autonomous fan control

Before a custom curve engine is enabled, it should include conflict detection, sleep/hibernate handoff, provider reopening after resume, immediate upward cooling transitions, delayed downward transitions, hysteresis, minimum hold times, write deduplication, safe return to Lenovo Auto and practical recovery from ungraceful service failure.

## Release rule

Compilation and CI are not evidence that a low-level provider is physically verified. Writable/provider behavior still needs validation on the actual device before stronger hardware claims are made.

See [Release Checklist](RELEASE-CHECKLIST.md) and [Alpha Testing](ALPHA-TESTING.md).
