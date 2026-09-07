# ThinkControl alpha testing guide

Use this checklist for **v0.1.0-alpha.43** and later candidates built from it. Automated CI is required, but physical X9 behavior, real Windows audio behavior and real battery charge behavior remain separate evidence classes and must not be inferred from hosted runners. The X9 is the current reference device, not the product boundary.

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
3. Try every ThinkControl Touchpad audio/media action that is configured: Volume, Media scrub, Track Previous, Track Next, and integrated Track Play/Pause when enabled.
4. None of those Touchpad actions may alter playback/volume. A short bounded `Media locked` indication should explain the block instead of the gesture appearing broken.
5. Deliberately control playback/volume from the media app or ordinary Windows controls. Media lock must not mute the output, stop external playback or freeze the system's own controls.
6. Enter Media lock while a ThinkControl Touchpad audio gesture is already active. The in-flight action must be cancelled and must not continue issuing writes afterwards.
7. Brightness and non-audio Touchpad actions must continue working.

### Silent

1. Record whether the current Windows output endpoint is muted before the test.
2. Enable **Silent**. The active default Windows output must become muted before ThinkControl claims the mode transition succeeded.
3. Try Compact/Advanced output volume and mute controls. ThinkControl must not unmute or change output volume while Silent is active.
4. Try the same Touchpad Volume/Media scrub/Track actions. They remain blocked and should give bounded Silent feedback.
5. Change microphone level/mute from ThinkControl. Microphone input remains independent and must still work where the Windows capture endpoint is available.
6. While Silent is active, switch the default output to another real endpoint. The newly active endpoint should converge to muted using the existing application status cadence, without a new rapid polling loop.
7. Leave Silent. Each output endpoint ThinkControl encountered during Silent should return to the mute state it had before ThinkControl first touched that endpoint during this Silent session.
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
- Hold for one or two seconds without releasing. Nothing should auto-fire while the finger remains down.
- Natural tiny movement up to about **3 mm maximum radial excursion** should remain eligible.
- Move clearly beyond 3 mm, return near the start and release. Play/Pause must remain disarmed.
- After leaving the 3 mm hold envelope, ordinary Track recognition resumes. Previous/Next still requires the unchanged **9 mm** deliberate swipe threshold.
- A Previous/Next swipe must never also toggle Play/Pause on release.
- Successful media feedback uses current-state text plus next-action icon: **Playing + pause bars**, **Paused + play triangle**.

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

Inspect the exact release-head CI artifact, especially `advanced-touchpad.png`, `advanced-touchpad-min.png`, `advanced-touchpad-wide.png`, `advanced-touchpad-light.png`, both selected/live corner fixtures and relevant gesture OSD fixtures. The wide fixture should show the Track-local Play/Pause option and the enabled lane as one continuous band. Corners must remain mirrored.

## Fans and hardware providers

Alpha.43 preserves the alpha.42 cooling lifecycle and alpha.41 low-level safety boundary. Audio Safety/Track-option/battery work must not change these paths.

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
3. Close only the ThinkControl UI while leaving the Windows service running, then reopen the UI. Quiet must remain physically active.
4. Reboot Windows with Quiet saved. During startup the UI must not paint the persisted preference as applied before service telemetry says so. After provider discovery, ThinkControl must actively restore Quiet.
5. Continue listening through the first several seconds after login. The one bounded 7-second startup-settle reassert should prevent a later-starting Lenovo component from leaving the machine back on Auto/base policy.
6. While Quiet is active, unplug AC and then reconnect it. Quiet must remain the active cooling intent.
7. Put the machine to sleep and resume while Quiet is selected. Quiet must be reasserted rather than becoming a UI-only saved label.
8. While Quiet is active, change Windows Performance preference. The cooling override must remain Quiet, while the new Windows mode becomes the baseline that Auto restores later.
9. Select **Auto** afterwards and confirm that latest Windows/Lenovo baseline is restored.
10. Repeat with Balanced and, when safely exposed, Max cooling.
11. At no point should Fans show Quiet/Balanced/Max as applied solely because `settings.json` says so.

