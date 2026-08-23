# Alpha testing on the ThinkPad X9-15 Gen 1

This guide is for testing ThinkControl `v0.1.0-alpha.4` on the X9 reference device, machine type `21Q6` or `21Q7`.

## Before installation

- Close other direct fan or EC-control utilities.
- Keep Lenovo platform drivers and services installed, especially Lenovo Intelligent Thermal Solution and Lenovo Power Management.
- Start the first test on AC power without a heavy workload.

## Installer, service and hardware setup

Install the published `ThinkControl-Setup-0.1.0-alpha.4.exe` rather than a loose development build.

Confirm:

1. Setup itself is only a few MB.
2. Setup downloads the matching `ThinkControl-Payload-0.1.0-alpha.4.zip` from GitHub Releases.
3. Payload hash verification succeeds.
4. An already installed compatible .NET 10 Desktop Runtime is not downloaded again.
5. Setup installs and starts `ThinkControlService` without adding X9-specific drivers itself.
6. ThinkControl identifies the machine as the verified X9 profile, not Beta / Untested.
7. If the verified EC provider is not ready, Hardware Setup offers the pinned PawnIO prerequisite inside the app.
8. After Hardware Setup completes, refresh the hardware state and confirm the relevant capabilities become available.

Hardware Setup is also available from Settings. Use it if the service is missing, stopped or a verified device-specific provider needs repair.

## Windows shell and UI

After installation, confirm:

1. Desktop, Start menu, taskbar and Notification Area all use the same clean canonical ThinkControl icon.
2. Compact opens above the notification area at 410 × 640 and cannot be dragged away.
3. Clicking ThinkControl again from the notification overflow area brings the existing window back to the foreground.
4. The Compact wordmark is anchored to the left and its ↖ action opens the full window.
5. Advanced uses the same wordmark treatment; its ↘ action returns toward the tray/Compact surface.
6. Advanced navigation reads: Home, Performance, Fans, Sensors, Battery, Display, Audio, Keyboard, Touchpad, System, Updates, Settings.
7. Switch through every Advanced page and confirm titles/cards keep the same left content rail instead of jumping horizontally.
8. Maximize Advanced and confirm the page rail remains left-aligned while unused space grows on the right.
9. Scroll long pages in Dark mode and confirm the scrollbar stays dark rather than switching to the white stock WPF scrollbar.
10. System, Light and Dark themes render without horizontal clipping.

Repeat the layout check at 100, 125 and 150 percent Windows scaling.

## Read-only telemetry

Check telemetry before changing fan state.

- CPU temperature is plausible and has a sensible source label.
- X9 fan RPM appears from the EC tachometer when the verified EC provider is ready.
- RPM remains stable under conservative polling and does not create periodic audible fan disturbances.
- Fan state starts in Lenovo Auto before manual control is used.
- Battery percentage and charge or discharge power are plausible.
- Battery time estimates settle gradually rather than changing sharply on every refresh.

A temporarily slow provider must not make ThinkControl report the Windows service as offline. After one complete status has been read, a transient provider timeout may keep the last complete status while the service itself still responds to Ping.

An unavailable value is not automatically a failure. Record the provider and capability explanation shown by ThinkControl.

## Cooling and fan noise

Continue only when ThinkControl identifies the verified X9 profile and fan control is available.

1. Start in Lenovo Auto.
2. In Compact, switch Fan noise through Silent, Normal and Cool, then back to Auto.
3. Confirm Compact and the Advanced Fans page agree on the selected profile after each change.
4. Confirm changing charger state does not unexpectedly replace the global cooling profile.
5. On the Fans page, allow each profile time to settle and watch the real control temperature/RPM rather than expecting a fixed RPM target.
6. Start fan characterization only while the machine is thermally safe and idle enough to test.
7. If testing advanced manual control, test Levels 1 through 7 one at a time and return to Lenovo Auto afterward.
8. After using a manual level, quit ThinkControl and confirm normal Lenovo fan behavior resumes.
9. Repeat service stop or uninstall after a manual-level test and confirm manual ownership is released.

