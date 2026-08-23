# Dependencies

ThinkControl `v0.1.0-alpha.1` uses a self-contained Windows installer. Most features should remain usable when an optional device-specific provider is missing.

## Dependency matrix

| Component | Purpose | Current behavior |
| --- | --- | --- |
| .NET runtime | WPF UI and managed service | Bundled with the installer |
| ThinkControl Service | Privileged hardware operations | Installed, started and removed by ThinkControl Setup |
| PawnIO | Current X9 EC access | Not installed automatically in alpha.1 |
| LibreHardwareMonitorLib | Sensor access and supporting hardware transport | Packaged with ThinkControl |
| Lenovo Power Management / `IBMPmDrv` | ThinkPad keyboard functions | Used when present and validated |
| Lenovo Intelligent Thermal Solution | Lenovo thermal policy | Detected for research; not required for current Windows power modes |
| Intel Innovation Platform Framework | OEM platform policy | Left under vendor control |
| Lenovo Vantage | OEM settings and maintenance | Optional |
| Lenovo Service Bridge | Lenovo support-site identification | Optional |

## .NET packaging

The UI and service are published as self-contained `win-x64` .NET 10 applications. Users do not need to install the .NET SDK or Desktop Runtime separately.

A future framework-dependent installer may reduce download size, but that is not the current release model.

## PawnIO

PawnIO is currently required for direct X9 EC fan access.

In `alpha.1`:

- ThinkControl does not bundle the PawnIO installer;
- setup does not download it automatically;
- missing PawnIO limits the X9 EC fan backend rather than blocking the application;
- Windows-level display, battery and other independent features continue to work.

Before automated PawnIO installation is added, the installer must use a pinned accepted version, verify the downloaded package, clearly identify that a kernel hardware-access driver is being installed and report restart requirements separately from failures.

ThinkControl should not automatically uninstall PawnIO because another application may also use it.

## Lenovo and Intel software

ThinkControl does not redistribute Lenovo or Intel platform software. Installed OEM providers may be used when their contract is understood and validated.

ThinkControl should:

- detect missing providers and report the resulting limitation;
- link to official acquisition paths where useful;
- avoid mirroring loosely matched device drivers;
- avoid automatic OEM-driver downgrades.

Lenovo Vantage and Lenovo Service Bridge are not required for ThinkControl to launch.

## Capability readiness

Readiness is reported per feature. For example:

```text
Display refresh       Ready
Battery telemetry     Ready
CPU temperature       Ready
Fan EC access         PawnIO unavailable
Keyboard backlight    Lenovo PM provider unavailable
```

The application should explain why a capability is unavailable instead of leaving a control enabled when it cannot work.

## Release package

The GitHub Actions packaging workflow:

1. reads the version from `version.json`;
2. builds and publishes the UI and service;
3. creates the Inno Setup installer;
4. smoke-tests installation and service startup;
5. smoke-tests uninstall and service removal;
6. generates `SHA256SUMS.txt`;
7. publishes the installer and checksum for a tagged release.

Authenticode signing is a separate release-hardening step and should not be implied when no signing certificate is configured.

## Reboots

The self-contained ThinkControl package does not normally require a reboot by itself. A hardware prerequisite may require one, and that state should be reported explicitly if prerequisite installation is automated later.

## Uninstall

The ThinkControl uninstaller removes application files and service registration owned by ThinkControl. It does not remove Lenovo or Intel platform software, and it does not remove PawnIO.

User settings and diagnostics are stored separately in the user profile.

See [installer/README.md](../installer/README.md) for installer details.
