# Device support model

## Principle

**Capabilities, not assumptions — but the UI should not punish an unvalidated device.**

ThinkControl exposes the same product areas across compatible Windows laptops wherever practical. Device validation controls whether a low-level backend is trusted to perform hardware writes; it does not turn the application into a stripped-down edition.

A device can be in one of three compatibility states:

- **Verified** — this exact device/profile and backend have passed on-device validation.
- **Experimental** — the device exposes a known provider/backend and safe probes pass, but the combination has not completed full validation yet.
- **Not validated** — ThinkControl does not yet have enough evidence to trust model-specific writes.

The status is visible in the app. It is not treated as a fatal startup error.

## Identity inputs

Read-only identity may use:

- SMBIOS manufacturer
- product family/name
- machine type/model
- BIOS version
- ACPI device IDs
- presence/version of Lenovo services and drivers
- supported Windows display modes
- presence of PawnIO

Unique serials are not needed for feature matching and must not be collected for compatibility telemetry.

## Profile matching

A bundled profile contains stable identifiers and verification facts. Example:

```text
Manufacturer: LENOVO
Family: ThinkPad
Product: ThinkPad X9-15 Gen 1
Machine types: 21Q6, 21Q7
Compatibility: Verified
```

Profiles and providers are versioned. A remote catalog may update support metadata, labels and read-only compatibility facts, but downloaded data must never become arbitrary executable EC/IOCTL instructions.

## Capability examples

- PerformanceMode
- LenovoThermalPolicy
- CpuTemperature
- FanRpm
- FanControl
- KeyboardBacklight
- DisplayRefresh
- AdaptiveBrightness
- BatteryTelemetry
- BatteryChargeThreshold

Each capability exposes its support state and provenance. Example:

```text
FanRpm
State: Verified
Provider: ThinkPad EC
Source: EC 0x84/0x85
```

An experimental provider can instead report:

```text
KeyboardBacklight
State: Experimental
Provider: IBMPmDrv
Health check: expected level state returned
Write verification: required
```

For fallback temperature:

```text
Temperature
State: SafeReadOnly
Provider: Windows thermal zone
Label: System thermal sensor — approximate
```

Never label an ACPI thermal-zone value as `CPU Package`.

## Provider selection

A device profile does not perform I/O itself. It authorizes a compiled provider to run an allowlisted health check. Provider code owns the exact hardware contract and safety rules.

A provider can become usable on an unvalidated device only when all of the following are true:

1. the provider is compiled into ThinkControl and explicitly marked as eligible for experimental probing;
2. probing is read-only or otherwise documented as non-destructive;
3. returned state is structurally valid and plausible;
4. no conflicting hardware controller is detected;
5. any eventual write operation has a known semantic meaning and mandatory read-back verification;
6. the provider has a defined fail-safe / restore path.

Passing a probe can move a capability from **Not validated** to **Experimental**. It does not make the entire laptop automatically Verified.

## Unvalidated-device behavior

A Not validated laptop still sees the normal ThinkControl areas:

```text
Performance
Fans
Display
Keyboard
Battery
System
Updates
Diagnostics
Settings
```

Windows-level features work whenever Windows exposes them. Hardware-specific sections remain visible and explain their current compatibility state instead of disappearing.

Examples:

```text
Display refresh         available if OS supports it
Brightness              available if OS supports it
Battery telemetry       available if Windows exposes it
CPU telemetry           safe provider where available
Fan RPM                 probe known read-only providers
Fan control             Experimental only after a provider-specific health check; never arbitrary EC writes
Keyboard backlight      Experimental only after known provider read + write/read-back verification
Unknown raw EC writes   never exposed
Unknown raw IOCTLs      never exposed
```

The target UX is therefore "same app, transparent confidence" rather than "supported laptop gets all features, everyone else gets a crippled app".

## Compatibility diagnostics

ThinkControl can help grow device support without requiring every user to manually file a report.

On a Not validated device, the app should offer **Help validate this device** during the compatibility check. With explicit consent, ThinkControl may keep a small local rolling diagnostic history and submit redacted compatibility summaries to a private diagnostics inbox.

Useful compatibility facts include:

- normal device/product name and non-unique machine type
- ThinkControl version/channel
- Windows version/build
- capability/provider name
- provider present / absent
- health-check outcome
- semantic operation success/failure
- read-back verification result
- operation duration / timeout class
- reasonable telemetry ranges such as temperature or RPM range
- sleep/resume recovery outcome

Do not collect or upload:

- serial number
- asset tag
- Windows username
- hostname
- email/account identifiers
- MAC addresses
- disk serials
- personal paths or filenames
- typed keys or key contents
- audio samples
- unrelated process/window contents

See `docs/DIAGNOSTICS.md` for the full privacy and transport design.

## Private diagnostics submission

A public GitHub repository does not provide a private subfolder for incoming user logs. Detailed submissions therefore use a separate private diagnostics inbox, ideally a private repository owned by the project.

The production path is:

1. ThinkControl gathers only allowlisted compatibility events locally;
2. redaction runs on-device;
3. the user can preview what will be sent;
4. diagnostics upload requires explicit opt-in;
5. ThinkControl POSTs the redacted bundle to a tiny project submission service;
6. the service authenticates with a GitHub App server-side;
7. the service creates or updates an item in the private diagnostics repository;
8. no GitHub PAT or private-repository credential is ever embedded in the desktop app.

For ordinary bugs that do not need private logs, the public repository uses `.github/ISSUE_TEMPLATE/bug-report.yml`.
