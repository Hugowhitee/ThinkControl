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
- Top-left and top-right guides should look like exact mirrors: same size, angle, inset, boundary weight and selected/live treatment.
- A corner launch should win only inside its deliberate diagonal recognition lane; ordinary edge gestures outside that lane should still behave normally.
- Test corner candidate/active behavior under real touch and confirm the settings layout does not reflow or oscillate.
- Test normal, maximized/fullscreen and restored Advanced sizes.
- Leave Touchpad for another page and confirm the rest of Advanced remains responsive during normal touchpad use.

## Fans and hardware providers

- Unsupported devices must remain safe/read-only.
- On the verified X9 path, confirm fan telemetry/control is enabled only after the EC provider passes its existing read-only validation gate.
- Test Lenovo Auto return after a manual/custom fan state.
- Do not interpret a UI control being visible as proof that a hardware write succeeded; verify readback/status.
- If PawnIO is missing/stale, test the existing repair/restart path before changing EC assumptions.
- Manual percentage and graph-curve operations should appear as fan-control diagnostics rather than generic hardware events.

The alpha.35 cleanup removes obsolete **current-client** cooling wrappers. The service-side legacy cooling IPC remains intentionally present for installed-client compatibility and is still covered by the immutable alpha.14.1 updater fixture.

## Diagnostics/device learning

- Normal supported hardware should not run an expensive discovery flow every time the app opens.
- Unknown/new-device collection should remain passive and hardware-focused.
- Sharing remains explicit; verify the preview contains no usernames, serial numbers, personal file paths/content, keystrokes or raw touch trails.
- After a successful share/report flow, the UI should not keep claiming the same report is still ready to send as if nothing happened.

## Repository/release hygiene

Alpha.35 also cleans the repository and workflow layer:

- current workflow action majors must not regress to the deprecated Node-20-era versions;
- removed compatibility helper/current-client cooling APIs must remain absent;
- generated/local build output and historical duplicate documentation remain rejected by repository hygiene;
- PR CI, package and installer runs for a superseded head should cancel rather than consume Windows runners after a newer head exists;
- immutable/tag release packaging must remain non-cancellable by that PR/ref optimization.

## Release acceptance

Before calling a candidate releasable, require:

- repository hygiene;
- Release build with no unexpected compiler warnings;
- Core tests;
- real Compact/Advanced WPF shell smoke;
- complete visual-QA matrix with representative screenshots manually inspected;
- Package ThinkControl;
- Installer reliability including legacy updater compatibility;
- exact final PR-head validation after version/docs are frozen;
- squash merge with expected-head guard;
- promotion/tag verification and an immutable prerelease with exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`;
- successful checksum verification of the published Setup/Payload.

Physical hardware checks above remain follow-up evidence and should be recorded honestly rather than converted into automated claims.
