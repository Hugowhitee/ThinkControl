# Cooling design

ThinkControl keeps Windows power policy and fan behavior as separate concepts.

## Power

Windows/Lenovo power preference is stored separately for battery and plugged-in operation. The active source selects its own **Efficiency**, **Balanced** or **Performance** preference. Home and Compact intentionally expose the battery preference as the quick control; the full Performance page is the source of truth for separate battery and AC configuration.

## Cooling

Cooling is global and does not change merely because AC power is connected or removed:

- **Auto** hands fan ownership to Lenovo/OEM firmware.
- **Quiet** delays fan upshifts while thermal headroom allows it.
- **Balanced** is the normal ThinkControl supervised curve.
- **Max cooling** upshifts earlier for lower chassis/component temperatures.
- Named custom curves use the same verified output mapping as the built-in curves.

The verified ThinkPad X9 backend uses discrete EC steps 1–7. User-facing percentages are targets mapped onto measured/verified discrete states; ThinkControl does not pretend the EC exposes continuous PWM.

## Safety invariants

- Raw control temperature is used for safety; smoothed temperature is used for normal curve decisions.
- CPU/GPU thermal domains use the hottest canonical control domain, not an average with unrelated SSD/battery sensors.
- Downshifts use hysteresis and minimum dwell time; meaningful cooling increases may happen immediately.
- If the control sensor or verified fan provider disappears, ThinkControl returns ownership to firmware Auto.
- At the high-temperature safety handoff, ThinkControl returns fan ownership to Lenovo firmware instead of trapping the machine at a manual state. Firmware may use behavior ThinkControl intentionally does not expose.
- Manual level 0 and the ineffective/unsafe raw override path remain blocked.
- Manual tests are temporary and restore the previous cooling profile, with firmware Auto as the fallback.
- Normal service shutdown/disposal returns ThinkControl-owned fan control to firmware.

## Calibration

Calibration exists only for the verified X9 discrete-EC provider and requires both writable fan control and real tachometer telemetry.

A calibration run is transactional:

1. verify the high-output path and a safe starting temperature;
2. settle each EC step 1–7;
3. collect three spaced tachometer samples per step;
4. reject missing, zero or internally implausible evidence;
5. require step 7 to remain a credible measured maximum;
6. replace the stored mapping only after the complete seven-step candidate validates.

Cancelling, losing telemetry, crossing the safety threshold or failing validation never replaces the previous known-good calibration with partial data. Every run returns fan ownership to firmware Auto when it finishes or stops.

Variable measured states are recorded rather than hidden. When a requested output lands on a known-variable state, the supervisor may move upward to the next safer state; it never moves downward and silently undershoots the requested cooling floor.
