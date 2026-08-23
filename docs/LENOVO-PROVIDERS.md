# Lenovo providers

Lenovo laptops do not use one universal hardware-control interface. ThinkControl therefore selects providers per capability instead of applying one model's implementation to an entire product family.

```text
Machine identity
      |
      v
Device or family profile
      |
      v
Provider candidates
      |
      v
Probe and read
      |
      v
Optional reversible write
      |
      v
Readback verification
```

A profile defines what is reasonable to probe. It does not authorize unknown EC registers, IOCTLs or ACPI methods.

## Windows providers

Windows APIs are preferred when the operating system already exposes the feature.

Examples include:

- power mode;
- internal display refresh rate;
- display brightness;
- adaptive brightness;
- battery energy and charge/discharge rate;
- system identity.

These providers can also work on non-Lenovo hardware.

## Keyboard backlight

### IBMPmDrv

`IBMPmDrv` is common on ThinkPads. ThinkControl accepts the provider only when its read operation returns a recognized Off, Low or High state. A write succeeds only when the requested state can be read back afterward.

### EnergyDrv

`EnergyDrv` appears on several ThinkBook, IdeaPad, Yoga and LOQ platforms. Known return formats are probed separately. Unknown values fail the probe rather than making the provider writable by assumption.

### Lenovo Vantage components

An installed Vantage or Commercial Vantage component can be considered as a provider only after its exact invocation and state-verification behavior have been implemented and tested. ThinkControl does not treat the presence of Vantage as proof that a private interface is safe to call.

## Fan telemetry

Read-only fan telemetry can use broader providers than fan control.

Current candidates include:

1. the verified X9 EC tachometer on the exact X9 profile;
2. `LENOVO_FAN_METHOD.Fan_GetCurrentFanSpeed` when exposed;
3. `Lenovo_DT_GetCPUFan` and `Lenovo_DT_GetSYSFan` when exposed;
4. Windows `CIM_Tachometer`;
5. other sensor providers that identify a real fan tachometer.

RPM values must remain plausible. Missing WMI classes or methods are treated as unsupported capability results, not application failures.

## Fan control

Writable fan providers require stricter validation than read-only telemetry.

### ThinkPad X9-15 Gen 1

The verified `21Q6/21Q7` provider uses:

```text
Control register   0x2F
RPM low byte       0x84
RPM high byte      0x85
Lenovo Auto        0x80
Manual levels      1 to 7
```

Fan-off `0x00` and the unverified `0x40` override family are not exposed.

### Other Lenovo devices

Other Lenovo families do not inherit the X9 EC backend.

A new writable provider can be added when its control contract is independently established, the current state is readable and the operation can be validated safely on the target hardware.

## Lenovo GameZone WMI

Some Legion, LOQ and related Lenovo systems expose `LENOVO_GAMEZONE_DATA`.

ThinkControl treats it as a capability-based vendor provider:

1. the class must exist;
2. a relevant support query must report support when the interface provides one;
3. the current state must be readable;
4. requested values must be limited to the documented or independently established contract;
5. state should be read back after a write when possible.

The presence of the class does not imply that every method is valid on every model.

## Lenovo Intelligent Thermal Solution

X9 research identified the following thermal-policy components:

```text
Service      LITSSVC
ACPI device  ACPI\LEN0100
Vantage      ThinkSmartSenseAddin
IPC          \\.\pipe\com.lenovo.its.pipe.setting
```

This is evidence of a Lenovo thermal-policy layer, not a direct PWM fan interface. ThinkControl should prefer supported Windows power APIs or a well-understood Lenovo policy contract over private firmware manipulation.

## Family provider candidates

| Family | Provider candidates |
| --- | --- |
| ThinkPad | Windows APIs, `IBMPmDrv`, Lenovo fan telemetry, model-specific providers when verified |
| ThinkBook | Windows APIs, `EnergyDrv`, Lenovo fan telemetry, supported vendor WMI |
| Yoga | Windows APIs, compatible Lenovo PM provider, Lenovo telemetry, supported vendor WMI |
| IdeaPad | Windows APIs, `EnergyDrv`, Lenovo telemetry, supported vendor WMI |
| LOQ | Windows APIs, compatible Lenovo PM provider, Lenovo telemetry, GameZone where supported |
| Legion | Windows APIs, Lenovo telemetry, GameZone and model-specific providers where supported |

Family membership is not enough to enable direct EC writes.

## Validation depth

ThinkControl uses three broad labels:

- Verified: tested on the actual hardware/provider combination;
- Beta: known provider candidates exist, but the exact model has not been fully validated;
- Generic: only platform-independent providers are assumed.

A beta device can still have working capabilities when those providers pass their own checks.

## Adding a provider

Most new devices should be investigated with a narrow set of evidence:

- exact product name and machine type;
- installed Lenovo services and drivers;
- relevant WMI classes and methods;
- safe read probes;
- readback tests for reversible controls.

Use ACPI dumps, Process Monitor traces or binary-driver analysis only when existing provider families do not explain the behavior.

The X9 record shows the evidence and safety decisions used for the first direct EC provider: [research/x9-15-gen1.md](research/x9-15-gen1.md).
