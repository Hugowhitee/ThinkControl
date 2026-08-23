# Lenovo provider model

ThinkControl aims to run across a broad range of Lenovo laptops without pretending that Lenovo has one universal hardware interface.

The compatibility model is therefore **provider-first**:

```text
machine identity
      ↓
family profile
      ↓
capability-specific provider candidates
      ↓
probe / read / sanity-check
      ↓
optional reversible write
      ↓
readback verification
```

A family profile tells ThinkControl **what is reasonable to probe**. It does not authorize arbitrary EC addresses, unknown IOCTLs or unverified ACPI methods.

## Provider classes

### Windows APIs

Preferred whenever Windows already exposes the feature.

Current examples:

- Windows power mode;
- internal display refresh rate;
- internal display brightness;
- adaptive brightness where supported;
- battery energy/rate/health data;
- generic system identity.

These are the broadest providers and can work outside Lenovo hardware too.

## Lenovo keyboard backlight

ThinkControl currently knows two Lenovo PM driver contract families.

### `IBMPmDrv`

Typical family: **ThinkPad**.

The provider is accepted only if the device opens and its GET operation returns one of the known Off / Low / High states. A SET is reported as successful only when a later GET returns the requested state.

### `EnergyDrv`

Typical families include **ThinkBook / IdeaPad / Yoga / LOQ-style Lenovo platforms**.

Two established return encodings are probed independently. Unknown return values fail the probe, so a new or incompatible EnergyDrv contract does not become writable automatically.

The write payloads are shared by the known variants and still require readback verification.

### Lenovo Vantage keyboard component

An installed Lenovo Vantage / Commercial Vantage keyboard add-in is a useful future fallback when a direct PM-driver contract is unavailable. ThinkControl may use an installed official Lenovo component only after its exact invocation and readback behavior are implemented and tested.

## Fan telemetry

Read-only fan telemetry can be much broader than fan control.

ThinkControl currently probes:

1. verified X9 EC tachometer on the exact X9 profile;
2. Lenovo `LENOVO_FAN_METHOD` and `Fan_GetCurrentFanSpeed` when exposed;
3. Lenovo desktop-style `Lenovo_DT_GetCPUFan` / `Lenovo_DT_GetSYSFan` classes;
4. Windows `CIM_Tachometer`;
5. other trustworthy sensor libraries where they expose a real fan sensor.

Returned RPM must be plausible. A missing class/method is a normal compatibility result, not an error.

## Fan control

Fan **writes** are deliberately much stricter than fan telemetry.

### ThinkPad X9-15 Gen 1

The exact `21Q6 / 21Q7` profile has a researched classic ThinkPad EC backend:

```text
control register  0x2F
RPM low/high       0x84 / 0x85
Lenovo Auto        0x80
manual levels      1-7
```

`0x00` and the unverified `0x40` override family are never exposed as normal controls.

### Other Lenovo devices

Family profiles do **not** inherit the X9 EC backend.

A future writable provider can be added when one of these is true:

- Lenovo WMI/firmware explicitly advertises a supported control capability and its state can be read back;
- an exact machine/family contract is independently established and safely validated;
- an exact device profile is added after physical testing.

## `LENOVO_GAMEZONE_DATA`

This Lenovo WMI class is used across Legion/LOQ and some adjacent Lenovo platforms. Known methods in the ecosystem include capability queries and Smart Fan / thermal mode reads and writes.

ThinkControl treats it as **self-advertising vendor WMI**:

1. class must exist;
2. the relevant `IsSupport...` method must report support where one exists;
3. current state must be readable;
4. requested state must be one of the known enum values for that contract;
5. state should be read back after a write whenever possible.

Its presence does not imply that every method is appropriate for every Lenovo family.

## Lenovo Intelligent Thermal Solution / LITS

The verified X9 research observed:

```text
service      LITSSVC
ACPI device  ACPI\LEN0100
Vantage      ThinkSmartSenseAddin
IPC          \\.\pipe\com.lenovo.its.pipe.setting
```

This is a thermal-policy surface, not proof of direct PWM control.

ThinkControl should prefer Windows power mode or a well-understood Lenovo policy provider over attempting to manipulate private firmware internals.

## Profile families

### ThinkPad — Beta / Untested except verified exact profiles

Provider candidates:

- Windows APIs;
- `IBMPmDrv` keyboard contract;
- Lenovo/Vantage keyboard provider when implemented;
- Lenovo read-only fan WMI/CIM;
- LITS when a specific thermal-policy contract is understood.

Direct EC fan control requires an exact verified profile.

### ThinkBook — Beta / Untested

Provider candidates:

- Windows APIs;
- `EnergyDrv` keyboard contract;
- Lenovo fan WMI/CIM;
- `LENOVO_GAMEZONE_DATA` only when present and the capability self-reports support.

### Yoga — Beta / Untested

Provider candidates:

- Windows display/power/battery APIs;
- either Lenovo keyboard PM driver contract when its read probe succeeds;
- Lenovo fan telemetry when exposed;
- vendor WMI only after capability verification.

### IdeaPad — Beta / Untested

Provider candidates:

- Windows APIs;
- `EnergyDrv` keyboard contract;
- Lenovo fan telemetry;
- GameZone-style WMI only on models that actually expose it.

### LOQ / Legion — Beta / Untested

Provider candidates:

- Windows APIs;
- Lenovo GameZone WMI capability layer;
- `EnergyDrv` where applicable;
- Lenovo fan telemetry methods;
- device-specific advanced features only after their capability query confirms support.

## Confidence shown to the user

ThinkControl distinguishes:

- **Verified** — physically validated ThinkControl profile;
- **Beta / Untested** — recognized Lenovo family and known provider candidates, but the exact machine has not been validated by this project;
- **Generic** — only generic safe providers are assumed.

A capability can still be available on a Beta device. The confidence label describes validation depth, not an artificial feature lock.

## Adding support without an X9-scale reverse engineering effort

Most new laptops should only require:

- machine identity;
- service/driver inventory;
- WMI class/method inventory;
- safe read probes;
- one or two reversible readback tests for writable controls.

Use deep ACPI dumps, Procmon traces or binary-driver investigation only when established Lenovo provider families do not explain the hardware behavior.

The X9 research remains the reference for how to document evidence and safety decisions: [research/x9-15-gen1.md](research/x9-15-gen1.md).
