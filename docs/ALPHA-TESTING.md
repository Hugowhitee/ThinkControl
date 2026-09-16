# ThinkControl alpha testing guide

Use this checklist for **v0.1.0-alpha.43** and later candidates built from it. Automated CI is required, but physical X9 behavior, real Windows audio behavior and real battery charging behavior remain separate evidence classes and must not be inferred from hosted runners.

## Install/update sanity

1. Install/update using the versioned prerelease installer.
2. Confirm ThinkControl starts as a normal-user app and the hardware service reaches Running without leaving the UI elevated.
3. Confirm Compact ↔ Advanced transitions do not create duplicate/invisible windows.
4. Exercise Check for updates from Home and Updates. An up-to-date result must not enable Install, and **Last checked** must refresh immediately.
5. Confirm an in-place update preserves the existing install directory.
6. A successful update confirmation remains dismissable and never strands a topmost notification.

## Windows startup and background gestures

Alpha.41 established **input/tray first, rich discovery later** and alpha.43 preserves it.

1. Enable **Start with Windows** and an obvious edge gesture.
2. Reboot or sign out/in and do not manually open ThinkControl.
3. Confirm the tray process appears without showing Compact/Advanced.
4. Try the configured gesture as soon as the process exists. It must not require first activating a ThinkControl window.
5. Repeat after a cold reboot and record time from desktop availability to first successful gesture.
6. Open ThinkControl afterwards and confirm rich hardware data fills asynchronously.
7. Disable gestures, restart with `--tray`, and confirm Raw Input is not kept alive solely because Start with Windows is enabled.
8. Confirm a normal visible launch still paints promptly rather than regressing into a black/blank first frame.

## Crash/shell regression

- Open/close Advanced repeatedly.
- Minimize Advanced, reopen directly to Touchpad, and confirm it becomes visible.
- Navigate Compact → Advanced → Compact several times.
- Open/dismiss the Inbox/notification sheet.
- Retain crash-report/journal evidence if a failure occurs; one later clean session is not proof the root cause disappeared.

## Audio lifecycle regression

1. Open Advanced → Audio.
2. Drag output volume and navigate away while dragging, then return.
3. Repeat with microphone level.
4. Confirm no delayed off-page write jumps a control later.
5. Leave Audio idle and confirm live endpoint state continues refreshing after the navigation cycle.

## Audio Safety — alpha.43

Audio Safety is one session-level state shared by Compact, Settings, Touchpad routing and Windows output writes. It is deliberately **not persisted** across process restart in alpha.43.

### State/UI truth

1. Fresh process starts at **Normal**.
2. Change Compact to **Media lock**; Settings must show the same canonical state.
3. Change Settings to **Silent**; Compact must immediately agree.
4. Return to Normal from either surface and confirm the other follows.
5. Restart after using Media lock/Silent. The new process starts Normal.
6. Confirm no new navigation page or standalone Touchpad Mute action was added.

### Media lock

1. Start media deliberately from a browser/app.
2. Enable Media lock.
3. Try ThinkControl Touchpad Volume, Media scrub, Track Previous/Next and integrated Play/Pause.
4. None may alter playback/volume; bounded `Media locked` feedback should explain the block.
5. External Windows/app controls must still work normally.
6. Enter Media lock while a ThinkControl audio gesture is active; the in-flight action must stop.
7. Non-audio gestures remain functional.

### Silent

1. Record the current default output endpoint's mute state.
2. Enable Silent; the active output must become muted before ThinkControl claims success.
3. ThinkControl output volume/unmute controls must not escape Silent.
4. Touchpad media/output actions remain blocked.
5. Microphone input remains independent.
6. Switch the Windows default output while Silent is active; the newly encountered endpoint should converge to muted on the existing bounded status cadence.
7. Leave Silent; each endpoint encountered during that Silent session returns to the mute state ThinkControl recorded before first touching it.
8. An endpoint already muted before Silent stays muted after exit.
9. A disappeared endpoint is not replaced by a guessed fallback write.
10. Orderly app exit while Silent also restores owned states.

Real endpoint switching/audible silence requires real Windows testing; hosted CI only proves policy/ownership code paths.

## Keyboard

