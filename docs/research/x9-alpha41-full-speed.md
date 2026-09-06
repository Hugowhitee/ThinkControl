# X9 alpha.41 full-speed research addendum

This addendum records the evidence behind `v0.1.0-alpha.41`'s narrow X9 Max-cooling change. It applies only to the verified ThinkPad X9-15 Gen 1 machine types `21Q6` / `21Q7`. It is not a generic Lenovo feature map or permission to probe/write arbitrary Other Mode attributes.

## Physical trigger for the investigation

Two earlier approaches produced different but insufficient results on the reference X9:

1. the alpha.38 per-fan `fanX_target` writer could expose credible native RPM telemetry, but fixed targets repeatedly produced an audible wave/re-kick pattern and nominal 100% remained weaker than naturally hot Lenovo Auto;
2. alpha.39/40 moved built-in profiles to the reviewed Lenovo LITSSvc thermal-policy path, which improved smoothness and kept Lenovo in the closed loop, but alpha.40 physical use still found **Max cooling / Performance policy materially less forceful than naturally hot Auto even when the displayed tachometer RPM looked high**.

This means an RPM reading alone is not sufficient evidence that a semantic policy has reached Lenovo's strongest physical cooling state. It does not mean the RPM reading is fabricated; it means tachometer speed, policy state and global full-speed ownership are distinct evidence classes.

## Independent semantic evidence for `0x04020000`

Multiple independent Lenovo tooling/codebases identify Other Mode feature ID `0x04020000` as the **Fan Full Speed** semantic, while the adjacent `0x04030001...` family is used for current/per-fan fan-speed data. Examples found during the alpha.41 review include:

- LenovoLegionToolkit's capability enum: `FanFullSpeed = 0x04020000`;
- HandheldCompanion's Lenovo device capability enum: `FanFullSpeed = 0x04020000`;
- ThinkBookToolkit's WMI fan backend, which probes the same full-speed feature through Lenovo Other Mode;
- additional modern Lenovo utilities/drivers using the same identifier for a full-speed override.

Those projects cover other Lenovo families. Their agreement establishes the **semantic identity of the feature ID**, not automatic support on the X9. ThinkControl therefore requires the exact X9 itself to prove a live boolean contract before any product write.

## Alpha.41 product gate

`LenovoOtherModeFullSpeedService` accepts exactly one feature ID and two values:

```text
Feature   0x04020000
Off       0
On        1
```

A transition is allowed only when:

- SMBIOS/device identity is the verified X9 `21Q6` or `21Q7`;
- an active `LENOVO_OTHER_METHOD` instance exists;
- `GetFeatureValue(0x04020000)` returns a real boolean `0` or `1`;
- if `LENOVO_CAPABILITY_DATA_00` explicitly contains the feature, that row does not reject the required VALID/GET/SET contract;
- the exact feature is read again immediately around the transition;
- `SetFeatureValue` is invoked with only `0` or `1`;
- the resulting value is verified by `GetFeatureValue` readback.

If the capability row is omitted, the exact-X9 direct-ID fallback exists only for this already-known semantic and only after a live boolean read. An explicitly invalid/read-only capability record remains authoritative.

A failed enable readback performs best-effort release to `0`. A failed disable never writes `1` as rollback. Failure is surfaced instead of falling through to another writer.

## Ownership model

Reading full speed as active does not prove ThinkControl enabled it.

- If ThinkControl changes `0 -> 1` and verifies it, the current service instance owns that full-speed override.
- If Max is selected while the feature is already `1`, ThinkControl does not claim ownership.
- Quiet/Balanced automatically release only ThinkControl-owned full speed; they refuse to silently turn off a state observed as externally owned.
- Normal service disposal likewise releases only ThinkControl-owned state.
- An explicit user **Auto** request is the deliberately wider recovery operation: it may turn the exact known boolean feature off after a service restart lost the earlier in-memory ownership marker, with the same live-read/readback gate.

This prevents routine cleanup from fighting another utility while still allowing a user to recover from a stale alpha.41 Max state after a process/service restart.

## Built-in cooling mapping

The alpha.41 X9 intent is:

```text
Quiet        Lenovo Quiet policy; ThinkControl-owned full speed off
Balanced     Lenovo Balanced policy; ThinkControl-owned full speed off
Max cooling  Lenovo Performance policy + verified 0x04020000 full speed
Auto         verified full-speed release + latest Lenovo power-policy baseline
```

This remains firmware/OEM semantic control. ThinkControl does not advertise custom curves, direct percentage output or arbitrary RPM targets from this backend.

## Explicitly unchanged/rejected paths

Alpha.41 does **not**:

- re-enable the physically rejected Other Mode `fanX_target` writer;
- raise or extrapolate Lenovo Fan Test Data maximum RPM;
- promote classic EC level 7 or the blocked `0x40` family to physical maximum;
- invoke EnergyDrv `ChangeFanSpeed 0x8310257C` without its exact X9 command/rollback contract;
- use the dust/maintenance high-speed `0x831020C0` family as product fan control;
- expose arbitrary Lenovo feature IDs or values through IPC/UI.

## Required physical validation

Hosted tests can prove identity/value gates, source wiring, ownership separation and rollback/readback structure. They cannot prove airflow/acoustics. On the real X9, alpha.41 still needs confirmation that:

- Max cooling produces a clear stronger Lenovo-style airflow state than alpha.40 Performance-policy-only Max;
- the new Max state is steady and does not reproduce the alpha.38 repeated wave/re-kick behavior;
- Max -> Balanced/Quiet releases the owned full-speed state cleanly;
- explicit Auto restores firmware ownership and the latest power-policy baseline;
- a machine/firmware that does not expose the boolean feature fails Max explicitly rather than activating another low-level fallback.

The main historical X9 evidence remains in [`x9-15-gen1.md`](x9-15-gen1.md); this file is the focused alpha.41 addendum for the new narrow full-speed contract.
