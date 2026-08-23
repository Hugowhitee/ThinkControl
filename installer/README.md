# ThinkControl installer

ThinkControl `v0.1.0-alpha.2` uses a small x64 Inno Setup **web bootstrapper** plus a separate SHA-256-pinned application payload.

## Release assets

```text
ThinkControl-Setup-0.1.0-alpha.2.exe
ThinkControl-Payload-0.1.0-alpha.2.zip
SHA256SUMS.txt
```

The Setup executable does not contain the ThinkControl UI/service payload or a bundled .NET runtime. The matching payload is downloaded from the same GitHub release and verified before extraction.

## Installation flow

Setup:

1. requests one administrator elevation;
2. checks for a compatible .NET 10 Desktop Runtime;
3. downloads the pinned official Microsoft x64 Desktop Runtime only when missing and verifies its SHA-256;
4. downloads `ThinkControl-Payload-<version>.zip` from the matching GitHub release;
5. verifies the payload against the SHA-256 compiled into that exact Setup build;
6. extracts only the verified `ui/` and `service/` payload under Program Files;
7. detects Lenovo machine type from local SMBIOS information;
8. on ThinkPad X9-15 Gen 1 `21Q6/21Q7`, offers **X9 hardware access (PawnIO 2.2.0)**;
9. downloads/verifies PawnIO only when selected and missing;
10. registers and starts `ThinkControlService`;
11. creates Start-menu and optional desktop shortcuts using the canonical v3 icon;
12. offers **Launch ThinkControl** on the completion page.

A normal user does not need to visit a .NET, PawnIO or payload download page manually.

## ThinkControl payload verification

The packaging workflow creates the application payload first, computes its SHA-256, and then compiles both the deterministic GitHub release URL and that hash into Setup.

A public release installer therefore downloads only its own matching payload. A payload from another build/version cannot pass the embedded hash check.

For CI only, Setup accepts a `/PAYLOAD=<local zip>` override. That local payload must still match the same compile-time SHA-256. This lets CI exercise the complete extraction/service/uninstall path before a GitHub release exists without weakening the public installer path.

Windows 11's built-in `tar.exe` performs ZIP extraction. Setup verifies that both `ui/ThinkControl.UI.exe` and `service/ThinkControl.Service.exe` exist before continuing.

## Verified prerequisite pins

### Microsoft .NET Desktop Runtime

```text
Version  10.0.10
Arch     x64
File     windowsdesktop-runtime-10.0.10-win-x64.exe
Source   https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.10/windowsdesktop-runtime-10.0.10-win-x64.exe
SHA-256  E82FC901C8F52D716293B2BC0830CE0DD254A06268C457A19E8FC503560A84D1
```

### PawnIO

```text
Version  2.2.0
File     PawnIO_setup.exe
Source   https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe
SHA-256  1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032
Mode     -install -silent
```

PawnIO is device-conditional. Setup does not offer it merely because a machine is manufactured by Lenovo.

## Failure behavior

The .NET runtime and ThinkControl payload are required application prerequisites. If either verified download/install/extraction fails, Setup stops with an explicit error rather than leaving a partial installation.

PawnIO is capability-scoped. If PawnIO fails, Setup continues and reports that X9 direct EC fan RPM/manual control may remain unavailable. Independent Windows and Lenovo features remain usable.

## Upgrade behavior

Running a newer ThinkControl Setup over an existing installation is supported. Before replacing the payload, Setup stops `ThinkControlService`; normal controller disposal attempts to return an active manual X9 fan level to Lenovo Auto. Existing `ui/` and `service/` payload directories are then replaced by the newly verified payload and the service registration is updated/restarted.

Inno Setup's application-closing flow handles a running ThinkControl tray process instead of failing immediately with a generic "currently running" message.

## Icons and shortcuts

The exact v3 application icon is used for Setup, `ThinkControl.UI.exe`, Start menu, optional desktop shortcut, Add/Remove Programs and the native Advanced-window title bar/taskbar entry. The tray uses the exact v3 mark ICO.

## Size budgets

Package CI enforces:

```text
Combined framework-dependent UI + service  <= 30 MB uncompressed
Compressed ThinkControl payload             <= 20 MB
ThinkControl web bootstrap installer         <= 5 MB
```

The 5 MB installer ceiling is deliberately strict: the application payload and .NET runtime must never be silently embedded back into Setup. The practical target is a roughly few-megabyte installer rather than the previous ~84 MB package.

## CI/package validation

Pull requests that change app/installer/branding/version code run the package workflow. It:

```text
verify exact v3 branding
        |
        v
build + publish framework-dependent UI/service
        |
        v
create + SHA-256 hash external payload ZIP
        |
        v
compile small Setup with payload URL/hash
        |
        v
install using SHA-verified local payload override
        |
        v
wait for ThinkControlService = Running
        |
        v
silent uninstall
        |
        v
verify service + files removed
        |
        v
generate installer + payload checksums
```

Tagged builds publish all three release assets. The release-publication workflow then verifies the installer, payload and checksum assets exist before recording the release as verified.

## Uninstall

The uninstaller stops/removes `ThinkControlService` and deletes ThinkControl-owned `ui/` and `service/` payload directories. It does not remove shared PawnIO or Lenovo/Intel platform components.
