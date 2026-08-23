# ThinkControl installer

ThinkControl `v0.1.0-alpha.3` uses a small x64 Inno Setup web bootstrapper plus a separate SHA-256-pinned application payload.

## Release assets

```text
ThinkControl-Setup-0.1.0-alpha.3.exe
ThinkControl-Payload-0.1.0-alpha.3.zip
SHA256SUMS.txt
```

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
9. offers **Launch ThinkControl** on the completion page.

The installer is deliberately device-neutral. It does not install Lenovo or X9-specific low-level providers.

After first launch, ThinkControl's in-app **Hardware Setup** checks the service and only offers an additional verified hardware prerequisite when the detected device profile actually requires it. The verified X9 `21Q6/21Q7` EC provider currently uses pinned PawnIO 2.2.0. Other laptops are not offered PawnIO merely because they are manufactured by Lenovo.

## ThinkControl payload verification

The packaging workflow creates the application payload first, computes its SHA-256, and then compiles both the deterministic GitHub release URL and that hash into Setup.

A public release installer therefore downloads only its own matching payload. A payload from another build/version cannot pass the embedded hash check.

For CI only, Setup accepts a `/PAYLOAD=<local zip>` override. The local payload must still match the compile-time SHA-256. This lets CI exercise the complete extraction, service and uninstall path before a GitHub release exists without weakening the public installer path.

Windows 11's built-in `tar.exe` performs ZIP extraction. Setup verifies that both `ui/ThinkControl.UI.exe` and `service/ThinkControl.Service.exe` exist before continuing.

## Microsoft .NET Desktop Runtime pin

```text
Version  10.0.10
Arch     x64
File     windowsdesktop-runtime-10.0.10-win-x64.exe
Source   https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.10/windowsdesktop-runtime-10.0.10-win-x64.exe
SHA-256  E82FC901C8F52D716293B2BC0830CE0DD254A06268C457A19E8FC503560A84D1
```

## Device-specific hardware setup

Low-level device prerequisites are owned by the application, not by the generic installer. Hardware Setup can:

- verify whether `ThinkControlService` is installed and running;
- repair the ThinkControl service registration with UAC when required;
- determine whether the validated device profile needs an additional hardware provider;
- download only a pinned provider build and verify its SHA-256 before starting its installer.

For the verified X9 direct EC backend, the current PawnIO pin is:

```text
Version  2.2.0
File     PawnIO_setup.exe
Source   https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe
SHA-256  1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032
Mode     -install -silent
```

Failure of a device-specific provider does not make unrelated Windows features unavailable. ThinkControl keeps those provider states separate and disables only the hardware capability that failed validation.

## Upgrade behavior

Running a newer ThinkControl Setup over an existing installation is supported. Before replacing the payload, Setup stops `ThinkControlService`; normal controller disposal attempts to return an active manual verified X9 fan level to Lenovo Auto. Existing `ui/` and `service/` payload directories are then replaced and the service registration is updated and restarted.

Inno Setup's application-closing flow handles a running ThinkControl tray process instead of failing immediately with a generic currently-running message.

## Icons and shortcuts

The canonical v3 application icon is used for Setup, `ThinkControl.UI.exe`, Start menu, optional desktop shortcut, Add/Remove Programs and the Advanced window. The notification-area icon uses the canonical v3 mark.

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

A release is published only from a version marked `releaseReady` after the validated `main` CI succeeds. The tagged packaging run publishes Setup, payload and `SHA256SUMS.txt` together.

## Uninstall

The uninstaller stops and removes `ThinkControlService` and deletes ThinkControl-owned UI and service payload directories. It does not remove shared hardware providers, Lenovo software or Intel platform components.
