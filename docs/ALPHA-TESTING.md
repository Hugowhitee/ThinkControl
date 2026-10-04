# ThinkControl alpha testing guide

Use this checklist for the **v0.1.0-alpha.60** experimental candidate and retain prior immutable **v0.1.0-alpha.59** cases as historical coverage. ThinkControl is a public repository, so its GitHub-hosted CI and Package workflows remain available even while private-repository included minutes are constrained. Use one exact-head run per meaningful checkpoint and avoid redundant reruns. Physical X9 behavior, real Windows audio behavior and real battery charging behavior remain separate evidence classes and must never be inferred from hosted runners.

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

## Alpha.46 Advanced shell regression

1. On a new/unknown-device learning state, Advanced must show `New device · completed/total` in the existing brand-row footprint rather than adding another sidebar row.
2. At the documented minimum Advanced window size, the learning indicator must remain fully visible and the navigation must not gain new clipping.
3. When the report becomes ready, the same surface must show `Report ready` and remain clickable to Settings.
4. When compatibility learning is no longer active, the normal ThinkControl wordmark must return.
5. Notification and Compact-view utilities must remain separately available; the learning status must not recreate the removed `ThinkControl.NotificationSlot` path.
6. Inspect the dedicated dark minimum-window and light report-ready WPF snapshots before promotion.

## Alpha.57 Fans composition and design handoff

1. Inspect Fans at minimum, normal and wide Advanced sizes in dark and light. The main flow must read as one **Cooling** section rather than separate Profile / Thermal control / Fan telemetry cards.
2. Confirm the Cooling profile selector remains the primary action, real temperature/RPM stay visible without invented channels, and the active controller owner is scanable without implementation narration.
3. On firmware-policy/X9 fallback states, **Edit curves** must not appear as a disabled primary control. Advanced fan controls may explain that custom curves are unavailable.
4. On a physically accepted direct writer, expand Advanced fan controls and confirm **Edit curves**, calibration and temporary-test paths remain reachable and unchanged in safety semantics.
5. Trigger a profile-write failure. Fans should show concise recovery copy and point to System for persistent problems; low-level LITSSvc/provider detail belongs in diagnostics rather than the normal control surface.
6. Compare the production WPF screenshots with the editable Fans Figma source recorded in `docs/UI-DESIGN-HANDOFF.md`. Production typography remains Segoe even though the Figma host uses Inter as a representation fallback.
7. Physical Quiet/Balanced/Max behavior on the reference X9 remains a separate real-device evidence class and must not be inferred from hosted visual/build gates.

## Alpha.59 draft candidate (issue #113, PR #114)

Alpha.59 is not released; historical alpha.58 tests below describe the earlier product.

- Modes: choose No mode, Focus, Battery saver and Performance from one dropdown. Explanatory rows are not competing selectors. Saving edits does not force a mode active; choosing it manually does. Verify per-facet failure and failed rollback states, and synchronization with Home/Compact.
- Cooling: physically verify Lenovo X9 Auto, Quiet, Balanced and Max under AC/DC, boot, resume, external Lenovo Vantage/F8 and Windows power policy changes. Compare actual acoustics/RPM/temperature under controlled safe loads. The OEM policy pipe acknowledgment alone is not readback. Direct EC and target-RPM writers remain blocked.
- Battery: review 80/85/90/95 presets, supported custom start/stop 5% windows, external custom preservation, real charge-stop/restart under AC. Do not imply a precise cycle-wear reduction or repeated charging when plugged in.
- UI: inspect WPF min/normal/wide dark/light visual QA for all pages; Defaults and Windows ↗ links must share controls.
- Require exact-head Windows CI, ShellSmoke, artifact inspection, packaged upgrade/service lifecycle and real X9 validation before merging/promoting.

## Alpha.56 real-device fan, Modes and updater stabilization

