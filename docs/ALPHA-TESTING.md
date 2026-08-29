# ThinkControl alpha testing guide

Use this checklist for **v0.1.0-alpha.35** and later candidates built from it. Automated CI is required, but physical X9 behavior remains a separate evidence class and must not be inferred from hosted runners.

## Install/update sanity

1. Install or update using the versioned GitHub prerelease installer.
2. Confirm ThinkControl starts as a normal-user app and the hardware service reaches Running without the UI remaining elevated.
3. Confirm Compact opens, Advanced opens from Compact, and switching back does not create duplicate windows.
4. Exercise Check for updates once. An up-to-date result must not enable an install action; a successful update confirmation must remain dismissable and must not strand a topmost notification over Advanced.
5. After an in-place update, confirm the previous install directory is preserved and the app relaunches into the expected surface.

## Crash/shell regression

The recurring `TargetParameterCountException` dispatcher bug was fixed and guarded in alpha.33. Keep issue/field validation separate from the source-level fix.

- Open/close Advanced repeatedly.
- Minimize Advanced, reopen directly to a heavy page such as Touchpad, and confirm the window becomes visible rather than remaining minimized/invisible.
- Navigate Compact → Advanced → Compact several times.
- Open and dismiss the Inbox/notification sheet.
- If a crash occurs, keep the generated report/journal evidence rather than marking the issue solved from a single clean session.

## Audio lifecycle regression

Alpha.35 adds a specific navigation-lifecycle fix and source regression guard.

1. Open Advanced → Audio.
2. Drag output volume, navigate away while dragging, then return.
3. Repeat with microphone level.
4. Confirm no delayed off-page write visibly jumps the control when returning.
5. Leave the Audio page idle for several seconds and confirm live output/microphone state continues refreshing after the navigation cycle.

The expected behavior is that Audio output/microphone debounce timers and temporary drag flags are discarded when the page hides or unloads.

## Keyboard

- Test Off, Low and High where the active provider exposes them.
- Test Auto only when ThinkControl reports a verified Lenovo/OEM Auto path. **Auto is firmware-managed; it is not a ThinkControl idle-dimming effect.**
- Confirm Fn+Space and ThinkControl readback remain sensible after changing firmware Auto/static modes.
- Breathing, Reactive and Audio belong to the Effects card and require the direct backlight provider.
- When only the Lenovo Vantage fallback is available, effects should remain unavailable rather than generating repeated Lenovo brightness pop-ups.

## Touchpad

The editor has one six-zone model: Top, Bottom, Left, Right, Top-left and Top-right.

- Selecting an edge must clear a selected corner, and selecting a corner must clear the edge selection.
- Top-left and top-right guides must be exact mirrors: same guard radius, lane size, angle, rounded end arc, boundary weight and selected/live treatment.
- Enable one corner launch and confirm the visible quarter-circle guard, diagonal lane and rounded end-cap correspond to the actual usable launch start area. The guard should reserve runtime input only while that corner action is enabled; turning the action Off must leave the nearby top/side edge available again.
- Start near either edge of the enabled quarter-circle guard where a side/top gesture would otherwise be plausible. The corner candidate must own that contact from the first frame and a rejected corner must stay locked out until lift rather than falling through into the edge recognizer.
- Start an ordinary edge gesture outside the corner guard/lane and confirm the edge still behaves normally.
- Confirm the old neutral center divider is gone: the lane should show a directional arrow, the inner end should be a semicircular arc rather than a flat 90-degree cross-line/filled blob, and enabled Compact/Advanced corners should show the matching semantic icon and text.
- Select each corner and confirm its editor exposes **Reverse swipe closes ThinkControl**. With it enabled, start in the rounded inner end-cap and swipe diagonally back toward the physical corner; Compact or Advanced should hide to tray. With it disabled, that outward swipe must not close ThinkControl.
- Verify reverse-close on both mirrored corners and confirm a wrong-direction/rejected reverse candidate cannot become a nearby edge gesture while the same contact remains down.
- Test corner candidate/active behavior under real touch and confirm the settings layout does not reflow or oscillate.
- Inspect selected and live fixtures at minimum, normal and wide Advanced sizes plus light/dark states. The existing left fixture covers inward launch; the mirrored right fixture covers the opt-in reverse-close state.
- Leave Touchpad for another page and confirm the rest of Advanced remains responsive during normal touchpad use.

## Fans and hardware providers

- Unsupported devices must remain safe/read-only.
- On the verified X9 path, fan writes must be enabled only after a concrete provider passes its own capability gate; model identity by itself is not permission to write.
- Test Lenovo Auto return after every manual/custom fan state.
- Do not interpret a UI control being visible as proof that a hardware write succeeded; verify status and physical response.
- If PawnIO is missing/stale, test the existing repair/restart path before changing EC assumptions.
- Manual percentage and graph-curve operations should appear as fan-control diagnostics rather than generic hardware events, and diagnostics should name the active provider rather than always claiming ThinkPad EC.

For draft PR #71, use this order and **do not start seven-step fan calibration unless the Fans page explicitly reports the EC fallback**:

1. Start in **Lenovo Auto** and leave ThinkControl open. Confirm OEM fan behavior remains smooth and merely opening the app does not disturb fan speed.
2. Open Advanced → Fans and record the provider shown in the status/detail text.
   - If it reports **Lenovo OEM target-RPM / Other Mode**, record the displayed Fan 1 and Fan 2 OEM target ranges. EC calibration and raw-step controls must be hidden.
   - If it reports the **verified X9 discrete EC fallback**, raw steps/calibration may be visible and the old EC-specific checks below apply.
   - If neither writable provider passes, ThinkControl must remain firmware-managed/read-only.
