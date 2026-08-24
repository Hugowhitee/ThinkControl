# ThinkControl device profiles

ThinkControl is designed as a general Windows laptop-control application. Device profiles describe **which providers are reasonable to probe** for a laptop; they do not contain hardware-control implementations and they never authorize arbitrary low-level writes by themselves.

The current fully reviewed low-level reference is the ThinkPad X9-15 Gen 1, but the profile layout is intentionally vendor-neutral so additional OEMs can be added without changing the UI architecture.

## Hierarchy

Profiles are organized from broadest to most specific:

```text
devices/
  Lenovo/
    _generic/
      profile.json
    ThinkPad/
      _family/
        profile.json
      X9-15-Gen1/
        profile.json
    Yoga/
      _family/
        profile.json
    ...

  ASUS/
    _generic/
      profile.json
    Zenbook/
      _family/
        profile.json
    <model>/
      profile.json

  Dell/
  HP/
  Acer/
  MSI/
  ...
```

The same pattern should be used for every OEM:

1. **Windows generic baseline** — capabilities exposed safely by Windows itself. This belongs in shared product/provider code and does not require a vendor profile.
2. **OEM generic** — safe provider discovery common to an OEM, for example Lenovo WMI classes or an ASUS service that can be probed read-only.
3. **Family** — provider preferences shared by a product family such as ThinkPad, Legion, Zenbook or XPS.
4. **Model** — exact machine identifiers and any low-level contracts that have been physically reviewed for that hardware.

A more specific profile may narrow or add provider preferences, but it must not silently weaken a safety rule inherited from a broader scope.

## Capability-first design

The interface is organized around capabilities such as:

- fan telemetry;
- fan control;
- temperature/sensor telemetry;
- keyboard backlight;
- performance/thermal policy;
- display control;
- battery telemetry and charge protection;
- audio processing;
- touchpad/haptics.

The UI must not become a collection of vendor-specific pages. A Fans page asks the provider registry for a fan capability; the selected implementation may later be Windows, Lenovo, ASUS, Dell, HP or another verified provider.

## Provider ownership

Profiles are data. Providers are code.

A profile may say that `IBMPmDrv`, `EnergyDrv`, a vendor WMI class or another named provider is worth probing. The actual implementation, validation, lifecycle, error handling and write allowlist belong to the hardware/provider layer.

This separation is important for maintainability and for future optional provider modules: adding support for a new OEM should not require rewriting the product shell, telemetry UI or common control logic.

## Matching

Profile matching should use non-secret hardware identity such as:

- SMBIOS manufacturer and product/model name;
- machine type / model code when the OEM exposes one;
- BIOS/firmware version when compatibility depends on it;
- ACPI/PnP IDs;
- installed provider/service/driver identities.

Serial numbers, Windows usernames, MAC addresses and disk identifiers are not required for profile selection.

When multiple profiles match, the future resolver should compose them from broad to specific and expose the resolved chain for diagnostics, for example:

```text
Windows generic
→ Lenovo generic
→ ThinkPad family
→ X9-15 Gen 1 model
```

## Safety tiers

A profile/provider capability should resolve to one of these practical states:

- **Windows-safe** — supported through a documented Windows API;
- **Read-only probe** — real telemetry may be read, but no hardware write is authorized;
- **Probe + readback** — reversible control is enabled only when the provider passes a known read and verification contract;
- **Model-verified write** — low-level writes are allowed only for explicitly matched hardware and an allowlisted contract;
- **Unavailable** — the feature stays visible with an actionable compatibility/provider state.

Remote metadata or a newly added JSON profile must never be sufficient on its own to enable arbitrary EC, port-I/O or IOCTL writes.

## Adding another laptop

Prefer adding support in this order:

1. reuse Windows-safe capabilities;
2. reuse an existing OEM/family provider after read-only detection;
3. add a new provider contract only when an existing interface cannot provide the capability;
4. add model-specific writes last, after readback and physical validation.

Do not copy an X9 register, Lenovo IOCTL, ASUS command or other model-specific contract into a broad family profile merely because two laptops look similar.
