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

Read-only RPM has a lower risk boundary than writable fan ownership. Telemetry candidates may include real hardware-monitor providers, Lenovo WMI/CIM data where exposed, Windows `CIM_Tachometer`, and exact-model verified read-only EC tachometer fallbacks.

Writable backends are modeled separately, for example:

- OEM-native thermal policy;
- discrete EC fan states;
- continuous percentage/PWM target;
- telemetry-only provider.

A generic `FanControl` label is not enough to infer EC or PWM semantics.

## ThinkPad X9-15 Gen 1

Machine types `21Q6` and `21Q7` are the current physically reviewed low-level reference. Their provider uses verified discrete fan states plus OEM Auto and an independently reviewed Lenovo thermal-policy path. Other Lenovo families do not inherit those writes.

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

Family membership never authorizes X9 EC or X9 LITS writes.

## OEM software launching

ThinkControl may open an installed Lenovo application through registered protocols/AUMIDs when useful. The presence of Lenovo Vantage is not a prerequisite for Windows-generic ThinkControl features and does not by itself prove any low-level provider capability.

## Validation principle

Profiles identify candidates; providers prove capabilities. Unknown values fail closed. Risky writes require stronger physical evidence than read-only telemetry, and conflicting reports prevent automatic promotion.
