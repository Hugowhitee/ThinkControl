# Cooling design

ThinkControl keeps Windows power policy and fan behavior as separate product concepts, while coordinating OEM policy backends so they do not fight each other.

## Power

Windows/Lenovo power preference is stored separately for battery and plugged-in operation. The active source selects its own **Efficiency**, **Balanced** or **Performance** preference. Home and Compact intentionally expose the battery preference as the quick control; the full Performance page is the source of truth for separate battery and AC configuration.

On the verified X9, applying a Windows power preference can also coordinate the reviewed Lenovo `LITSSvc` thermal-policy state. If a fan cooling profile is currently overriding that OEM policy, a later power-mode change updates the restore baseline instead of silently cancelling the cooling profile.

## Cooling

Cooling is global and does not change merely because AC power is connected or removed:

- **Auto** clears any ThinkControl cooling override and returns to the current Lenovo/OEM power-policy baseline.
- **Quiet** requests the verified Lenovo Quiet thermal policy on the X9 firmware-policy backend.
- **Balanced** requests the verified Lenovo Balanced thermal policy on that backend.
- **Max cooling** requests the verified Lenovo Performance cooling policy on that backend.
- Named custom curves require an active physically accepted direct-output provider; they are not approximated through firmware policy.

The alpha.39 X9 product backend intentionally keeps Lenovo firmware in the closed-loop fan controller for the built-in profiles. This is a semantic thermal-policy backend, **not** a direct RPM/PWM/percentage backend. The rejected alpha.38 Other Mode target-RPM writer is therefore not needed to keep Quiet/Balanced/Max cooling usable.

A future direct provider may expose continuous target RPM or calibrated discrete states. In that case ThinkControl can use the generic `FanCurveDefinition` model and `FanSupervisor` output logic. The backend must advertise the appropriate direct-output capability and pass its own physical acceptance gate before custom curves or manual percentages are enabled.

## X9 provider ordering

For the verified X9 `21Q6/21Q7` reference:

1. Native Lenovo fan telemetry is preferred when real per-fan channels are available.
2. Built-in cooling profiles use the reviewed Lenovo firmware thermal-policy path.
3. The alpha.38 Other Mode `fanX_target` writer remains read-only after repeated speed cycling and a useful maximum below naturally hot firmware Auto.
4. A transient native telemetry miss does not re-enable the known-inferior classic EC writer after the native path has been confirmed.
5. Classic EC steps remain provider-specific investigation/diagnostic behavior, not the normal product backend and not a substitute for a physically accepted direct writer.

## Safety invariants

- Firmware/OEM Auto is the ownership fallback.
- A firmware-policy profile changes only a reviewed semantic policy state; Lenovo firmware remains responsible for actual fan ramping and thermal protection.
- Raw control temperature is used for safety when ThinkControl directly supervises an output provider; smoothed temperature is used for normal direct curve decisions.
- CPU/GPU thermal domains use the hottest canonical control domain, not an average with unrelated SSD/battery sensors.
- Direct-output downshifts use hysteresis and minimum dwell time; meaningful cooling increases may happen immediately.
- If the control sensor or a direct provider disappears, ThinkControl returns direct ownership to firmware Auto.
- At the high-temperature safety handoff, direct ThinkControl ownership returns to Lenovo firmware instead of trapping the machine at a manual state.
- Manual level 0 and the ineffective/unsafe raw override path remain blocked.
- Manual direct-output tests are temporary and restore the previous cooling profile, with firmware Auto as the fallback.
- Normal service shutdown/disposal releases direct fan ownership and clears a temporary firmware cooling override back to the stored power-policy baseline where possible.
- Telemetry refresh never performs fan writes.
- Fixed low-level targets are not continuously rewritten merely to hold a state.

## Firmware-policy override lifecycle

The X9 firmware-policy coordinator keeps one base Lenovo power-policy mode and at most one fan-profile override.

1. Before selecting Quiet/Balanced/Max cooling, the UI sends the current Windows power preference so the service has a known restore baseline.
2. The selected built-in profile becomes the active Lenovo thermal-policy override.
3. If the Windows power preference changes while that override is active, the new preference replaces the stored baseline but does not overwrite the cooling profile.
4. Selecting Auto clears the cooling override and reapplies the latest baseline.
5. A failed policy transition is reported explicitly; ThinkControl does not claim the profile changed when the Lenovo pipe rejects or times out.

This ordering keeps the two user-facing controls independent without making two actors repeatedly overwrite the same Lenovo policy surface.

## Calibration

Calibration exists only for a physically accepted direct provider that explicitly advertises it. It is **not** required for the X9 firmware-policy backend.

For a discrete direct-output provider, a calibration run is transactional:

1. verify the direct high-output path and a safe starting temperature;
2. settle every provider-defined output state;
3. collect spaced tachometer samples;
4. reject missing, zero or internally implausible evidence;
5. require the top verified state to remain a credible measured maximum for that provider contract;
6. replace the stored mapping only after the complete candidate validates.

Cancelling, losing telemetry, crossing the safety threshold or failing validation never replaces the previous known-good calibration with partial data. Every direct calibration run returns fan ownership to firmware Auto when it finishes or stops.

Variable measured states are recorded rather than hidden. When a requested direct output lands on a known-variable state, the supervisor may move upward to the next safer state; it never moves downward and silently undershoots the requested cooling floor.
