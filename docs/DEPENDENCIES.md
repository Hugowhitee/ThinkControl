# Dependencies

ThinkControl `v0.1.0-alpha.1` uses a framework-dependent Windows package so the application does not carry duplicate copies of the .NET runtime. Optional hardware providers remain capability-scoped: a missing provider should limit only the feature that needs it.

## Dependency matrix

| Component | Purpose | Current behavior |
| --- | --- | --- |
| .NET 10 Desktop Runtime x64 | WPF UI and managed service | Required; setup downloads pinned `10.0.10` only when missing |
| ThinkControl Service | Privileged hardware operations | Installed, started and removed by ThinkControl Setup |
| PawnIO 2.2.0 | Verified X9 EC fan access | Offered automatically on detected X9 `21Q6/21Q7`; downloaded only when selected and missing |
| LibreHardwareMonitorLib | Sensor access and supporting hardware transport | Packaged with ThinkControl |
| Lenovo Power Management / `IBMPmDrv` | ThinkPad keyboard functions | Used only when a known read contract succeeds |
| `EnergyDrv` | Lenovo keyboard functions on supported families | Used only when a known read contract succeeds |
| Lenovo Vantage ThinkKeyboard component | Keyboard fallback | Reuses the installed Lenovo component when its read/write path validates |
| Lenovo Intelligent Thermal Solution | Lenovo thermal policy | Left installed and vendor-owned; ThinkControl coordinates through established power-mode surfaces |
| Intel Innovation Platform Framework | OEM platform policy | Left under vendor control |
| Lenovo Vantage / Commercial Vantage | OEM settings and maintenance | Optional; ThinkControl can launch the installed app directly |

## .NET packaging

The UI and service are published as framework-dependent `win-x64` .NET 10 applications. This avoids the previous alpha package layout in which both processes carried their own self-contained runtime.

Setup checks `Microsoft.WindowsDesktop.App` before installing ThinkControl. If a compatible .NET 10 Desktop Runtime is absent, it downloads the pinned Microsoft x64 runtime, verifies its SHA-256 and runs it silently. The SDK is never required on an end-user machine.

Current pin:

```text
Version  10.0.10
File     windowsdesktop-runtime-10.0.10-win-x64.exe
SHA-256  E82FC901C8F52D716293B2BC0830CE0DD254A06268C457A19E8FC503560A84D1
```

If the verified runtime cannot be downloaded or installed, setup stops instead of leaving a non-starting ThinkControl installation behind.

## PawnIO

PawnIO is required only for the direct EC backend used by the verified ThinkPad X9-15 Gen 1 profile.

For machine types `21Q6` and `21Q7`, setup exposes an **X9 hardware access** task. When selected and PawnIO is not already installed, setup downloads the official PawnIO `2.2.0` installer and verifies the exact release asset before running it.

```text
Version  2.2.0
File     PawnIO_setup.exe
SHA-256  1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032
Silent   -install -silent
```

The URL, hash and silent switch match the `namazso.PawnIO` `2.2.0` manifest in Microsoft's `winget-pkgs` repository.

PawnIO remains a shared machine dependency. ThinkControl does not remove it during uninstall because another application may use it. If PawnIO installation fails, ThinkControl still installs; only the X9 EC fan capability can remain unavailable.

## Lenovo and Intel software

ThinkControl does not redistribute Lenovo or Intel platform software. Installed OEM providers may be used only when their contract is understood and validated.

ThinkControl should:

- report which provider backs each capability;
- keep Windows-level features available when an unrelated Lenovo provider is missing;
- avoid mirroring loosely matched device drivers;
- avoid automatic OEM-driver downgrades;
- use installed Vantage components only through validated local interfaces.

## Capability readiness

Readiness is per feature, for example:

```text
Display refresh       Ready · Windows
Battery telemetry     Ready · Windows/ACPI
CPU temperature       Ready · sensor provider
Fan EC access         PawnIO unavailable
Keyboard backlight    Lenovo provider unavailable
```

An unavailable capability stays disabled with an explanation rather than pretending a backend exists.

## Release package

The package workflow:

1. reads `version.json`;
2. builds the solution;
3. publishes framework-dependent UI and service payloads;
4. fails if the combined payload exceeds the size budget;
5. builds the Inno Setup bootstrap installer;
6. fails if the installer exceeds its size budget;
7. smoke-tests install and service startup;
8. smoke-tests uninstall and service removal;
9. generates `SHA256SUMS.txt`;
10. publishes only tagged release builds.

Authenticode signing is a separate hardening step and is not implied when no signing certificate is configured.

## Reboots

ThinkControl itself does not normally require a reboot. A prerequisite installer can request one; setup preserves that state instead of treating it as a generic failure.

## Uninstall

The ThinkControl uninstaller removes application files and `ThinkControlService`. It does not remove PawnIO or Lenovo/Intel platform software. User settings and local diagnostics remain under the user profile unless the user deletes them separately.

See [installer/README.md](../installer/README.md) for the complete setup flow.