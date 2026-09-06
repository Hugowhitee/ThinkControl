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

A verified firmware thermal-policy capability is **not** the same capability as direct fan output. It may expose semantic profiles such as Quiet/Balanced/Performance while leaving manual percentages, custom RPM curves and raw EC states unavailable.

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
- Firmware thermal-policy profiles keep OEM firmware in the fan-control loop; ThinkControl changes only a reviewed semantic policy state.
- Manual direct-output tests are temporary and bounded.
- The first temporary fan test remembers the previous cooling profile.
- `End test`, timeout, leaving the Fans page, provider failure and normal shutdown restore prior ownership/profile where possible; firmware Auto is the fallback.
- Telemetry refresh never creates fan-control writes.
- Unchanged low-level fan states are not continuously rewritten.
- Missing control temperature/provider state returns supervised direct cooling to firmware ownership.
- Hot/safety handoff returns control to firmware rather than trapping the machine at a ThinkControl manual state.

See [Cooling design](COOLING-DESIGN.md) for the canonical curve/calibration lifecycle.

## Calibration

Calibration is characterization of an already verified writable **direct-output** backend; it is not hardware discovery by write-probing and it is not required for a semantic OEM firmware-policy backend.

For a verified discrete provider, a new calibration is accepted only after every allowed state has complete, plausible tachometer evidence. Collection occurs separately from persistence: cancellation, telemetry loss, safety failure or an inconsistent result leaves the previous known-good mapping untouched. Partial calibration is never promoted to verified mapping data.

## Verified X9 low-level boundary

The current physically reviewed low-level reference is ThinkPad X9-15 Gen 1 machine type `21Q6` / `21Q7`.

### Normal product cooling path

Alpha.39 keeps the user-facing **Auto / Quiet / Balanced / Max cooling** profiles functional without re-authorizing a direct writer that failed physical testing. The built-ins use the already reviewed X9 `LITSSvc`/ThinkSmartSense semantic thermal-policy path:

```text
Quiet        -> Lenovo Quiet policy
Balanced     -> Lenovo Balanced policy
Max cooling  -> Lenovo Performance cooling policy
Auto         -> clear cooling override and restore the current power-policy baseline
```

The service still performs exact-X9 identity and AC/DC checks before translating the semantic policy to the existing allowlisted LITSSvc command. The desktop UI never supplies a raw Lenovo command ID. While a cooling override is active, Windows power preference remains a separate setting: changes update the stored restore baseline instead of competing with the selected cooling profile.

This path deliberately leaves Lenovo firmware responsible for the closed-loop fan algorithm. It must not be described as direct RPM, PWM, percentage or EC control.

### Direct fan-write boundary

The alpha.38 Lenovo Other Mode `fanX_target` writer is **physically rejected for product control** on the X9. A fixed requested target repeatedly sped up/slowed down, and the nominal maximum target remained below naturally hot Lenovo Auto. Firmware metadata and successful `fanX_input` telemetry therefore do not authorize target writes.

Read-side native dual-fan telemetry remains useful. The explicit target-`0` Auto cleanup/reassertion path is retained only to release stale previously owned target state. Once native two-fan evidence has been established, a transient native telemetry miss must not silently re-authorize the known-inferior EC direct writer.

The classic ThinkPad EC family remains research/diagnostic evidence rather than the normal alpha.39 X9 cooling backend:

```text
Lenovo/OEM Auto   0x80
Manual states     0x01 .. 0x07   provider-specific only
Fan off           0x00           blocked
0x40 override     unverified and blocked
```

A percentage may be shown only if an active physically accepted direct provider defines that semantic mapping. Step 7 is not proof of the laptop's physical maximum and must never be relabelled as such.

### Readback and transport discipline

- Direct manual writes and return-to-Auto require the matching provider's readback/recovery contract.
- Supported keyboard hardware writes require their provider/readback contract.
- Low-level transport uses bounded waits, shared hardware locks and failure recovery rather than high-frequency blind polling.
- X9 tachometer access remains conservative because aggressive EC polling was observed to disturb fan behavior.
- A firmware-policy profile is sent once as a semantic policy transition; ThinkControl does not fight Lenovo's closed loop by continuously rewriting fixed fan targets.

## Diagnostics and device learning

Diagnostics and licensing are independent concerns. Opting out of optional diagnostics must never disable a paid entitlement or safety behavior.

Automatic/future compatibility evidence must be allowlisted and deliberately redacted. Never upload usernames, hostnames, serial numbers, personal paths/content, browser content, keystrokes, touch coordinates/trails, memory dumps or arbitrary raw personal logs. See [Diagnostics and privacy](DIAGNOSTICS.md).

Device-learning states are conceptually `Observed → Candidate → Verified → Regression watch`. Read-only evidence may promote with a lower threshold than risky writes; conflicting reports prevent automatic promotion.

## Release rule

A green compiler, snapshot or hosted CI runner is not physical hardware verification. Hardware-write claims require appropriate real-device evidence in addition to software gates.

For alpha.39 specifically, release validation must distinguish the two X9 fan evidence classes: the direct target writer remains rejected, while the built-in firmware-policy profiles require real-device confirmation that Quiet/Balanced/Max preserve smooth Lenovo-managed behavior and that Auto restores the current power-policy baseline.

Before release promotion, follow [Release readiness](RELEASE_READINESS.md) and the current [Alpha testing](ALPHA-TESTING.md) checklist. Do not weaken a safety or backwards-compatibility contract merely to make the repository smaller.
