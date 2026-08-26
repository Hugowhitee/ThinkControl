# ThinkControl installer

ThinkControl `v0.1.0-alpha.19` uses a small x64 Inno Setup web bootstrapper plus a separate SHA-256-pinned application payload.

## Release assets

```text
ThinkControl-Setup-0.1.0-alpha.19.exe
ThinkControl-Payload-0.1.0-alpha.19.zip
SHA256SUMS.txt
```

Release CI also renders selected WPF Compact/Home/Touchpad/System/Sensor-detail previews as validation evidence. Public GitHub Releases intentionally contain only Setup, Payload and `SHA256SUMS.txt`.

The Setup executable does not embed the ThinkControl UI/service payload or a duplicate .NET runtime. The matching payload is downloaded from the same GitHub release and verified before extraction.

## Installation flow

Setup:

1. requests administrator elevation;
2. checks for a compatible .NET 10 Desktop Runtime;
3. downloads the pinned official Microsoft x64 Desktop Runtime only when missing and verifies its SHA-256;
4. downloads `ThinkControl-Payload-<version>.zip` from the matching GitHub release;
5. verifies the payload against the SHA-256 compiled into that Setup build;
6. extracts only the verified `ui/` and `service/` payload under Program Files;
7. registers and starts `ThinkControlService`;
8. creates Start menu and optional desktop shortcuts using the canonical v3 icon;
9. offers a default-enabled **Start ThinkControl with Windows** choice, which can be changed later in Settings;
10. offers **Launch ThinkControl** on first installation.

The installer itself stays device-neutral. Hardware/provider recovery happens after startup so one setup build can work across supported laptops.

## Required components after installation

ThinkControl's in-app **Inbox** checks providers independently and opens one focused prerequisite prompt instead of treating the whole laptop as supported/unsupported.

It can:

- verify and repair `ThinkControlService`;
- offer pinned PawnIO 2.2.0 when additional LibreHardwareMonitor sensor access is useful;
- retry telemetry/provider discovery after installation;
- point Lenovo laptops to Lenovo Vantage / Windows Update when the Lenovo keyboard/platform provider is missing.

Installing PawnIO on an unverified laptop only expands read-only discovery. It never authorizes unknown EC/PWM writes. Direct fan control remains restricted to reviewed device profiles such as the verified X9 `21Q6/21Q7` profile.

## ThinkControl payload verification

The packaging workflow creates the application payload first, computes its SHA-256, and then compiles both the deterministic GitHub release URL and that hash into Setup.

A public release installer therefore downloads only its own matching payload. A payload from another build/version cannot pass the embedded hash check.

For CI only, Setup accepts a `/PAYLOAD=<local zip>` override. The local payload must still match the compile-time SHA-256. This lets CI exercise the complete extraction, service and uninstall path before a GitHub release exists without weakening the public installer path.

The built-in `tar.exe` on supported Windows 10/11 systems performs ZIP extraction. Setup verifies that both `ui/ThinkControl.UI.exe` and `service/ThinkControl.Service.exe` exist before continuing.

## Microsoft .NET Desktop Runtime pin

```text
Version  10.0.10
Arch     x64
File     windowsdesktop-runtime-10.0.10-win-x64.exe
Source   https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.10/windowsdesktop-runtime-10.0.10-win-x64.exe
SHA-256  E82FC901C8F52D716293B2BC0830CE0DD254A06268C457A19E8FC503560A84D1
```

## PawnIO pin

```text
Version  2.2.0
File     PawnIO_setup.exe
Source   https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe
SHA-256  1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032
Mode     -install -silent
```

Failure of a device-specific provider does not make unrelated Windows features unavailable. ThinkControl keeps provider states separate and disables only the capability whose prerequisites or verification are missing.

## Upgrade behavior

Running a newer ThinkControl Setup over an existing installation is a first-class update path. Existing installs switch Setup into update wording, skip first-install shortcut choices and retain the normal install location.

Before replacing files, Setup closes a running ThinkControl tray/UI instance, stops `ThinkControlService`, replaces the verified payload and updates/restarts the service registration. Normal controller disposal attempts to hand an active verified fan override back to Lenovo Auto before low-level access closes.

The in-app updater downloads the matching installer plus `SHA256SUMS.txt`, verifies the installer and starts Setup with update/relaunch parameters. A silent in-app update can relaunch ThinkControl automatically afterward.

## Icons and shortcuts

The canonical v3 multi-resolution application icon is used for Setup, `ThinkControl.UI.exe`, Start menu, optional desktop shortcut, Add/Remove Programs and the Advanced window. The notification-area icon uses the same bold T/C design language with a runtime-optimized small-size mark and no stray status dot.

## Size budgets

Package CI enforces:

```text
Combined framework-dependent UI + service  <= 65 MB uncompressed
Compressed ThinkControl payload             <= 20 MB
ThinkControl web bootstrap installer         <= 5 MB
```

The practical installed-payload target remains much smaller than the hard ceiling. The 5 MB bootstrap ceiling prevents the application payload or .NET runtime from being silently embedded back into Setup.

## CI and release validation

Pull requests that change application, installer, branding or version code run the package workflow. It verifies branding, restores/builds the solution, publishes framework-dependent UI and service files, creates and hashes the external payload, builds the bootstrapper, installs it with the verified local payload override, waits for `ThinkControlService` to reach Running, uninstalls it and verifies cleanup.

Tagged release packaging repeats the validated path and publishes Setup, payload, `SHA256SUMS.txt` and the selected WPF previews in one immutable GitHub Release creation.

## Uninstall

The uninstaller stops and removes `ThinkControlService` and deletes ThinkControl-owned UI and service payload directories. It does not remove shared hardware providers, Lenovo software or Intel platform components.
