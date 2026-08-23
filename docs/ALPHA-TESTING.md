# Alpha testing on the ThinkPad X9-15 Gen 1

This guide is for testing a prerelease build on the X9 reference device, machine type `21Q6` or `21Q7`.

## Before installation

- Close other fan-control and EC-control utilities.
- Keep Lenovo platform drivers and services installed.
- Connect AC power for the first hardware test.
- Start without a heavy workload running.

ThinkControl Setup installs the UI and `ThinkControlService`. Direct X9 EC fan access may also require PawnIO. Windows-level display and battery features do not depend on that EC provider.

## Basic checks

After installation, confirm:

1. ThinkControl appears in the Windows notification area.
2. The compact window opens and hides normally.
3. The Advanced window opens and returns to the compact view correctly.
4. The detected model and machine type are correct.
5. System, Light and Dark themes render without clipping.

## Read-only telemetry

Check telemetry before changing fan state.

- CPU temperature is plausible and has a sensible source label.
- Fan RPM appears only when a valid fan provider is available.
- Fan state starts in Lenovo Auto before manual control is used.
- Battery percentage and charge/discharge power are plausible.
- Battery time estimates settle gradually rather than changing sharply on every refresh.

An unavailable value is not automatically a failure. Record which provider or capability is missing when reporting the result.

## Fan control

Continue only when ThinkControl identifies the verified X9 profile and fan control is available.

1. Start in Lenovo Auto.
2. Select Level 1 and allow the fan to settle.
3. Test Levels 2 through 7 one at a time.
4. Confirm the reported state follows each selection.
5. Return to Lenovo Auto.
6. After using a manual level, quit ThinkControl and confirm normal Lenovo fan behavior resumes.

ThinkControl does not expose fan-off `0x00` or the unverified `0x40` override states.

If fan behavior becomes abnormal, select Lenovo Auto and stop the test. Do not run another direct EC fan controller at the same time.

## Keyboard backlight

When the Lenovo PM provider is available:

1. Test Off, Low and High.
2. Confirm each state matches the physical keyboard.
3. Test Auto idle behavior.
4. Test Breathing and observe the real transition between Low and High.
5. Test Reactive at a normal typing pace.
6. Treat Audio mode as experimental and confirm it responds without excessive writes or UI lag.

The effects use the supported discrete hardware states. They do not assume a continuous backlight-brightness interface.

## Display

Verify:

- 60 Hz and the panel maximum switch correctly;
- Auto chooses the expected refresh rate on AC and battery;
- brightness follows the slider;
- adaptive brightness changes only when Windows exposes the setting.

## Battery

Verify:

- percentage and power source are correct;
- live power is plausible;
- Wh and health values are plausible when available;
- time estimates become more stable after several samples.

## Sleep and resume

After the basic tests pass:

1. Sleep and resume while Lenovo Auto is active, then confirm telemetry recovers.
2. Test a manual fan level, return to Lenovo Auto, then sleep and resume again.
3. Do not intentionally leave manual fan control active across a sleep test until the normal Auto recovery path is confirmed.

## Reporting results

Use the [bug report form](https://github.com/Hugowhitee/ThinkControl/issues/new?template=bug-report.yml) for problems or compatibility results.

Useful information includes:

- ThinkControl version;
- exact laptop model and machine type;
- affected section;
- expected behavior;
- actual behavior;
- whether Lenovo Auto restored normal fan behavior;
- screenshot for visual issues;
- exported support bundle when relevant.

Automatic network diagnostics submission is not enabled in the current release.
