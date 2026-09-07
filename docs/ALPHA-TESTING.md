# ThinkControl alpha testing guide

Use this checklist for **v0.1.0-alpha.43** and later candidates built from it. Automated CI is required, but physical X9 behavior and real Windows audio behavior remain separate evidence classes and must not be inferred from hosted runners. The X9 is the current reference device, not the product boundary.

## Install/update sanity

1. Install or update using the versioned GitHub prerelease installer.
2. Confirm ThinkControl starts as a normal-user app and the hardware service reaches Running without the UI remaining elevated.
3. Confirm Compact opens, Advanced opens from Compact, and switching back does not create duplicate windows.
4. Exercise Check for updates from Home and Updates. An up-to-date result must not enable Install, and **Last checked** must refresh immediately on the shared state.
5. After an in-place update, confirm the previous install directory is preserved and the app relaunches into the expected surface.
6. A successful update confirmation must remain dismissable and must not strand a topmost notification over Advanced.

## Windows startup and background gestures

Alpha.41 tightened the startup critical path around **input/tray first, rich discovery later**. Alpha.43 keeps that architecture.

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

## Audio Safety — alpha.43

Alpha.43 adds one session-level Audio Safety state shared by Compact, Settings, Touchpad routing and Windows output writes. It is deliberately **not persisted** across process restart in this release.

### State and UI truth

1. Start a fresh ThinkControl process. Audio Safety must start at **Normal**.
2. Change the Compact selector to **Media lock**. Open Advanced → Settings and confirm the same canonical state is selected there.
3. Change Settings to **Silent** and return to Compact. Compact must immediately show Silent rather than keeping an independent stale selection.
4. Return to **Normal** from either surface and confirm the other surface follows.
5. Restart ThinkControl after using Media lock/Silent. The new process must start in Normal; alpha.43 must not persist a stale mute-ownership claim.
6. Confirm Audio Safety does not create a new navigation page or a standalone Touchpad Mute action.

### Media lock

Use a media app/browser with audio that you can deliberately control outside ThinkControl.

1. Start playback deliberately using the app itself and choose a comfortable Windows output volume.
2. Enable **Media lock**.
3. Try every ThinkControl Touchpad audio/media action that is configured:
   - Volume;
   - Media scrub;
   - Track Previous;
   - Track Next;
   - integrated Track Play/Pause when enabled.
4. None of those Touchpad actions may alter playback/volume. A short bounded `Media locked` indication should explain the block instead of the gesture appearing broken.
5. Deliberately control playback/volume from the media app or ordinary Windows controls. Media lock must **not** mute the output, stop external playback or freeze the system's own controls.
6. Enter Media lock while a ThinkControl Touchpad audio gesture is already active. The in-flight action must be cancelled and must not continue issuing writes afterwards.
7. Brightness and non-audio Touchpad actions must continue working.

### Silent

1. Record whether the current Windows output endpoint is muted before the test.
2. Enable **Silent**. The active default Windows output must become muted before ThinkControl claims the mode transition succeeded.
3. Try Compact/Advanced output volume and mute controls. ThinkControl must not unmute or change output volume while Silent is active.
4. Try the same Touchpad Volume/Media scrub/Track actions. They remain blocked and should give bounded Silent feedback.
5. Change microphone level/mute from ThinkControl. Microphone input remains independent and must still work where the Windows capture endpoint is available.
6. While Silent is active, switch the default output to another real endpoint (for example speakers ↔ Bluetooth/headphones). The newly active endpoint should converge to muted using the existing application status cadence, without a new rapid polling loop.
7. Leave Silent. Each output endpoint ThinkControl encountered during Silent should return to the mute state it had **before ThinkControl first touched that endpoint** during this Silent session.
8. If an endpoint was already muted before Silent, it should remain muted after Silent exits.
9. If an encountered endpoint disappears before exit, ThinkControl must not guess another endpoint to restore in its place.
10. Enable Silent, then exit ThinkControl normally. The orderly shutdown should restore owned prior endpoint mute states.

Do not treat hosted CI as proof of actual endpoint switching or audible silence; record real Windows behavior separately.

## Keyboard

- Test Off, Low and High where the provider exposes them.
- Test Auto only when ThinkControl reports a verified firmware/OEM Auto path. **Auto is firmware-managed; it is not a ThinkControl idle-dimming effect.**
- Confirm Fn+Space and ThinkControl readback remain sensible after changing firmware Auto/static modes.
- Breathing, Reactive and Audio belong to Effects and appear only when the provider advertises `KeyboardEffects`.
- Confirm a saved effect is not reset during startup before provider capability is known.
- When only the current Lenovo Vantage fallback is available, Effects should remain unavailable rather than generating repeated Lenovo brightness popups.

