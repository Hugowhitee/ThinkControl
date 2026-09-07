# ThinkControl alpha testing guide

Use this checklist for **v0.1.0-alpha.42** and later candidates built from it. Automated CI is required, but physical X9 behavior remains a separate evidence class and must not be inferred from hosted runners. The X9 is the current reference device, not the product boundary.

## Install/update sanity

1. Install or update using the versioned GitHub prerelease installer.
2. Confirm ThinkControl starts as a normal-user app and the hardware service reaches Running without the UI remaining elevated.
3. Confirm Compact opens, Advanced opens from Compact, and switching back does not create duplicate windows.
4. Exercise Check for updates from Home and Updates. An up-to-date result must not enable Install, and **Last checked** must refresh immediately on the shared state.
5. After an in-place update, confirm the previous install directory is preserved and the app relaunches into the expected surface.
6. A successful update confirmation must remain dismissable and must not strand a topmost notification over Advanced.

## Windows startup and background gestures

Alpha.41 tightened the startup critical path around **input/tray first, rich discovery later**. Alpha.42 keeps that architecture.

1. Enable **Start with Windows** and at least one obvious edge gesture such as Volume or Brightness.
2. Sign out/in or reboot. Do not manually open ThinkControl after the desktop appears.
3. Confirm ThinkControl reaches the tray without showing Compact/Advanced.
4. As soon as the tray process exists, try the configured gesture. It should work without first activating a ThinkControl window.
5. Repeat after a cold reboot and sign-out/sign-in. Record roughly how long from desktop availability until the first successful gesture.
6. Open ThinkControl afterwards and confirm rich system information fills normally.
7. Disable gestures, restart with `--tray`, and confirm Raw Input is not kept alive merely because Start with Windows is enabled.
8. Confirm a normal visible launch still paints promptly and never regresses into a blank/black first frame.

Configured Raw Input starts from the early application Startup hook after cheap identity is available. Rich WMI/service discovery remains asynchronous; `Application.Activated` is recovery, not first-start ownership.

## Crash/shell regression

The recurring `TargetParameterCountException` dispatcher bug was fixed and guarded in alpha.33 and remains a regression boundary.

- Open/close Advanced repeatedly.
- Minimize Advanced, reopen directly to Touchpad, and confirm the window becomes visible rather than remaining minimized/invisible.
- Navigate Compact → Advanced → Compact several times.
- Open and dismiss the Inbox/notification sheet.
- If a crash occurs, retain report/journal evidence rather than treating one later clean session as proof the issue is solved.

## Audio lifecycle regression

1. Open Advanced → Audio.
2. Drag output volume and navigate away while dragging, then return.
3. Repeat with microphone level.
4. Confirm no delayed off-page write jumps the control when returning.
5. Leave Audio idle for several seconds and confirm live output/microphone state continues refreshing after the navigation cycle.

## Keyboard

- Test Off, Low and High where the provider exposes them.
- Test Auto only when ThinkControl reports a verified firmware/OEM Auto path. **Auto is firmware-managed; it is not a ThinkControl idle-dimming effect.**
- Confirm Fn+Space and ThinkControl readback remain sensible after changing firmware Auto/static modes.
- Breathing, Reactive and Audio belong to Effects and appear only when the provider advertises `KeyboardEffects`.
- Confirm a saved effect is not reset during startup before provider capability is known.
- When only the current Lenovo Vantage fallback is available, Effects should remain unavailable rather than generating repeated Lenovo brightness popups.

## Touchpad — alpha.42 physical focus

Alpha.42 specifically addresses the integrated Track center action and reverse-close reliability. The center target is spatially easy to hit but intentionally slow enough to avoid accidental media playback.

### Six-zone editor and corners

- Selecting an edge clears a selected corner, and selecting a corner clears the selected edge.
- Top-left and top-right are exact mirrors: same guard radius, lane size, angle, rounded end arc, fill, boundary weight and selected/live treatment.
- Edge-band visuals stop/clip around enabled corner geometry instead of creating a darker overlap.
- A corner candidate owns a contact from the first frame; a rejected corner stays locked until lift and must not fall through into a neighboring edge gesture.
- Ordinary edge gestures still work outside corner ownership geometry.

### Integrated Track lane

