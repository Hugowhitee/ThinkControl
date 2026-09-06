# ThinkControl alpha testing guide

Use this checklist for **v0.1.0-alpha.40** and later candidates built from it. Automated CI is required, but physical X9 behavior remains a separate evidence class and must not be inferred from hosted runners. The X9 is the current reference device, not the product boundary.

## Install/update sanity

1. Install or update using the versioned GitHub prerelease installer.
2. Confirm ThinkControl starts as a normal-user app and the hardware service reaches Running without the UI remaining elevated.
3. Confirm Compact opens, Advanced opens from Compact, and switching back does not create duplicate windows.
4. Exercise Check for updates from Home and from Updates. An up-to-date result must not enable an install action, and **Last checked must refresh immediately** on the shared state.
5. After an in-place update, confirm the previous install directory is preserved and the app relaunches into the expected surface.
6. A successful update confirmation must remain dismissable and must not strand a topmost notification over Advanced.

## Windows startup and background gestures

Alpha.39 changed application-side `--tray` startup so the rich WMI inventory no longer blocks the synchronous startup path and enabled edge gestures no longer depend on a WPF window activation. Alpha.40 preserves that architecture unchanged. The installer still uses the existing per-user Windows Run entry; do not infer real logon timing from hosted CI.

1. In Settings, enable **Start with Windows** and enable at least one easily observable edge gesture, for example volume or brightness.
2. Sign out/in or reboot. Do **not** manually open ThinkControl after the desktop appears.
3. Confirm ThinkControl reaches the tray without showing its normal Compact/Advanced surface.
4. As soon as the tray process is present, use the configured edge gesture. It should work without first clicking the tray icon or activating a ThinkControl window.
5. Repeat once after a cold reboot and once after sign-out/sign-in. Record roughly how long from desktop availability until the first successful gesture.
6. Open ThinkControl afterwards and confirm CPU/GPU/BIOS/system information fills in normally.
7. Disable gestures, restart ThinkControl with `--tray`, and confirm raw gesture ownership is not kept alive merely because Start with Windows is enabled.
8. If the process itself is still launched conspicuously late by Windows even though its own startup is fast, record that separately. Do not change machine-wide Explorer startup-delay policy as a workaround.

## Crash/shell regression

The recurring `TargetParameterCountException` dispatcher bug was fixed and guarded in alpha.33. Keep issue/field validation separate from the source-level fix.

- Open/close Advanced repeatedly.
- Minimize Advanced, reopen directly to a heavy page such as Touchpad, and confirm the window becomes visible rather than remaining minimized/invisible.
- Navigate Compact → Advanced → Compact several times.
- Open and dismiss the Inbox/notification sheet.
- If a crash occurs, keep generated report/journal evidence rather than marking the issue solved from a single clean session.

## Audio lifecycle regression

The existing Audio navigation-lifecycle guard remains part of the alpha.40 baseline.

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

Alpha.40 preserves the alpha.39 single-lane Track visual and improves its center tap recognition/assignment UX. Test the physical pad, not only screenshots.

### Six-zone editor and corner geometry

- Selecting an edge must clear a selected corner, and selecting a corner must clear the edge selection.
- Top-left and top-right must be exact mirrors: same guard radius, lane size, angle, rounded end arc, fill, boundary weight and selected/live treatment.
- The right corner must look like a horizontal mirror of the left, not a separately approximated overlay.
- Edge-band visuals must stop/clip around enabled corner geometry instead of creating a square/darker overlap underneath the corner.
- Idle corner lines/fills must use the same state grammar as the other selectable Touchpad regions.
- Enable one corner launch and confirm the visible quarter-circle guard, diagonal lane and rounded end-cap correspond to the actual usable launch start area.
- Turning the corner action Off must release that runtime area back to the neighboring edge recognizer.
- Start near either edge of the enabled quarter-circle guard where a side/top gesture would otherwise be plausible. The corner candidate must own that contact from the first frame and a rejected corner must stay locked out until lift.
- Start an ordinary edge gesture outside the corner guard/lane and confirm the edge still behaves normally.