## Touchpad — alpha.43 physical focus

Alpha.43 keeps alpha.42's deliberate Track-center hold/release and reverse-close geometry, and adds a Track-local option that can remove the center Play/Pause affordance entirely.

### Six-zone editor and corners

- Selecting an edge clears a selected corner, and selecting a corner clears the selected edge.
- Top-left and top-right are exact mirrors: same guard radius, lane size, angle, rounded end arc, fill, boundary weight and selected/live treatment.
- Edge-band visuals stop/clip around enabled corner geometry instead of creating a darker overlap.
- A corner candidate owns a contact from the first frame; a rejected corner stays locked until lift and must not fall through into a neighboring edge gesture.
- Ordinary edge gestures still work outside corner ownership geometry.

### Track control — Play/Pause enabled

- Assign **Track control** to Bottom first.
- Confirm a **Play / Pause** switch appears only for the selected Track edge, not for Volume/Brightness/Media scrub/Off and not as a standalone action in the action menu.
- With Play/Pause enabled, confirm Previous, Play/Pause and Next remain inside one continuous lane. There must be no floating skip icons or separate Play/Pause pill.
- Old serialized standalone PlayPause settings should sanitize into Track control rather than Off and should preserve the enabled center by default.
- The visible Play/Pause start segment occupies about **28%** of the lane (`0.36..0.64`), and the separators match that recognition target.
- Repeated **quick center taps must do nothing**: no playback toggle and no media popup.
- Hold the center for roughly half a second, keep mostly still, then release. Play/Pause should toggle **exactly once on release**.
- Hold for one or two seconds without releasing. Nothing should auto-fire while the finger remains down. Crossing the 450 ms time threshold alone is not a command.
- Natural tiny movement up to about **3 mm maximum radial excursion** should remain eligible.
- Move clearly beyond 3 mm, return near the start and release. Play/Pause must remain disarmed; returning cannot erase earlier motion.
- After leaving the 3 mm hold envelope, ordinary Track recognition resumes. Previous/Next still requires the unchanged **9 mm** deliberate swipe threshold.
- A Previous/Next swipe must never also toggle Play/Pause on release.
- Alternate quick tap → deliberate hold/release → Previous → deliberate hold/release → Next several times and look for overlap or accidental playback.
- Successful media feedback uses current-state text plus next-action icon: **Playing + pause bars**, **Paused + play triangle**. `Playback toggled` is acceptable only when fallback state genuinely cannot be known.

Release-to-commit is intentional. It trades a small amount of button-like immediacy for a clear final intent confirmation so a resting/incidental touch cannot start media merely because its dwell time crossed 450 ms.

### Track control — Play/Pause disabled

1. Turn the Track-local **Play / Pause** switch off.
2. The center fill, separators and Play/Pause glyph must disappear immediately. The lane should read visually as Previous / Next only.
3. Tap/hold/release the physical center repeatedly. It must never toggle playback and must not behave like an invisible center target.
4. Previous/Next must still work with the same 9 mm threshold.
5. Navigate away/reopen Touchpad and confirm the setting persists.
6. Move Track control to another edge or temporarily remove Track, then assign it again. The explicit Play/Pause-off preference should survive.
7. Turn Play/Pause back on. The center visual and center recognition should return together.

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

The wide fixture should show the Track-local Play/Pause option and the enabled Previous / Play-Pause / Next lane as one continuous band. The editor copy must say **hold for about half a second, then release**, not imply a quick tap. If a deterministic Play/Pause-off fixture is present, confirm it contains only Previous/Next and no hidden-looking center separators. Corners must remain visually mirrored.

## Fans and hardware providers

Alpha.43 preserves the alpha.42 cooling lifecycle and alpha.41 low-level safety boundary. Audio Safety/Track-option work must not change these paths.

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

### Runtime truth and saved-profile persistence — carry-forward alpha.42 boundary

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
12. Confirm temporary/direct fan tests, where a future accepted direct writer exists, still retain their prior Auto-on-UI-exit/timeout safety class.

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

Before calling alpha.43 releasable, require repository hygiene, zero-warning Release build, all Core/source tests, real Compact/Advanced WPF shell smoke, the complete visual-QA matrix with representative screenshots manually inspected, Package ThinkControl, an exact final frozen-head CI + Package rerun, complete diff/review-thread review, expected-head merge, post-merge main verification, immutable promotion to `v0.1.0-alpha.43`, exactly Setup + Payload + `SHA256SUMS.txt` + `ui-overview.png`, and checksum verification of the published Setup/Payload.

Physical Touchpad, Audio Safety and X9 cooling checks remain separate evidence classes and must be recorded honestly rather than converted into hosted-CI claims.
