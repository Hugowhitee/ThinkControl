# ThinkControl alpha testing guide

Use this checklist for **v0.1.0-alpha.38** and later candidates built from it. Automated CI is required, but physical X9 behavior remains a separate evidence class and must not be inferred from hosted runners. The X9 is the current reference device, not the product boundary.

## Install/update sanity

1. Install or update using the versioned GitHub prerelease installer.
2. Confirm ThinkControl starts as a normal-user app and the hardware service reaches Running without the UI remaining elevated.
3. Confirm Compact opens, Advanced opens from Compact, and switching back does not create duplicate windows.
4. Exercise Check for updates from Home and from Updates. An up-to-date result must not enable an install action, and **Last checked must refresh immediately** on the shared state.
5. After an in-place update, confirm the previous install directory is preserved and the app relaunches into the expected surface.
6. A successful update confirmation must remain dismissable and must not strand a topmost notification over Advanced.

## Crash/shell regression

The recurring `TargetParameterCountException` dispatcher bug was fixed and guarded in alpha.33. Keep issue/field validation separate from the source-level fix.

- Open/close Advanced repeatedly.
- Minimize Advanced, reopen directly to a heavy page such as Touchpad, and confirm the window becomes visible rather than remaining minimized/invisible.
- Navigate Compact → Advanced → Compact several times.
- Open and dismiss the Inbox/notification sheet.
- If a crash occurs, keep the generated report/journal evidence rather than marking the issue solved from a single clean session.

## Audio lifecycle regression

The existing Audio navigation-lifecycle guard remains part of the alpha.38 baseline.

1. Open Advanced → Audio.
2. Drag output volume, navigate away while dragging, then return.
3. Repeat with microphone level.
4. Confirm no delayed off-page write visibly jumps the control when returning.
5. Leave the Audio page idle for several seconds and confirm live output/microphone state continues refreshing after the navigation cycle.

## Keyboard

- Test Off, Low and High where the active provider exposes them.
- Test Auto only when ThinkControl reports a verified firmware/OEM Auto path. **Auto is firmware-managed; it is not a ThinkControl idle-dimming effect.**
- Confirm Fn+Space and ThinkControl readback remain sensible after changing firmware Auto/static modes on Lenovo hardware.
- Breathing, Reactive and Audio belong to the Effects card and must be enabled only when the active provider advertises `KeyboardEffects`.
- Confirm a saved effect is not silently reset during startup before the provider capability is known.
- When only the current Lenovo Vantage fallback is available, Effects should remain unavailable rather than generating repeated Lenovo brightness pop-ups.
- If a future non-Lenovo provider advertises `KeyboardEffects`, the same generic Effects UI should become available without vendor-specific page logic.

## Touchpad

Alpha.38 changes both the visual grammar and the optional Track-center interaction. Test the physical pad, not only screenshots.

### Six-zone editor and corner geometry

- Selecting an edge must clear a selected corner, and selecting a corner must clear the edge selection.
- Top-left and top-right must be exact mirrors: same guard radius, lane size, angle, rounded end arc, fill, boundary weight and selected/live treatment.
- The right corner must look like a horizontal mirror of the left, not a separately approximated overlay.
- Edge-band visuals must stop/clip around enabled corner geometry instead of creating a square/darker overlap underneath the corner.
- Idle corner lines/fills must use the same state grammar as the other selectable Touchpad regions.
- Enable one corner launch and confirm the visible quarter-circle guard, diagonal lane and rounded end-cap correspond to the actual usable launch start area.
- Turning the corner action Off must release that runtime area back to the neighboring edge recognizer.
- Start near either edge of the enabled quarter-circle guard where a side/top gesture would otherwise be plausible. The corner candidate must own that contact from the first frame and a rejected corner must stay locked out until lift rather than falling through into the edge recognizer.
- Start an ordinary edge gesture outside the corner guard/lane and confirm the edge still behaves normally.
- Confirm the lane shows a directional arrow and enabled Compact/Advanced corners show the matching semantic icon/text.

### Track-center Play/Pause

- Assign **Track control** to an edge and enable the optional center Play/Pause action.
- Confirm a small Play/Pause target is visibly drawn inside the canonical `TouchpadVisualizer`; there must be no separate hidden overlay target.
- A short low-travel tap inside that visible target should toggle Play/Pause once.
- A normal Previous/Next swipe that starts outside the target must remain a track swipe.
- A swipe crossing the center target must not accidentally become a tap.
- A long rest or large movement in the target must not commit Play/Pause.
- User-facing copy/feedback should say **tap**, not hold-and-release.

### Reverse-close and lifecycle

- Select each corner and test **Reverse swipe closes ThinkControl**.
- With it enabled, start in the rounded inner end-cap and swipe diagonally back toward the physical corner; Compact or Advanced should hide to tray.
- With it disabled, that outward swipe must not close ThinkControl.
- With Compact visible and Windows client-area animations enabled, reverse-close must hide cleanly without producing a false `shell.exception` for the successful `hide-to-tray` transition.
- Verify reverse-close on both mirrored corners and confirm a wrong-direction/rejected reverse candidate cannot become a nearby edge gesture while the same contact remains down.
- Test candidate/active behavior under real touch and confirm the settings layout does not reflow or oscillate.
- Leave Touchpad for another page and confirm the rest of Advanced remains responsive during normal touchpad use.

### Visual-QA review

Inspect the final CI artifact at minimum/normal/wide widths and light/dark where available:

