# Hardware safety policy

> **Applies to the current `v0.1.0-alpha.1` implementation and its future low-level providers.** This page distinguishes safeguards that are enforced in alpha.1 from requirements for later autonomous hardware control.

## Enforced in alpha.1

### 1. X9-specific writes are model-gated

The current privileged fan/keyboard controller allows X9-specific writes only when the detected manufacturer is Lenovo and the machine type is `21Q6` or `21Q7`.

Other laptops may use safe Windows-level features, but they do not inherit the X9 EC/register contract.

### 2. No raw-write UI or IPC

ThinkControl exposes semantic service operations only. The UI cannot request arbitrary:

- EC register writes;
- I/O port writes;
- ACPI method calls;
- IOCTL payloads.

The X9 register/driver knowledge remains inside compiled hardware-provider code.

### 3. X9 fan write allowlist

Current X9 fan contract:

- control register: `0x2F`;
- Lenovo BIOS/EC Auto: `0x80`;
- allowed manual levels: `0x01` through `0x07`;
- fan-off `0x00`: never written;
- unverified `0x40` override family: never written.

A percentage is never transformed into an arbitrary EC value.

### 4. Read-back verification

Manual fan writes and return-to-Auto are followed by EC read-back. A write that does not verify is treated as failure.

The current Lenovo keyboard backend similarly performs read-after-write verification for Off / Low / High.

### 5. Failed fan write falls back toward Lenovo Auto

If a manual fan write fails after ThinkControl has attempted to take direct control, the EC backend attempts to restore Lenovo Auto before surfacing the failure.

### 6. Normal service disposal returns manual fan ownership

If the ThinkControl service/controller is disposed normally while a manual X9 level is active, it attempts to return the fan controller to Lenovo Auto.

This covers normal service stop/replacement/uninstall paths that allow disposal code to run. It is **not** a guarantee for an abrupt process/kernel/power failure.

### 7. Duplicate writes are suppressed

An unchanged manual fan level is not continuously rewritten. UI telemetry refresh does not imply an EC write.

### 8. Tachometer access is deliberately conservative

Prior X9 testing correlated aggressive repeated tachometer reads with an audible periodic fan disturbance. Alpha.1 therefore polls RPM sparsely and keeps RPM separate from any future control-loop clock.

### 9. Shared EC mutexes

The X9 EC transport participates in the known ThinkPad/shared EC mutex conventions used by the proven research implementation:

```text
Access_Thinkpad_EC
Global\Access_EC
```

Failure to obtain a required lock is an error; ThinkControl does not bypass coordination by writing anyway.

### 10. No remote hardware-write instructions

A remote support catalog, issue, diagnostics response or downloaded metadata file cannot introduce new EC addresses or arbitrary write payloads. New low-level write support must ship as compiled provider code in a normal ThinkControl version.

### 11. Privilege minimization

The WPF UI runs as the signed-in user. The Windows service owns the privileged low-level operations. Display UX, graphs, update discovery, keyboard-effect activity logic and normal settings do not require the UI process to run as administrator.

### 12. Diagnostics exclude unique/private identifiers by design

Current compatibility diagnostics exclude serial number, asset tag, Windows username, hostname, MAC addresses, disk serials, typed text and audio samples. Exported data is built from an allowlisted/redacted schema.

See [Diagnostics and privacy](DIAGNOSTICS.md).

## Not yet guaranteed by alpha.1

The following are safety requirements for later autonomous/custom fan control, but the current manual-level alpha must **not** claim they are complete already.

### Third-party fan-controller conflict detection

The final autonomous controller should detect known competing direct EC fan tools and refuse direct ownership when a conflict is present.

Alpha.1 does not yet provide comprehensive conflict arbitration across FanControl plugins, TPFanControl/TPFanCtrl2, NBFC-style tools or unknown EC controllers.

### Full sleep/hibernate recovery lifecycle

A future direct-control profile should explicitly return Auto before sleep/hibernate, release low-level handles, reopen providers after resume and revalidate state before reapplying policy.

This sequence still requires implementation/physical validation before being described as guaranteed behavior.

### Ungraceful-crash guardian

Normal `Dispose`/service-stop cleanup cannot execute if the process is terminated abruptly. A future autonomous fan engine needs a stronger independent recovery mechanism/guardian before ThinkControl can guarantee recovery from an ungraceful service crash.

### Autonomous fan-curve safety

The planned custom curve engine will require:

- immediate upward cooling transitions;
- delayed downward transitions;
- hysteresis;
- minimum hold time;
- write deduplication;
- delayed Auto handoff where appropriate;
- provider conflict checks;
- sleep/resume revalidation;
- fail-safe recovery.

That engine is not part of `alpha.1`; current fan control is Lenovo Auto plus explicit manual levels `1–7`.

## Release rule

No future low-level feature should be documented as Verified merely because its code compiles or CI passes. Hardware confidence requires evidence from the actual device/provider combination.

For the first X9 alpha, see [Release Checklist](RELEASE-CHECKLIST.md) for the remaining physical validation pass.
