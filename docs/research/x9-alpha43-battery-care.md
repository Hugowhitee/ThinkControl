# X9 alpha.43 battery charge protection research

## Goal

Add a useful battery-longevity control without inventing a generic percentage writer or making the Battery page depend on Lenovo Vantage.

The user-facing requirement is deliberately small: show the current firmware charge-protection state, offer a concise dropdown, and make the trade-off understandable without promising a made-up number of "saved cycles".

## Lenovo evidence

Modern upstream Lenovo WMI support defines the Other Mode attribute encoding as:

```text
bits 31..24  device
bits 23..16  feature
bits 15..8   mode
bits 7..0    type
```

The PSU device is `0x03`. Lenovo's Other Mode battery support defines feature `0x01` as the PSU charge type and AC type `0x01`, which produces attribute **`0x03010001`**.

Upstream Linux maps that feature to two semantic values:

```text
0 = Standard
1 = Long Life
```

The Linux battery-charge patch for this exact Lenovo WMI feature states that devices implementing `0x03010001` can enable/disable charging at **80%** through the Lenovo Other Mode interface. Current upstream exposes the same contract as Standard / Long Life rather than as an arbitrary numeric threshold.

References:

- Linux Lenovo capability encoding: `drivers/platform/x86/lenovo/wmi-capdata.h`
- Linux Lenovo Other Mode implementation: `drivers/platform/x86/lenovo/wmi-other.c`
- battery charge support patch discussion/implementation: `platform/x86: lenovo-wmi-other: Add WMI battery charge control support`

Lenovo's own battery guidance recommends limiting the upper charge threshold to **80% or lower** for systems that stay connected to AC most of the time, because remaining near 100% can increase unnecessary degradation. Lenovo's Conservation Mode documentation likewise describes keeping charge around 75–80% for long-term battery health.

## Product decision

For alpha.43 the exact-X9 provider exposes only:

- **Battery care · 80%** — Lenovo Long Life charge type;
- **Full charge · 100%** — Lenovo Standard charge type.

ThinkControl does **not** expose 60%, 70%, 85%, 90%, 95% or a freeform slider on this backend. Other laptops/providers may legitimately support those values, but that requires their own verified contract. G-Helper follows the same general principle on ASUS hardware by constraining the UI to model/provider-supported values rather than assuming every percentage works.

The write gate is intentionally stricter than the fan full-speed read fallback:

1. exact verified X9 `21Q6/21Q7` identity;
2. `LENOVO_CAPABILITY_DATA_00` row for `0x03010001` must be present;
3. capability must advertise VALID + GET + SET;
4. live value immediately before the write must be exactly `0` or `1`;
5. requested value is only Standard (`0`) or Long Life (`1`);
6. post-write readback must match.

If the capability row is missing, ThinkControl may report a live read-only state but does not authorize a write.

## Why the UI does not claim “2× fewer cycles”

A charge limit can reduce high-state-of-charge exposure, but actual cell wear depends on temperature, time at voltage, depth of discharge, workload and battery chemistry. A fixed “x fewer cycles” number would therefore be synthetic.

Alpha.43 reports only facts it can defend:

- 80% leaves **20 percentage points of headroom** from a full charge;
- it reduces time spent at high state of charge when the machine is frequently plugged in;
- 100% provides maximum runtime when that capacity is needed.

The existing firmware cycle counter and ThinkControl health trend remain separate measurements.

## Battery-history UX research

Long unstructured event lists are not a good battery-history model. Mainstream battery surfaces aggregate first and drill down second: macOS shows recent windows such as 24 hours / 10 days, while Windows groups recent battery usage rather than presenting one destructive raw log.

ThinkControl therefore keeps the current day → session drill-down model and makes retention explicit:

- recent detailed graphs: user-selectable 7 / 14 / 30 days;
- compact session summaries: one year;
- automatic compaction preserves learned estimates;
- destructive reset moves behind a collapsed **Manage history** surface;
- reset warns that ThinkControl's learned estimates and local health trend will be relearned, while firmware health/cycle count/charge protection are unaffected.

This preserves useful battery learning while making accidental data loss much less likely.
