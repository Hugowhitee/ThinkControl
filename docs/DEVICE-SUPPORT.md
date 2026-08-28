# Device support

This document describes compatibility for ThinkControl `v0.1.0-alpha.31`.

ThinkControl evaluates support **per capability and provider**. The current physically reviewed low-level reference is Lenovo ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`), but that model is not the product boundary. Windows-safe functionality and future OEM providers share the same capability-first UI.

## Support levels

### Verified

The relevant low-level provider/capability contract has been explicitly authorized and physically reviewed for the exact scope.

Current verified reference:

- Lenovo ThinkPad X9-15 Gen 1, machine type `21Q6` or `21Q7`.

“Verified” applies to the reviewed capabilities/provider path; it does not mean every optional Windows/OEM feature is guaranteed on every driver/BIOS revision.

### Beta / candidate

ThinkControl recognizes an OEM/family/provider combination that is reasonable to probe, but the exact hardware has not completed physical validation. Read-only telemetry can be exposed when the provider identifies it honestly. Writable controls still require the provider-specific identity/readback/safety contract.

### Generic

No OEM-specific profile is required. Windows-level features work where Windows exposes the relevant documented interface.

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

Future ASUS, Dell, HP, Acer, MSI and other integrations should use the same hierarchy rather than create vendor-specific copies of the main UI. See [devices/README.md](../devices/README.md).

Profiles select/prioritize providers; providers own implementation and write safety. Profile metadata alone is never permission to perform arbitrary low-level writes.

## Compatibility matrix

| Device scope | Windows-safe features | Sensors / fan telemetry | Keyboard | Fan writes | OEM thermal policy | Status |
| --- | --- | --- | --- | --- | --- | --- |
| ThinkPad X9-15 Gen 1 `21Q6/21Q7` | Supported where Windows exposes them | Generic real sensor providers plus verified X9 fallback paths | Reviewed Lenovo providers with read/probe contract | Verified discrete X9 provider, firmware Auto fallback | Reviewed X9 Lenovo semantic policy coordination | Verified reference |
| Other ThinkPads | Supported | Generic/read-only Lenovo providers when exposed | Known Lenovo providers after probe | Requires verified provider/family/model contract | Capability-specific only | Beta/candidate |
| ThinkBook / Yoga / IdeaPad | Supported | Generic/read-only providers | Known Lenovo providers after probe | Requires verified provider/family/model contract | Capability-specific only | Beta/candidate |
| Legion / LOQ | Supported | Generic/supported Lenovo telemetry providers | Provider-dependent | Provider-specific only when verified | Capability-specific only | Beta/candidate |
| Other Lenovo | Supported | Conservative read-only discovery | Provider discovery only | Disabled without verified write provider | No X9 semantic-command reuse | Beta/candidate |
| Other Windows laptops | Where Windows supports it | Generic safe sensor providers when available | OEM provider required for hardware backlight | OEM/family/model write provider required | OEM provider required | Generic / expandable |

## Windows-level baseline

These can work without a Lenovo/model-specific profile when Windows exposes the necessary interface:

- power behavior (**Efficiency / Balanced / Performance** in ThinkControl);
- separate stored battery and plugged-in power preferences;
- display refresh-rate selection/automatic policy;
- internal display brightness and adaptive brightness;
- Windows display/power/sleep/Night light navigation;
- system output, microphone and volume controls;
- battery percentage/source/watts/Wh/health and filtered time estimates;
- local charge/discharge history;
- compatible read-only sensor/temperature telemetry;
- compatible Precision Touchpad visualization/edge gestures;
- themes, tray/startup behavior, updates and local diagnostics.

Unavailable data is shown as unavailable rather than replaced with a synthetic value.

## Sensors and fan telemetry

ThinkControl prefers provider-reported hardware identity and real sensor domains. LibreHardwareMonitor/PawnIO is one broad Windows provider route, not a vendor lock-in.

A generic ACPI thermal zone is not automatically relabelled as CPU Package. Exact/family read-only fallbacks remain honestly sourced and become a cooling control-temperature input only when the provider safety contract permits it.

Read-only fan RPM may come from a real generic sensor provider, a verified model-specific telemetry fallback or an OEM/Windows telemetry interface. Missing channels/provider classes are valid compatibility outcomes; ThinkControl never fabricates RPM or separate Fan 1/Fan 2 identity merely because the chassis physically contains two fans.

## ThinkPad X9-15 Gen 1

The verified low-level X9 provider is restricted to machine types `21Q6` and `21Q7` and exposes a **discrete** fan-output model plus firmware Auto ownership. The user-facing cooling supervisor can map curve/percentage targets onto verified/calibrated discrete states; the UI does not imply continuous PWM.

Key product-level invariants:

- firmware Auto is the safe handoff/fallback state;
- arbitrary EC writes and fan-off/unverified override paths are not exposed;
- control-temperature/provider loss and high-temperature safety hand ownership back to firmware;
- temporary manual tests restore the previous cooling profile with Auto fallback;
- seven-state calibration requires real tachometer telemetry, validates a complete candidate before replacement and never persists a partial failed/cancelled run;
- raw EC/calibration controls are shown only while the verified X9 provider plus required capabilities are active;
- power preferences use Windows power behavior plus reviewed X9 semantic thermal-policy coordination for the exact X9 scope;
- keyboard hardware control uses reviewed Lenovo provider paths and shares serialized ownership with user-session effects.

Concrete X9 transport/register evidence is intentionally maintained in [Lenovo Providers](LENOVO-PROVIDERS.md), [Hardware Safety](HARDWARE-SAFETY.md) and [X9 research](research/x9-15-gen1.md) rather than copied into every support/product document.

## Precision Touchpad and haptics

Precision Touchpad input is a Windows/user-session capability. Compatible devices can use live visualization and precision edge gestures independent of Lenovo device support.

Optional top-corner launch lanes are separate from the four precision edge bindings and use one shared physical geometry for UI/recognition. Track center Play/Pause is also separately capability/configuration gated.

Haptic settings remain granular: if Windows/provider support for one haptic/click-force setting is missing, ThinkControl disables/explains that setting rather than claiming the whole Precision Touchpad is absent.

## Dolby / audio

Normal Windows audio controls are generic. Dolby controls depend on the installed DAX provider, not laptop brand alone. Direct controls appear only when semantic operations can be verified; otherwise ThinkControl can keep normal Windows audio usable and, where appropriate, open official Dolby Access rather than guess private IDs.

## Adding another OEM/model

Support should normally be added in this order:

1. reuse Windows-safe capabilities;
2. add/read an OEM-generic provider;
3. narrow behavior in a family profile when necessary;
4. add exact-model low-level writes only after physical validation and recovery/readback design.

Compatibility matching may use normal SMBIOS manufacturer/product/machine type/BIOS context, ACPI/PnP IDs and installed provider/service identities. Serial numbers, usernames, hostnames, MAC addresses and disk identifiers are not needed for support matching.

Use the [bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml) for compatibility issues. A prepared redacted ThinkControl support/device report can be attached when useful.