1. On the reference X9, choose Quiet while Windows performance is Balanced. The baseline command may use AC Balanced command 503; a clean pipe close without the legacy Int32 reply must not block the subsequent Quiet policy command.
2. Repeat Quiet, Balanced and Max on AC and battery. The selected profile must not fail solely because LITSSvc closes cleanly after accepting an allowlisted policy write. Partial replies, access errors and true timeouts still fail closed.
3. Open Modes with no custom modes. Focus, Battery saver and Performance must be visible starter choices. Focus must disable ThinkControl Touchpad gestures so edge gestures are off.
4. In the Modes list, click the mode row itself to select it; there must be no separate Activate/Use button. Edit remains a separate secondary action and must not activate the mode.
5. On Home, Keyboard Off / Low / High / Auto must share the same vertical baseline/rhythm as the Display segmented control beside it.
6. Start with a recent successful update-check timestamp and no in-memory result. Updates must settle to an idle terminal status rather than staying on Checking automatically/Checking forever.
7. Simulate an unreachable release endpoint. A check is bounded to 20 seconds and must leave the Checking state with a failure result.
8. Run the normal installer/update compatibility and exact-head visual matrix before promotion.

## Alpha.55 context Modes and X9 fan ownership

1. Create a mode with Performance, Cooling, Refresh rate, Audio, Touchpad gestures and Keyboard light in different sparse combinations. Omitted settings must remain untouched.
2. Build a **School** mode with Wi-Fi trigger chosen from the current/saved Windows profiles or typed manually. Save without applying while away from school. After a stable match, the mode takes effect; disconnect/leave school and confirm the previous manual mode or regular settings return. Test a Wi-Fi roam shorter than five seconds: it must not flap. Manual choice wins until context changes.
3. Repeat with process, AC/battery, battery threshold and schedule. Test both Any/All on multi-trigger modes. When modes overlap, High > Normal > Low priority, then Process > Wi-Fi > Battery threshold > Power > Schedule; stable ties do not flap. Leaving a high-priority mode should activate a still-matching lower-priority mode before ultimately restoring the manual state.
4. Leave an automatically activated mode and confirm its owned settings restore. A subsystem changed manually while the mode is active must remain manual and the mode must show Modified.
5. On the reference X9, force/observe a transient provider/telemetry miss. Fans must still expose Auto / Quiet / Balanced / Max through the verified Lenovo firmware-policy fallback; custom curves and manual percentages remain unavailable without a physically accepted direct writer.
6. Select Quiet, Balanced and Max repeatedly on the physical X9. The selector must not snap to Auto merely because RPM/provider telemetry is temporarily missing. Each profile action must complete through the reviewed Lenovo policy path or show a real error.
7. Confirm transient LITSSvc pipe delays are retried only within the bounded request window. Unauthorized access remains a hard failure; raw EC/target-RPM writers remain blocked.
8. Inspect Modes list/editor and the exact X9 firmware-fallback WPF fixtures in dark and light before promotion.

## Alpha.53 Modes and shared-header candidate

1. Compact and Home show **Mode**, not a duplicate Audio Safety mini-editor. Normal, Gesture lock and Silent remain available; Silent must not change cooling, Windows performance, microphone, Battery Preservation or display state.
2. In Advanced → Modes, activate each built-in. Normal owns no temporary subsystem state; Gesture lock and Silent keep the existing Audio Safety semantics.
3. Create a custom mode with only one control. Activating it must leave every omitted subsystem unchanged. Add Audio Safety, Touchpad gestures and Keyboard light in different combinations and verify the editor never exposes unsupported fan/performance/battery settings.
4. While a custom mode owns multiple facets, manually change one owned subsystem from its normal page. The mode must remain active as **Modified** and only that facet becomes manual. Leaving the mode must restore still-owned facets without undoing the manual override.
5. Reapply a Modified mode. The current manual state becomes the new rollback baseline for the reclaimed facet; leaving the mode later returns to that baseline.
6. Edit Touchpad sensitivity or edge actions while a mode temporarily owns Touchpad On/Off. Those edits may persist, but the temporary enabled state must not leak into the saved gesture preference. Only explicitly toggling Edge gestures releases that facet.
7. Restart ThinkControl after using a custom mode. Custom definitions remain; the active mode starts at Normal.
8. Switch through **every** Advanced destination at minimum, normal and wide widths in dark and light. The title baseline and right-side action rail must not jump. No page may recreate its own title row or move ordinary status into the header.
9. Inspect the Modes list and custom editor for unnecessary explanatory copy, duplicate state labels, excessive cards or wrapped header actions. The interface must remain usable without implementation narration.
10. Run exact-head CI, Core/source tests, Compact ↔ Advanced ShellSmoke, the complete WPF matrix and Package ThinkControl. Manually inspect the uploaded visual gallery before release.