3. With the OEM target-RPM provider active, test temporary manual **25%, 50%, 75% and 100%**. The status text should show the concrete requested RPM for each fan, all targets must stay inside the displayed Lenovo-reported ranges, and Fan 1/Fan 2 telemetry should settle plausibly without the old repeating wave/re-kick behavior.
4. Still on the OEM provider, test Quiet, Balanced and Max Cooling. Curves should use continuous target percentages rather than collapsing to EC steps. Listen for smooth ramps and watch for stale RPM after each change.
5. At OEM **100%**, compare the actual settled RPM/noise/cooling directly with a naturally hot **Lenovo Auto** state. This is the key acceptance check: the target-RPM provider is useful only if its effective high-cooling range is materially equivalent to what Lenovo can use. Lenovo documents Fan Test Data min/max as reference constraints and notes firmware can physically run outside them, so a green OEM API alone does **not** prove the range problem is solved.
6. Return to Lenovo Auto several times. Confirm both fans hand back cleanly with no stall, zero-RPM surprise, stale manual target or persistent left/right divergence. A partial OEM multi-fan write failure should also request Auto for all writable channels.
7. If the EC fallback is active instead, enter Quiet, Balanced and Max Cooling one at a time. The previous RPM should disappear/settle rather than remain displayed while the physical fans have already changed speed. Managed Fan 1/Fan 2 values should be plausible; temporary unavailable telemetry is preferable to reusing one old/ambiguous fan value.
8. On the EC fallback only, manual 100% remains **standard EC step 7**, the highest verified normal EC state. Do not report it as Lenovo Auto's hottest or absolute physical fan ceiling. The separate `0x40` full-speed/disengaged family remains blocked because exact-X9 testing has not validated it safely.
9. After the session, Advanced → Diagnostics → **Export support bundle** should contain bounded X9 fan samples from the existing status stream (profile, control temperature, applied output and up to two RPM values). The diagnostics path must not create another hardware polling loop.
10. If Other Mode is unavailable **or** its constrained 100% still falls materially below hot Lenovo Auto, run `tools/research/Capture-LenovoAuto.ps1` for naturally occurring `lenovo-auto-hot` and `lenovo-auto-cool` states plus a managed comparison. The script is observational: it uses ThinkControl `GetStatus`, Lenovo/LITSSvc state, read-only EnergyDrv queries and local OEM binary evidence scanning. It never invokes `ChangeFanSpeed 0x8310257C`, dust-removal/high-speed `0x831020C0`, arbitrary EC writes or brute-forced command values.

The standard ThinkPad distinction remains important: EC levels `0..7` are normal manual states; `0x80` hands cooling back to firmware Auto; the separate `0x40` full-speed/disengaged family is not the same as step 7. Lenovo Other Mode target-RPM control is a different OEM contract again and must be evaluated by its actual physical range, not by its name.

The alpha.35 cleanup removes obsolete **current-client** cooling wrappers. The service-side legacy cooling IPC remains intentionally present for installed-client compatibility and is still covered by the immutable alpha.14.1 updater fixture.

## Diagnostics/device learning

- Normal supported hardware should not run an expensive discovery flow every time the app opens.
- Unknown/new-device collection should remain passive and hardware-focused.
- Sharing remains explicit; verify the preview contains no usernames, serial numbers, personal file paths/content, keystrokes or raw touch trails.
- After a successful share/report flow, the UI should not keep claiming the same report is still ready to send as if nothing happened.

## Repository/release hygiene

Current validation is intentionally split by ownership rather than duplicated across three full Windows builds:

- **CI** owns repository hygiene, solution build/tests, real Compact ↔ Advanced ShellSmoke and the WPF visual-QA matrix;
- **Package ThinkControl** owns candidate publish/payload/bootstrap plus the deep installer/service/IPC lifecycle, non-elevating UI contract, custom-location update behavior, clean uninstall and real oldest-supported alpha.14.1 → candidate updater regression;
- ordinary PR Package runs do not render a duplicate copy of the 85-snapshot visual matrix; tagged/versioned release packaging still renders the public release overview;
- superseded PR CI/Package runs should cancel rather than consume stale Windows runner time;
- immutable/tag release packaging must remain non-cancellable by that PR/ref optimization.

Do not recreate a standalone full installer build merely because an older checklist names one. If validation ownership changes in the future, inspect the current workflow definitions and preserve equivalent or stronger coverage.

## Release acceptance

Before calling a candidate releasable, require:

- repository hygiene;
- Release build with no unexpected compiler warnings;
- Core tests;
- real Compact/Advanced WPF shell smoke;
- complete visual-QA matrix with representative screenshots manually inspected;
- Package ThinkControl including its current deep installer/service/IPC and oldest-supported updater compatibility checks;
- exact final PR-head validation after version/docs are frozen;
- final diff/changed-file review so new code is actually wired and duplicate/dead paths are intentional;
- squash merge with expected-head guard where supported;
- promotion/tag verification and an immutable prerelease with exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`;
- successful checksum verification of the published Setup/Payload.

Physical hardware checks above remain follow-up evidence and should be recorded honestly rather than converted into automated claims.
