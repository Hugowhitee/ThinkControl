# Hardware safety

This document defines the non-negotiable safety boundary for privileged and low-level hardware control in ThinkControl. Product UI is capability-driven; a manufacturer/model name alone never grants a write capability.

## Core rules

### Capabilities, not brands, authorize UI

Keep these concepts separate:

- Windows-generic controls;
- OEM-native thermal policy;
- fan telemetry only;
- writable fan target;
- discrete EC fan states;
- percentage/PWM fan control;
- keyboard hardware control;
- haptic touchpad control;
- Precision Touchpad input only.

The UI may show a low-level control only when the active provider exposes the matching capability and its required validation state. Raw EC wording must not appear for a generic percentage/PWM provider, and Lenovo-specific wording must not appear merely because SMBIOS says Lenovo.

### Unknown hardware is read-only first

New/unknown devices may use documented Windows APIs and verified read-only provider probes. Risky firmware, EC, IOCTL, ACPI or OEM command writes require reviewed provider code, a recovery model and evidence appropriate to the risk.

One independent machine is not enough evidence to promote risky write behavior to broadly verified support. Conflicting evidence blocks promotion.

### No generic raw-write interface

The desktop UI and public IPC expose semantic operations only. They do not accept arbitrary EC registers, port I/O, ACPI methods, IOCTL payloads or OEM command IDs.

Remote device metadata and diagnostics can select or score known provider/profile candidates, but cannot inject executable low-level writes. New write contracts ship as reviewed application/provider code.

### Privilege stays in the service

The WPF app remains an ordinary user process. Privileged hardware ownership belongs to `ThinkControl.Service`; Windows-safe UI, touchpad input, media actions and normal update checks do not require an elevated desktop process.

## Fan ownership and recovery

- Firmware/OEM Auto is the safe ownership fallback.
- Manual tests are temporary and bounded.
- The first temporary fan test remembers the previous cooling profile.
- `End test`, timeout, leaving the Fans page, provider failure and normal shutdown restore prior ownership/profile where possible; firmware Auto is the fallback.
- Telemetry refresh never creates fan-control writes.
- Unchanged low-level fan states are not continuously rewritten.
- Missing control temperature/provider state returns supervised cooling to firmware ownership.
- Hot/safety handoff returns control to firmware rather than trapping the machine at a ThinkControl manual state.

See [Cooling design](COOLING-DESIGN.md) for the canonical curve/calibration lifecycle.

## Calibration

Calibration is characterization of an already verified writable backend; it is not hardware discovery by write-probing.

For the verified X9 discrete backend, a new calibration is accepted only after all seven allowed states have complete, plausible tachometer evidence. Collection occurs separately from persistence: cancellation, telemetry loss, safety failure or an inconsistent result leaves the previous known-good mapping untouched. Partial calibration is never promoted to verified mapping data.

## Verified X9 low-level boundary

The current physically reviewed low-level reference is ThinkPad X9-15 Gen 1 machine type `21Q6` / `21Q7`.

Verified fan-control state family:

```text
Lenovo/OEM Auto   0x80
Manual states     0x01 .. 0x07
Fan off           0x00  blocked
0x40 override     unverified and blocked
```

A UI percentage, where shown, is a normalized target mapped onto calibrated verified discrete states. It is never treated as an arbitrary EC value or proof that the hardware exposes continuous PWM.

The reviewed X9 Lenovo thermal-policy provider is a separate semantic policy path. It may coordinate the current power preference only after the exact X9 provider/identity checks pass. It must not be described as direct fan RPM/PWM control. Exact transport/command evidence belongs in [X9 research](research/x9-15-gen1.md).

### Readback and transport discipline

- Manual X9 fan writes and return-to-Auto use state readback.
- Supported keyboard hardware writes require their provider/readback contract.
- Low-level transport uses bounded waits, shared hardware locks and failure recovery rather than high-frequency blind polling.
- X9 tachometer access remains conservative because aggressive polling was observed to disturb fan behavior.

## Diagnostics and device learning

Diagnostics and licensing are independent concerns. Opting out of optional diagnostics must never disable a paid entitlement or safety behavior.

Automatic/future compatibility evidence must be allowlisted and deliberately redacted. Never upload usernames, hostnames, serial numbers, personal paths/content, browser content, keystrokes, touch coordinates/trails, memory dumps or arbitrary raw personal logs. See [Diagnostics and privacy](DIAGNOSTICS.md).

Device-learning states are conceptually `Observed → Candidate → Verified → Regression watch`. Read-only evidence may promote with a lower threshold than risky writes; conflicting reports prevent automatic promotion.

## Release rule

A green compiler, snapshot or hosted CI runner is not physical hardware verification. Hardware-write claims require appropriate real-device evidence in addition to software gates.

Before release promotion, follow [Release readiness](RELEASE_READINESS.md) and the current [Alpha testing](ALPHA-TESTING.md) checklist. Do not weaken a safety or backwards-compatibility contract merely to make the repository smaller.