## Alpha.52 interaction and X9 safety candidate

1. On Advanced → Audio, drag **System volume** continuously across a wide range. The thumb/value must follow the pointer immediately without the short post-drag locked feeling; endpoint refresh must not pull it backward while the drag is active. Releasing the pointer commits the requested Windows endpoint value once.
2. Repeat with **Microphone input**. Keyboard adjustments must also commit without restoring a stale endpoint value. Navigate away during an unfinished drag and confirm no delayed off-page write occurs.
3. On Battery, verify **Battery preservation** has one shared-style on/off switch. The selector is organized by charge limit: 80% Strong protection, 85% Recommended, 90% More runtime and 95% Light protection; turning the switch off restores ordinary charging instead of selecting a fake `Full charge` preset.
4. With preservation active **and charging**, Battery ETA must say time to the active target (for example `to 85%`), not `to full`. At/near the cap on AC it should report the paused limit state; inside the start/stop hysteresis while plugged in it should say when charging resumes. Unplugged, it must return to normal remaining-runtime ETA instead of inventing a charge-to-target time.
5. The charge-wear line must compare **0% → selected limit** against the model's **0% → 100% = 100% wear reference**. It must stay stable when the live battery percentage changes, use whole-number percentages rather than fake precision, and never look like the firmware **CYCLES** counter. The tooltip explains that a real top-up starting above 0% has lower modeled session wear and that the top end is disproportionately stressful.
6. On Advanced Home, click **SENSORS**. It must open the existing live Sensor details window directly; other telemetry metrics retain their existing navigation behavior.
7. In Touchpad, select a top corner. **Reverse swipe closes ThinkControl** must use the same shared switch geometry as the rest of ThinkControl, not a square checkbox.
8. On Fans, a supported temporary direct-output test must expose one stateful action: **Start test** becomes **End test** while active. Target controls are locked during the test and the previous profile/Auto is still restored on timeout, page close or explicit End.
9. On the reference X9, raw/discrete EC fan output must not appear as an available direct writer. Auto / Quiet / Balanced / Max cooling remain available through the reviewed Lenovo firmware-policy/full-speed semantics. A missing OEM sample must never make the legacy EC writer reappear.
10. Run exact-head CI, ShellSmoke, deterministic dark/light WPF visual QA and Package ThinkControl. Manually inspect the visual artifact before release; a green build is not a substitute for UI review.
11. Switch through Performance, Fans, Battery, Display, Audio, Keyboard and Touchpad. Page-title baselines and the top-right Defaults / Windows links / switches must stay on one title-action rail rather than jumping vertically between tabs.

## Alpha.51 Compact dropdown dismiss regression

1. In Compact, open and dismiss **Performance**, **Fan mode**, **Refresh rate**, **Keyboard** and **Audio safety** one by one by clicking elsewhere inside ThinkControl. The closed selector must immediately return to its ordinary resting visual state; a lighter hover/focus fill must not remain latched.
2. Repeat by selecting an item, pressing Escape, and clicking a different Compact control. Closing a popup must not require deactivating the whole app to clear highlight/focus.
3. Confirm keyboard navigation still works after dismissal: Tab/Shift+Tab can focus selectors again and reopening a selector still behaves normally.
4. Confirm Advanced ComboBoxes retain the shared normal styling/behavior; alpha.51 must not fork the global `TcComboBox` template merely to fix Compact popup capture.
5. Inspect Compact dark and light visual snapshots for unchanged layout, spacing and disabled states. Hosted screenshots cannot prove the transient popup-capture bug, so the source regression plus the interaction check above are both required.

