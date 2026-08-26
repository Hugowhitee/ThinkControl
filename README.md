<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg">
    <img alt="ThinkControl" src="assets/brand/v3/wordmark/ThinkControl_wordmark_light.svg" width="430">
  </picture>

  <p><strong>A compact ThinkPad-first Windows control app for power, cooling, sensors, display, audio, keyboard, touchpad and battery telemetry.</strong></p>

  [![Windows CI](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Hugowhitee/ThinkControl/actions/workflows/ci.yml)
  [![Release](https://img.shields.io/github/v/release/Hugowhitee/ThinkControl?include_prereleases&label=release)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![Downloads](https://img.shields.io/github/downloads/Hugowhitee/ThinkControl/total?label=downloads)](https://github.com/Hugowhitee/ThinkControl/releases)
  [![License](https://img.shields.io/badge/license-source--available-555)](LICENSE)

  **[Download](https://github.com/Hugowhitee/ThinkControl/releases)** ·
  **[Device support](docs/DEVICE-SUPPORT.md)** ·
  **[Documentation](docs/README.md)** ·
  **[Report a bug](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml)**

  <br><br>
  <a href="https://buymeacoffee.com/hugowhite">
    <img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me a Coffee" height="44">
  </a>
</div>

## ThinkControl alpha.19

ThinkControl is a lightweight Windows 10/11 companion focused first on Lenovo and ThinkPad laptops. It brings controls normally spread across Windows Settings, Lenovo utilities and monitoring tools into a compact notification-area popup and a resizable Advanced window.

This public alpha is ThinkPad-first: Windows-safe controls can still work elsewhere, but Lenovo/ThinkPad provider discovery is the supported product focus. Low-level fan/PWM/EC controls stay visible and unavailable until an exact provider and readback contract passes. The ThinkPad X9-15 Gen 1 is the currently verified low-level reference device.

**Current prerelease:** `v0.1.0-alpha.19`

**Reviewed low-level reference profile:** ThinkPad X9-15 Gen 1 (`21Q6` / `21Q7`)

**Platform:** Windows 10 version 2004 (build 19041) or newer, x64 · .NET 10

Download the latest setup from **[GitHub Releases](https://github.com/Hugowhitee/ThinkControl/releases)**. For a normal install, this is the only file to download:

```text
ThinkControl-Setup-0.1.0-alpha.19.exe
```

The release also contains a versioned application payload and `SHA256SUMS.txt`. Those are updater/verification infrastructure used so ThinkControl can download and verify the complete app **before** elevation; users do not need to extract or install them manually.

## Interface

Release CI renders the real WPF interface in dark/light themes at minimum, normal and wide window sizes. The complete screenshot gallery is kept as a CI visual-QA artifact rather than adding PNG files to the public release download list. Public releases intentionally contain only Setup, the updater payload and checksums.

## Highlights

| Area | What ThinkControl provides |
| --- | --- |
| **Home** | Battery, CPU and fan overview plus quick access, notifications and support |
| **Performance** | Quiet, Balanced and Performance preferences with separate battery/AC behavior |
| **Fans** | Firmware/OEM Auto, verified manual levels and supervised cooling profiles where a writable provider passes validation |
| **System & sensors** | Hardware setup plus a focused details window for real CPU, GPU, fan and other detected telemetry |
| **Battery** | Watts, Wh, health, filtered ETA, day-grouped charge/discharge sessions, `% + W` timelines and retained local trends |
| **Display** | Brightness, adaptive brightness, refresh rate and automatic 60 Hz / maximum switching |
| **Audio** | Windows output and microphone volume/mute plus direct Dolby profile and Music IEQ controls where DAX exposes semantic readback |
| **Keyboard** | Hardware brightness plus Auto, Breathing, Reactive and experimental Audio effects where a verified provider is available |
| **Touchpad** | Live touch point/trail, configurable edge gestures, haptics and themed gesture pop-ups |
| **Updates** | Automatic checks, Last checked time, SHA-256 verified download and explicit one-click installer handoff |

ThinkControl does **not** invent missing RPM values, fake PWM percentages, guessed EC registers or synthetic sensor readings. Low-level controls remain visible but unavailable until their provider is actually detected and validated.

## Alpha.19 stabilization changes

Alpha.19 completes the ThinkPad-first stabilization pass with physically verified X9 fan output, a reliable updater elevation handoff, compact battery history and consistent controls throughout the WPF interface.

- Hardware attention opens a dedicated setup/repair window, with the same entry explained clearly on System.
- Battery temperature never borrows another sensor's identity: a genuine reading is labelled **BATTERY TEMP**, while the explicit control-temperature fallback is labelled **DEVICE TEMP**.
- Fan curves retain a fixed live temperature/target/RPM status and marker without an obstructive graph bubble; PWM/EC availability remains visible and capability-gated.
- Dolby Access uses Windows packaged-app activation and always reports a completed success/failure state.
- Compatibility reports are prepared locally after useful discovery when consent is enabled; GitHub submission remains explicit.
- Settings use durable atomic replacement and recover from the newest valid temporary/backup copy after an interrupted write.
- Shared button/selector hover uses an overlay, preserving custom and selected fills without size or layout shifts.
- The notification bell badge is anchored to the bell and the bell toggles the notification sheet open and closed.
- On the verified X9, 100% fan output now maps to physically confirmed EC step 7; the echoed but ineffective `0x47` state is never written.
- The updater no longer lets Setup terminate itself while closing ThinkControl, and verifies that the elevated installer survives the handoff.
- All sliders share endpoint-correct geometry at both 0% and 100%, with reserved reset-icon space and no heavy focus ring.
- Battery sessions are grouped into expandable days. Detailed graph samples expire after 7, 14 or 30 days while compact one-year summaries remain available for health trends and learned estimates.
- First install offers a minimal, default-enabled **Start ThinkControl with Windows** choice; Settings can change it later and installer opt-out is preserved.
- Sensors now live naturally under System in a dedicated detail window, while Audio adds native microphone gain and mute controls.
- Brightness and track-change gesture pop-ups now match the volume surface and icon treatment; media seeking remains intentionally silent.

### Reliability foundation

- The X9 EC reader keeps stale-output draining and write readback but restores the bounded read-ready behavior used by LibreHardwareMonitor: prefer fresh OBF, then accept the established IBF-clear fallback when firmware completes a valid EC read without reliably asserting OBF. This prevents successful fan writes from being falsely rejected during readback.
- Manual X9 fan writes remain restricted to `21Q6` / `21Q7`, verified by readback and returned to firmware/OEM Auto on supported failure/disposal paths. Manual/custom ownership is supervised against lost temperature/provider state.
- Provider refresh cannot recycle EC/sensor/keyboard providers while ThinkControl still owns a fan level; active ownership must return successfully to Lenovo Auto first, and characterization blocks refresh until stopped.
- Lenovo keyboard control requires provider readback. The Vantage fallback now loads the adjacent `Contract_Keyboard.dll` when present and correctly marshals enum parameters used by Lenovo's add-in.
- Direct Off/Low/High keyboard clicks are authoritative: they wait for an in-flight effect write instead of being silently dropped. Background hooks/effect loops remain demand-driven and stop when not needed.
- Hardware IPC operation timeouts are long enough for verified provider readback and recovery work to finish without the UI falsely reporting failure while the service is still completing the command.
- The privileged hardware service creates its named-pipe endpoint before slow provider discovery and keeps a cached hardware snapshot, so the UI can distinguish a running Windows service from a working app connection.
- The demand-driven status scheduler no longer accumulates losing semaphore waiters; idle telemetry sleeps cleanly and wakes predictably on real demand.
- Service repair verifies the same named-pipe `Ping` protocol the app uses instead of treating SCM `Running` as sufficient proof.
- LibreHardwareMonitor uses its PawnIO-backed provider path with PawnIO 2.2.0 as the minimum compatible low-level component. Installation, driver/device accessibility, provider readiness and actual telemetry remain separate states.
- The always-on runtime scheduler uses the Windows power manager and cheap display APIs instead of repeatedly running battery WMI, `powercfg` and full display discovery.
- Sensor discovery avoids unnecessary storage, battery, network, controller and PSU providers in the always-on path and uses bounded retry/recycle behavior rather than repeatedly hammering failed providers.
- On the verified X9 profile, real LibreHardwareMonitor/PawnIO fan telemetry is preferred when available. Direct EC tachometer and read-only thermal access are conservative fallbacks, and periodic EC control-register probing is removed from the normal status loop.
- Hardware Setup and Notifications surface root causes instead of multiplying one failed dependency into several identical repair actions.
- Update checking, Home and the Updates page share one release state. A user-triggered update downloads Setup + Payload + checksums, verifies SHA-256, shows the Windows elevation handoff and keeps update controls locked while the installer owns the swap.
- Automatic update checks never install or open UAC by themselves.
- A dedicated Windows installer-reliability gate now validates clean install, service start, named-pipe `Ping`, `GetStatus` telemetry, in-place reinstall/update behavior and uninstall cleanup before release work is merged.
- Old ephemeral GitHub Actions artifacts are cleaned automatically after seven days; immutable GitHub Release assets are never touched by that cleanup.
- Touchpad media seeking uses smaller per-frame deltas and coalesced GSMTC writes; browser sessions update more responsively while Spotify/Apple Music keep a conservative cadence.
- Gesture OSD placement, Home quick controls and hardware/sensor state propagation were tightened so unavailable states stay explicit rather than looking blank or stale.
- Display header actions use a real layout rather than hard-coded spacing, eliminating the Windows Settings / Defaults overlap seen in the visual audit.
- `version.json` is the build version source of truth for normal builds as well as packaging, preventing stale hard-coded app versions from appearing in the UI.
- Windows CI builds with zero compiler warnings, runs the core test suite and renders the complete visual-QA matrix before packaging.

## ThinkPad-first capability architecture

ThinkControl grows device support from broad to specific:

`Windows generic → OEM generic → product family → exact model`

Profiles select reasonable providers to probe; provider code owns implementation, readback, lifecycle and write safety. The current publication focus is Lenovo/ThinkPad, while the capability-first internals keep Windows-safe features reusable and avoid hard-wiring the UI to one exact machine.

See [Device support](docs/DEVICE-SUPPORT.md) and the [device-profile hierarchy](devices/README.md).

## Updates

ThinkControl checks GitHub Releases shortly after startup and periodically afterwards. **Background checks never open UAC and never install anything by themselves.**

When an update is available, clicking **Install update** performs this flow:

`Downloading Setup + Payload + checksums → Verifying SHA-256 → Approve Windows prompt → Installing → Relaunch`

Both the small setup executable and application payload are downloaded **before** elevation and verified against `SHA256SUMS.txt`. ThinkControl stays open while files are downloaded and verified. If the Windows administrator prompt is cancelled or setup fails before handoff, the running installation remains usable and the updater does not loop prompts on startup.

The setup detects an existing installation, skips first-install shortcut questions in update mode, closes the running tray/UI instance only when replacement is ready, updates the hardware service and relaunches ThinkControl when installation completes.

## Touchpad

The Touchpad page shows the real contact point with a red marker and fading trail. Edge areas brighten on hover and turn red only when selected. Default actions are Volume on the left edge, Brightness on the right and Media seek on the top edge.

Slow movement stays precise while faster movement accelerates within bounded limits. Volume and brightness writes are coalesced rather than fanning one OS call out per touch frame. Media seek uses an accumulated target and a bounded write cadence so slow movement can remain precise without flooding Spotify or browser media sessions.

## Hardware recovery

ThinkControl separates normal Windows features from privileged/provider-specific hardware access. Hardware Setup distinguishes the layers rather than collapsing everything into one online/offline flag:

1. **ThinkControl hardware service** — Windows service state plus the app-to-service IPC connection.
2. **Low-level/sensor access** — required driver/device access and sensor-provider readiness, such as PawnIO/LHM where applicable.
3. **Model-specific provider** — exact-model telemetry/readback validation before a low-level write capability is enabled.
4. **OEM platform integration** — keyboard, thermal-policy or other OEM provider availability and readback.

If a required component is missing, controls remain visible and ThinkControl explains what is unavailable instead of leaving them silently disabled. The notification inbox prioritizes prerequisite/root-cause repairs before downstream provider retries.

The X9-15 Gen 1 (`21Q6` / `21Q7`) remains the reviewed low-level reference profile. Unknown direct fan writes stay blocked, and manual fan ownership returns to firmware/OEM Auto on supported disposal paths.

## Audio, battery and reset behavior

Audio includes a live Windows system-volume slider. Where Dolby DAX exposes direct semantic control, ThinkControl can switch supported main profiles and Music Intelligent Equalizer options without opening Dolby Access. Unsupported DAX builds remain explicit rather than falling back to guessed profile IDs.

Battery history stores compact local charge and discharge sessions with duration, Wh, average/peak power and percentage-per-hour statistics. Session views use separate aligned battery-level (%) and power (W) graphs instead of a cluttered dual Y axis. Battery temperature is only shown when the real battery driver exposes it.

Individual changed sliders expose a small reset-to-default action. Page-level defaults use a small **Defaults** action; **Reset all** is separated in Settings and requires confirmation. Relevant controls also include a subtle **Open Windows settings** link when Windows owns the same setting.

## Help improve device support

On unverified laptops, ThinkControl can prepare a hardware-only device report using:

`Detect → Useful data → Ready to share`

When consent is enabled, ThinkControl automatically prepares the report locally after stable provider/capability information is found. Reports exclude serial numbers, Windows usernames, hostnames, personal paths and raw personal logs. Nothing is uploaded automatically; **Share to GitHub** only opens a draft and GitHub still requires an explicit submission.

## Development and safety

Windows CI builds the solution, runs tests and renders the WPF visual-QA matrix. Packaging CI publishes framework-dependent UI/service payloads, builds the small web bootstrapper and performs a real silent installer/service lifecycle test. Pull requests that touch runtime or installer code additionally run the deeper **install → service IPC → in-place reinstall/update → uninstall** reliability smoke.

```powershell
dotnet restore ThinkControl.slnx
dotnet build ThinkControl.slnx -c Release
.\tools\visual-qa.ps1
```

Automated CI and source-level protocol cross-checking cannot prove physical-device behavior. Fan ownership/RPM, OEM keyboard readback, touchpad HID behavior, Dolby DAX behavior and haptic response still require a run on the actual target hardware before that exact installed driver/firmware combination is treated as physically confirmed.

See [Device support](docs/DEVICE-SUPPORT.md), [Hardware safety](docs/HARDWARE-SAFETY.md), [Diagnostics & privacy](docs/DIAGNOSTICS.md) and [X9-15 research](docs/research/x9-15-gen1.md).
