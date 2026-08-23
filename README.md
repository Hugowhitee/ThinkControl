# ThinkControl

**A lightweight hardware companion for Lenovo ThinkPads.**

ThinkControl is a small Windows tray utility for fast, trustworthy control of everyday ThinkPad hardware features. The ThinkPad X9-15 Gen 1 (21Q6/21Q7) is the first reference machine, but the codebase is designed around capability detection and verified device profiles rather than model-name assumptions.

## Product principles

- **Capabilities, not assumptions.** Features appear only when a verified backend exists.
- **Safe by default.** Unknown ThinkPads get Windows-level read-only/safe features; unverified hardware writes stay disabled.
- **Small daily-driver UI.** Quiet / Balanced / Performance, temperature, real RPM, display, keyboard and battery status should be fast to reach.
- **Privilege separation.** The WPF UI runs as the user. A small privileged service owns verified low-level hardware operations.
- **One-install experience.** Required runtime and verified device-conditional hardware access are handled by the bootstrapper instead of a manual dependency checklist.
- **Fail back to Lenovo.** Direct fan control must return to Lenovo Auto on failure, service stop, sleep/hibernate and shutdown.
- **No fake precision.** The X9 fan interface exposes discrete EC levels, not continuous 0-100% PWM.

## Repository status

The repository is currently in the **v0.1 foundation phase**. Active EC writes remain intentionally disabled while device identity, dependency readiness, capability resolution, service IPC and read-only telemetry foundations are established.

See:

- `docs/PRODUCT.md` — product scope and roadmap
- `docs/ARCHITECTURE.md` — process, privilege and provider architecture
- `docs/HARDWARE-SAFETY.md` — mandatory hardware-write safety rules
- `docs/DEVICE-SUPPORT.md` — capability and device-profile model
- `docs/DEPENDENCIES.md` — runtime, PawnIO and OEM software policy
- `docs/DESIGN.md` — compact popup and Advanced UI rules
- `docs/V0.1-ACCEPTANCE.md` — explicit v0.1 completion criteria
- `docs/research/x9-15-gen1.md` — preserved X9 hardware research
- `installer/README.md` — one-file bootstrap installer behavior

## Planned structure

```text
ThinkControl/
  src/
    ThinkControl.UI/
    ThinkControl.Service/
    ThinkControl.Core/
    ThinkControl.Hardware/
    ThinkControl.DeviceProfiles/
  devices/
    Lenovo/ThinkPad/X9-15-Gen1/
  docs/
    research/
  installer/
  tests/
  .github/workflows/
```

## Technology baseline

- Windows desktop app: WPF
- Runtime: .NET 10 LTS
- Hardware privilege boundary: Windows service
- Local IPC: Windows-authenticated named pipe
- Low-level access, when required and verified: PawnIO rather than a custom kernel driver
- Distribution goal: small native bootstrap installer + downloaded app payload
- Dependency behavior: Windows-level features remain usable when optional/device-conditional hardware access is absent
- Updates: GitHub Releases, explicit user action, no permanent updater service

## Dependency philosophy

A normal user should install **one ThinkControl setup file**.

The bootstrapper owns required runtime setup. PawnIO is offered only when the verified device profile has low-level capabilities that need it. Lenovo/Intel platform drivers are detected and diagnosed, but remain serviced through official Lenovo/Microsoft channels. Vantage and Lenovo Service Bridge are optional integrations rather than hard dependencies.

## Visual direction

The compact UI follows a dense utility hierarchy: device + trustworthy telemetry in the header, a small 60-second temperature sparkline, Quiet/Balanced/Performance as the primary control, then display/brightness/keyboard quick controls. The production UI should be flatter and calmer than the early dark mockup: minimal texture, restrained accent, 1 px separators and one consistent Windows-friendly icon set.

## Reference repositories

`Hugowhitee/X9-Helper` and `Hugowhitee/Thinkpad_Fancontrol` are research sources only. ThinkControl does not inherit their architecture by default.
