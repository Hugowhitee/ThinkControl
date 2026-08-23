# Dependencies and hardware readiness

> **Current dependency model for `v0.1.0-alpha.1`.** The alpha installer is self-contained. A future small bootstrapper may change runtime acquisition later, but it is not the current install path.

ThinkControl should keep Windows-level features usable even when a device-specific low-level prerequisite is missing.

## Current dependency matrix

| Component | Classification | Needed for | Alpha.1 behavior |
| --- | --- | --- | --- |
| .NET runtime | App runtime | WPF UI + managed service | **Bundled in the self-contained installer payload**; no separate runtime install required |
| ThinkControl Service | Required ThinkControl component | privileged X9 fan/keyboard operations | Installed/started/uninstalled by ThinkControl Setup |
| PawnIO | Device-conditional hardware access | current X9 EC fan state/RPM/manual control | **Not automatically installed in alpha.1**; feature remains limited if PawnIO is absent |
| LibreHardwareMonitorLib | Managed app dependency | CPU sensor access + PawnIO transport library | Packaged with ThinkControl; not a separate user install |
| Lenovo Power Management / `IBMPmDrv` | OEM platform component | current X9 keyboard Off/Low/High provider | Detected/used when present; not bundled or replaced by ThinkControl |
| Lenovo Intelligent Thermal Solution (`LITSSVC`) | OEM platform component | future verified Lenovo thermal-policy coordination | Research/detection only for alpha.1; not required by the current Windows power-mode implementation |
| Intel Innovation Platform Framework | OEM platform component | Windows/OEM platform policy | Vendor-owned; ThinkControl does not replace it |
| Lenovo Vantage | Optional integration | Lenovo maintenance/support/settings | Not required |
| Lenovo Service Bridge | Optional integration | Lenovo support/product detection | Not required |

## Self-contained .NET packaging

The first alpha publishes both UI and service as self-contained `win-x64` .NET 10 applications. This increases installer size but removes a first-run runtime prerequisite and makes the package easier to smoke-test as one unit.

Users do not need the .NET SDK and do not need to visit the Microsoft runtime download page before installation.

A future smaller bootstrapper may switch ThinkControl to framework-dependent payloads and install/detect the .NET 10 Desktop Runtime. That is a distribution optimization, not current alpha behavior.

## PawnIO

PawnIO is device-conditional and currently required by the X9 EC fan backend.

### Alpha.1 status

- ThinkControl does not bundle PawnIO setup;
- the installer does not automatically download/install it;
- a missing/unhealthy PawnIO provider limits X9 fan EC features rather than preventing the whole app from launching;
- display, battery and other Windows-level capabilities can continue independently.

### Rules for future automated PawnIO setup

Before ThinkControl adds one-click PawnIO installation, the release path must:

1. use the normal signed distribution, never an unrestricted/developer mode;
2. pin an exact accepted version and exact release asset;
3. verify the downloaded asset/hash before execution;
4. verify publisher/trust when available;
5. clearly tell the user that a kernel hardware-access driver is being installed;
6. use the documented normal installer mode;
7. surface reboot-required separately from failure;
8. probe the actual provider after installation;
9. avoid uninstalling PawnIO automatically because other tools may share it.

## OEM Lenovo / Intel components

ThinkControl is not a Lenovo/Intel driver distribution system.

For OEM platform components it should:

- use an installed known provider only when its contract has been verified;
- show missing/unavailable state honestly;
- link to official Lenovo/Microsoft acquisition paths where useful;
- never mirror a vaguely matched model-specific driver package;
- never downgrade OEM software automatically.

The current alpha does not require Vantage or Lenovo Service Bridge to launch.

## Hardware readiness in the UI

Readiness is capability-specific. The app may be fully usable for Windows-level features while an X9 hardware provider is unavailable.

Examples:

```text
Display refresh       Ready
Battery telemetry     Ready
CPU temperature       Ready
Fan EC access         Unavailable · PawnIO/provider missing
Keyboard backlight    Unavailable · Lenovo PM provider missing
```

ThinkControl should explain the missing provider rather than displaying a dead control or pretending the feature worked.

## Release/package security

Current ThinkControl-owned release artifacts are built in GitHub Actions. The package workflow:

- resolves the version from `version.json`;
- verifies a tagged release matches that version exactly;
- builds/publishes UI + service;
- produces the Inno Setup installer;
- smoke-tests install/service/uninstall;
- creates `SHA256SUMS.txt`;
- attaches the installer/checksum to a tagged GitHub Release.

Authenticode signing for ThinkControl itself is still a future release-hardening step unless/until a signing certificate is configured. The absence of signing must not be hidden in documentation.

## Reboots

The normal self-contained ThinkControl install does not inherently require a Windows reboot.

A future hardware prerequisite such as PawnIO may report a restart requirement; when prerequisite automation exists, ThinkControl must surface that state separately and keep independent Windows-level capabilities usable where safe.

## Uninstall

The current ThinkControl uninstaller removes:

- ThinkControl UI files;
- ThinkControl service files;
- ThinkControl service registration;
- installer-owned application files.

It does not remove Lenovo/Intel platform components or PawnIO. Local user settings/diagnostics are user-profile data and should be treated separately from system-wide program files.

See [Installer](../installer/README.md) for the actual alpha package flow.