### Integrated Track lane — alpha.40 focus

- Assign **Track control** to Bottom first, then repeat on another edge if useful.
- Confirm the selected band remains one continuous lane with Previous, Play/Pause and Next all **inside** it; there must be no floating skip icons or separate rounded Play/Pause pill.
- Confirm the edge action menu no longer contains a separate **Play / pause** action and still has **Track control**.
- If upgrading from settings that used standalone Play/Pause, confirm that edge migrates to Track control rather than silently becoming Off.
- The center Play/Pause start segment remains about 20% of the lane.
- Make a quick center tap with ordinary finger wobble. It should reliably toggle Play/Pause once even with a few millimetres of small diagonal/inward drift.
- Specifically test taps with roughly 2–4 mm off-axis movement; these were the alpha.39 failure mode because normal edge direction classification could win before lift.
- Hold longer than roughly 700 ms or move clearly beyond the bounded tap envelope; Play/Pause must not commit.
- Start in the center and deliberately move past the tap envelope into a real horizontal/vertical lane swipe. Normal Track recognition must resume rather than trapping the contact as a tap.
- Confirm Previous/Next still needs the existing deliberate ~9 mm swipe threshold. A 4–5 mm movement must not skip tracks.
- Repeatedly alternate: center tap → Previous swipe → center tap → Next swipe. Look for missed stops/starts and accidental skips.
- The popup after a successful toggle must use current-state text plus next-action icon: **Playing + pause bars**, **Paused + play triangle**. If the fallback cannot know state, `Playback toggled` is acceptable and must not pretend to know whether play/pause won.

### Edge assignment swapping

- Give all four edges distinct non-Off actions where possible.
- Select one edge, then choose an action already assigned to a different edge.
- Confirm the two actions **swap**. The old edge must receive the selected edge's previous action instead of becoming Off.
- Confirm edge-specific sensitivity/inversion tuning stays with each physical edge during the swap.
- Repeat the swap in both directions and after reopening the Touchpad page to confirm persistence.
- If the selected edge is already Off, moving an occupied action there may leave Off behind; there is no second active action to exchange in that case.

### Reverse-close and lifecycle

- Select each corner and test **Reverse swipe closes ThinkControl**.
- With it enabled, start in the rounded inner end-cap and swipe diagonally back toward the physical corner; Compact or Advanced should hide to tray.
- With it disabled, that outward swipe must not close ThinkControl.
- With Compact visible and Windows client-area animations enabled, reverse-close must hide cleanly without producing a false `shell.exception` for a successful `hide-to-tray` transition.
- Verify reverse-close on both mirrored corners and confirm a wrong-direction/rejected reverse candidate cannot become a nearby edge gesture while the same contact remains down.
- Leave Touchpad for another page and confirm the rest of Advanced remains responsive during normal touchpad use.

### Visual-QA review

Inspect the final CI artifact at minimum/normal/wide widths and light/dark where available:

- `advanced-touchpad.png`;
- `advanced-touchpad-min.png`;
- `advanced-touchpad-wide.png`;
- `advanced-touchpad-light.png`;
- `advanced-touchpad-top-left-selected.png`;
- `advanced-touchpad-top-right-selected.png`;
- `advanced-touchpad-top-left-live.png`;
- `advanced-touchpad-top-right-live.png`;
- gesture OSD fixtures, including a Track/media state fixture if the snapshot matrix exposes one.

The wide fixture exercises live Bottom Track control. Verify the three Track glyphs remain inside one continuous band, center separators remain subtle and no duplicate standalone Play/Pause row reappears in the editor. Corner selected/live fixtures must remain mirrored.

## Fans and hardware providers

Alpha.40 does not change the alpha.39 X9 cooling backend. The same physical/safety evidence applies.

