# Device support

This document describes compatibility for ThinkControl `v0.1.0-alpha.23`.

ThinkControl's current public focus is Lenovo and ThinkPad laptops. Support is evaluated **per capability and provider**, not by assuming every laptop in a family uses the same interface. Windows-safe features may work elsewhere, but other OEMs are not marketed as fully supported in this alpha.

The current physically reviewed low-level reference is the Lenovo ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`). That is the first verified model profile, not the long-term product boundary.

## Support levels

### Verified

A low-level provider contract has been explicitly authorized and physically reviewed for the exact hardware scope.

- Lenovo ThinkPad X9-15 Gen 1, machine type `21Q6` or `21Q7`.

### Beta

ThinkControl recognizes an OEM/family and may probe known provider types, but the exact model has not been fully validated. Writable controls still require a recognized provider contract plus readback/safety validation.

### Generic

No OEM-specific profile is required. Windows-level features can work wherever Windows exposes the documented capability.

## Profile hierarchy

```text
Windows generic capability
→ OEM generic profile
→ product-family profile
→ exact-model profile
```

Example today:

```text
Windows
→ Lenovo
→ ThinkPad
→ X9-15 Gen 1
```

Future OEMs should use the same structure without creating vendor-specific copies of the main UI. See [`devices/README.md`](../devices/README.md).

## Current compatibility matrix

| Device scope | Windows features | Sensor / fan telemetry | Keyboard | Low-level fan control | OEM thermal policy | Status |
| --- | --- | --- | --- | --- | --- | --- |
| ThinkPad X9-15 Gen 1 `21Q6/21Q7` | Supported | LHM/PawnIO first, verified X9 EC fallback | Lenovo PM/EnergyDrv + validated Vantage component fallback | Lenovo Auto + levels 1–7 | Verified X9 LITSSvc semantic commands | Verified reference |
| Other ThinkPads | Supported | Generic sensors + Lenovo read-only providers when exposed | Known Lenovo providers after read probe | Exact provider/model contract required | Capability-specific only | Beta |
| ThinkBook / Yoga / IdeaPad | Supported | Generic sensors + Lenovo read-only providers | Known Lenovo providers after read probe | Exact provider/family contract required | Capability-specific only | Beta |
| Legion / LOQ | Supported | Generic sensors + supported Lenovo providers | Compatible Lenovo provider | Provider-specific only when verified | Capability-specific only | Beta |
| Other Lenovo | Supported | Conservative read-only discovery | Known provider discovery | Disabled without verified provider | No X9 command reuse | Beta |
| Other Windows laptops | Where Windows supports it | Generic safe sensor providers when available | OEM provider required | OEM/family/model provider required | OEM provider required | Generic / expandable |

## Windows-level baseline

These features can work without a Lenovo or model-specific profile when Windows exposes the necessary interface:

- Windows power behavior (**Efficiency / Balanced / Performance** in ThinkControl);
- display refresh-rate selection and automatic refresh policy;
- internal display brightness and adaptive brightness;
- Night light / Power & battery Settings shortcuts;
- Windows system output and microphone controls;
- battery percentage, source, watts, Wh, health and filtered time estimates;
- charge/discharge session history;
- compatible read-only temperature/sensor telemetry;
- themes, tray operation, startup settings, updates and diagnostics.

Unavailable data is shown as unavailable rather than replaced with a synthetic value.

## Sensors and fan telemetry

ThinkControl prefers provider-reported hardware identity and real sensor domains. LibreHardwareMonitor/PawnIO is one broad provider route on Windows, not a vendor lock-in.

A generic ACPI thermal zone is never automatically relabelled as CPU Package. Model-specific read-only thermal fallbacks may be exposed under an honest provider/source label and may only become a control-temperature source when the provider's safety model permits it.

Read-only RPM may come from, in order of preference where applicable:

1. LibreHardwareMonitor/PawnIO or another real hardware sensor provider;
2. an exact-model verified EC tachometer fallback;
3. OEM WMI/CIM telemetry;
4. Windows `CIM_Tachometer` where implemented.

Missing provider classes are normal compatibility results. ThinkControl never fabricates RPM.

## ThinkPad X9-15 Gen 1

The X9 low-level profile is restricted to machine type `21Q6` or `21Q7`.

| Capability | Implementation |
| --- | --- |
| Fan RPM | LHM/PawnIO when exposed; verified EC tachometer `0x84/0x85` as conservative fallback |
| EC transport | `0x1604/0x1600` preferred, `0x66/0x62` fallback; stale output is drained and reads use bounded readiness behavior |
| Fan state | EC `0x2F` during provider validation and explicit control/readback paths; not continuously polled |
| Lenovo Auto | `0x80` with readback |
| Manual fan control | Levels `1` through `7` |
| Fan off | `0x00` blocked |
| Unverified override | `0x40` family never written |
| Normal service exit | Attempts to return manual ownership to Lenovo Auto |
| Temperature | LHM/PawnIO preferred; verified read-only EC thermal fallback may feed safe control temperature |
| Power modes | Windows mode plus verified X9 semantic Lenovo policy coordination |
| Keyboard Off/Low/High | Lenovo PM/EnergyDrv with readback and validated Lenovo Vantage component fallback |
| Keyboard effects | User-session Auto/Breathing/Reactive/experimental Audio policies |

The X9 chassis contains two physical fans, but ThinkControl does not issue an unverified selector write merely to manufacture separate Fan 1/Fan 2 readings. Only telemetry that a real provider can identify is reported.

Supervised custom cooling uses discrete verified fan states with smoothing, hysteresis and dwell so short temperature noise does not cause rapid fan hunting or unnecessary hardware writes.

## Touchpad and haptics

Precision Touchpad gestures are a Windows/user-session feature. The visualizer and gesture engine can be used where the device exposes compatible Precision Touchpad input. Haptic controls remain capability-gated: a missing click-force or feedback API disables only that control rather than implying the whole touchpad is unsupported.

## Dolby / audio

Normal Windows audio controls are generic. Dolby controls depend on the installed DAX provider, not the laptop brand alone. Direct controls are exposed only when semantic operations can be verified; compatible OEM DAX3 systems may use the bounded official Dolby Access bridge instead of guessed private IDs.

## Adding another OEM or model

Support should normally be added in this order:

1. reuse Windows-safe capabilities;
2. add/read an OEM-generic provider;
3. narrow behavior in a family profile when necessary;
4. add exact-model low-level writes only after physical validation and recovery/readback design.

Compatibility matching can use SMBIOS manufacturer/model, machine type, BIOS version when relevant, ACPI/PnP IDs and installed provider/service identities. Serial numbers, usernames, MAC addresses and disk identifiers are not needed for matching.

Use the [bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml) to report a device or compatibility issue.