- Test Off, Low and High where the provider exposes them.
- Test Auto only when a verified firmware/OEM Auto path exists; Auto is not a software idle-dimming effect.
- Confirm Fn+Space/readback remains sensible on Lenovo hardware.
- Breathing/Reactive/Audio appear only with `KeyboardEffects`.
- Confirm a saved effect is not restored before provider capability is known.
- The Lenovo Vantage fallback must not advertise repeated effects if doing so causes OEM popups.

## Touchpad — alpha.43 physical focus

### Six-zone editor/corners

- Edge and corner selection remain mutually exclusive.
- Top-left/right are exact mirrors in geometry and state treatment.
- Edge bands clip cleanly around enabled corners.
- A corner candidate owns the contact from the first eligible frame and rejected input stays locked until lift.
- Ordinary edge gestures still work outside corner ownership geometry.

### Track control — Play/Pause enabled

1. Assign Track control to Bottom.
2. Confirm the selected Track editor has a **Play / Pause** switch but the action menu has no standalone current Play/Pause entry.
3. With the switch on, the lane remains one continuous **Previous | Play/Pause | Next** affordance.
4. Quick center taps must do nothing.
5. Hold the center for roughly half a second, remain mostly still, then release. It toggles exactly once **on release**.
6. Holding one or two seconds without releasing must not auto-fire.
7. Natural movement up to about **3 mm maximum radial excursion** remains eligible.
8. Move beyond 3 mm, return near the start, release: Play/Pause remains disarmed.
9. Previous/Next still needs **9 mm** and cannot also toggle Play/Pause on release.
10. OSD semantics remain **Playing + pause bars**, **Paused + play triangle**.

Release-to-commit is intentional: release is the final intent confirmation so a resting touch cannot start global media simply because a timer elapsed.

### Track control — Play/Pause disabled

1. Turn the Track-local switch off.
2. Center fill/separators/glyph disappear immediately.
3. Tap/hold the physical center repeatedly; it must never act as an invisible Play/Pause target.
4. Previous/Next continues with the same 9 mm threshold.
5. Reopen Touchpad and confirm the choice persists.
6. Move/remove/reassign Track and confirm the explicit Play/Pause-off choice survives.

### Edge assignment swapping

- Give edges distinct actions.
- Select an action already used on another edge; the two action kinds must swap rather than clearing the old edge.
- Sensitivity/inversion remain with their physical edges.

### Reverse close

- Enable **Reverse swipe closes ThinkControl** on both top corners.
- Start from several points in the **inner half of the visible diagonal lane** and swipe toward the physical corner.
- Compact/Advanced should hide reliably on both mirrored sides.
- The outer guard remains an inward-launch start.
- With reverse close disabled, the same outward swipe must not hide ThinkControl.
- Rejected reverse input must not fall through into an edge gesture while the same contact remains down.

### Touchpad visual QA

Inspect exact-head renders for normal/minimum/wide/light Touchpad plus both selected/live corner fixtures. Confirm Track-local Play/Pause state is visually truthful, the lane remains coherent, and corners remain mirrored.

## Fans and hardware providers

Alpha.43 preserves the alpha.42 cooling lifecycle and alpha.41 low-level safety boundary.

- The rejected per-fan `fanX_target` writer remains read-only.
- `0x04020000` full speed remains a separately exact-X9-gated boolean semantic.
- No classic-EC fallback or guessed EnergyDrv command is introduced.
- Firmware policy remains distinct from direct RPM/PWM control.
- Manual percentages and Raw EC diagnostics stay hidden on the X9 firmware-policy backend.
- Lower profiles and Auto release ThinkControl-owned full speed before claiming the lower state.
- Failed full-speed probe/readback fails closed.

### Saved-profile runtime truth

1. Start in Auto and confirm Fans reports service runtime, not merely saved settings.
2. Select Quiet and verify service + physical behavior agree.
3. Close only the UI while service stays running, reopen, and confirm Quiet remains physically active.
4. Reboot with Quiet saved. UI must not paint Quiet before service restoration succeeds.
5. Listen through the bounded seven-second startup convergence retry and confirm late Lenovo login work does not leave the machine back at Auto/base policy.
6. Unplug/replug AC while Quiet is active; the intent remains Quiet.
7. Sleep/resume; Quiet is reasserted.
8. Change Windows Performance preference while Quiet remains selected; the new Windows mode becomes Auto's restore baseline without cancelling Quiet.
9. Select Auto and confirm that latest baseline is restored.
10. Repeat with Balanced and safely exposed Max.

