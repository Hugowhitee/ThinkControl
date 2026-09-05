# ThinkControl alpha testing guide

Use this checklist for **v0.1.0-alpha.37** and later candidates built from it. Automated CI is required, but physical X9 behavior remains a separate evidence class and must not be inferred from hosted runners.

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

Alpha.35 added the current Audio navigation-lifecycle guard; alpha.36 and alpha.37 preserve it.

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

Alpha.36 introduced the completed corner-zone integration. Alpha.37 keeps that geometry/recognizer model unchanged and fixes the Compact reverse-close shell handoff plus the mirrored reverse-close visual fixture.

- Selecting an edge must clear a selected corner, and selecting a corner must clear the edge selection.
- Top-left and top-right guides must be exact mirrors: same guard radius, lane size, angle, rounded end arc, boundary weight and selected/live treatment.
- Enable one corner launch and confirm the visible quarter-circle guard, diagonal lane and rounded end-cap correspond to the actual usable launch start area. The guard should reserve runtime input only while that corner action is enabled; turning the action Off must leave the nearby top/side edge available again.
- Start near either edge of the enabled quarter-circle guard where a side/top gesture would otherwise be plausible. The corner candidate must own that contact from the first frame and a rejected corner must stay locked out until lift rather than falling through into the edge recognizer.
- Start an ordinary edge gesture outside the corner guard/lane and confirm the edge still behaves normally.
- Confirm the old neutral center divider is gone: the lane should show a directional arrow, the inner end should be a semicircular arc rather than a flat 90-degree cross-line/filled blob, and enabled Compact/Advanced corners should show the matching semantic icon and text.
- Select each corner and confirm its editor exposes **Reverse swipe closes ThinkControl**. With it enabled, start in the rounded inner end-cap and swipe diagonally back toward the physical corner; Compact or Advanced should hide to tray. With it disabled, that outward swipe must not close ThinkControl.
- With Compact visible and Windows client-area animations enabled, perform reverse-close and confirm it hides cleanly without producing a false `shell.exception` for the successful `hide-to-tray` transition.
- Verify reverse-close on both mirrored corners and confirm a wrong-direction/rejected reverse candidate cannot become a nearby edge gesture while the same contact remains down.
- Test corner candidate/active behavior under real touch and confirm the settings layout does not reflow or oscillate.
- Inspect selected and live fixtures at minimum, normal and wide Advanced sizes plus light/dark states. The left fixture covers inward launch; the mirrored right fixture covers the opt-in reverse-close state.
- In the live top-right reverse fixture, confirm the visible trail is one outward movement toward the physical corner. It must not contain an inward segment immediately followed by a reversal from the same synthetic contact.
- Leave Touchpad for another page and confirm the rest of Advanced remains responsive during normal touchpad use.

## Fans and hardware providers

Alpha.37 preserves alpha.36's X9 fan architecture: use the highest-capability verified Lenovo provider and fail closed when only telemetry is proven. The old EC path remains available only when it is genuinely the active fallback provider.

- Unsupported devices must remain safe/read-only.
- On the verified X9 path, fan writes must be enabled only after a concrete provider passes its own safety gate; model identity by itself is not permission to write.
- Test Lenovo Auto return after every ThinkControl-owned manual/custom fan state.
- Do not interpret a UI control being visible as proof that a hardware write succeeded; verify status and physical response.
- If PawnIO is missing/stale, test the existing repair/restart path before changing EC assumptions.
- Manual percentage and graph-curve operations should appear as fan-control diagnostics rather than generic hardware events, and diagnostics should name the active provider rather than always claiming ThinkPad EC.

Current exact-X9 physical evidence established before alpha.36 that `dev.1191` could expose both physical fan RPMs through the EC investigation path, but EC Max Cooling remained below naturally hot Lenovo Auto and could still sound electronically buzzy/wavy. That negative evidence is why the current architecture does not present EC step 7 as Lenovo's physical maximum.

