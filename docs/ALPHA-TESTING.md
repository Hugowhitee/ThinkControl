# Alpha testing on the ThinkPad X9-15 Gen 1

This guide is for physically validating ThinkControl `v0.1.0-alpha.31` on the current verified low-level reference device, ThinkPad X9-15 Gen 1 machine type `21Q6` or `21Q7`.

Automated CI proves software contracts, deterministic WPF states and installer/service lifecycle. It cannot prove physical fan response, haptic feel, OEM keyboard behavior or a previously reported crash is gone on the real machine.

## Before installation

- Close other direct fan/EC-control utilities.
- Keep normal Lenovo platform drivers/services installed, especially Lenovo Intelligent Thermal Solution and Lenovo Power Management.
- Start low-level fan/calibration tests on AC power without a heavy workload.
- If testing an update from alpha.30, keep the existing installation location so updater location preservation is exercised.

## Installer and cold startup

Install the published `ThinkControl-Setup-0.1.0-alpha.31.exe`, not a loose development build.

Confirm:

1. Setup obtains the matching alpha.31 payload and verifies its SHA-256 checksum.
2. `ThinkControlService` installs/starts and app-to-service IPC becomes healthy.
3. ThinkControl identifies `21Q6/21Q7` as the verified X9 profile.
4. Hardware Setup distinguishes service reachability from missing/unavailable providers instead of calling every provider problem “offline”.
5. A cold launch never exposes a large empty/black ThinkControl client area. The painted loading surface appears first when startup work takes noticeable time and remains until Compact/Advanced has rendered.
6. Repeat cold launches with the preferred opening view set to Compact and Advanced.

## Crash #60 and Compact ↔ Advanced shell regression

Alpha.31 contains the software fix for the reported `TargetParameterCountException`. This physical loop is the release follow-up for issue #60.

1. Open Compact from the notification area.
2. Switch to Advanced.
3. Return to Compact.
4. Repeat the transition at least 15 times, including quick but deliberate clicks.
5. While Compact is open, click Chrome/another normal application and confirm Compact remains visible rather than auto-hiding because it lost focus.
6. Trigger/open a ThinkControl attention/update popup while Compact is visible and confirm the popup stays above its owner rather than disappearing behind Compact.
7. Tray actions while Advanced is open must transition safely rather than leave both windows stuck visible.
8. Minimize/maximize/restore Advanced and use normal Windows Snap/caption/system-menu behavior.
9. ThinkControl must not terminate, become transparent, leave an unpainted frame or record another `TargetParameterCountException`.

Do not close issue #60 merely because CI shell smoke passes. Close it after the published alpha.31 installer survives this physical workflow.

## Home and Compact

Check minimum, normal and wide Advanced widths plus Compact dark/light/on-battery states.

- Home Battery / CPU / Fans / Power / Sensors labels and values do not clip.
- Battery ETA remains readable at the minimum supported width.
- Home Performance text wraps rather than escaping/clipping its card.
- Home and Compact quick power selectors represent the **battery** preference.
- Full Performance still lets battery and plugged-in preferences be configured independently.
- Changing the battery preference in Home is reflected in Compact immediately and vice versa.
- Compact quick selectors show complete borders/content and the metric editor still opens and saves normally.

## Read-only telemetry

Before changing fan state, verify telemetry plausibility:

- CPU/control temperature has an honest provider/source label.
- fan RPM appears only from a real tachometer provider;
- firmware/Lenovo Auto is the starting cooling owner unless a saved supervised profile is deliberately restored;
- battery percentage, charging state and charge/discharge power are plausible;
- ETA settles rather than jumping wildly on every sample;
- unavailable values show a provider/capability explanation instead of invented data.

## Cooling profiles and temporary manual control

Continue only when the verified X9 fan-control provider is active.

1. Start from Auto and switch through Quiet, Balanced and Max cooling, then return to Auto.
2. Confirm Compact, Home and Fans agree on the selected profile.
3. Under a steady workload, listen for repeated fan-state hunting around thresholds.
4. Confirm meaningful heating still causes prompt cooling increases.
5. Start a temporary manual percentage test and confirm the previous profile is remembered.
6. `End test` restores the previous profile immediately.
7. Repeat and let the 30-second timeout restore automatically.
8. Repeat and leave the Fans page; the temporary test must end and restore.
9. Test raw EC steps only through the X9 diagnostic expander. They must not be visible when the verified X9 capability path is unavailable.
10. Exit/stop/uninstall while ThinkControl owns a manual/supervised state and confirm Lenovo/OEM firmware regains normal cooling ownership.