## Alpha.50 Silent ownership and preservation-impact regression

1. Enable **Silent** while audio is playing. Test both a normal click and the activation race: start holding Volume Up just before clicking Silent, release it after Silent becomes active, then repeat with Volume Down/Mute. The laptop must end muted, future repeats must be blocked, and releasing a key that began before the hook must never leave Windows behaving as if that key is still held.
2. While Silent is active, try changing mute/volume from Windows Settings, the system mixer and a normal app. A deliberate external unmute may momentarily request a state change, but CoreAudio must reassert mute promptly; no multi-second audible escape is acceptable.
3. Repeat step 2 rapidly while holding/repeating a control. The final state must still be muted; an event arriving during an existing enforcement pass must not be lost.
4. Change the default render endpoint while Silent is active. The new default output must become muted without waiting for the normal multi-second app status cadence. A brief not-ready device transition may use only the bounded 40/120/350 ms retry burst.
5. Leave Silent. The standard volume keys must work immediately again, and only endpoint mute states actually remembered by ThinkControl may be restored.
6. Confirm Gesture lock is unchanged: physical keyboard and Windows/app volume controls still work in Gesture lock.
7. On Battery Preservation, verify the graphic is a single current-level fill with exactly two aligned threshold markers. There must be no permanent three-color zones and no lightning/pause glyphs. While charging, the fill uses the normal accent; when parked at the upper cap it may switch to the warning state.
8. Verify the copy is plain language: for the 85% preset it reads **Charges up to 85%, then pauses. Charging starts again below 80%.** The state line should say **85% limit active**, not repeat the whole threshold pair.
9. Verify the visible wear estimate is a stable charge-limit comparison, not a live-session counter: **Charging from 0% to 90%: ~20% of the modeled wear of charging to 100%.** Changing the current battery level must not change that line. The expected preset values are approximately 80% → 6%, 85% → 11%, 90% → 20%, and 95% → 43% of the 0%→100% wear reference.
10. The wear line must remain explicitly comparative rather than claiming measured pack wear. Its tooltip/caveat must mention the generic Li-ion SOC/voltage model, the high-voltage end-charge relationship, and pack-dependent chemistry/temperature/use.
11. Review Compact/Advanced Silent states and Battery Preservation dark/light screenshots at full resolution. The wear line must stay visually secondary and the shorter gauge must not clip/collide with the selector.
12. From **Max cooling**, click fan **Auto** several times under live telemetry from **Home** as well as the Fans page. Home Auto must become the visible intent immediately, stay disabled while the write is in flight, and remain selected during the short post-success confirmation lease rather than bouncing back to stale Max/Quiet telemetry.
13. With Breathing or Reactive through the experimental fallback, repeated automatic level changes may hide only ThinkControl-generated Lenovo tposd popups. Disable the effect, then press Fn+Space: the normal Lenovo popup must return. Test quick disable during an effect burst and app Quit; no permanently hidden OEM popup.
14. Select keyboard **Audio**, play silence, quiet audio and louder audio. The fallback should visibly move through Off / Low / High instead of idling at Low; repeat after changing the default output device or after a stop/restart of playback.
15. Audio mode must store no audio and a loopback failure must not create a permanent restart loop; switching away from Audio must cancel any pending restart.
16. Install an `alpha.50-dev.N` package, then publish/check against canonical `alpha.50`. The updater must treat the public alpha.50 as newer. A dev test build must never strand the user from the matching public release.

## Alpha.49 Battery Preservation visual clarity