### Current exact-X9 physical expectations

Confirmed negative evidence remains: alpha.38 fixed target-RPM control repeatedly waved/re-kicked and nominal target 100% remained physically weaker than naturally hot Lenovo Auto. Therefore `fanX_target` remains read-only.

Current built-ins are expected to behave as:

```text
Auto         -> release ThinkControl full-speed ownership; restore latest Lenovo power-policy baseline
Quiet        -> full speed off; Lenovo Quiet policy
Balanced     -> full speed off; Lenovo Balanced policy
Max cooling  -> Lenovo Performance policy + verified 0x04020000 full-speed boolean when safely exposed
```

Under repeatable load, compare Quiet/Balanced/Max and record provider sources/RPM, but do not use RPM alone as proof of airflow intensity.

## Battery care — alpha.43

Alpha.43 adds an exact-X9 Lenovo Other Mode battery charge-type provider. Hosted CI can validate only the semantic gate/readback architecture; the real charge boundary needs the reference X9.

### Provider and UI truth

1. Open Advanced → Battery and inspect **Charge protection** before changing anything.
2. If the provider advertises a verified writable contract, the dropdown should show the actual firmware state: **Battery care · 80%** or **Full charge · 100%**. It must not default from a saved ThinkControl preference.
3. If the exact capability is unavailable or ambiguous, the dropdown stays disabled/read-only and the Battery settings fallback remains available.
4. Confirm there are only the two values supported by this provider. No arbitrary slider or invented 60/70/85/90/95% choices should appear.
5. Provider/detail text should report the live Lenovo semantic/readback state, not claim generic charge-threshold support.

### Real X9 charge behavior

1. Start from Full charge/Standard with the battery below 80% and AC connected if practical.
2. Select **Battery care · 80%**. The service response/readback must report 80%/Long Life before the UI claims the change succeeded.
3. Use the laptop long enough to verify normal charging stops/holds around Lenovo's intended 80% boundary. Do not mark this physically verified merely because WMI returned `1`.
4. Restart only the UI, then restart the service/reboot separately. Reopen Battery and confirm the selector reads actual firmware state rather than a remembered desired value.
5. Select **Full charge · 100%**. Readback must report Standard/100%, and ordinary charging should be able to continue above 80% when AC/battery conditions require it.
6. Repeat one 80 → 100 → 80 round trip and confirm every transition has truthful readback with no UAC from the normal-user UI.
7. If firmware rejects or changes the contract, ThinkControl must fail closed and leave the selector unavailable rather than trying another Lenovo feature or EC register.

The UI intentionally says **20 percentage points of headroom** rather than promising "x fewer cycles". Battery wear depends on more than charge percentage and that multiplier would be synthetic.

## Battery history — alpha.43

1. Open Battery with multiple recorded days. The history should be grouped by day, with compact daily charge/use totals rather than one endless session list.
2. Expand one day and open a session. Graphs and essential statistics should still open normally.
3. Open **Manage history**. The destructive reset is no longer a primary page button.
4. Change detailed-graph retention between **7 / 14 / 30 days**. The setting should persist and old detailed points should compact automatically while one-year summaries remain.
5. Confirm ordinary retention/compaction does not wipe learned estimates merely because old raw graph points expire.
6. Choose **Reset all history…** and cancel. Nothing should change.
7. Repeat and accept only in a test environment. The warning must state that local sessions, graphs, health trend and ThinkControl learned estimates are reset, while firmware battery health, cycle count and charge protection are untouched.
8. After reset, the Battery page should recover cleanly into a learning/empty state and future sampling should start building new history without errors.
9. Review minimum/normal/wide/light Battery screenshots. Charge protection and Manage history must not crowd the existing health/usage hierarchy.

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

Physical Touchpad, Audio Safety, battery charge behavior and X9 cooling checks remain separate evidence classes and must be recorded honestly rather than converted into hosted-CI claims.
