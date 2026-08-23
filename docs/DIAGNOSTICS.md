# Diagnostics and privacy

ThinkControl diagnostics are intended for compatibility and hardware troubleshooting. They are not advertising analytics.

## Purpose

Diagnostics can help determine:

- which providers are available on a device;
- why a capability is unavailable;
- whether a hardware operation succeeded and verified correctly;
- whether a provider is reliable enough to promote from beta to verified support.

## Consent

Detailed network submission is opt-in. The current release does not provide automatic private diagnostics upload.

ThinkControl may keep a small local diagnostic history for troubleshooting. Local data follows the same field restrictions and retention rules described below.

## Compatibility states

Compatibility can be tracked per provider or capability rather than only per laptop.

Current terminology:

- Verified
- Beta or experimental
- Not validated

A laptop can therefore have a verified Windows display path and an unvalidated low-level fan path at the same time.

## Diagnostic events

Events should describe application and hardware operations semantically. Examples include:

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
battery.telemetry_read
service.connected
service.disconnected
system.sleep
system.resume
operation.timeout
operation.failed
```

Do not store arbitrary memory, bulk EC dumps or unrelated device state simply because it is accessible.

## Allowed data

A diagnostic record may include:

- ThinkControl version and channel;
- event schema version;
- UTC timestamp;
- Windows version and build;
- manufacturer and normal product name;
- non-unique Lenovo machine type;
- BIOS version when relevant;
- capability and provider name;
- compatibility state;
- operation name and result;
- categorized error code;
- operation duration;
- whether a write passed readback verification;
- fan level or Lenovo Auto state;
- bounded RPM or temperature observations;
- installed ThinkControl prerequisite versions;
- sleep and resume outcome.

## Data that must not be collected

ThinkControl diagnostics exclude:

- serial number;
- asset tag;
- unique hardware UUID used to identify a physical laptop;
- Windows username;
- hostname;
- email address or account ID;
- MAC address;
- disk serial number;
- browser history;
- document names;
- clipboard contents;
- typed keys or text;
- microphone or loopback audio samples;
- screenshots unless the user explicitly attaches one;
- unrelated process or window inventories.

Keyboard activity used by the Reactive backlight effect is not persisted. Audio used by the Audio effect is reduced to a local level measurement and the audio samples are not retained.

## Local storage

Recommended location:

```text
%LOCALAPPDATA%\ThinkControl\Diagnostics\
```

Recommended policy:

- JSON Lines format;
- bounded files;
- maximum three rolling files;
- maximum 1 MB per file;
- maximum seven days retention;
- rotation by size and date.

## Redaction

Redaction happens before any data leaves the machine.

1. Build records from strongly typed allowlisted fields.
2. Reject fields outside the schema.
3. Sanitize free-form error text.
4. Remove accidental user-profile fragments.
5. Remove non-ThinkControl filesystem paths where possible.
6. Show the final data in Preview before manual submission.

Structured error categories are preferred over raw exception text in shared diagnostics.

## Future private submission

Detailed diagnostics should not be posted automatically to the public ThinkControl repository.

A future private submission path should use a project-controlled HTTPS endpoint. Any GitHub or storage credential must remain on the server side. ThinkControl must not embed a GitHub personal access token in the desktop application.

## Network cadence

If private opt-in submission is implemented later, it should send small event summaries at meaningful points rather than streaming sensor data.

Appropriate triggers include first compatibility scan, provider state changes and repeated hardware-operation failures. Every keypress, RPM poll or brightness change should not produce a network request.

## User controls

Diagnostics settings should provide:

- compatibility status;
- upload consent when a private endpoint exists;
- last submission status;
- Preview data;
- Export support bundle;
- Send diagnostics now when supported;
- Open bug report;
- Delete local diagnostics.

ThinkControl remains usable with network diagnostics disabled.

## Public bug reports

The repository issue form asks for the information normally needed to reproduce a problem:

- device family;
- exact model;
- ThinkControl version;
- affected area;
- problem description.

Screenshots, reproduction steps and support bundles are optional unless a specific issue requires them.