Physical fan acceptance remains separate from CI. RPM telemetry alone is not proof of airflow intensity.

## Battery preservation — alpha.43

Alpha.43 uses the verified-X9 Lenovo Windows Power Manager threshold path (`PWRMGRV` + `IBMPmDrv`). Hosted CI can prove the identity/range/fixed-command/rollback architecture; it cannot prove the battery physically obeys the charge boundaries.

### Provider/UI truth

1. Open Advanced → Battery before changing anything.
2. The card must show **actual Lenovo state**, not a saved ThinkControl preference.
3. When the provider is writable, the dropdown offers only the small named presets:
   - `Daily · 75–85% (recommended)`
   - `Desk · 55–80%`
   - `Maximum care · 40–60%`
   - `Full charge · 100%`
4. If Lenovo currently has another valid pair, it should appear as `Custom · start–stop%` and remain untouched until a named preset is deliberately selected.
5. If PWRMGRV/IBMPmDrv is missing or inaccessible, the dropdown stays read-only and the Lenovo settings fallback remains available.
6. Provider text must describe Lenovo PM Device/PWRMGRV state; it must not claim a generic EC threshold backend.
7. No UI claims “x fewer cycles”. The impact explanation should state the real start/stop boundary, headroom below full and hysteresis trade-off.

### Real X9 charge behavior

Use a test window that can be observed without repeatedly forcing unnecessary battery cycles. **75–85%** is the primary release check.

1. Read the current thresholds and record them before changing anything.
2. Select **Daily · 75–85%**. Service status must report `75–85%` only after the Lenovo PM Device calls and PWRMGRV readback succeed.
3. With AC connected and battery below the stop threshold, confirm normal charging can rise toward 85%.
4. Confirm charging stops/holds around the intended 85% boundary under normal conditions.
5. While battery remains above the 75% start threshold, confirm ordinary tiny top-ups do not repeatedly restart charging.
6. After battery drops below the start threshold in normal use, confirm charging can resume when AC is connected.
7. Restart only the UI, then restart service/reboot separately. The Battery page must re-read actual Lenovo state rather than painting a remembered desired value.
8. Select **Full charge · 100%**. Confirm thresholds release and ordinary charging can continue beyond the prior ceiling when conditions permit.
9. If a driver call/readback fails, the UI must report rejection and the provider must request rollback rather than trying another EC/ACPI path.
10. The normal-user UI must never show UAC for these changes; privileged access belongs to `ThinkControl.Service`.

Do not convert a successful driver/registry response into “physically verified” until the real battery behavior above is observed.

## Battery history — alpha.43

1. With multiple recorded days, the normal page shows the most recent **7 days** only.
2. `Show older` expands to 14 days without changing retention or deleting data.
3. Days remain compact summaries; expand a day and open a session to verify graphs/statistics still work.
4. Open **Manage history**. Destructive reset is not a primary page action.
5. Change detailed-graph retention between **7 / 14 / 30 days** and verify persistence.
6. Old detailed point arrays should compact automatically while one-year summaries remain.
7. Ordinary compaction must not erase learned estimates merely because raw graph points expire.
8. Choose **Reset all history…** and cancel; nothing changes.
9. Accept reset only in a test environment. The warning must say local sessions/graphs/health trend/learned estimates are cleared while firmware health, cycle count and charge thresholds are not changed.
10. After reset, ThinkControl should safely begin learning new history again.

## Release acceptance

Before calling alpha.43 releasable, require:

- repository hygiene;
- Release build with no unexpected warnings/errors;
- all Core/source tests;
- real Compact ↔ Advanced WPF ShellSmoke;
- complete visual-QA matrix with representative screenshots manually inspected;
- Package ThinkControl including installer/service/IPC and oldest-supported updater compatibility;
- exact final frozen-head CI + Package rerun after `releaseReady=true`;
- final changed-file/diff/review-thread review;
- expected-head merge;
- post-merge `main` verification;
- immutable `v0.1.0-alpha.43` promotion;
- exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`;
- checksum verification of published Setup/Payload.

Physical Touchpad, Audio Safety, battery charging and X9 cooling checks remain separate evidence classes and must be recorded honestly rather than converted into hosted-CI claims.