1. On Battery with preservation enabled, the bar must have exactly three semantic regions: green below the resume threshold, amber between resume/stop, and subdued red above the stop threshold.
2. The resume threshold must have a lightning/charge cue and its percentage; the stop threshold must have a pause/stop cue and its percentage. There must be no generic 10% ruler ticks and no lock glyph.
3. The live battery position must remain a distinct high-contrast marker and must not be mistaken for either threshold.
4. Verify the normal writable card in both dark and light theme at full resolution. Colors must remain distinguishable without becoming a traffic-light dashboard or overwhelming the rest of Battery.
5. The visible helper copy must stay concise and follow the same left-to-right meaning as the graphic: resume below start, stop at upper threshold.
6. Re-run the GitHub release promotion source regression: transient release-asset metadata/download propagation must retry rather than fail a valid immutable release.

## Alpha.48 UX clarity and live-state regression

1. On Advanced Home, enable fan **Auto**. Quiet / Balanced / Max and **More profiles** must become visibly disabled while the Auto switch remains enabled and clearly paired with its label. Turn Auto off and confirm Balanced becomes the deliberate fallback.
2. Inspect Compact Audio Safety in dark and light themes. The selector must be compact, aligned with its label and show `Normal / Gesture lock / Silent` without clipping.
3. Enable **Gesture lock**. ThinkControl Touchpad Volume, seek and Track media actions must be blocked, while the physical keyboard volume keys and ordinary Windows/app audio controls must still work. The UI/OSD must say Gesture lock rather than implying a global media lock.
4. Enable **Silent**, then use the physical keyboard volume/mute controls and an app/Windows volume control. The active output must remain/re-converge muted immediately from CoreAudio notification handling; a visible multi-second audible escape is a failure.
5. While Silent is active, switch the default output endpoint. The existing bounded status path may perform endpoint convergence; no new polling timer is allowed.
6. On Battery, inspect Battery Preservation with a live start/stop pair. The threshold view and current battery marker must be legible; the card should need only the concise stop/resume sentence in normal writable state.
7. Switch light → dark and dark → light while Advanced remains open. Sidebar shell-mode controls and the native caption/text/border must repaint immediately without minimize/reopen.
8. Review `advanced-home-fan-auto.png`, `advanced-home-fan-auto-light.png`, Compact safety states and Battery Preservation snapshots at full resolution before promotion.

## Alpha.47 mode and visual polish

1. Settings → **App icon opens** must use exactly `Compact` / `Advanced`; `Full` must not reappear as a competing user-facing name for Advanced.
2. Select Advanced as the opening preference and confirm Start/desktop/taskbar/second-launch routing opens Advanced while the tray icon still owns Compact.
3. In Compact, inspect `Normal`, `Media lock` and `Silent`; the selector and footer links must remain aligned with no clipping at the fixed production size.
4. Repeat the Compact Silent state in light theme and confirm the disabled volume state remains readable without looking broken.
5. In Advanced → Settings, inspect the Advanced-opening selection and Silent state in dark and light themes. Segment geometry, helper copy and active-state hierarchy must match the shared design system.
6. Review the dedicated WPF mode-state snapshots before promotion; green rendering alone is not sufficient.

## Audio lifecycle regression

1. Open Advanced → Audio.
2. Drag output volume continuously; the thumb/value must follow locally without repeated endpoint refresh snapping it back. Release once and confirm the Windows value converges to the requested value.
3. Navigate away during a drag, return, and confirm no delayed off-page write jumps the control later.
4. Repeat both checks with microphone level.
5. Leave Audio idle and confirm live endpoint state continues refreshing after the navigation cycle.

## Alpha.44 cold-start and edge-control stabilization

### Windows cold boot / saved cooling

1. Enable **Start with Windows**, save Quiet or Balanced, then perform a full Windows restart.
2. Do not open Compact/Advanced after sign-in. Confirm the saved cooling intent converges once the hardware service/provider becomes ready rather than waiting for a later window activation or sleep/resume.
3. Repeat with a deliberately delayed/restarted ThinkControl service during login. The bounded cold-start convergence may retry, but it must stop after its startup window and must not create permanent fast tray polling.
4. Change cooling profile while startup convergence is still possible; an older saved generation must never overwrite the new user choice.
5. Confirm runtime/service state—not merely settings JSON—reports the applied profile before treating the restore as successful.

