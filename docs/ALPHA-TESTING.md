# Alpha testing on the ThinkPad X9-15 Gen 1

This guide is for testing a development or prerelease build on the X9 reference device (machine type 21Q6 or 21Q7).

## Before installing

- Close other fan-control or EC-control utilities.
- Leave Lenovo's normal platform drivers/services installed.
- Keep the laptop connected to AC power for the first hardware test.
- Do not start with a heavy workload running.

ThinkControl's installer installs the UI and `ThinkControlService`. A clean machine may still need the verified PawnIO prerequisite before X9 EC fan telemetry/control can become available; Windows-level features such as display and battery telemetry do not depend on that EC path.

## First launch

Confirm these basic items first:

1. ThinkControl appears in the Windows tray.
2. The compact popup opens and closes normally.
3. Advanced opens from the compact popup and can dock back.
4. The detected device name/machine type is correct.
5. System, Dark and Light themes render without clipping.

## Telemetry

Check the read-only values before changing fan state:

- CPU temperature appears and the source is sensible.
- Fan RPM appears only if the X9 EC provider is available.
- Fan state reports Lenovo Auto before manual control is used.
- Battery percentage and charging/discharging power look plausible.
- Charging ETA settles gradually instead of jumping dramatically every refresh.

If a value is unavailable, do not treat a dash (`—`) as a failure by itself. Record which capability/provider is unavailable in Settings > Compatibility diagnostics.

## Fan control

Only continue when ThinkControl identifies the verified X9 profile and fan control is available.

1. Start in **Lenovo Auto**.
2. Select Level 1 and wait for the fan to settle.
3. Progress through Levels 2–7 one at a time.
4. Confirm the reported state follows the selected level.
5. Return to **Lenovo Auto**.
6. Close/quit ThinkControl after a manual level has been used, then confirm normal Lenovo fan behavior resumes.

ThinkControl deliberately does not expose fan-off `0x00` or the unverified `0x40` override states.

If fan behavior sounds abnormal, immediately select **Lenovo Auto** and stop the test. Do not run another EC fan controller at the same time.

## Keyboard backlight

When the Lenovo PM backend is available:

1. Test Off, Low and High individually.
2. Confirm each state matches the actual keyboard.
3. Test Auto idle behavior.
4. Test Breathing and observe whether Lenovo's own transition between Low and High produces a smooth effect.
5. Test Reactive at a normal typing pace.
6. Treat Audio mode as experimental and verify only that it responds without causing excessive writes or UI lag.

ThinkControl effects are software policies over the real hardware levels; they are not presented as a native continuous 0–100% backlight API.

## Display and battery

Verify:

- 60 Hz and panel maximum switch correctly.
- Auto refresh chooses the intended AC/battery refresh rate.
- Brightness follows the slider.
- Adaptive brightness switch changes only when Windows exposes that setting.
- Battery W/Wh/health values are plausible when exposed by ACPI.
- ETA becomes more stable after several samples.

## Sleep and resume

After the basic checks pass:

1. Put Windows to sleep while Lenovo Auto is active; resume and verify telemetry recovers.
2. Select a manual fan level, return to Lenovo Auto, then sleep/resume again.
3. Do not intentionally leave a manual fan level active across a sleep test until Lenovo Auto recovery has been confirmed in the normal path.

## Reporting a problem

Use the repository's Bug Report issue form for normal problems. The form accepts an exact free-form laptop model and optional screenshots/log attachments.

For compatibility details, Settings > Compatibility diagnostics can preview or export the redacted local support bundle. Network diagnostics submission remains disabled until the private project endpoint is configured.

Useful information in a report:

- exact ThinkControl version shown in the app;
- exact laptop model/machine type;
- affected section;
- expected vs actual behavior;
- whether Lenovo Auto restored normal fan behavior;
- screenshot if the problem is visual.