- `advanced-touchpad.png`;
- `advanced-touchpad-wide.png`;
- `advanced-touchpad-top-left-selected.png`;
- `advanced-touchpad-top-right-selected.png`;
- `advanced-touchpad-top-left-live.png`;
- `advanced-touchpad-top-right-live.png`.

The two selected fixtures and two live fixtures must remain mirrored and the visible center Track target must fit naturally into the same visualizer.

## Fans and hardware providers

Alpha.38 keeps alpha.37's reviewed X9 low-level fan boundaries but changes the **generic product contract**: calibration belongs to the active provider capability, not to a model name in the UI.

- Unsupported devices must remain safe/read-only.
- Fan writes must be enabled only after a concrete provider passes its own safety gate; model identity by itself is not permission to write.
- The Fans page must not decide that `21Q6`, `21Q7`, `X9` or `Lenovo` means calibration is required.
- `FanCalibrationSupported` / `FanCalibrationRequired` must drive the calibration card, Inbox attention and dependent-control lock.
- A provider that does not require calibration must not inherit the X9 discrete fallback setup flow.
- While calibration is running, competing fan controls must remain locked and firmware/OEM Auto must be restored after finish/stop/failure.
- A failed/partial calibration must not replace a previously verified complete mapping.
- Test firmware/OEM Auto return after every ThinkControl-owned manual/custom fan state.
- Do not interpret a visible UI control as proof that a hardware write succeeded; verify status and physical response.
- If PawnIO is missing/stale, test the existing repair/restart path before changing EC assumptions.
- Manual percentage and graph-curve operations should appear as fan-control diagnostics and identify the active provider rather than always claiming ThinkPad EC.

### Current exact-X9 physical sequence

Current exact-X9 evidence established before alpha.38 that the EC investigation path can expose both physical fan RPMs, but EC maximum output remained below naturally hot Lenovo Auto and could sound electronically buzzy/wavy. That negative evidence is why the product does not treat EC state 7 as Lenovo's physical maximum.

Use this order on alpha.38:

1. Install/restart normally and start in firmware/Lenovo Auto.
2. Open Advanced → Fans and record provider/detail plus Fan 1/Fan 2 sources.
3. If `Lenovo Other Mode direct target-RPM` is active, test manual **25 → 50 → 75 → 100%** with time to settle. Confirm both fans move plausibly and that the earlier repeating wave/re-kick/buzzy character is absent.
4. Compare 100% with naturally hot Lenovo Auto without assuming Fan Test metadata equals the absolute physical ceiling.
5. Return to Lenovo Auto repeatedly. Both ThinkControl-owned target channels must release cleanly with no stale target or persistent divergence.
6. If only EnergyDrv native telemetry is available, keep control read-only and confirm Fan 1/Fan 2 readings plausibly track physical sound.
7. If the discrete EC fallback is active, confirm the calibration card appears because its provider capability requires a mapping. Complete the real tachometer calibration before judging percentage curves.
8. Stop a calibration mid-run and confirm Auto is restored and no partial result is promoted.
9. Export a Diagnostics support bundle after observation so provider/source distinctions can be compared with physical behavior.
10. The separate ambiguous `0x40` EC family remains blocked; do not brute-force unknown write encodings.

The service-lifetime native-telemetry latch is intentionally not persisted across reboot/service restart yet. Persisting it without a BIOS/driver-aware evidence key could make an old capability observation survive a real platform change.

Startup may also observe a manual-looking EC value another utility owns. ThinkControl limits automatic cleanup to state/provider ownership it can justify; it must not reset another tool merely because a numeric register resembles one of ThinkControl's manual states.

## Diagnostics/device learning

- Normal supported hardware should not run an expensive discovery flow every time the app opens.
- Unknown/new-device collection should remain passive and hardware-focused.
- Sharing remains explicit; verify the preview contains no usernames, serial numbers, personal file paths/content, keystrokes or raw touch trails.
- After a successful share/report flow, the UI should not keep claiming the same report is still ready to send as if nothing happened.

## Repository/release hygiene

Validation is intentionally split by ownership rather than duplicated across three full Windows builds:

- **CI** owns repository hygiene, solution build/tests, real Compact ↔ Advanced ShellSmoke and the WPF visual-QA matrix;
- **Package ThinkControl** owns candidate publish/payload/bootstrap plus deep installer/service/IPC lifecycle, non-elevating UI contract, custom-location update behavior, clean uninstall and the real oldest-supported alpha.14.1 → candidate updater regression;
- tagged/versioned release packaging renders the public release overview;
- superseded PR CI/Package runs may cancel rather than consume stale Windows runner time;
- immutable/tag release packaging must remain non-cancellable by that PR/ref optimization.

## Release acceptance

Before calling a candidate releasable, require:

- repository hygiene;
- Release build with no unexpected compiler warnings;
- Core tests;
- real Compact/Advanced WPF shell smoke;
- complete visual-QA matrix with representative screenshots manually inspected;
- Package ThinkControl including installer/service/IPC and oldest-supported updater compatibility checks;
- exact final PR-head validation after version/docs are frozen;
- final diff/changed-file review so new code is actually wired and duplicate/dead paths are intentional;
- merge with expected-head guard;
- promotion/tag verification and an immutable prerelease with exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`;
- successful checksum verification of the published Setup/Payload.

Physical hardware checks above remain follow-up evidence and should be recorded honestly rather than converted into automated claims.