ThinkControl does not expose arbitrary EC writes, fan-off or unverified override families.

## Fan calibration

Calibration is evidence collection for the verified seven-state X9 mapping; it is not a subjective fan-noise wizard.

1. Start only while control temperature is safely below the calibration start limit.
2. Confirm real tachometer telemetry is visible before starting.
3. Start calibration and let the full seven-step run complete without a workload change if possible.
4. Confirm progress advances through all seven EC states and each result reports measured RPM/stability evidence.
5. A successful run should report a verified calibration and keep step 7 as a plausible measured maximum.
6. Start another run, then cancel it partway through. The previous complete verified calibration must remain available; partial results must not replace it.
7. If fan telemetry/provider access disappears or temperature becomes unsafe, calibration must stop safely and return to firmware Auto without overwriting known-good calibration.
8. There should be no `Clearly audible here` control; that old marker did not participate in output mapping and has been removed.

## Performance

Test **Efficiency, Balanced and Performance** separately for battery and plugged-in operation. These are power-policy preferences, not fixed fan-RPM targets. Switching power source should use the stored preference for that source without silently changing the other preference.

## Keyboard

1. Test Off, Low and High and confirm the physical keyboard matches.
2. Test Auto/user-session effects where available.
3. While an effect is active, make direct static changes repeatedly. They must not be silently dropped behind an in-flight effect write.
4. Re-enable Auto/effect mode and confirm the effect owner resumes cleanly rather than leaving two competing write loops.

## Touchpad visualization and edge gestures

Start with gestures disabled and confirm normal Windows touchpad behavior. Then enable gestures.

### Trail integrity

- Move one contact, lift completely, then touch far away: no straight line may connect separate contacts.
- Large implausible coordinate jumps also break the trail instead of drawing a fake segment.
- Live visualization should stop doing unnecessary input work when the page is no longer visible and gestures are disabled.

### Precision edge actions

- Continuous Volume/Brightness/Media scrub starts only from the configured edge band and uses bounded/coalesced OS writes.
- Track control needs deliberate travel before Previous/Next fires; small incidental movement should do nothing.
- Optional center Play/Pause only commits after a deliberate low-travel hold/release inside the visible bounded center zone.
- A normal skip swipe through/near center still wins as Previous/Next rather than becoming Play/Pause.
- Play/Pause OSD should reflect the resulting Playing/Paused state.

### Corner launches

- Configure top-left and top-right independently using only Off / Compact / Advanced.
- The visible diagonal lane is the real launch-start area; touching outside it must not use a hidden square corner hitbox.
- Corner tap alone does nothing.
- Normal vertical scrolling/along-edge movement from the corner does nothing.
- Deliberate diagonal inward travel from a configured lane launches exactly once.
- Re-test at minimum Advanced width and confirm the corner controls/zones remain readable and clickable.

## Haptics

Where Windows reports configurable haptics:

- haptic enable/strength and click-force controls match the real Windows/Precision Touchpad capability state;
- changing one setting is reflected physically and survives normal page navigation;
- unsupported haptic/click-force capability is disabled with accurate copy rather than pretending the whole Precision Touchpad is absent.

## Battery and history

- Charge/discharge watts, remaining/full Wh, health and cycle count are plausible when exposed.
- Battery temperature appears only from a battery-specific credible source.
- Expand daily history and verify scrolling/layout at minimum/normal widths.
- Switching retention preference does not destroy unrelated settings.

## Updates and notifications

- `Check for updates` leaves install/update action disabled when no newer version exists and shows a clear up-to-date state.
- An available update remains discoverable after dismissing the proactive popup; dismissal suppresses interruption, not the underlying update state.
- Completed-update confirmation is passive and auto-dismisses; it must not offer a misleading Ignore/Later action that changes window state.
- Update install performs verified download first and triggers at most the intentional elevation handoff.

## Reporting a result

If something fails, record the exact alpha version, Windows build, machine type/BIOS, last action and whether the relevant provider/capability was available. Use ThinkControl's prepared redacted crash/support report where useful. Do not post serial numbers, usernames, personal paths, raw personal logs or touch coordinates.
