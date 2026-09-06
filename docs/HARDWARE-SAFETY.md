# Hardware safety

This document defines the non-negotiable safety boundary for privileged and low-level hardware control in ThinkControl. Product UI is capability-driven; a manufacturer/model name alone never grants a write capability.

## Core rules

### Capabilities, not brands, authorize UI

Keep these concepts separate:

- Windows-generic controls;
- OEM-native thermal policy;
- OEM global full-speed semantic;
- fan telemetry only;
- writable per-fan target;
- discrete EC fan states;
- percentage/PWM fan control;
- keyboard hardware control;
- haptic touchpad control;
- Precision Touchpad input only.

The UI may show a low-level control only when the active provider exposes the matching capability and required validation state. Raw EC wording must not appear for a generic provider, and Lenovo-specific wording must not appear merely because SMBIOS says Lenovo.

A verified firmware thermal-policy capability is **not** the same capability as direct fan output. Likewise, a verified boolean full-speed semantic is not permission to expose arbitrary feature IDs, manual percentages, custom RPM curves or raw EC states.

### Unknown hardware is read-only first

New/unknown devices may use documented Windows APIs and verified read-only provider probes. Firmware, EC, IOCTL, ACPI or OEM command writes require reviewed provider code, a recovery model and evidence appropriate to the risk.

One independent machine is not enough evidence to promote risky write behavior broadly. Conflicting evidence blocks promotion.

### No generic raw-write interface

The desktop UI and public IPC expose semantic operations only. They do not accept arbitrary EC registers, port I/O, ACPI methods, IOCTL payloads, Lenovo feature IDs or OEM command IDs.

Remote metadata/diagnostics can select or score known provider/profile candidates but cannot inject executable low-level writes. New write contracts ship as reviewed provider code.

### Privilege stays in the service

The WPF app remains an ordinary user process. Privileged hardware ownership belongs to `ThinkControl.Service`; Windows-safe UI, touchpad input, media actions and normal update checks do not require an elevated desktop process.

## Fan ownership and recovery

- Firmware/OEM Auto is the ownership fallback.
- Firmware thermal-policy profiles leave OEM firmware in the closed-loop fan controller.
- A narrow full-speed override may be owned only if ThinkControl itself successfully changes and verifies that exact known state.
- Lower firmware profiles and Auto must release ThinkControl-owned full speed before claiming the lower state is active.
- Manual direct-output tests are temporary and bounded.
- `End test`, timeout, page exit, provider failure and shutdown restore prior ownership/profile where possible; firmware Auto is the fallback.
- Telemetry refresh never creates fan-control writes.
- Unchanged low-level fan states are not continuously rewritten.
- Missing control temperature/provider state returns supervised direct cooling to firmware ownership.
- Hot/safety handoff never traps the machine at a ThinkControl manual state.

See [Cooling design](COOLING-DESIGN.md) for the canonical lifecycle.

## Calibration

Calibration characterizes an already verified writable **direct-output** backend; it is not hardware discovery by write-probing and is not required for semantic firmware-policy/full-speed profiles.

For a verified discrete provider, a new calibration is accepted only after every allowed state has complete, plausible tachometer evidence. Cancellation, telemetry loss, safety failure or inconsistency leaves the previous known-good mapping untouched. Partial calibration is never promoted.

## Verified X9 low-level boundary

The current physically reviewed low-level reference is ThinkPad X9-15 Gen 1 machine type `21Q6` / `21Q7`.

### Normal product cooling path

Alpha.41 keeps **Auto / Quiet / Balanced / Max cooling** useful without re-authorizing the rejected per-fan writer:

```text
Auto         -> release ThinkControl-owned full speed if any; restore latest Lenovo power-policy baseline
Quiet        -> release ThinkControl-owned full speed; Lenovo Quiet policy
Balanced     -> release ThinkControl-owned full speed; Lenovo Balanced policy
Max cooling  -> Lenovo Performance policy + verified global full-speed boolean when safely exposed
```

