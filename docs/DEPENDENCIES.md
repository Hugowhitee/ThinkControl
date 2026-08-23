# Dependencies

ThinkControl `v0.1.0-alpha.2` uses a small web bootstrapper and a separate framework-dependent application payload. Most features remain usable when an optional device-specific provider is missing.

## Dependency matrix

| Component | Purpose | Current behavior |
| --- | --- | --- |
| .NET 10 Desktop Runtime x64 | WPF UI and managed service | Checked by Setup; pinned Microsoft runtime downloaded only when missing |
| ThinkControl payload | UI, service and managed libraries | Downloaded from the matching GitHub release and SHA-256 verified before extraction |
| ThinkControl Service | Privileged hardware operations | Installed, started and removed by ThinkControl Setup |
| PawnIO 2.2.0 | Verified X9 EC fan access | Offered only on detected X9 `21Q6/21Q7`; pinned URL and SHA-256 |
| LibreHardwareMonitorLib | Sensor access and supporting hardware transport | Packaged in the ThinkControl payload |
| Lenovo Power Management / `IBMPmDrv` | ThinkPad keyboard functions | Used when present and validated |
| Lenovo `EnergyDrv` | Additional Lenovo keyboard contract | Used only through the established read/readback contract |
| Lenovo Intelligent Thermal Solution | X9 thermal policy | Verified X9 policy commands coordinated through `LITSSvc` |
| Intel Innovation Platform Framework | Windows/OEM platform policy | Left under vendor control |
| Lenovo Vantage | OEM settings and maintenance | Optional; installed app can be launched directly from ThinkControl |
| Lenovo Service Bridge | Lenovo support-site identification | Optional |

## Web bootstrap packaging

The release contains three distribution assets:

```text
ThinkControl-Setup-0.1.0-alpha.2.exe
ThinkControl-Payload-0.1.0-alpha.2.zip
SHA256SUMS.txt
```

The Setup executable does not contain the application payload. Its build embeds the exact URL and SHA-256 for the matching payload ZIP. Setup downloads that payload from the same GitHub release and verifies it before extracting `ui/` and `service/` under Program Files.

Package CI enforces separate budgets for the uncompressed managed payload, the compressed release payload and the web bootstrap executable. The bootstrap executable has a 5 MB hard ceiling so runtime/application files cannot silently creep back into Setup.

## .NET runtime

UI and service are published as framework-dependent `win-x64` .NET 10 applications. This prevents UI and service from each carrying their own copy of the runtime.

Setup checks `%ProgramFiles%\dotnet\shared\Microsoft.WindowsDesktop.App\10.*`. If a compatible runtime is absent, Setup downloads the pinned Microsoft x64 Desktop Runtime and verifies its SHA-256 before execution. The SDK is never required on an end-user machine.

## PawnIO

PawnIO is required only for the verified X9 direct EC fan backend.

For `v0.1.0-alpha.2`:

- Setup reads Lenovo SMBIOS identity locally;
- the PawnIO task is shown only for X9 machine types `21Q6` or `21Q7`;
- the exact PawnIO 2.2.0 release URL is pinned;
- the installer SHA-256 matches the published Microsoft Winget manifest;
- a PawnIO failure limits X9 EC fan RPM/manual control but does not block independent Windows/Lenovo features;
- ThinkControl does not uninstall shared PawnIO because another application may also use it.

## Lenovo thermal policy

The verified X9 profile can coordinate Windows power mode with the observed Lenovo Intelligent Cooling named-pipe contract:

```text
AC: 502 Eco, 503 Balanced, 504 Performance
DC: 507 Eco, 508 Balanced, 509 Performance
```

Those commands are gated to X9 `21Q6/21Q7`. They are thermal-policy commands, not arbitrary fan PWM/RPM commands, and are never applied generically to unknown Lenovo models.

## Lenovo and Intel software

ThinkControl does not redistribute Lenovo or Intel platform software. Installed OEM providers may be used when their contract is understood and validated.

ThinkControl should detect missing providers and report the resulting limitation, avoid mirroring loosely matched device drivers, and avoid automatic OEM-driver downgrades.

Lenovo Vantage and Lenovo Service Bridge are not required for ThinkControl to launch.

## Capability readiness

Readiness is reported per feature. For example:

```text
Display refresh       Ready
Battery telemetry     Ready
CPU temperature       Ready
Fan EC access         PawnIO unavailable
Keyboard backlight    Lenovo PM provider unavailable
Lenovo thermal policy LITSSvc unavailable
```

The application should explain why a capability is unavailable instead of leaving a control enabled when it cannot work.

## Release validation

The GitHub Actions packaging workflow:

1. reads the version from `version.json`;
2. verifies canonical v3 branding assets and rejects legacy WPF TC geometry;
3. builds and publishes framework-dependent UI/service outputs;
4. creates and hashes the external payload ZIP;
5. builds the small Inno Setup web bootstrapper with the exact payload URL/hash;
6. smoke-tests Setup using the same payload through a local validation override;
7. confirms `ThinkControlService` reaches Running;
8. uninstalls and confirms service/files are removed;
9. generates checksums for both installer and payload;
10. publishes installer, payload and checksums for a tagged release.

Authenticode signing remains a separate release-hardening step and is not implied when no signing certificate is configured.

## Uninstall

The ThinkControl uninstaller removes UI/service files and unregisters `ThinkControlService`. It does not remove Lenovo or Intel platform software and does not remove shared PawnIO.

User settings and diagnostics are stored separately in the user profile.

See [installer/README.md](../installer/README.md) for installer details.