Use this order on alpha.37:

1. Install the release and restart ThinkControl/the hardware service as the installer normally does. Start in **Lenovo Auto**.
2. Open Advanced → Fans and record the provider/detail plus Fan 1/Fan 2 source text.
   - **Best case:** `Lenovo Other Mode direct target-RPM` appears with two live writable fan channels. Canonical channels use VALID+GET+SET Capability Data. If the detail says `direct-ID fallback`, Lenovo omitted the matching Capability Data record; ThinkControl still requires the exact-X9 controller gate, a sane Fan Test reference range and a plausible live `GetFeatureValue` from the documented fan ID immediately before writing. An explicitly present invalid/readonly capability is never bypassed.
   - **Native telemetry only:** two channels from `Lenovo EnergyDrv · QueryFanSpeed 0x83102570` may appear while controls remain disabled because the matching writer is not validated.
   - **Fallback:** if no native Lenovo two-fan surface is proven, the exact-model discrete EC provider may remain available. Treat it as fallback, not equivalent to Lenovo's full Auto range.
3. If direct OEM target-RPM is active, test manual **25 → 50 → 75 → 100%** with time to settle. Confirm both fans move plausibly and that the previous repeating wave/re-kick/buzzy character is absent. The target shown in UI should be an OEM percentage/RPM concept, not an EC step.
4. Compare 100% with a naturally hot Lenovo Auto state. Lenovo Fan Test Data provides safe/reference constraints rather than proof of the absolute physical ceiling, so record whether the OEM target actually reaches the useful hot-Auto range instead of assuming that from metadata.
5. Return to **Lenovo Auto** repeatedly. Both ThinkControl-owned target channels must be released with target `0`; no fan should remain on a stale target or diverge persistently from the other.
6. If only EnergyDrv native telemetry is available, keep Lenovo Auto active and confirm Fan 1/Fan 2 readings plausibly track physical sound. Fan controls must stay read-only/disabled rather than silently falling back to EC after native two-fan evidence has been proven during that service lifetime.
7. Export a Diagnostics support bundle after the observation. Bounded fan samples should preserve provider/source distinctions without starting another hardware polling loop.
8. For deeper Lenovo Auto research, run `tools/research/Capture-LenovoAuto.ps1 -Label lenovo-auto-hot -BundleRelevantOemBinaries` during a naturally hot Auto state and a separate `-Label lenovo-auto-cool` capture. The script is observational: Lenovo Other Mode uses `GetFeatureValue` only, EnergyDrv uses read/query contracts only, and no `SetFeatureValue`, `ChangeFanSpeed 0x8310257C`, dust/high-speed write or arbitrary EC write is invoked.
9. If the direct Other Mode writer is unavailable and EnergyDrv telemetry is confirmed, use the optional binary bundle with `tools/research/Analyze-LenovoOemFanBinaries.ps1`. Public code proves `ChangeFanSpeed 0x8310257C` exists, but its exact X9 `dwFanCtrlCmd` encoding/rollback semantics remain unverified and must not be brute-forced.
10. The classic EC path remains investigation/fallback evidence. EC step 7 is only the highest verified normal EC state. The separate `0x40` family remains blocked after exact-X9 testing echoed `0x47` while producing 0 RPM.

The service-lifetime native-telemetry latch is intentionally not persisted across reboot/service restart yet. Persisting it without a BIOS/driver-aware evidence key could make an old capability observation survive a real platform change.

Likewise, startup may observe an EC manual-looking value that another utility owns. ThinkControl limits automatic cleanup to a state/provider it actually took ownership of; it must not reset another tool merely because the numeric register resembles one of ThinkControl's manual steps.

The alpha.35 cleanup removed obsolete **current-client** cooling wrappers. The service-side legacy cooling IPC remains intentionally present for installed-client compatibility and is still covered by the immutable alpha.14.1 updater fixture.

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
