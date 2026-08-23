# Device support model

## Principle

**Capabilities, not assumptions.**

The question is never simply "Is this a Lenovo?". ThinkControl resolves a set of independently verified capabilities for the exact observed machine.

## Identity inputs

Read-only identity may use:

- SMBIOS manufacturer
- product family/name
- machine type/model
- BIOS version
- ACPI device IDs
- presence/version of Lenovo services and drivers
- supported Windows display modes
- presence of PawnIO

Unique serials are not needed for feature matching.

## Profile matching

A bundled profile contains stable identifiers and verification facts. Example:

```text
Manufacturer: LENOVO
Family: ThinkPad
Product: ThinkPad X9-15 Gen 1
Machine types: 21Q6, 21Q7
```

Profiles are versioned. A match may be `Verified`, `ExperimentalReadOnly` or `Unknown`.

## Capability examples

- PerformanceMode
- LenovoThermalPolicy
- CpuTemperature
- FanRpm
- FanControl
- KeyboardBacklight
- DisplayRefresh
- AdaptiveBrightness
- BatteryTelemetry
- BatteryChargeThreshold

A capability descriptor should expose both support and provenance, e.g.:

```text
FanRpm
Support: Verified
Provider: ThinkPad EC
Source: EC 0x84/0x85
```

For fallback temperature:

```text
Temperature
Support: SafeReadOnly
Provider: Windows thermal zone
Label: System thermal sensor — approximate
```

Never label an ACPI thermal-zone value as `CPU Package`.

## Provider selection

A device profile does not itself perform I/O. It authorizes a compiled provider to attempt a health check. This prevents arbitrary downloaded data from becoming executable hardware instructions.

## Unknown ThinkPad behavior

Unknown device:

```text
Windows power mode      available if OS supports it
Display refresh         available if OS supports it
Brightness              available if OS supports it
Battery telemetry       read-only where available
Lenovo support links    available
Direct EC writes        disabled
Undocumented IOCTLs     disabled
```

The UI should say that the device has not yet been validated rather than showing broken controls.

## Support submission

Future `Help add support` flow:

1. gather allowlisted read-only capability data
2. redact unique identifiers
3. show the exact payload
4. ask for explicit send confirmation
5. POST to a small submission service
6. submission service uses a GitHub App token server-side to create/update a GitHub issue

No GitHub PAT is embedded in ThinkControl.