- Unsupported devices must remain safe/read-only.
- A direct fan writer must be enabled only after a concrete provider passes both its code/provider gate and any required real-device acceptance gate.
- A semantic firmware-policy backend must remain distinct from direct RPM/PWM control.
- `FanCalibrationSupported` / `FanCalibrationRequired` must drive the calibration task and dependent direct-control lock.
- Once calibration is complete/ready, the calibration card should disappear from the top of the Fans page.
- Manual percentage controls must be hidden on the X9 firmware-policy backend and whenever no verified direct writer exists.
- **Raw EC diagnostics** must appear only when the active provider explicitly exposes the discrete-EC semantic contract.

### Current exact-X9 physical state

The alpha.38 Lenovo Other Mode target-RPM writer failed its physical finished-product gate:

- a fixed target repeatedly speeds up/slows down instead of settling smoothly;
- nominal ThinkControl 100% remains below naturally hot firmware Auto;
- `FanSupervisor` does not continuously rewrite a manual target while active.

The writer therefore remains read-only. Native Fan 1/Fan 2 telemetry and explicit target-0 Auto cleanup may remain, while the normal built-ins use the reviewed Lenovo LITSSvc thermal-policy path:

```text
Quiet        -> Lenovo Quiet policy
Balanced     -> Lenovo Balanced policy
Max cooling  -> Lenovo Performance cooling policy
Auto         -> clear cooling override and restore current power-policy baseline
```

For real X9 testing:

1. Start in Auto and confirm Auto/Quiet/Balanced/Max remain offered.
2. Record Fan 1/Fan 2/provider sources.
3. Compare Quiet, Balanced and Max under repeatable load; look for smooth Lenovo-managed behavior and no alpha.38 fixed-target re-kick cycle.
4. Change Windows performance preference while a non-Auto cooling profile is active; confirm the cooling profile stays active and the new preference becomes the Auto restore baseline.
5. Select Auto and confirm the latest baseline returns.
6. Confirm custom/direct percentage/raw EC UI does not appear merely because Other Mode metadata advertises SET.
7. If upgrading from stale alpha.38 direct ownership, explicitly reassert Auto and confirm firmware ownership returns.

A future direct writer may be enabled only after two real channels, stable fixed-target settling, useful high-cooling range comparable with naturally hot Auto and repeated clean Auto handoff are independently demonstrated.

## Diagnostics/device learning

- Normal supported hardware should not run an expensive discovery flow every time the app opens.
- Unknown/new-device collection should remain passive and hardware-focused.
- Sharing remains explicit; verify the preview contains no usernames, serial numbers, personal file paths/content, keystrokes or raw touch trails.
- After a successful share/report flow, the UI should not keep claiming the same report is still ready to send as if nothing happened.

## Repository/release hygiene

Validation is split by ownership:

- **CI** owns repository hygiene, solution build/tests, real Compact ↔ Advanced ShellSmoke and the WPF visual-QA matrix;
- **Package ThinkControl** owns candidate publish/payload/bootstrap plus deep installer/service/IPC lifecycle, non-elevating UI contract, custom-location update behavior, clean uninstall and the oldest-supported updater regression;
- tagged/versioned release packaging renders the public release overview;
- superseded PR CI/Package runs may cancel rather than consume stale Windows runner time;
- immutable/tag release packaging must remain non-cancellable by that PR/ref optimization.

## Release acceptance

Before calling a candidate releasable, require:

- repository hygiene;
- Release build with no unexpected compiler warnings;
- Core/source tests;
- real Compact/Advanced WPF shell smoke;
- complete visual-QA matrix with representative screenshots manually inspected;
- Package ThinkControl including installer/service/IPC and oldest-supported updater compatibility checks;
- exact final PR-head validation after version/docs are frozen;
- final diff/changed-file review so new code is actually wired and duplicate/dead paths are intentional;
- merge with expected-head guard;
- promotion/tag verification and an immutable prerelease with exactly Setup, Payload, `SHA256SUMS.txt` and `ui-overview.png`;
- successful checksum verification of the published Setup/Payload.

Physical hardware checks remain a separate evidence class and should be recorded honestly rather than converted into automated claims.
