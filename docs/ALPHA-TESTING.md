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
- Test Lenovo Auto return after every ThinkControl-owned manual/custom fan state.
- Do not interpret a UI control being visible as proof that a hardware write succeeded; verify status and physical response.
- If PawnIO is missing/stale, test the existing repair/restart path before changing EC assumptions.
- Manual percentage and graph-curve operations should appear as fan-control diagnostics rather than generic hardware events, and diagnostics should name the active provider rather than always claiming ThinkPad EC.

For draft PR #71, the current physical evidence has already ruled out treating seven-step EC control as the finished X9 product path. `dev.1191` exposed both physical fan RPMs, but the writable provider stayed `Fallback · verified X9 discrete EC telemetry/control`, Max Cooling remained below naturally hot Lenovo Auto, and the EC path could still have a faint electronic/buzzy character. The next candidate therefore validates native Lenovo telemetry first and deliberately fails closed on fan writes when that native path is proven.

Use this order:

1. Install the exact PR artifact and restart ThinkControl/the hardware service as the installer normally does. Start in **Lenovo Auto**. Do not select an EC profile just to make the page look active.
2. Open Advanced → Fans and record the hardware/provider detail plus Fan 1/Fan 2 source text.
   - The desired `EnergyDrv` result is two channels sourced from `Lenovo EnergyDrv · QueryFanSpeed 0x83102570`.
   - If a fully constrained `LENOVO_OTHER_METHOD` writer unexpectedly appears on a newer firmware/software stack, record its target ranges and stop; that is a different writable path that needs its own physical range test.
   - If neither native path exposes two channels, record the exact detail rather than assuming absence from one failed sample.
3. If two native Lenovo channels are shown while no validated OEM writer exists, **fan controls must remain read-only/disabled**. The page should no longer advertise the discrete EC writer merely because PawnIO can reach `0x2F`.
4. Keep Lenovo Auto active while the machine naturally moves between low and higher cooling. Confirm Fan 1/Fan 2 RPM changes plausibly track the physical sound and that merely observing the page does not introduce the previous wave/re-kick/buzzy behavior.
5. With the native two-channel path successfully visible once, use the existing provider-refresh/repair action if convenient, or simply leave the page running long enough to catch a temporary query miss. During the same hardware-service lifetime, a transient native telemetry failure must **not** make EC fan controls reappear. The service deliberately latches native two-fan evidence until restart.
6. Export a Diagnostics support bundle after the native observation. The bounded fan samples should preserve the provider/source distinction without starting another polling loop.
7. Run `tools/research/Capture-LenovoAuto.ps1 -Label lenovo-auto-hot -BundleRelevantOemBinaries` during a naturally hot Auto state and a separate `-Label lenovo-auto-cool` capture. The script is observational: it uses ThinkControl `GetStatus`, Lenovo/LITSSVC state, read-only EnergyDrv queries and local OEM binary evidence scanning. It never invokes `ChangeFanSpeed 0x8310257C`, dust/high-speed `0x831020C0`, family-specific Geek/full-speed overlays, arbitrary EC writes or brute-forced command values.
8. Share/review the JSON and optional OEM-binary ZIP so the exact X9 `dwFanCtrlCmd` semantics can be recovered from evidence. Public code proves that `ChangeFanSpeed 0x8310257C` exists, but does not define the X9 command encoding.
9. Only after a native writer is recovered and gated should managed profiles be re-enabled for final validation. Then test 25/50/75/100%, Quiet/Balanced/Max Cooling, compare the maximum directly with naturally hot Lenovo Auto, and repeatedly return to Auto. Acceptance requires smooth settling, truthful two-fan telemetry, reliable handoff and materially equivalent useful high-cooling range.
10. The classic EC path remains investigation/fallback evidence for configurations where no native Lenovo fan surface has ever been established. EC step 7 is only the highest verified normal EC state. The separate `0x40` family remains blocked after exact-X9 testing echoed `0x47` while producing 0 RPM.

The service-lifetime native-telemetry latch is intentionally not persisted across reboot/service restart yet. Persisting it without a BIOS/driver-aware evidence key could make an old capability observation survive a real platform change. If physical testing confirms EnergyDrv consistently on the exact X9, durable capability evidence can be designed deliberately rather than hardcoded from one sample.

Likewise, startup may observe an EC manual-looking value that another utility owns. ThinkControl now limits automatic EC cleanup to a state/provider it actually took ownership of; it must not reset another tool merely because the numeric register resembles one of ThinkControl's manual steps.

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
- ordinary PR Package runs do not render a duplicate copy of the visual matrix; tagged/versioned release packaging still renders the public release overview;
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
