# Alpha testing on the ThinkPad X9-15 Gen 1

This guide is for testing ThinkControl `v0.1.0-alpha.2` on the X9 reference device, machine type `21Q6` or `21Q7`.

## Before installation

- Close other direct fan/EC-control utilities.
- Keep Lenovo platform drivers/services installed, especially Lenovo Intelligent Thermal Solution and Lenovo Power Management.
- Start the first test on AC power without a heavy workload.

## Installer and identity

Install the published `ThinkControl-Setup-0.1.0-alpha.2.exe` rather than a loose development build.

Confirm:

1. Setup itself is only a few MB.
2. Setup downloads the matching `ThinkControl-Payload-0.1.0-alpha.2.zip` from GitHub Releases.
3. The payload hash verification succeeds.
4. An already installed compatible .NET 10 Desktop Runtime is not downloaded again.
5. On X9 `21Q6/21Q7`, Setup offers the pinned PawnIO 2.2.0 hardware-access task.
6. `ThinkControlService` reaches Running.
7. ThinkControl identifies the machine as the verified X9 profile, not Beta/Untested.

## Windows shell and UI

After installation, confirm:

1. The tray icon uses the current v3 TC mark.
2. Compact opens above the notification area and cannot be dragged away.
3. Compact uses one diagonal `↖` action to open Advanced.
4. Advanced uses the normal Windows title bar with the v3 application icon, minimize, maximize/restore and close.
5. Hovering the native maximize button exposes Windows 11 Snap Layouts.
6. The `↘` action returns to Compact.
7. Settings shows Compatibility diagnostics exactly once.
8. System, Light and Dark themes render without clipping.

Repeat the layout check at 100, 125 and 150 percent Windows scaling.

## Read-only telemetry

Check telemetry before changing fan state.

- CPU temperature is plausible and has a sensible source label.
- X9 fan RPM appears from the EC tachometer when PawnIO/EC access is ready.
- RPM remains stable under normal conservative polling and does not create periodic audible fan disturbances.
- Fan state starts in Lenovo Auto before manual control is used.
- Battery percentage and charge/discharge power are plausible.
- Battery time estimates settle gradually rather than changing sharply on every refresh.

An unavailable value is not automatically a failure. Record the provider/capability explanation shown by ThinkControl.

## Fan control

Continue only when ThinkControl identifies the verified X9 profile and fan control is available.

1. Start in Lenovo Auto.
2. Select Level 1 and allow the fan to settle.
3. Test Levels 2 through 7 one at a time.
4. Confirm the reported state follows each selection.
5. Return to Lenovo Auto.
6. After using a manual level, quit ThinkControl and confirm normal Lenovo fan behavior resumes.
7. Repeat service stop/uninstall after a manual-level test and confirm manual ownership is released.

ThinkControl does not expose fan-off `0x00` or the unverified `0x40` override family.

If fan behavior becomes abnormal, select Lenovo Auto and stop the test. Do not run another direct EC controller concurrently.

## Quiet, Balanced and Performance

These modes have two layers on the verified X9:

1. Windows power mode changes immediately.
2. ThinkControl then asks Lenovo Intelligent Thermal Solution to apply the matching X9 thermal policy.

Test on AC:

```text
Quiet        -> LITSSvc 502
Balanced     -> LITSSvc 503
Performance  -> LITSSvc 504
```

Then test on battery:

```text
Quiet        -> LITSSvc 507
Balanced     -> LITSSvc 508
Performance  -> LITSSvc 509
```

Compare CPU package behavior, fan behavior and responsiveness after allowing each policy time to settle. These are thermal-policy commands, not direct fan-RPM targets, so do not expect an instant fixed RPM for each mode.

## Keyboard backlight

1. Test Off, Low and High.
2. Confirm each state matches the physical keyboard.
3. If the direct Lenovo PM/EnergyDrv provider is unavailable, confirm the validated installed Vantage ThinkKeyboard fallback is used rather than simply reporting Unavailable.
4. Test Auto idle behavior.
5. Test Breathing and observe the real Low/High transition.
6. Test Reactive at normal typing pace.
7. Treat Audio as experimental and confirm it responds without excessive writes or UI lag.

The effects use real discrete hardware states; they do not assume a continuous backlight-brightness API.

## Lenovo Vantage and updates

- Click the Commercial Vantage action and confirm the already installed application opens directly instead of Microsoft Store.
- Run Check for updates and confirm it reports a normal release status rather than a raw HTTP/GitHub 404.

## Display and battery

Verify 60 Hz / panel maximum switching, automatic refresh behavior on AC/battery, brightness, adaptive brightness, battery percentage/power/Wh/health and filtered time estimates where exposed by Windows.

## Sleep and resume

1. Sleep/resume while Lenovo Auto is active and confirm telemetry recovers.
2. Test a manual fan level, return to Lenovo Auto, then sleep/resume again.
3. Do not intentionally leave manual fan control active across sleep until Auto recovery behavior has been confirmed.

## Uninstall

Uninstall ThinkControl and confirm:

- `ThinkControlService` disappears;
- ThinkControl UI/service files are removed;
- shared PawnIO remains installed if it was installed because another program may also use it;
- Lenovo/Intel vendor software is untouched.

## Reporting results

Use the [bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml). Useful information includes ThinkControl version, exact model/machine type, affected section, expected/actual behavior, whether Lenovo Auto restored normal fan behavior, screenshots for visual issues and the privacy-safe support bundle when relevant.

Automatic network diagnostics submission is not enabled in this prerelease.