ThinkControl does not expose fan-off `0x00` or the unverified `0x40` override family.

If fan behavior becomes abnormal, select Lenovo Auto and stop the test. Do not run another direct EC controller concurrently.

## Quiet, Balanced and Performance

On the verified X9, Windows power mode and Lenovo thermal policy are separate providers. Test all three modes on AC and battery and allow each policy time to settle. They are policy commands, not fixed fan-RPM targets.

## Keyboard backlight

1. Test Off, Low and High.
2. Confirm each state matches the physical keyboard.
3. If one Lenovo provider is unavailable, confirm another validated provider is used only when its own probe and readback succeed.
4. Test Auto idle behavior.
5. Test Breathing and observe the real Low/High transition.
6. Test Reactive at normal typing pace.
7. Treat Audio as experimental and confirm it responds without excessive writes or UI lag.

The effects use real discrete hardware states; they do not assume a continuous backlight-brightness API.

## Touchpad gestures

Start with Edge gestures disabled and verify normal Windows touchpad behavior first. Then enable the default preset.

1. Confirm the visual resembles a mostly square-cornered modern touchpad and shows full edge-width bands, not rounded fake buttons.
2. Slide vertically from the left edge and confirm Volume changes in the expected direction.
3. Slide vertically from the right edge and confirm Brightness changes.
4. With seekable media active, slide horizontally from the top edge and confirm relative media seeking.
5. Start at the top-right corner and verify horizontal intent selects Top while vertical intent selects Right.
6. Put down a second finger during a candidate or active edge gesture and confirm ThinkControl cancels it.
7. Tap and release an edge without moving far enough to activate; the pointer must not remain captured.
8. Move inward in the wrong direction and confirm the gesture cancels and normal pointer movement recovers.
9. Use Test gestures to inspect Candidate, Claimed, Active and rejected states.
10. Change Edge width, Activation distance and Edge tolerance and verify the touchpad visual updates consistently.

The cursor must restore correctly after release, cancellation, timeout, disabling gestures, application exit, lock or sleep/resume.

## Haptic touchpad settings

On Windows 11 24H2 or newer, open Touchpad and check the haptic section immediately after opening the page, before touching the pad.

- Haptic controls remain visible even when unsupported.
- On the X9 haptic touchpad, generic Precision Touchpad enumeration should discover capability support without requiring the first physical touch.
- If Windows/HID reports haptic feedback support, test the feedback toggle and 0–100 intensity slider.
- If Windows/HID reports click-force support, test click sensitivity.
- If a capability is not reported, only that control should stay greyed out; the entire haptics section must not disappear.

## Lenovo Vantage and updates

- Click the Commercial Vantage action and confirm the installed application opens directly when resolvable.
- Run Check for updates and confirm it reports a normal release status rather than a raw GitHub error.

## Display and battery

Verify 60 Hz and panel maximum switching, automatic refresh behavior on AC and battery, brightness, adaptive brightness, battery percentage, power, Wh, health and filtered time estimates where exposed by Windows.

## Sleep and resume

1. Sleep and resume while Lenovo Auto is active and confirm telemetry recovers.
2. Test a manual fan level, return to Lenovo Auto, then sleep and resume again.
3. Test Touchpad gestures after resume and confirm cursor capture state has not leaked across sleep.
4. Do not intentionally leave manual fan control active across sleep until Auto recovery behavior has been confirmed.

## Uninstall

Uninstall ThinkControl and confirm:

- `ThinkControlService` disappears;
- ThinkControl UI and service files are removed;
- a shared hardware provider such as PawnIO remains installed if it was added by Hardware Setup, because another application may also use it;
- Lenovo and Intel vendor software is untouched.

## Reporting results

Use the [bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml). Include the ThinkControl version, exact model and machine type, affected section, expected and actual behavior, screenshots for visual issues and the privacy-safe support bundle when relevant.

Automatic network diagnostics submission is not enabled in this prerelease.