### Continuous edge controls

1. Touch a configured Volume/Brightness edge, cross the normal recognition threshold and immediately release. Claim alone must not change the value.
2. Move less than roughly **1.5 mm** beyond claim; no continuous write should commit.
3. Move deliberately farther and confirm the control remains responsive and accelerates without large single-frame jumps.
4. While Windows/CoreAudio is delayed or the output endpoint is changing, move farther and release. ThinkControl must not later catch up through a hidden queue toward 0/100%.
5. After release/cancel, confirm no delayed gesture write remains pending.
6. For Track Previous/Next, movement below **12 mm** then release does nothing.
7. Cross **12 mm** deliberately; the skip commits once on release, not while the finger is still moving.
8. Track-center Play/Pause remains 450 ms / ≤3 mm / release-to-commit.

Hosted CI covers routing, bounds, readback ownership and lifecycle structure. Actual finger feel, audio-stack stalls and physical fan convergence remain real-device evidence.

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

## Touchpad — alpha.44 physical focus

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
9. Previous/Next in the current alpha.44 candidate needs **12 mm**, commits on release, and cannot also toggle Play/Pause.
10. OSD semantics remain **Playing + pause bars**, **Paused + play triangle**.

Release-to-commit is intentional: release is the final intent confirmation so a resting touch cannot start global media simply because a timer elapsed.

### Track control — Play/Pause disabled

1. Turn the Track-local switch off.
2. Center fill/separators/glyph disappear immediately.
3. Tap/hold the physical center repeatedly; it must never act as an invisible Play/Pause target.
4. Previous/Next continues with the current 12 mm release-to-commit threshold.
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

Alpha.44 preserves the alpha.43/alpha.42 cooling lifecycle and alpha.41 low-level safety boundary.

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
3. When the provider is writable, Battery Preservation has one on/off switch and the dropdown is organized by charge limit:
   - `80% · Strong protection` → resumes below 75%
   - `85% · Recommended` → resumes below 80%
   - `90% · More runtime` → resumes below 85%
   - `95% · Light protection` → resumes below 90%
4. If Lenovo currently has another valid pair, it should appear as `Custom · stop% limit · resume start%` and remain untouched until a preset is deliberately selected.
5. If PWRMGRV/IBMPmDrv is missing or inaccessible, the switch/dropdown stay read-only and the Lenovo settings fallback remains available.
6. Provider text must describe Lenovo PM Device/PWRMGRV state; it must not claim a generic EC threshold backend.
7. The comparative charge-wear line uses a percentage of a full 0→100% charge as its comparison baseline, explicitly distinguishes that model from the firmware battery cycle count, and does not promise a fixed lifetime improvement.

### Real X9 charge behavior

Use a test window that can be observed without repeatedly forcing unnecessary battery cycles. **80–85%** (the 85% Recommended preset) is the primary release check.

1. Read the current thresholds and record them before changing anything.
2. Select **85% · Recommended**. Service status must report `80–85%` only after the Lenovo PM Device calls and PWRMGRV readback succeed.
3. With AC connected and battery below the stop threshold, confirm normal charging can rise toward 85%.
4. Confirm charging stops/holds around the intended 85% boundary under normal conditions.
5. While battery remains above the 75% start threshold, confirm ordinary tiny top-ups do not repeatedly restart charging.
6. After battery drops below the start threshold in normal use, confirm charging can resume when AC is connected.
7. Restart only the UI, then restart service/reboot separately. The Battery page must re-read actual Lenovo state rather than painting a remembered desired value.
8. Switch **Battery Preservation off**. Confirm thresholds release and ordinary charging can continue beyond the prior ceiling when conditions permit.
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
