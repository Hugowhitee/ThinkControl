# ThinkControl alpha testing guide

Use this checklist for **v0.1.0-alpha.42** and later candidates built from it. Automated CI is required, but physical X9 behavior remains a separate evidence class and must not be inferred from hosted runners. The X9 is the current reference device, not the product boundary.

## Install/update sanity

1. Install or update using the versioned GitHub prerelease installer.
2. Confirm ThinkControl starts as a normal-user app and the hardware service reaches Running without the UI remaining elevated.
3. Confirm Compact opens, Advanced opens from Compact, and switching back does not create duplicate windows.
4. Exercise Check for updates from Home and from Updates. An up-to-date result must not enable an install action, and **Last checked must refresh immediately** on the shared state.
5. After an in-place update, confirm the previous install directory is preserved and the app relaunches into the expected surface.
6. A successful update confirmation must remain dismissable and must not strand a topmost notification over Advanced.

## Windows startup and background gestures

Alpha.41 tightened the application startup critical path after comparing ThinkControl with lightweight helper apps such as G-Helper. Alpha.42 does not change that architecture. The relevant principle remains **input/tray first, rich discovery later**.

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

The existing Audio navigation-lifecycle guard remains part of the alpha.42 baseline.

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

Alpha.42 is specifically a physical-reliability follow-up for the integrated Track center action and reverse-close gesture. The final center model is deliberately conservative: spatially easy to hit, temporally intentional. Test these on the real pad, not only screenshots.

### Six-zone editor and corner geometry

- Selecting an edge must clear a selected corner, and selecting a corner must clear the edge selection.
- Top-left and top-right must be exact mirrors: same guard radius, lane size, angle, rounded end arc, fill, boundary weight and selected/live treatment.
- Edge-band visuals must stop/clip around enabled corner geometry instead of creating a darker overlap underneath the corner.
- Start near either edge of an enabled corner guard where a side/top gesture would otherwise be plausible. The corner candidate must own that contact from the first frame and a rejected corner must stay locked out until lift.
- Start an ordinary edge gesture outside the corner guard/lane and confirm the edge still behaves normally.

### Integrated Track lane — alpha.42 focus

- Assign **Track control** to Bottom first, then repeat on another edge if useful.
- Confirm the selected band remains one continuous lane with Previous, Play/Pause and Next all **inside** it; there must be no floating skip icons or separate Play/Pause pill.
- Confirm the edge action menu contains no separate **Play / pause** action and still has **Track control**.
- If upgrading from old settings that used standalone Play/Pause, confirm that edge migrates to Track control rather than becoming Off.
- The visible center Play/Pause start segment should occupy about **28%** of the lane (`0.36..0.64`) and the visual separators must match that recognition target.
- Perform repeated **quick center taps**. They must do **nothing**: no playback toggle and no media popup. This is an intentional accidental-playback guard.
- Press and hold the center for roughly half a second, keeping the finger mostly still, then release. Play/Pause should toggle **once on release**.
- Keep holding for one or two seconds before releasing. Nothing should auto-fire while the finger is still down; release should still toggle once if the contact never left the hold envelope.
- Make natural tiny finger adjustments while holding. Movement up to about **3 mm maximum radial excursion** should remain eligible.
- Move clearly more than 3 mm, then return close to the start and release. Play/Pause must remain disarmed; returning to the start must not erase the earlier movement.
- Perform a deliberate Track swipe. Once the contact leaves the 3 mm hold envelope, ordinary edge direction recognition should resume, and Previous/Next should still require the unchanged **9 mm** Track threshold.
- A swipe must never also toggle Play/Pause on release.
- Repeatedly alternate: quick center tap (no-op) → deliberate center hold/release (toggle) → Previous swipe → center hold/release (toggle) → Next swipe. Look for accidental playback, missed deliberate toggles or action overlap.
- The popup after a successful deliberate toggle must use current-state text plus next-action icon: **Playing + pause bars**, **Paused + play triangle**. If the fallback cannot know state, `Playback toggled` is acceptable and must not invent state.
- Check the help text in the editor. It should explicitly say to hold for about half a second and release, and that quick taps are ignored.

### Edge assignment swapping

- Give all four edges distinct non-Off actions where possible.
- Select one edge, then choose an action already assigned to another edge.
- Confirm the two actions **swap**. The old edge must receive the selected edge's previous action instead of becoming Off.
- Confirm edge-specific sensitivity/inversion tuning stays with each physical edge during the swap.
- Repeat after reopening the Touchpad page to confirm persistence.

### Reverse-close — alpha.42 focus

- Select each top corner and enable **Reverse swipe closes ThinkControl**.
- Instead of aiming only for the rounded cap, start at several points through the **inner half of the visible diagonal lane** and swipe diagonally back toward the physical corner. Compact or Advanced should hide to tray reliably.
- Test starts near the beginning, middle and rounded end of that inner-half reverse target on both top-left and top-right; the two sides should feel like exact mirrors.
- Start in the **outer corner guard** and move inward. That must remain the normal launch direction, not reverse close.
- With reverse close disabled, the same inner-lane outward swipe must not close ThinkControl.
- A rejected reverse candidate must not fall through into a nearby edge gesture while the same contact remains down.
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

The wide fixture exercises live Bottom Track control. Verify the three Track glyphs remain inside one continuous band, the wider center region still reads as part of that same band, and no duplicate standalone Play/Pause row reappears. The help copy must describe the deliberate hold-to-release behavior. Corner geometry itself should remain visually unchanged and mirrored; alpha.42 expands reverse recognition only within the already-rendered inner lane.

## Fans and hardware providers

Alpha.42 does not change the alpha.41 fan architecture. The rejected per-fan `fanX_target` writer stays read-only and the exact-X9 Lenovo Other Mode **global full-speed boolean** semantic `0x04020000` remains separately gated.

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

Alpha.41/42 built-ins are expected to behave as:

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
4. Select **Max cooling**. Listen/feel for a clear step to the strongest Lenovo-style airflow.
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
