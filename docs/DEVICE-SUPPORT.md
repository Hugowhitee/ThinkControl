# Device support

This document describes compatibility for ThinkControl `v0.1.0-alpha.1`.

ThinkControl evaluates support per capability. A family profile selects reasonable providers to probe, but it does not make every feature available on every model.

## Support levels

### Verified

The relevant provider and capability have been tested on the actual hardware used by ThinkControl.

Current reference device:

- Lenovo ThinkPad X9-15 Gen 1, machine type `21Q6` or `21Q7`.

### Beta

ThinkControl recognizes the Lenovo family and can probe known provider types, but the exact model has not been fully validated by this project. Individual controls still require a successful probe and, for writable features, a valid readback path.

### Generic

No Lenovo-specific profile is assumed. Windows-level features can still work where the operating system exposes them.

## Current matrix

| Device family | Windows features | Fan telemetry | Keyboard backlight | Low-level fan control | Status |
| --- | --- | --- | --- | --- | --- |
| ThinkPad X9-15 Gen 1, `21Q6` / `21Q7` | Supported | X9 EC plus safe fallbacks | `IBMPmDrv` | Lenovo Auto and levels 1 to 7 | Verified reference |
| Other ThinkPads | Supported | Lenovo WMI/CIM when exposed | `IBMPmDrv` when verified | Exact provider or profile required | Beta |
| ThinkBook | Supported | Lenovo WMI/CIM when exposed | `EnergyDrv` when verified | Supported vendor provider required | Beta |
| Yoga | Supported | Lenovo WMI/CIM when exposed | Compatible Lenovo PM provider when verified | Supported vendor provider required | Beta |
| IdeaPad | Supported | Lenovo WMI/CIM when exposed | `EnergyDrv` when verified | Supported vendor provider required | Beta |
| LOQ | Supported | Lenovo WMI/CIM when exposed | Compatible Lenovo PM provider | GameZone provider only when firmware reports support | Beta |
| Legion | Supported | Lenovo WMI/CIM when exposed | Compatible Lenovo PM or lighting provider | GameZone provider only when firmware reports support | Beta |
| Other Lenovo | Supported | Conservative read-only discovery | Known provider discovery | Disabled without a verified provider | Beta |
| Other Windows laptops | Where Windows supports it | Generic read-only sensors only | Lenovo backend unavailable | Unavailable | Generic |

## Windows-level features

These features do not require a model-specific Lenovo profile when Windows exposes the necessary interface:

- Quiet, Balanced and Performance power modes;
- display refresh-rate selection;
- automatic 60 Hz and maximum-refresh policy;
- internal display brightness;
- adaptive brightness;
- battery percentage and power source;
- charge and discharge rate in watts;
- battery energy in Wh and estimated health;
- filtered time-to-full or time-remaining estimates;
- read-only temperature sensors;
- themes, tray operation, startup settings, updates and diagnostics.

Unavailable data is shown as unavailable rather than replaced with an estimate that looks like a hardware reading.

## Lenovo keyboard providers

ThinkControl currently knows the following Lenovo PM driver families:

- `IBMPmDrv`, commonly found on ThinkPads;
- `EnergyDrv`, used on several ThinkBook, IdeaPad, Yoga and LOQ platforms.

A provider is accepted only when its read operation returns a recognized state. Writable operations require readback verification.

## Fan telemetry

Read-only RPM can be obtained from several sources:

1. the verified X9 EC tachometer when the X9 profile is active;
2. `LENOVO_FAN_METHOD.Fan_GetCurrentFanSpeed` when exposed;
3. `Lenovo_DT_GetCPUFan` or `Lenovo_DT_GetSYSFan` when exposed;
4. Windows `CIM_Tachometer`;
5. other read-only sensor providers that identify a real fan tachometer.

Missing WMI classes or providers are normal compatibility results.

## Lenovo GameZone WMI

Some Legion, LOQ and related Lenovo platforms expose `LENOVO_GAMEZONE_DATA`. ThinkControl treats it as a capability-based vendor provider. The class and relevant support query must exist before a control is enabled.

The presence of the class alone does not authorize every method.

## ThinkPad X9-15 Gen 1

The X9 low-level fan backend is restricted to machine type `21Q6` or `21Q7`.

| Capability | Implementation |
| --- | --- |
| Fan RPM | EC tachometer registers `0x84/0x85` with conservative polling |
| Fan state | EC register `0x2F` |
| Lenovo Auto | `0x80` with readback |
| Manual fan control | Levels `1` through `7` |
| Fan off | `0x00` blocked |
| Unverified override | `0x40` family never written |
| Normal service exit | Attempts to return manual control to Lenovo Auto |
| Keyboard Off, Low and High | Lenovo PM provider with readback |
| Keyboard Auto | User-session policy over supported hardware states |
| Breathing | Rate-limited Low and High transitions |
| Reactive | Local keyboard-activity response without storing typed content |
| Audio | Experimental local loopback level response without retaining audio samples |

The underlying research is documented in [research/x9-15-gen1.md](research/x9-15-gen1.md).

## Device identification

Compatibility matching may use:

- manufacturer;
- model and product name;
- Lenovo machine type;
- BIOS version when relevant;
- ACPI and PnP device IDs;
- installed provider versions;
- Windows display capabilities.

ThinkControl does not require the laptop serial number, asset tag, MAC address or disk serial for compatibility matching.

## Adding support for another device

A useful validation report normally includes:

1. exact product name and Lenovo machine type;
2. relevant Lenovo drivers and services;
3. provider or WMI availability;
4. plausible read-only telemetry;
5. readback results for reversible controls;
6. a support bundle from the actual laptop when needed.

Deep ACPI or driver analysis is reserved for capabilities that cannot be explained by an established provider.

Use the [bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml) to report a device or compatibility issue.