The service performs exact-X9 identity checks before translating semantic policy into the reviewed Lenovo LITSSvc contract. The desktop UI never supplies raw Lenovo command IDs.

### Global full-speed semantic

Alpha.41 adds one narrow exact-X9 Lenovo Other Mode contract: **feature `0x04020000` as boolean full speed**.

The provider may write it only when all relevant gates pass:

- exact verified X9 identity (`21Q6/21Q7`);
- active `LENOVO_OTHER_METHOD`;
- live `GetFeatureValue(0x04020000)` returns only `0` or `1`;
- if firmware explicitly supplies a capability row, it must advertise the required valid/read/write contract;
- only values `0` and `1` are accepted by product code;
- every transition is verified by reading `0x04020000` back;
- failed enable readback attempts a best-effort release to `0`;
- failed disable never turns full speed back on;
- ThinkControl records ownership only if its own successful call changed the state.

An omitted capability row is not broad permission. The exact known feature may use a live-read fallback only on the verified X9 and only after returning a real boolean immediately before the transition.

This contract must not be generalized to arbitrary Other Mode attributes or presented as continuous RPM/PWM control.

### Rejected per-fan writer boundary

The alpha.38 Lenovo Other Mode `fanX_target` writer remains **physically rejected for product control**. A fixed target repeatedly sped up/slowed down and nominal maximum target remained below naturally hot Lenovo Auto. Firmware metadata and successful `fanX_input` telemetry therefore do not authorize those target writes.

Read-side native dual-fan telemetry remains useful. Target `0` on the rejected per-fan path is retained only to release stale previously owned state. Once native two-fan evidence is established, transient telemetry loss must not silently re-authorize the known-inferior EC writer.

The classic ThinkPad EC family remains research/diagnostic evidence rather than the normal alpha.41 X9 cooling backend:

```text
Lenovo/OEM Auto   0x80
Manual states     0x01 .. 0x07   provider-specific only
Fan off           0x00           blocked
0x40 override     unverified and blocked
```

A percentage may be shown only if an active physically accepted direct provider defines that semantic mapping. Step 7 is not proof of physical maximum.

### Readback and transport discipline

- Full-speed writes require the exact feature's live read and post-write readback.
- Direct manual writes and return-to-Auto require their provider's readback/recovery contract.
- Supported keyboard writes require provider/readback validation.
- Low-level transport uses bounded waits and failure recovery rather than high-frequency blind polling.
- X9 tachometer access remains conservative because aggressive EC polling can disturb fan behavior.
- Firmware policy is sent as semantic transitions; ThinkControl does not fight Lenovo's closed loop by continuously rewriting fixed targets.
- Reported RPM is telemetry, not proof that airflow/cooling intensity equals Lenovo's strongest physical state.

## Diagnostics and device learning

Diagnostics and licensing are independent. Opting out of optional diagnostics must never disable a paid entitlement or safety behavior.

Automatic/future compatibility evidence must be allowlisted and redacted. Never upload usernames, hostnames, serial numbers, personal paths/content, browser content, keystrokes, touch coordinates/trails, memory dumps or arbitrary raw personal logs. See [Diagnostics and privacy](DIAGNOSTICS.md).

Device-learning states are conceptually `Observed → Candidate → Verified → Regression watch`. Read-only evidence may promote with a lower threshold than risky writes; conflicting reports prevent automatic promotion.

## Release rule

A green compiler, snapshot or hosted CI runner is not physical hardware verification. Hardware-write claims require appropriate real-device evidence in addition to software gates.

For alpha.41, automated validation can prove the exact-ID/value/readback/ownership architecture and fail-closed paths. It cannot prove that Max cooling on the user's physical X9 has the expected airflow/acoustic behavior. That remains a separate post-install evidence item and must not be converted into a hosted-CI claim.

Before release promotion, follow [Release readiness](RELEASE_READINESS.md) and [Alpha testing](ALPHA-TESTING.md). Do not weaken safety or backwards-compatibility contracts merely to make the implementation simpler.
