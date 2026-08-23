# Lenovo providers

Lenovo laptops do not use one universal hardware-control interface. ThinkControl selects providers per capability instead of applying one model's implementation to an entire product family.

```text
Machine identity
      |
      v
Device/family profile
      |
      v
Provider candidates
      |
      v
Probe and read
      |
      v
Optional semantic write
      |
      v
Readback / protocol response
```

A profile defines what is reasonable to probe. It does not authorize unknown EC registers, raw named-pipe commands, IOCTLs or ACPI methods.

## Windows providers

Windows APIs are preferred when the OS already exposes the feature: power mode, internal display refresh/brightness, adaptive brightness, battery telemetry and system identity. These providers can also work on non-Lenovo hardware.

## Keyboard backlight

### IBMPmDrv

Common on ThinkPads. ThinkControl accepts it only when the read contract returns a recognized Off, Low or High state. A write succeeds only when the requested state can be read back.

### EnergyDrv

Appears on multiple Lenovo families. Known contracts/return formats are probed separately; unknown values fail the probe instead of making the provider writable by assumption.

### Installed Lenovo Vantage ThinkKeyboard

On systems where direct PM/EnergyDrv control is unavailable, ThinkControl can probe the installed Lenovo ThinkKeyboard component as a fallback. The component must actually validate; installed-file presence alone is not sufficient.

## Fan telemetry

Read-only RPM can use broader providers than fan control. Candidates include the verified X9 EC tachometer, Lenovo fan WMI/CIM interfaces when exposed, Windows `CIM_Tachometer` and other providers that identify a real tachometer.

Missing WMI classes/methods are ordinary unsupported-capability results.

## Fan control

Writable fan providers require stricter validation.

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

Other Lenovo families do not inherit this backend.

## Lenovo Intelligent Thermal Solution

X9 research established this observed policy path:

```text
Commercial Vantage
  -> ThinkSmartSenseAddin
  -> \\.\pipe\com.lenovo.its.pipe.setting
  -> LITSSvc
  -> Lenovo thermal / firmware stack
```

The contract writes one UInt32 command and reads one Int32 response.

ThinkControl `v0.1.0-alpha.2` implements a **verified-profile semantic provider** for X9 `21Q6/21Q7` only:

| Power source | Quiet | Balanced | Performance |
| --- | ---: | ---: | ---: |
| AC | 502 | 503 | 504 |
| Battery | 507 | 508 | 509 |

The UI never supplies a raw command ID. It requests Quiet/Balanced/Performance, the privileged service re-checks X9 identity, reads AC/DC state and selects the allowlisted command.

Lenovo has not published the Int32 response-value semantics, so ThinkControl considers receipt of the complete response to be the protocol readback boundary rather than inventing a `0 == success` rule.

This provider is a thermal-policy interface. It is not presented as a continuous or direct fan-RPM/PWM interface. Windows power mode remains the primary OS-level policy surface.

## Lenovo GameZone WMI

Some Legion, LOQ and related systems expose `LENOVO_GAMEZONE_DATA`. ThinkControl treats it as capability-based: class/support query/current state must validate before a write is enabled. Presence of the class does not imply every method is valid on every model.

## Vantage launching

ThinkControl resolves registered local Vantage protocols first and then installed Start-app AUMIDs. Microsoft Store is not opened when an installed Commercial Vantage/Lenovo Vantage instance can be resolved.

## Family provider candidates

| Family | Provider candidates |
| --- | --- |
| ThinkPad | Windows APIs, `IBMPmDrv`, Lenovo read-only telemetry, exact model-specific providers when verified |
| ThinkBook | Windows APIs, `EnergyDrv`, Lenovo telemetry, supported vendor WMI |
| Yoga | Windows APIs, compatible Lenovo PM provider, Lenovo telemetry, supported vendor WMI |
| IdeaPad | Windows APIs, `EnergyDrv`, Lenovo telemetry, supported vendor WMI |
| LOQ | Windows APIs, compatible Lenovo PM provider, Lenovo telemetry, GameZone where supported |
| Legion | Windows APIs, Lenovo telemetry, GameZone and exact model-specific providers where supported |

Family membership is never enough to enable X9 EC or X9 LITSSvc writes.

## Validation labels

- **Verified:** explicitly authorized/tested provider profile.
- **Beta:** known provider candidates exist, but the exact model has not been fully validated.
- **Generic:** only platform-independent providers are assumed.

A beta device can still have working individual capabilities when those providers pass their own checks.

The X9 evidence record is [research/x9-15-gen1.md](research/x9-15-gen1.md).
