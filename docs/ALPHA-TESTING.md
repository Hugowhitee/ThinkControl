# ThinkControl alpha testing guide

Use this checklist for **v0.1.0-alpha.41** and later candidates built from it. Automated CI is required, but physical X9 behavior remains a separate evidence class and must not be inferred from hosted runners. The X9 is the current reference device, not the product boundary.

## Install/update sanity

1. Install or update using the versioned GitHub prerelease installer.
2. Confirm ThinkControl starts as a normal-user app and the hardware service reaches Running without the UI remaining elevated.
3. Confirm Compact opens, Advanced opens from Compact, and switching back does not create duplicate windows.
4. Exercise Check for updates from Home and from Updates. An up-to-date result must not enable an install action, and **Last checked must refresh immediately** on the shared state.
5. After an in-place update, confirm the previous install directory is preserved and the app relaunches into the expected surface.
6. A successful update confirmation must remain dismissable and must not strand a topmost notification over Advanced.

## Windows startup and background gestures

Alpha.41 tightens the application startup critical path after comparing ThinkControl with lightweight helper apps such as G-Helper. The goal is not to copy G-Helper's single-process architecture; ThinkControl keeps its privileged hardware service. The relevant principle is **input/tray first, rich discovery later**.

1. In Settings, enable **Start with Windows** and enable at least one obvious edge gesture, for example volume or brightness.
2. Sign out/in or reboot. Do **not** manually open ThinkControl after the desktop appears.
3. Confirm ThinkControl reaches the tray without showing its normal Compact/Advanced surface.
4. As soon as the tray process exists, use the configured edge gesture. It should work without first clicking the tray icon or activating a ThinkControl window.
5. Repeat after a cold reboot and after sign-out/sign-in. Record roughly how long from desktop availability until the first successful gesture.
6. Open ThinkControl afterwards and confirm CPU/GPU/BIOS/system information fills in normally.
7. Disable gestures, restart ThinkControl with `--tray`, and confirm raw gesture ownership is not kept alive merely because Start with Windows is enabled.
8. Confirm normal visible startup still paints promptly and does not regress into a blank/black first frame.
9. If the process itself is launched conspicuously late by Windows, record that separately. Do not change machine-wide Explorer startup-delay policy as a workaround.

Implementation expectation: configured Raw Input registration happens from the earliest application Startup hook after cheap registry identity is available; rich WMI/service discovery remains asynchronous. `Application.Activated` is recovery only, not first-start ownership.

## Crash/shell regression

The recurring `TargetParameterCountException` dispatcher bug was fixed and guarded in alpha.33. Keep issue/field validation separate from the source-level fix.

- Open/close Advanced repeatedly.
- Minimize Advanced, reopen directly to a heavy page such as Touchpad, and confirm the window becomes visible rather than remaining minimized/invisible.
- Navigate Compact → Advanced → Compact several times.
- Open and dismiss the Inbox/notification sheet.
- If a crash occurs, keep generated report/journal evidence rather than marking the issue solved from a single clean session.

## Audio lifecycle regression

The existing Audio navigation-lifecycle guard remains part of the alpha.41 baseline.

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

## Touchpad

Alpha.41 preserves the single-lane Track visual but changes center Play/Pause from a timing-sensitive gesture into a button-like release action. Test the physical pad, not only screenshots.

### Six-zone editor and corner geometry

- Selecting an edge must clear a selected corner, and selecting a corner must clear the edge selection.
- Top-left and top-right must be exact mirrors: same guard radius, lane size, angle, rounded end arc, fill, boundary weight and selected/live treatment.
- Edge-band visuals must stop/clip around enabled corner geometry instead of creating a darker overlap underneath the corner.
- Start near either edge of an enabled corner guard where a side/top gesture would otherwise be plausible. The corner candidate must own that contact from the first frame and a rejected corner must stay locked out until lift.
- Start an ordinary edge gesture outside the corner guard/lane and confirm the edge still behaves normally.

### Integrated Track lane — alpha.41 focus

- Assign **Track control** to Bottom first, then repeat on another edge if useful.
- Confirm the selected band remains one continuous lane with Previous, Play/Pause and Next all **inside** it; there must be no floating skip icons or separate Play/Pause pill.
- Confirm the edge action menu contains no separate **Play / pause** action and still has **Track control**.
- If upgrading from old settings that used standalone Play/Pause, confirm that edge migrates to Track control rather than becoming Off.
- The center Play/Pause start segment remains about 20% of the lane.
- Treat the center like a button: touch inside the visible center segment and release. There should be no need to learn a special short/long press duration.
- Hold the finger still for well over one second, then release. Play/Pause should still commit once as long as the contact remained a center-button candidate.
- Make ordinary small diagonal/inward finger movement while pressing the center. It should remain a Play/Pause candidate instead of disappearing into a wrong-direction dead zone.
- Test movement in the former alpha.40 dead region around 4.5–8 mm. It should no longer become a no-op merely because it exceeded the old tap slop but stayed below the 9 mm skip threshold.
- Deliberately cross the existing **9 mm** Track swipe threshold. Previous/Next should win and Play/Pause must not fire on release.
- Repeatedly alternate: center press/release → Previous swipe → center press/release → Next swipe. Look for missed stops/starts and accidental skips.
- The popup after a successful toggle must use current-state text plus next-action icon: **Playing + pause bars**, **Paused + play triangle**. If the fallback cannot know state, `Playback toggled` is acceptable and must not invent state.

