# Cooling design

ThinkControl keeps Windows power policy and fan behavior as separate product concepts while coordinating OEM policy backends so they do not fight each other.

## Power

Windows/Lenovo power preference is stored separately for battery and plugged-in operation. The active source selects **Efficiency**, **Balanced** or **Performance**. Home and Compact intentionally expose the battery preference as the quick control; the full Performance page remains the source of truth for separate battery and AC configuration.

On the verified X9, applying a Windows power preference also coordinates the reviewed Lenovo `LITSSvc` thermal-policy state. If a cooling profile is overriding that policy, a later power-mode, AC/DC or resume transition updates the restore baseline **and reasserts the selected cooling profile for the current power source**. Lenovo uses different reviewed commands for AC and DC, so retaining only an in-memory Quiet/Balanced/Max label is not sufficient.

## Cooling

Cooling is global and does not change merely because AC power is connected or removed:

- **Auto** releases any ThinkControl-owned full-speed override, clears the cooling override and returns to the latest Lenovo/OEM power-policy baseline.
- **Quiet** releases ThinkControl-owned full speed if needed, then requests Lenovo Quiet thermal policy.
- **Balanced** releases ThinkControl-owned full speed if needed, then requests Lenovo Balanced thermal policy.
- **Max cooling** requests Lenovo Performance thermal policy and, where the exact X9 safely exposes it, requests the verified Lenovo Other Mode global full-speed boolean.
- Named custom curves require an active physically accepted direct-output provider; they are not approximated through firmware policy or the full-speed boolean.

The alpha.42 X9 product backend intentionally leaves Lenovo firmware in the closed-loop thermal controller. The full-speed semantic is a narrow OEM override, **not** a generic RPM/PWM/percentage backend. The rejected alpha.38 per-fan target writer remains read-only.

A future direct provider may expose continuous target RPM or calibrated discrete states. Such a provider must advertise the matching capability and pass its physical acceptance gate before custom curves or manual percentages appear.

## X9 provider ordering

For the verified X9 `21Q6/21Q7` reference:

1. Native Lenovo fan telemetry is preferred when real per-fan channels are available.
2. Quiet/Balanced use reviewed Lenovo firmware thermal policy.
3. Max cooling uses Lenovo Performance policy plus the exact known `0x04020000` full-speed boolean only when the feature live-reads safely and every transition verifies readback.
4. The alpha.38 per-fan Other Mode `fanX_target` writer remains read-only after repeated speed cycling and a useful maximum below naturally hot Auto.
5. A transient native telemetry miss does not re-enable the known-inferior classic EC writer after the native path has been confirmed.
6. Classic EC steps remain provider-specific investigation/diagnostic behavior, not the normal product backend.

## Full-speed ownership

The full-speed boolean has its own ownership model because a read of `1` does not prove ThinkControl set it.

- ThinkControl records ownership only when its own successful enable call changes the feature from `0` to `1` and readback confirms `1`.
- If the feature was already `1`, ThinkControl may treat Max as compatible with that state but does not claim ownership.
- A ThinkControl-owned full-speed state is released before Quiet/Balanced/Auto and on normal **service** disposal.
- Closing/restarting only the normal-user UI does not reset an active firmware-policy profile; the privileged service remains its owner.
- A failed enable readback triggers a best-effort release to `0`.
- A failed disable never writes `1` as rollback.
- Failure to safely read/write/verify the exact feature fails the Max transition closed; the service does not guess another writer.

## Restart, resume and source-transition persistence

The stored profile is a preference; the service telemetry is the applied-state source of truth. Alpha.42 explicitly separates those concepts.

1. On app startup, the UI initially shows the service/runtime profile rather than painting the saved preference as already applied.
2. Once the service advertises the relevant cooling capability, the saved profile is actively restored through the same normal semantic API used by an explicit user selection.
3. For the X9 firmware backend, ThinkControl seeds the current Windows power mode as the Auto restore baseline and then applies Quiet/Balanced/Max.
4. A single bounded reassert occurs after startup settles. This covers the practical login race where Lenovo services may finish their own policy work shortly after ThinkControl first becomes available; it is not a periodic polling loop.
5. Windows AC/DC and resume notifications update the baseline and cause the active firmware cooling override to be reasserted using the current source-specific Lenovo command.
6. Closing or restarting the UI preserves a firmware-policy profile. Direct/manual output remains a separate safety class and is returned to Auto when the UI exits.
7. Normal hardware-service shutdown still releases ThinkControl-owned cooling state before hardware disposal.

This prevents a saved **Quiet** selector from remaining visible while the machine has actually drifted back to an Auto/base Lenovo policy.

## Safety invariants

- Firmware/OEM Auto is the ownership fallback.
- Firmware-policy profiles change only reviewed semantic policy state; Lenovo firmware remains responsible for thermal protection and ramping.
- Reassertion reuses the same reviewed Quiet/Balanced/Performance path; it does not introduce guessed EC/RPM writes or an arbitrary policy reader.
- The full-speed feature is exact-X9 and boolean only; no arbitrary feature IDs/values are accepted.
- Raw control temperature is used for safety when ThinkControl directly supervises an output provider; smoothed temperature is used for normal direct curve decisions.
- Direct-output downshifts use hysteresis/minimum dwell while meaningful cooling increases may happen immediately.
- If a direct provider disappears, ThinkControl returns direct ownership to firmware Auto.
- At high-temperature safety handoff, direct ThinkControl ownership returns to Lenovo firmware rather than trapping a manual state.
- Manual level 0 and unverified raw override paths remain blocked.
- Manual direct-output tests are temporary and restore previous profile or Auto.
- Normal service shutdown/disposal releases direct fan ownership, ThinkControl-owned full speed and temporary firmware cooling overrides where possible.
- Telemetry refresh never performs fan writes.
- Fixed low-level targets are not continuously rewritten merely to hold a state.
- RPM telemetry is not treated as proof of physical airflow intensity or maximum cooling.

## Firmware-policy override lifecycle

The X9 coordinator keeps one base Lenovo power-policy mode, at most one cooling profile override, and separate ownership state for the global full-speed semantic.

1. Before selecting Quiet/Balanced/Max, the UI sends the current Windows power preference so the service has a restore baseline.
2. Quiet/Balanced ensure ThinkControl-owned full speed is released, then apply the corresponding Lenovo policy.
3. Max applies Lenovo Performance policy, then requests verified full speed when the exact feature is safely available.
4. If Windows power preference, power source or resume state changes while a cooling override is active, the new preference replaces the stored baseline and the selected cooling override is immediately reasserted for the current source.
5. Selecting Auto releases ThinkControl-owned full speed, clears the profile override and reapplies the latest baseline.
6. A failed policy/full-speed transition is reported explicitly; ThinkControl does not claim a profile changed when Lenovo rejects or cannot verify it.

This ordering keeps Performance and Fans independent without allowing source changes to leave the UI and actual Lenovo policy out of sync.

## Calibration

Calibration exists only for a physically accepted direct provider that explicitly advertises it. It is **not** required for the X9 firmware-policy/full-speed backend.

For a discrete direct-output provider, a calibration run is transactional:

1. verify the direct high-output path and safe starting temperature;
2. settle every provider-defined output state;
3. collect spaced tachometer samples;
4. reject missing, zero or internally implausible evidence;
5. require the top verified state to remain credible for that provider contract;
6. replace stored mapping only after the complete candidate validates.

Cancelling, losing telemetry, crossing safety threshold or failing validation never replaces a previous known-good calibration with partial data. Every direct calibration run returns fan ownership to firmware Auto when it finishes or stops.
