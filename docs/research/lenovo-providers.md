# Lenovo provider research

Lenovo laptops do not expose one universal hardware-control interface. This reference records provider families that ThinkControl may probe and the boundaries that keep one model's implementation from leaking into another.

A profile selects reasonable provider candidates. The provider itself owns probing, readback, lifecycle and write safety. Installed-file/class presence alone is never sufficient to make a provider writable.

## Windows-first capabilities

Use Windows APIs when the OS already owns the feature: power preference, display refresh/brightness, battery status, system audio and machine identity. These paths are vendor-neutral and should remain usable independently of Lenovo hardware providers.

## Keyboard backlight

Known Lenovo candidates include:

- `IBMPmDrv` on many ThinkPads;
- `EnergyDrv` on multiple Lenovo families;
- the installed Lenovo ThinkKeyboard/Vantage component as a bounded fallback where its semantic contract validates.

A candidate must return a recognized state before writes are enabled. A requested write succeeds only under the provider's readback/response contract. ThinkControl user-session effects are separate from hardware backlight capability; Auto therefore does not require unrelated effect capabilities when normal backlight writes are valid.

## Fan telemetry versus fan control

Read-only RPM has a lower risk boundary than writable fan ownership. Telemetry candidates may include real hardware-monitor providers, Lenovo WMI/CIM data where exposed, Windows `CIM_Tachometer`, Lenovo `EnergyDrv` query contracts where their semantics are known, and exact-model verified read-only EC tachometer fallbacks.

Writable backends are modeled separately, for example:

- OEM-native thermal policy;
- OEM semantic target-RPM control;
- discrete EC fan states;
- telemetry-only provider.

A generic `FanControl` label is not enough to infer EC, PWM or target-RPM semantics. A native OEM telemetry path does not become writable merely because a nearby driver IOCTL exists: the write command format, rollback and readback contract must be recovered and validated independently.

## ThinkPad X9-15 Gen 1

Machine types `21Q6` and `21Q7` are the current physically reviewed low-level reference. Physical testing has shown that the classic ThinkPad EC path can expose both fan tachometers, but its seven manual states do not reproduce Lenovo Auto's useful high-cooling range or acoustic behavior. That path is therefore investigation/fallback evidence rather than the desired product fan-control boundary.

The preferred X9 hierarchy is now:

1. Lenovo `LENOVO_OTHER_METHOD` target-RPM control, but only if exact-device capability data exposes at least two VALID+GET+SET channels with sane constraints and live reads;
2. Lenovo-native fan telemetry through Other Mode or the known read-only `EnergyDrv` `QueryFanSpeed` contract;
3. read-only EC thermal/tachometer evidence only when richer native telemetry is unavailable;
4. writable EC steps only on machines where no native Lenovo fan surface has been established and the exact-model EC contract remains explicitly allowed.

On the physically tested X9 candidate, Other Mode did not become writable. PR #71 therefore also probes `EnergyDrv` `QueryFanSpeed` (`0x83102570`) for fan indices 0/1 read-only. When two native Lenovo fan channels are present, ThinkControl prioritizes those RPMs and deliberately stops advertising the known-inferior EC writer while the exact OEM target-RPM command is recovered. Public Lenovo reverse-engineering proves a separate `ChangeFanSpeed` IOCTL (`0x8310257C`) exists, but does not define the X9 `dwFanCtrlCmd` encoding; ThinkControl must not brute-force it.

Exact registers, transport observations and physical test evidence are maintained in [x9-15-gen1.md](x9-15-gen1.md).

## Lenovo Intelligent Thermal Solution

X9 research established a Lenovo policy path through `LITSSvc`. ThinkControl exposes only semantic **Efficiency / Balanced / Performance** behavior in product UI; the service selects the reviewed source-specific command only after exact provider/identity checks.

This is thermal-policy coordination, not a direct fan PWM/RPM interface. Lenovo's returned integer semantics are not guessed; the implementation uses only the protocol boundary supported by observed evidence.

## GameZone and family providers

Some Legion/LOQ-family systems expose `LENOVO_GAMEZONE_DATA` or related OEM WMI contracts. ThinkControl treats those as provider candidates, not family-wide guarantees: support queries/current state/readback must validate before any semantic write is exposed.

Typical candidate hierarchy:

| Family | Conservative candidates |
| --- | --- |
| ThinkPad | Windows APIs, `IBMPmDrv`, Lenovo telemetry, exact-model providers when verified |
| ThinkBook / Yoga / IdeaPad | Windows APIs, `EnergyDrv`/compatible Lenovo PM paths, read-only Lenovo telemetry |
| Legion / LOQ | Windows APIs, Lenovo telemetry, GameZone-style providers only where validated |

Family membership never authorizes X9 EC, X9 LITS or X9 EnergyDrv writes.

## OEM software launching

ThinkControl may open an installed Lenovo application through registered protocols/AUMIDs when useful. The presence of Lenovo Vantage is not a prerequisite for Windows-generic ThinkControl features and does not by itself prove any low-level provider capability.

## Validation principle

Profiles identify candidates; providers prove capabilities. Unknown values fail closed. Risky writes require stronger physical evidence than read-only telemetry, and conflicting reports prevent automatic promotion.