- Assign **Track control** to Bottom first.
- Confirm Previous, Play/Pause and Next remain inside one continuous lane. There must be no floating skip icons or separate Play/Pause pill.
- The edge menu must contain Track control but no separate current Play/Pause action.
- Old serialized standalone PlayPause settings should sanitize into Track control rather than Off.
- The visible Play/Pause start segment occupies about **28%** of the lane (`0.36..0.64`), and the separators match that recognition target.
- Repeated **quick center taps must do nothing**: no playback toggle and no media popup.
- Hold the center for roughly half a second, keep mostly still, then release. Play/Pause should toggle **exactly once on release**.
- Hold for one or two seconds. Nothing should auto-fire while the finger remains down.
- Natural tiny movement up to about **3 mm maximum radial excursion** should remain eligible.
- Move clearly beyond 3 mm, return near the start and release. Play/Pause must remain disarmed; returning cannot erase earlier motion.
- After leaving the 3 mm hold envelope, ordinary Track recognition resumes. Previous/Next still requires the unchanged **9 mm** deliberate swipe threshold.
- A Previous/Next swipe must never also toggle Play/Pause on release.
- Alternate quick tap → deliberate hold/release → Previous → deliberate hold/release → Next several times and look for overlap or accidental playback.
- Successful media feedback uses current-state text plus next-action icon: **Playing + pause bars**, **Paused + play triangle**. `Playback toggled` is acceptable only when fallback state genuinely cannot be known.

### Edge assignment swapping

- Give the four edges distinct non-Off actions where possible.
- Choose an action already assigned elsewhere and confirm the two actions **swap** rather than clearing the previous edge.
- Sensitivity and inversion stay with the physical edge.
- Reopen Touchpad and verify persistence.

### Reverse close

- Enable **Reverse swipe closes ThinkControl** on each top corner.
- Start from several points through the **inner half of the visible diagonal lane** and swipe back toward the physical corner. Compact or Advanced should hide to tray reliably.
- Test the beginning, middle and rounded-end area of that inner-half target on both sides.
- The two sides should feel mirrored.
- Starting in the **outer guard** and moving inward remains the normal launch gesture, never reverse close.
- With reverse close disabled, the same outward inner-lane swipe must not hide ThinkControl.
- A rejected reverse candidate must not fall through to an edge action while the same contact remains down.

### Touchpad visual QA

Inspect the exact release-head CI artifact, especially:

- `advanced-touchpad.png`
- `advanced-touchpad-min.png`
- `advanced-touchpad-wide.png`
- `advanced-touchpad-light.png`
- both selected corner fixtures
- both live corner fixtures
- relevant gesture OSD fixtures

The wide fixture must show Previous / Play-Pause / Next inside one continuous band with the wider center integrated rather than overlaid. Help copy must describe hold-about-half-a-second + release and state that quick taps are ignored. Corners must remain visually mirrored.

## Fans and hardware providers

Alpha.42 keeps the alpha.41 low-level safety boundary but **does change cooling-profile lifecycle/persistence** after physical testing showed that a saved Quiet selection could survive in the UI while firmware had effectively returned to Auto/base policy.

The rejected per-fan `fanX_target` writer remains read-only. The exact-X9 Lenovo Other Mode global full-speed boolean `0x04020000` remains separately gated. No classic-EC fallback or guessed EnergyDrv command is introduced.

### Capability and safety regression

- Unsupported devices remain safe/read-only.
- A direct target-RPM writer stays disabled unless it independently passes provider and physical acceptance gates.
- Firmware policy remains semantically distinct from direct RPM/PWM control.
- `FanCalibrationSupported` / `FanCalibrationRequired` continue to own direct calibration UI.
- Manual percentages and Raw EC diagnostics remain hidden on the X9 firmware-policy backend.
- Max cooling may use the exact-X9 full-speed boolean only when the feature live-reads as boolean, is safely writable and every transition verifies readback.
- Quiet/Balanced release ThinkControl-owned full speed before applying lower Lenovo policy.
- Explicit Auto and service shutdown release owned full speed and restore firmware ownership.
- A failed full-speed probe/readback fails closed; it must not revive `fanX_target`, classic EC or arbitrary EnergyDrv writes.

### Runtime truth and saved-profile persistence — alpha.42 release blocker

This section specifically tests the bug reported immediately before alpha.42 release.

