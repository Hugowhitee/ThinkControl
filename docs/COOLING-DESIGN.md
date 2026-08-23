# Cooling design

ThinkControl keeps platform power policy and fan behavior as separate concepts.

## Power

Windows/Lenovo power preference is stored separately for battery and plugged-in operation. The active source selects its own Efficiency, Balanced or Performance preference.

## Cooling

Cooling is global and does not change merely because AC power is connected or removed:

- **Lenovo Auto** hands fan ownership to firmware.
- **Silent** delays fan upshifts while thermal headroom allows it.
- **Normal** is the balanced ThinkControl curve.
- **Cool** upshifts earlier for lower chassis/component temperatures.

The verified ThinkPad backend uses discrete levels 1–7. ThinkControl does not present those levels as PWM percentages.

## Safety invariants

- Raw control temperature is used for safety; smoothed temperature is used for normal curve decisions.
- CPU/GPU thermal domains use the hottest canonical domain, not an average with unrelated SSD/battery sensors.
- Downshifts use hysteresis and minimum dwell time; upshifts may happen immediately.
- If the control sensor/provider disappears, ThinkControl returns to Lenovo Auto.
- At the high-temperature safety handoff, ThinkControl returns fan ownership to Lenovo firmware instead of trapping the machine at manual level 7. Firmware may use behavior that ThinkControl intentionally does not expose, such as full-speed/disengaged.
- Manual level 0 and the 0x40 override state remain blocked.
- The normal service shutdown path returns manual fan ownership to Lenovo firmware.

## Characterization

The optional one-time fan characterization validates a high response first and then measures levels 1–7. It records real tachometer data only for fans the providers actually expose. A user can mark the level that becomes clearly audible while the test runs.

Characterization is allowed only with the verified direct-control provider and a cool-enough starting temperature. It always returns to Lenovo Auto when it completes, is stopped, encounters missing telemetry, or reaches a safety condition.
