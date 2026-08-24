# Diagnostics and privacy

ThinkControl diagnostics exist for hardware compatibility and troubleshooting, not advertising analytics.

## Current behavior

ThinkControl keeps a small, bounded **local** troubleshooting history so hardware/provider failures can be diagnosed. The Settings compatibility-sharing toggle is enabled by default, but it does not create automatic network traffic.

ThinkControl does not automatically upload diagnostics. **Share device report** only works while optional compatibility sharing is enabled and creates a sanitized, hardware-only GitHub issue draft in the user's browser. The user can inspect and edit the report before submitting it. Turning sharing off does not secretly upload or submit the existing local history; the local data can be deleted independently at any time.

This design intentionally avoids embedding a GitHub personal access token in the desktop application.

## Purpose

Diagnostics help determine:

- which providers and sensor types a laptop exposes;
- why a capability is unavailable;
- whether an operation succeeded and passed readback checks;
- whether read-only fan/sensor telemetry exists even when write control is unavailable;
- which device/provider combinations are strong candidates for a reviewed support profile.

## Compatibility states

Compatibility is tracked per capability/provider rather than assuming an entire laptop is either supported or unsupported.

- **Verified** — physically validated implementation with required safety/readback checks.
- **Experimental** — a known family/provider is present but exact hardware has not yet been physically validated.
- **Not validated** — Windows-level features may work, but model-specific hardware behavior is unknown.

Unknown devices are probed read-only first. A device report is evidence for investigation; it does not automatically authorize unknown EC/PWM writes.

## Local diagnostic events

Examples include:

```text
app.started
compatibility.device_detected
capability.probe_started
capability.probe_passed
capability.probe_failed
fan.rpm_read
fan.level_set
fan.returned_to_auto
keyboard.level_set
power.profile_set
display.refresh_set
service.connected
service.disconnected
```

ThinkControl does not persist bulk EC dumps or arbitrary device memory as general diagnostics.

## Allowed data

A local diagnostic bundle may include:

- ThinkControl version/channel;
- UTC timestamps;
- Windows version/build;
- manufacturer and normal product name;
- non-unique machine type/model code;
- BIOS version;
- capability/provider names;
- compatibility state;
- semantic operation/result and categorized error code;
- operation duration/readback result;
- bounded fan level, RPM or temperature observations;
- ThinkControl prerequisite/provider versions.

A **Share device report** is narrower than the local support bundle. It summarizes model, capability booleans, sensor types and provider sources and asks the user to add only physical verification they personally observed.

## Excluded data

ThinkControl diagnostics and generated device reports exclude:

- serial number and asset tag;
- unique hardware UUIDs;
- Windows username and hostname;
- email/account identifiers;
- MAC address;
- disk serial number;
- browser/document/clipboard contents;
- typed keys or text;
- microphone/loopback samples;
- screenshots unless the user explicitly attaches one;
- unrelated process/window inventories;
- personal filesystem paths.

Reactive keyboard activity is not persisted. Audio-reactive effects use only transient local level measurements.

## Local storage

Diagnostics are stored under:

```text
%LOCALAPPDATA%\ThinkControl\Diagnostics\
```

The recorder uses bounded JSON Lines files with a maximum of three rolling files, 1 MB per file and seven days retention.

## Redaction

Redaction happens before export/sharing:

1. Records are built from strongly typed allowlisted fields.
2. Unknown tags are dropped.
3. User/profile and machine-name fragments are replaced.
4. Free-form values are length bounded and control characters removed.
5. Device support reports include provider/type summaries rather than raw logs or unique sensor IDs.
6. The final public GitHub issue is shown to the user before submission.

## User controls

Settings provides:

- optional compatibility sharing on/off;
- compatibility status;
- Share device report;
- Preview data;
- Export support bundle;
- Bug report;
- Delete local diagnostics.

ThinkControl remains usable with optional compatibility sharing disabled.

## Future private endpoint

If automatic/private submission is added later, it should use a project-controlled HTTPS endpoint with server-side credentials. A GitHub or storage credential must never be embedded in the desktop application. Network events should be sparse capability summaries, not streamed sensor telemetry.