1. Start in **Auto**, open Fans, and confirm the selector/status describe the service's actual runtime state rather than merely the saved setting.
2. Select **Quiet**. Confirm the service reports Quiet as active and fan behavior changes to Lenovo's Quiet policy.
3. Close only the ThinkControl UI while leaving the Windows service running, then reopen the UI. Quiet must remain physically active; closing the user interface must not silently call Auto for a service-owned firmware profile.
4. Reboot Windows with Quiet saved. During startup the UI must not paint the persisted preference as applied before service telemetry says so. After provider discovery, ThinkControl must actively restore Quiet.
5. Continue listening through the first several seconds after login. The one bounded **7-second startup-settle reassert** should prevent a later-starting Lenovo component from leaving the machine back on Auto/base policy. There must be no repeating timer/polling fight.
6. While Quiet is active, unplug AC and then reconnect it. Quiet must remain the active cooling intent across the Lenovo AC/DC-specific policy-command change.
7. Put the machine to sleep and resume while Quiet is selected. Quiet must be reasserted rather than becoming a UI-only saved label.
8. While Quiet is active, change Windows Performance preference. The cooling override must remain Quiet, while the new Windows mode becomes the baseline that Auto will restore later.
9. Select **Auto** afterwards and confirm that latest Windows/Lenovo baseline is restored.
10. Repeat the lifecycle checks with **Balanced**. Repeat with **Max cooling** only if the exact full-speed feature passes its existing safe live/readback contract.
11. At no point should Fans show Quiet/Balanced/Max as applied solely because `settings.json` says so; the visible active profile must follow runtime/service telemetry.
12. Confirm temporary/direct fan tests, where a future accepted direct writer exists, still retain their prior Auto-on-UI-exit/timeout safety class. The firmware-profile persistence fix must not weaken direct-writer cleanup.

The startup-settle behavior is intentionally **one bounded retry**, not a background enforcement loop. AC/DC, resume and explicit power-baseline changes are real state transitions and may reassert the currently owned firmware profile.

### Current exact-X9 physical expectations

Confirmed negative evidence remains:

- alpha.38 fixed target-RPM control repeatedly sped up/slowed down instead of settling smoothly;
- nominal ThinkControl target 100% remained physically weaker than naturally hot Lenovo Auto even when telemetry looked high;
- therefore `fanX_target` remains read-only.

Current built-ins are expected to behave as:

```text
Auto         -> release ThinkControl full-speed ownership; restore latest Lenovo power-policy baseline
Quiet        -> full speed off; Lenovo Quiet policy
Balanced     -> full speed off; Lenovo Balanced policy
Max cooling  -> Lenovo Performance policy + verified 0x04020000 full-speed boolean when safely exposed
```

Under repeatable load, compare Quiet/Balanced/Max and record provider sources/RPM, but do not use RPM alone as proof of airflow intensity. Max should remain steady rather than reproduce the alpha.38 re-kick/wave behavior. If Max cannot safely engage, collect exact provider/readback evidence rather than broadening any writer.

## Diagnostics/device learning

- Supported hardware should not run expensive discovery every time the app opens.
- Unknown-device collection remains passive and hardware-focused.
- Sharing is explicit; preview must contain no usernames, serial numbers, personal file paths/content, keystrokes or raw touch trails.
- After a successful share/report flow, the UI should not keep claiming the same report is ready as if nothing happened.

## Repository/release hygiene

Validation is split by ownership:

- **CI** owns repository hygiene, zero-warning Release build, Core/source tests, real Compact ↔ Advanced ShellSmoke and WPF visual QA.
- **Package ThinkControl** owns publish/payload/bootstrap, service + IPC lifecycle, non-elevating UI contract, custom-location update preservation, clean uninstall and oldest-supported updater compatibility.
- Tagged release packaging owns the public overview/release assets.
- Superseded PR runs may cancel; immutable/tag release packaging must remain non-cancellable by that optimization.

## Release acceptance

Before calling a candidate releasable, require repository hygiene, zero-warning Release build, all Core/source tests, real Compact/Advanced WPF shell smoke, the complete visual-QA matrix with representative screenshots manually inspected, Package ThinkControl, an exact final frozen-head CI + Package rerun, complete diff review, expected-head merge, post-merge main verification, immutable promotion to `v0.1.0-alpha.42`, exactly Setup + Payload + `SHA256SUMS.txt` + `ui-overview.png`, and checksum verification of the published Setup/Payload.

Physical hardware checks remain a separate evidence class and must be recorded honestly rather than converted into hosted-CI claims.
