# Dependencies and hardware readiness

ThinkControl should never require users to manually assemble a stack of unrelated utilities before the app becomes useful. The default experience is one installer, one UAC elevation, and a clear readiness result.

## Dependency policy

Dependencies are classified as one of four types:

- **Required app runtime** — needed for ThinkControl itself to launch.
- **Device-conditional hardware access** — only needed when a verified capability uses it.
- **OEM platform component** — Lenovo/Intel software that belongs to the laptop platform and should be obtained from the OEM, not mirrored by ThinkControl.
- **Optional integration** — useful companion software but never required for ThinkControl.

ThinkControl must keep Windows-level safe features available when an optional or hardware dependency is missing.

## Runtime matrix

| Component | Classification | Required for | Install behavior |
| --- | --- | --- | --- |
| .NET 10 Desktop Runtime | Required app runtime | WPF UI and managed service | Bootstrapper installs the official Microsoft runtime when missing |
| ThinkControl Service | Required ThinkControl component | privileged hardware operations and background profile enforcement | Installed and maintained by the ThinkControl bootstrapper |
| PawnIO | Device-conditional hardware access | verified low-level sensors, ThinkPad EC telemetry and direct EC fan control | Explicit opt-in; bootstrapper downloads official signed setup and invokes normal silent install |
| Lenovo Intelligent Thermal Solution (`LITSSVC`) | OEM platform component | verified Lenovo Intelligent Cooling/thermal policy backend | Detect only; offer Lenovo Drivers action when missing |
| Lenovo Power Management (`IBMPMSVC` / `IBMPmDrv`) | OEM platform component | verified ThinkPad PM/ACPI commands such as keyboard features | Detect only; offer Lenovo Drivers action when missing |
| Intel Innovation Platform Framework | OEM platform component | Windows/OEM thermal and energy policy stack | Detect/diagnose only; do not replace directly |
| Commercial Vantage / Lenovo Vantage | Optional integration | warranty, Lenovo maintenance flows, obscure device settings | Never required; offer Open/Install link only |
| Lenovo Service Bridge | Optional integration | Lenovo Support product detection | Never required; offer official install link only |

## PawnIO rules

ThinkControl does not write or ship a custom kernel driver while PawnIO can provide the verified primitive safely.

Production rules:

1. Use the **normal signed PawnIO build only**. Never install the unrestricted/developer build.
2. Do not silently add PawnIO without telling the user that a kernel driver is being installed.
3. Prefer on-demand download of the official PawnIO setup rather than embedding the binary in the ThinkControl repository or bootstrapper.
4. Pin the accepted PawnIO release and SHA-256 in the ThinkControl release manifest.
5. Verify the downloaded file before execution. Public releases should also verify Authenticode trust/publisher.
6. Invoke the official setup in its documented normal silent mode (`-install -silent`).
7. Interpret a reboot-required exit code and surface `Restart required` instead of reporting failure.
8. If PawnIO is absent or unhealthy, disable only the capabilities that require it. Do not break the whole UI.
9. Do not assume LibreHardwareMonitor being present means PawnIO is installed. Probe the actual driver/service state.

## Installer experience

### Recommended setup

The normal installer should present one concise summary before elevation:

```text
Install ThinkControl

ThinkControl                         Required
Microsoft .NET Desktop Runtime      Install if needed
Hardware access (PawnIO)            Recommended on this verified ThinkPad

[ Install ]
```

The PawnIO line appears as recommended only when the detected device profile has verified capabilities that need it. On an unknown ThinkPad, the installer must not install low-level hardware access merely because the manufacturer is Lenovo.

### Advanced setup

Advanced setup may allow:

```text
[x] ThinkControl application
[x] ThinkControl hardware service
[x] Microsoft .NET Desktop Runtime (if missing)
[x] Hardware access — PawnIO (recommended)
[ ] Start ThinkControl with Windows
[ ] Launch ThinkControl after setup
```

The app and service cannot be deselected in a normal install. Startup and launch-after-setup are user choices.

## First-run readiness

ThinkControl should summarize readiness with three user-facing states:

### Full

All dependencies required by the currently verified device capabilities are available.

```text
Hardware access   Full
PawnIO            2.2.0
Lenovo thermal    Ready
Power management  Ready
```

### Limited

ThinkControl works, but low-level features are unavailable because an optional/device-conditional dependency is missing.

```text
Hardware access   Limited

Fan RPM and custom fan control need hardware access.
[ Install hardware access ]
```

### Needs attention

An expected OEM platform component is missing or unhealthy.

```text
Device software   Needs attention
Lenovo Power Management is missing.

[ Open Lenovo Drivers ]
```

`Needs attention` must not claim the laptop is unsafe or broken. It means only that one or more ThinkControl capabilities cannot be trusted yet.

## OEM driver policy

ThinkControl must not become a second Lenovo driver distribution system.

For Lenovo/Intel platform components:

- detect exact known services/drivers where verified;
- show the installed version when a trustworthy source is available;
- link to Lenovo Drivers & Software for repair/install;
- never mirror OEM installers without explicit redistribution permission;
- never auto-install a vaguely matched package based only on a product name;
- never downgrade a newer OEM component automatically.

A future Lenovo catalog integration may resolve exact packages by machine type, but it must remain an explicit, verified package flow.

## Security and update validation

The bootstrapper must be native/self-contained so it does not depend on the .NET runtime it may need to install.

Downloads are treated differently by trust domain:

- **ThinkControl payload:** release manifest + SHA-256; Authenticode required once public signing is enabled.
- **Microsoft .NET runtime:** official Microsoft source; verify Authenticode trust/publisher before execution.
- **PawnIO:** official PawnIO source; exact version and SHA-256 pinned by the ThinkControl release manifest; verify Authenticode trust when available.
- **Lenovo software:** launch official Lenovo/Microsoft acquisition path instead of downloading opaque third-party mirrors.

The bootstrapper must never execute an unverified payload just because HTTPS download succeeded.

## Reboot behavior

Most ThinkControl updates should not reboot Windows.

If a dependency installer reports that a restart is required:

- complete the ThinkControl installation where safe;
- show `Restart required for hardware access`;
- keep Windows-level ThinkControl features usable before restart;
- do not repeatedly reinstall the dependency on every launch;
- verify readiness again after restart.

## Uninstall behavior

Removing ThinkControl removes:

- ThinkControl UI files;
- ThinkControl service;
- ThinkControl scheduled/startup entries;
- ThinkControl-owned caches and update state (with an option to preserve profiles).

It should **not uninstall shared OEM drivers**.

PawnIO may be shared by LibreHardwareMonitor or other tools. The default ThinkControl uninstaller therefore leaves PawnIO installed and may offer a separate explicit `Remove PawnIO if unused` action only after checking that this is safe.
