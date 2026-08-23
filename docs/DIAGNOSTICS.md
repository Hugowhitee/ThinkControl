# Diagnostics and compatibility telemetry

ThinkControl uses diagnostics to answer a narrow engineering question: **which capabilities work reliably on which hardware, and how did a verified/experimental backend behave?**

Diagnostics are not advertising analytics and are not intended to profile the user.

## Goals

- discover compatibility on devices the project owner does not physically have
- catch provider failures that users may never file manually
- distinguish a missing backend from a backend that is present but failing
- measure hardware-operation reliability without collecting personal activity
- make it easier to promote an Experimental device/capability to Verified

## Consent

Detailed upload is opt-in.

Recommended first-run flow for a Not validated device:

```text
This device has not been validated with ThinkControl yet.

ThinkControl can run safe compatibility checks and, if you allow it,
send redacted technical diagnostics to help validate this model.

[Allow diagnostics]   [Not now]
```

The user can change this later in Settings > Diagnostics.

ThinkControl may keep a small local diagnostic history needed for troubleshooting even when upload is disabled, but the local file follows the same redaction rules and has a short retention window.

## Compatibility states

- `Verified`
- `Experimental`
- `NotValidated`

The state is per capability/provider where possible. A laptop can therefore have a Verified display backend, Experimental keyboard backend and NotValidated fan-control backend at the same time.

## Events worth recording

Examples:

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
hardware.conflict_detected
operation.timeout
operation.failed
```

Events should be semantic. Never record arbitrary raw memory, arbitrary EC dumps or unrelated device state just because it is available.

## Allowed fields

A diagnostic envelope may include:

- ThinkControl version and channel
- event schema version
- UTC timestamp rounded to a useful resolution
- OS name/version/build
- normal manufacturer/product name
- non-unique ThinkPad machine type/model code
- BIOS version where useful for compatibility
- capability name
- provider/backend name
- compatibility state
- operation name
- success/failure
- categorized error code
- operation duration
- whether a write was read-back verified
- fan level / Lenovo Auto state
- bounded RPM or temperature observations
- installed ThinkControl prerequisite/provider versions
- sleep/resume lifecycle outcome

## Always redact / never collect

- serial number
- asset tag
- UUID intended to identify the physical laptop
- Windows username
- hostname
- email/account name or ID
- IP address when it can be avoided by application-layer storage
- MAC addresses
- disk serials
- browser history
- document names
- personal filesystem paths
- clipboard contents
- typed key values or text
- raw keyboard event contents
- microphone/audio samples
- screenshots unless the user explicitly attaches one
- unrelated running-process/window lists

The app may observe **that keyboard activity occurred** for a Reactive backlight effect. That activity is not diagnostic data and must not be persisted.

The Audio backlight effect reduces loopback audio to a local RMS level. Audio samples and RMS history are not diagnostic data and must not be uploaded.

## Local storage

Recommended path:

```text
%LOCALAPPDATA%\ThinkControl\Diagnostics\
```

Recommended format:

- rolling JSON Lines (`.jsonl`)
- small bounded files
- no unbounded debug log
- retention measured in days, not months
- rotate by size and date

Suggested defaults:

- maximum 3 rolling files
- maximum 1 MB per file
- maximum 7 days retention

## Redaction pipeline

Redaction happens **before** data leaves the laptop.

1. construct diagnostics from strongly typed allowlisted fields;
2. reject fields that are not in the schema;
3. sanitize free-form error messages;
4. replace accidental username/home-directory fragments;
5. remove path-like strings unless they point to a ThinkControl-owned directory;
6. preview the final envelope in the UI;
7. upload only after consent.

Prefer structured error codes over raw exception text in uploaded summaries.

## Private transport

Do not upload detailed logs directly into the public `Hugowhitee/ThinkControl` repository.

A public repository does not have a private folder for incoming issue data. The intended architecture is:

```text
ThinkControl desktop app
        |
        | HTTPS, redacted JSON/ZIP
        v
ThinkControl diagnostics endpoint
        |
        | server-side GitHub App credential
        v
private diagnostics repository / storage
```

The private repository can be something like `Hugowhitee/ThinkControl-Diagnostics` and should not contain source secrets from the desktop application.

No GitHub PAT is embedded in ThinkControl.

## Upload cadence

With consent enabled, prefer low-volume meaningful summaries rather than continuous streaming.

Good triggers:

- after the first compatibility scan on a new Not validated device
- after a provider changes state
- after repeated hardware-operation failure
- after successful completion of an Experimental validation sequence
- at most one small periodic health summary per day while the app is actively used

Do not send a network request for every sensor poll, keypress, RPM sample or brightness change.

## User controls

Settings > Diagnostics should expose:

- compatibility state
- diagnostics upload toggle
- last upload status/time
- `Preview data`
- `Export support bundle`
- `Send diagnostics now`
- `Open public bug report`
- `Delete local diagnostics`

The user should be able to use ThinkControl with diagnostics upload disabled.

## Public bug reports

The repository provides a structured issue form at `.github/ISSUE_TEMPLATE/bug-report.yml`.

Public reports intentionally ask for only a few required fields:

- device family
- exact model
- ThinkControl version
- affected area
- problem description

Everything else is optional, including reproduction steps and attachments.
