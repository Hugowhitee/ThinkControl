# ThinkControl documentation

This directory contains product, hardware and development documentation for ThinkControl.

For installation, start with the main [README](../README.md) and the [Releases](https://github.com/Hugowhitee/ThinkControl/releases) page.

## Documentation index

| Document | Purpose |
| --- | --- |
| [Device Support](DEVICE-SUPPORT.md) | Current device/capability support and validation levels |
| [Device Profile Hierarchy](../devices/README.md) | Generic → OEM → family → model profile architecture |
| [Lenovo Providers](LENOVO-PROVIDERS.md) | Current Lenovo driver, WMI and hardware-provider research |
| [Hardware Safety](HARDWARE-SAFETY.md) | Rules for privileged and low-level hardware access |
| [Architecture](ARCHITECTURE.md) | Project boundaries, service architecture and IPC |
| [Product Specification](PRODUCT.md) | Current product behavior and scope |
| [Design System](DESIGN.md) | UI layout, typography, controls and visual rules |
| [UI Editing](UI_EDITING.md) | Editing WPF/XAML with Visual Studio and Blend |
| [Diagnostics and Privacy](DIAGNOSTICS.md) | Local diagnostics, redaction and support bundles |
| [Dependencies](DEPENDENCIES.md) | Runtime and provider dependencies |
| [Alpha Testing](ALPHA-TESTING.md) | Physical validation on the current X9 reference device |
| [Release Checklist](RELEASE-CHECKLIST.md) | Packaging and release checks |
| [v0.1 Acceptance](V0.1-ACCEPTANCE.md) | Historical acceptance criteria for the first alpha |

## Compatibility terminology

### Verified

The relevant low-level provider/capability has been physically reviewed for the exact device scope. The current reference device is the ThinkPad X9-15 Gen 1 with machine type `21Q6` or `21Q7`.

### Beta

ThinkControl recognizes an OEM or product family and has providers that are reasonable to probe, but the exact model has not been fully validated. Individual controls still require a successful capability/readback check.

### Generic

Only vendor-neutral/platform-safe capabilities are assumed. Windows-level features may work without an OEM-specific profile.

These labels describe validation depth. They do not grant low-level hardware access by themselves.

## Device profiles

ThinkControl uses one profile pattern for every OEM:

```text
devices/
  <OEM>/
    _generic/
    <Family>/
      _family/
      <Model>/
```

The existing Lenovo profiles are the first implementation of that layout. Future ASUS, Dell, HP, Acer, MSI and other provider families should use the same hierarchy rather than adding vendor-specific product shells.

Profiles identify provider candidates. Hardware implementations, readback and write allowlists stay in provider code, and exact registers or write payloads are never enabled merely by remote metadata.

See [`devices/README.md`](../devices/README.md) for the full rules.

## Device research

The detailed X9 research record is available at [research/x9-15-gen1.md](research/x9-15-gen1.md). It documents the evidence used to establish the first verified low-level provider.

Most new devices should first reuse Windows-safe capabilities and existing OEM/family providers. Useful validation data normally includes exact product identity, relevant OEM drivers/services, provider availability, plausible read-only telemetry and readback results for reversible controls.

Use the [bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml) for compatibility reports. An exported ThinkControl support bundle can be attached when needed.
