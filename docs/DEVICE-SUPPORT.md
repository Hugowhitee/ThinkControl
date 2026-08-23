# Device support

This document describes compatibility for ThinkControl `v0.1.0-alpha.2`.

ThinkControl evaluates support per capability. A family profile selects reasonable providers to probe, but it does not make every feature available on every model.

## Support levels

### Verified

The relevant low-level profile is explicitly authorized for the hardware model. Current reference device:

- Lenovo ThinkPad X9-15 Gen 1, machine type `21Q6` or `21Q7`.

### Beta

ThinkControl recognizes the Lenovo family and can probe known provider types, but the exact model has not been fully validated by this project. Writable controls still require a recognized provider and readback/contract validation.

### Generic

No Lenovo-specific profile is assumed. Windows-level features can still work where the operating system exposes them.

## Current matrix

| Device family | Windows features | Fan telemetry | Keyboard backlight | Low-level fan control | Lenovo thermal policy | Status |
| --- | --- | --- | --- | --- | --- | --- |
| ThinkPad X9-15 Gen 1 `21Q6/21Q7` | Supported | Verified X9 EC tachometer | Lenovo PM/EnergyDrv + validated Vantage fallback | Lenovo Auto + levels 1 to 7 | Verified-profile LITSSvc commands | Verified reference |
| Other ThinkPads | Supported | Lenovo WMI/CIM when exposed | `IBMPmDrv` when verified | Exact provider/profile required | No X9 command reuse | Beta |
| ThinkBook | Supported | Lenovo WMI/CIM when exposed | `EnergyDrv` when verified | Supported vendor provider required | Capability-specific only | Beta |
| Yoga | Supported | Lenovo WMI/CIM when exposed | Compatible Lenovo PM provider when verified | Supported vendor provider required | Capability-specific only | Beta |
| IdeaPad | Supported | Lenovo WMI/CIM when exposed | `EnergyDrv` when verified | Supported vendor provider required | Capability-specific only | Beta |
| LOQ | Supported | Lenovo WMI/CIM when exposed | Compatible Lenovo PM provider | GameZone only when firmware reports support | Capability-specific only | Beta |
| Legion | Supported | Lenovo WMI/CIM when exposed | Compatible Lenovo PM/lighting provider | GameZone only when firmware reports support | Capability-specific only | Beta |
| Other Lenovo | Supported | Conservative read-only discovery | Known provider discovery | Disabled without verified provider | X9 commands disabled | Beta |
| Other Windows laptops | Where Windows supports it | Generic read-only sensors only | Lenovo backend unavailable | Unavailable | Unavailable | Generic |

## Windows-level features

These can work without a model-specific Lenovo profile when Windows exposes the necessary interface:

- Quiet, Balanced and Performance power modes;
- display refresh-rate selection and automatic refresh policy;
- internal display brightness and adaptive brightness;
- battery percentage, power source, watts, Wh, health and filtered time estimates;
- read-only temperature sensors;
- themes, tray operation, startup settings, updates and diagnostics.

Unavailable data is shown as unavailable rather than replaced with a value that looks like real hardware telemetry.

## Lenovo keyboard providers

ThinkControl knows Lenovo PM driver families including `IBMPmDrv` and `EnergyDrv`. A provider is accepted only when its read operation returns a recognized state; writable operations require readback verification.

On supported systems, the installed Lenovo Vantage ThinkKeyboard component can be probed as a fallback. Presence alone does not enable the provider.

## Fan telemetry

Read-only RPM can be obtained from several sources:

1. verified X9 EC tachometer on X9 `21Q6/21Q7`;
2. `LENOVO_FAN_METHOD.Fan_GetCurrentFanSpeed` when exposed;
3. `Lenovo_DT_GetCPUFan` / `Lenovo_DT_GetSYSFan` when exposed;
4. Windows `CIM_Tachometer`;
5. other read-only providers that identify a real fan tachometer.

Missing WMI classes/providers are normal compatibility results.

## ThinkPad X9-15 Gen 1

The X9 low-level profile is restricted to machine type `21Q6` or `21Q7`. Machine-type parsing prioritizes those codes so Lenovo SKU strings cannot accidentally classify the reference X9 as an unrelated four-character token.

| Capability | Implementation |
| --- | --- |
| Fan RPM | EC tachometer `0x84/0x85`, conservative polling |
| Fan state | EC `0x2F` |
| Lenovo Auto | `0x80` with readback |
| Manual fan control | Levels `1` through `7` |
| Fan off | `0x00` blocked |
| Unverified override | `0x40` family never written |
| Normal service exit | Attempts to return manual control to Lenovo Auto |
| Power modes | Windows Best efficiency / Balanced / Best performance |
| Lenovo Intelligent Cooling | AC `502/503/504`, DC `507/508/509`, semantic X9-only service operation |
| Keyboard Off/Low/High | Lenovo PM/EnergyDrv with readback and validated Vantage fallback |
| Keyboard effects | User-session Auto/Breathing/Reactive/experimental Audio policies |

The Lenovo Intelligent Cooling commands are thermal-policy commands, not direct fan-RPM/PWM controls.

## Device identification

Compatibility matching may use manufacturer, model/product name, Lenovo machine type, BIOS version when relevant, ACPI/PnP IDs, installed provider versions and Windows display capabilities.

ThinkControl does not require laptop serial number, asset tag, MAC address or disk serial for compatibility matching.

## Adding support for another device

A useful validation report normally includes exact product name/machine type, relevant Lenovo drivers/services, provider/WMI availability, plausible read-only telemetry, readback results for reversible controls and a privacy-safe support bundle when needed.

Use the [bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml) to report a device or compatibility issue.
