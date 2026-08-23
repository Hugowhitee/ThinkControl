# ThinkControl installer

ThinkControl `v0.1.0-alpha.1` uses a compact x64 Inno Setup bootstrap installer.

## Release file

```text
ThinkControl-Setup-0.1.0-alpha.1.exe
```

The installer contains the framework-dependent ThinkControl UI and hardware service. It deliberately does **not** embed a second copy of the .NET runtime in each managed payload.

## Installation flow

Setup:

1. requests one administrator elevation;
2. checks for a compatible .NET 10 Desktop Runtime;
3. downloads the official Microsoft x64 Desktop Runtime only when missing;
4. verifies the pinned Microsoft runtime SHA-256 before execution;
5. detects the Lenovo machine type from local SMBIOS information;
6. on ThinkPad X9-15 Gen 1 machine types `21Q6` / `21Q7`, offers **X9 hardware access (PawnIO 2.2.0)**;
7. downloads the official PawnIO 2.2.0 release only when selected and missing;
8. verifies the PawnIO release asset against the SHA-256 published in Microsoft's Winget package repository;
9. installs ThinkControl under Program Files;
10. registers and starts `ThinkControlService`;
11. offers **Launch ThinkControl** on the completion page.

A normal user does not need to visit a .NET or PawnIO download page manually.

## Verified prerequisite pins

### Microsoft .NET Desktop Runtime

```text
Version  10.0.10
Arch     x64
File     windowsdesktop-runtime-10.0.10-win-x64.exe
Source   https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.10/windowsdesktop-runtime-10.0.10-win-x64.exe
SHA-256  E82FC901C8F52D716293B2BC0830CE0DD254A06268C457A19E8FC503560A84D1
```

The .NET download is made through Inno Setup's HTTPS downloader and the pinned SHA-256 is checked before the runtime is executed.

### PawnIO

```text
Version  2.2.0
File     PawnIO_setup.exe
Source   https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe
SHA-256  1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032
Mode     -install -silent
```

The PawnIO URL, SHA-256 and silent switch match the `namazso.PawnIO` `2.2.0` installer manifest in `microsoft/winget-pkgs`.

PawnIO is device-conditional. Setup does not install it merely because a computer is manufactured by Lenovo.

## Failure behavior

The required .NET runtime is an application prerequisite. If its verified download or installation fails, ThinkControl Setup stops with an explicit prerequisite error rather than installing an application that cannot start.

PawnIO is different: it enables the verified X9 EC fan backend, but it is not required for the entire application. If PawnIO cannot be downloaded or installed, setup continues and reports that X9 fan RPM/manual EC control may remain unavailable. Windows display, battery, power-mode and other independent features remain usable.

A prerequisite can request a Windows restart. Setup preserves that restart-required state instead of presenting it as an ordinary failure.

## Upgrade behavior

Running a newer ThinkControl setup over an existing installation is supported.

Before replacing the service payload, setup stops `ThinkControlService`. Normal service/controller disposal returns an active manual X9 fan level to Lenovo Auto before closing its EC provider. Inno Setup's application-closing flow handles a running ThinkControl tray process rather than failing immediately with a generic "currently running" message.

The existing service registration is then updated to the new executable and restarted.

## Icons and shortcuts

The installer uses the same ThinkControl application icon embedded in `ThinkControl.UI.exe` for:

- Setup;
- the Start menu shortcut;
- the optional desktop shortcut;
- Add/Remove Programs;
- the native Advanced-window title bar and taskbar entry.

The tray notification icon remains the tray-specific asset.

## Package size

The release workflow publishes UI and service as framework-dependent `win-x64` applications and enforces two regression budgets:

```text
Combined UI + service payload  <= 30 MB
ThinkControl setup executable   <= 20 MB
```

The actual release installer should normally be well below the previous self-contained package because the .NET runtime is downloaded only when the machine does not already have it.

## CI/package validation

Feature-branch pushes do not run expensive Windows packaging. The package workflow is reserved for an explicit validation run or a version tag.

Its lifecycle test is:

```text
publish framework-dependent UI + service
        |
        v
measure payload size
        |
        v
build Inno Setup
        |
        v
measure installer size
        |
        v
silent install
        |
        v
wait for ThinkControlService = Running
        |
        v
silent uninstall
        |
        v
verify files + service registration removed
        |
        v
generate SHA256SUMS.txt
```

## Lenovo and Intel software

ThinkControl Setup does not bundle or replace Lenovo/Intel platform components such as Lenovo Power Management, Lenovo Intelligent Thermal Solution, Intel Innovation Platform Framework, Lenovo Vantage or Lenovo Service Bridge.

Installed official Lenovo providers may be used when their contract is supported, but OEM servicing remains owned by Lenovo/Windows Update.

## Uninstall

The uninstaller removes ThinkControl-owned UI/service files and unregisters `ThinkControlService`. It does not remove shared PawnIO or Lenovo/Intel platform components.
