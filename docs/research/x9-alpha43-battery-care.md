# X9 alpha.43 battery preservation research

## Goal

Add useful battery-longevity control without inventing a generic firmware writer or turning the Battery page into another OEM utility.

The product requirement is intentionally small: read the actual Lenovo threshold configuration, offer a few useful charge windows, explain the trade-off honestly, and keep unsupported hardware read-only.

## Evidence

### Lenovo / Windows threshold model

Lenovo Vantage exposes separate **start charging below** and **stop charging at** thresholds on supported ThinkPads. Lenovo battery guidance also recommends avoiding long periods near 100% when full unplugged capacity is not needed. The generic Linux power-supply model mirrors the same semantics with `charge_control_start_threshold` and `charge_control_end_threshold`; ThinkPad ACPI supports those two thresholds on compatible machines.

The important product distinction is that start/stop thresholds create **hysteresis**: a laptop can remain on AC without repeatedly topping the battery by tiny amounts.

### Current Windows Lenovo PM Device protocol

For alpha.43 the strongest current Windows implementation evidence is the open-source `wesmar/lbm` Lenovo Battery Manager (2026). It uses the installed Lenovo Power Manager configuration and the Lenovo PM kernel device rather than a guessed EC register:

```text
Registry:
HKLM\SOFTWARE\WOW6432Node\Lenovo\PWRMGRV\ConfKeys\Data\<battery>

Values:
ChargeStartPercentage
ChargeStopPercentage
ChargeStartControl
ChargeStopControl

Device:
\\.\IBMPmDrv
```

The reviewed semantic commands are:

```text
0x22261C  select charge mode
0x222630  set start threshold
0x222638  set stop threshold

0x00000101  primary battery threshold mode
0x00000000  automatic/full-charge mode
0x00000100 | percent  primary-battery threshold payload
```

The driver reports rejection through result bit 31. When disabling thresholds, both latched threshold commands are cleared before selecting automatic mode. LBM also keeps PWRMGRV configuration in sync and broadcasts the Lenovo settings change so Lenovo user-mode components see the same state.

Older open-source Lenovo battery tools independently document the same IBMPmDrv start/stop IOCTL family. This is substantially better evidence than inventing a new ACPI/EC write.

### Lenovo Other Mode Long Life is not enough

Upstream Linux Lenovo Other Mode also exposes PSU charge type `0x03010001` as Standard / Long Life on some Lenovo gaming-family devices, with Long Life commonly corresponding to an 80% ceiling. That two-state feature is useful evidence, but it does **not** implement the Vantage-style 75–85% start/stop pair the X9 UI exposes. Alpha.43 therefore does not use `0x03010001` as the X9 product backend.

## Alpha.43 product gate

`LenovoBatteryChargeProtectionService` is initially restricted to verified X9 machine types `21Q6/21Q7` and only becomes writable when all of these are true:

1. the Lenovo PWRMGRV battery configuration exists;
2. the actual battery subkey exposes start/stop percentages and control flags;
3. `\\.\IBMPmDrv` can be opened by the privileged ThinkControl service;
4. the requested pair stays in ThinkControl's deliberately narrow Vantage-style product range;
5. start and stop are five-percent steps and `start < stop`;
6. every driver command succeeds and does not return the Lenovo rejection bit;
7. PWRMGRV is re-read and must match the requested state;
8. failures make a best-effort rollback to the exact configuration observed before the request.

The desktop IPC accepts only an ordered semantic pair such as `75,85` or `off`. It never accepts a raw device path, IOCTL, battery ID or firmware register.

ThinkControl does not change the installed Lenovo driver service start type. If the Lenovo PM Device is unavailable, the Battery page remains read-only and offers the Lenovo battery-settings fallback.

## Presets

The UI deliberately avoids a long slider/range editor. Alpha.43 ships four small choices:

- **Daily · 75–85%** — recommended general-purpose window, close to the Vantage-style daily threshold shown on the reference system;
- **Desk · 55–80%** — more high-charge avoidance while retaining useful unplugged reserve;
- **Maximum care · 40–60%** — intended for machines that spend most of the day connected to AC;
- **Full charge · 100%** — disables the threshold window when maximum runtime is needed.

An existing Lenovo pair that does not match a ThinkControl preset is displayed as **Custom · start–stop%** and is not overwritten until the user deliberately selects a named preset.

No preset is silently applied on first run. Actual Lenovo configuration remains the source of truth.

## Why the UI does not claim “2× fewer cycles”

A lower charge ceiling reduces high-state-of-charge exposure, but battery wear depends on cell chemistry, temperature, calendar time, depth of discharge, workload and charging behavior. A fixed cycle-life multiplier would therefore be synthetic.

The UI reports only defensible facts:

- how many percentage points below full the stop threshold sits;
- the actual start and stop boundaries;
- hysteresis width and its role in avoiding tiny top-ups;
- the trade-off against available unplugged capacity.

Firmware cycle count and ThinkControl's measured health trend remain separate telemetry.

## Battery-history UX

The previous history surface could still grow vertically even though the file itself was already bounded. Alpha.43 keeps the existing safe storage policy and improves presentation instead of deleting more data:

- default view shows the most recent **7 days**;
- **Show older** expands that view to 14 days without changing retention;
- days remain collapsed summaries with session drill-down;
- detailed graph retention is user-selectable at 7 / 14 / 30 days;
- older sessions compact automatically into one year of small summaries;
- the destructive reset is behind **Manage history** and explicitly explains that ThinkControl's learned estimates/history are reset while firmware health, cycle count and charge thresholds are untouched.

This separates **what is shown** from **what is retained**, so the normal Battery page stays short without throwing away useful history.

## Physical validation boundary

Hosted CI can verify the exact identity gate, fixed IOCTL constants, semantic request validation, rollback paths, registry readback architecture, build and UI. It cannot prove the reference X9's embedded controller physically starts/stops charging at the requested percentages.

Before treating X9 threshold control as physically verified, test at least one 75–85% round trip on the real machine, confirm the firmware holds near 85% on AC, drops without immediate top-up until the start boundary is crossed, and returns to ordinary charging after Full charge is selected. Record that separately from CI.