### Edge assignment swapping

- Give all four edges distinct non-Off actions where possible.
- Select one edge, then choose an action already assigned to another edge.
- Confirm the two actions **swap**. The old edge must receive the selected edge's previous action instead of becoming Off.
- Confirm edge-specific sensitivity/inversion tuning stays with each physical edge during the swap.
- Repeat after reopening the Touchpad page to confirm persistence.

### Reverse-close and lifecycle

- Select each corner and test **Reverse swipe closes ThinkControl**.
- With it enabled, start in the rounded inner end-cap and swipe diagonally back toward the physical corner; Compact or Advanced should hide to tray.
- With it disabled, that outward swipe must not close ThinkControl.
- Verify reverse-close on both mirrored corners and confirm a rejected reverse candidate cannot become a nearby edge gesture while the same contact remains down.
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
- relevant media/gesture OSD fixtures.

The wide fixture exercises live Bottom Track control. Verify the three Track glyphs remain inside one continuous band, center separators remain subtle and no duplicate standalone Play/Pause row reappears.

## Fans and hardware providers

Alpha.41 keeps the rejected per-fan `fanX_target` writer read-only, but adds a distinct exact-X9 path for Lenovo Other Mode's known **global full-speed boolean** semantic `0x04020000`. This is not an arbitrary RPM target and must not be generalized to unknown machines.

- Unsupported devices must remain safe/read-only.
- A direct target-RPM writer must remain disabled unless it independently passes its provider and physical acceptance gates.
- A semantic firmware-policy backend must remain distinct from direct RPM/PWM control.
- `FanCalibrationSupported` / `FanCalibrationRequired` must continue to drive direct calibration UI.
- Manual percentage controls and Raw EC diagnostics must remain hidden on the X9 firmware-policy backend.
- **Max cooling** may use the exact-X9 full-speed boolean only if the known feature live-reads as boolean and the provider exposes a safe writable contract; every transition must verify readback.
- Quiet/Balanced must release any ThinkControl-owned full-speed override before applying their Lenovo firmware policy.
- Auto/service shutdown must release any ThinkControl-owned full-speed override and restore firmware ownership.
- A failed full-speed probe or readback must fail closed; do not fall back to the rejected `fanX_target` writer, guessed EC states or arbitrary EnergyDrv commands.

### Current exact-X9 physical state

Confirmed negative evidence from earlier builds remains valid:

- alpha.38 fixed target-RPM control repeatedly sped up/slowed down instead of settling smoothly;
- nominal ThinkControl target 100% remained physically weaker than naturally hot Lenovo Auto even when telemetry looked high;
- therefore `fanX_target` remains read-only.

Alpha.41 built-ins are expected to behave as:

```text
Auto         -> release ThinkControl full-speed ownership if any; restore current Lenovo power-policy baseline
Quiet        -> ensure full-speed is off; Lenovo Quiet policy
Balanced     -> ensure full-speed is off; Lenovo Balanced policy
Max cooling  -> Lenovo Performance policy + verified Lenovo Other Mode full-speed boolean when safely exposed
```

For real X9 testing:

1. Start in Auto and confirm Auto/Quiet/Balanced/Max remain offered.
2. Record Fan 1/Fan 2/provider sources and RPM, but do not treat RPM alone as proof of airflow/cooling intensity.
3. Compare Quiet and Balanced under repeatable load; both should remain smooth Lenovo-managed profiles.
4. Select **Max cooling**. Listen/feel for a clear step to the strongest Lenovo-style airflow. It should be materially closer to naturally hot Auto/full cooling than alpha.40 Performance-policy-only behavior.
5. Confirm Max remains steady rather than reproducing the alpha.38 repeated re-kick/wave behavior.
6. Switch Max → Balanced and Max → Quiet. Confirm full-speed releases promptly and the lower firmware profile takes effect.
7. Select Auto and confirm the latest Windows/Lenovo power-policy baseline returns.
8. Change Windows performance preference while a non-Auto cooling profile is active; confirm the cooling profile stays active and the new preference becomes the later Auto baseline.
9. Confirm custom/direct percentage/raw EC UI does not appear merely because Other Mode metadata advertises SET.
10. If Max fails to engage, collect the exact provider/detail/readback evidence instead of broadening the writer.

A future direct per-fan writer still requires two real channels, stable fixed-target settling, useful high-cooling range comparable with naturally hot Auto and repeated clean Auto handoff before it can be re-enabled.

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
