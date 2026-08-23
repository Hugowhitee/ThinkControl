# Product plan

## Positioning

ThinkControl is the fast hardware-control layer that Lenovo Vantage is not trying to be: a compact tray companion for settings a ThinkPad owner may change several times per day. Lenovo Vantage/Commercial Vantage remains the place for warranty, broad firmware maintenance, diagnostics and obscure vendor settings.

The target experience is closer to G-Helper: startup to tray, one-click compact popup, immediate profile switching and an optional larger Advanced window.

## Daily-driver surface

The compact popup should eventually contain:

- ThinkPad identity and active profile
- CPU temperature with the exact source label
- actual fan RPM when safely obtainable
- thin 60-second temperature sparkline
- Quiet / Balanced / Performance
- display refresh rate
- brightness
- keyboard backlight
- battery status
- one path into Advanced

No large gauges, decorative telemetry or controls that do nothing.

## Advanced sections

1. Performance
2. Fans
3. Display
4. Keyboard
5. Battery
6. Lenovo Software
7. Device
8. Settings

## Profiles

Profiles coordinate multiple subsystems through one authority rather than letting independent components fight each other.

### Quiet

Intended outcome: lowest distracting noise while retaining safe automatic recovery.

### Balanced

Intended outcome: default everyday behavior with Lenovo/Windows policy cooperation.

### Performance

Intended outcome: stronger platform power policy and more aggressive verified fan behavior.

Future profiles may define separate AC and battery behavior and may be exported/imported as `.thinkprofile` files.

## Fan UX

The X9 reference hardware exposes Lenovo Auto plus manual levels 1 through 7. ThinkControl must represent that truthfully. A user may edit a temperature curve, but every point resolves to a discrete fan state. The table is the source of truth; a graph is only a visualization.

Required control-engine behavior once direct fan control is implemented:

- immediate upward cooling transitions
- delayed downward transitions
- temperature hysteresis
- minimum hold time
- delayed handoff to Lenovo Auto
- duplicate-write suppression
- backend-specific RPM sampling that avoids repeatedly disturbing the EC
- sleep/resume recovery
- crash/service-stop recovery to Lenovo Auto
- conflict detection with other EC fan controllers

## Lenovo software page

ThinkControl should report and link to, rather than mirror, Lenovo software and support components such as:

- Commercial Vantage / Lenovo Vantage
- Lenovo Intelligent Thermal Solution
- Lenovo Service Bridge
- Drivers & Software
- diagnostics
- BIOS/support pages

Lenovo Service Bridge is support/product-detection integration, not a hardware-control backend.

## Unknown ThinkPads

An unknown ThinkPad is never treated as "close enough" to a known model. It may receive safe Windows features and read-only detection. Hardware-write features remain hidden/disabled until a verified release adds support.

Unknown-device diagnostics are explicit opt-in. The preview must strip serial number, Windows user name, host name, MAC addresses, disk serials and account identifiers.

## Non-goals for the first public versions

- touchpad volume/brightness gestures
- RGB or audio-reactive keyboard effects
- undervolting
- custom PL1/PL2 controls
- private Intel IPF calls
- raw EC register editor
- unverified 0x40 fan override
- universal "all ThinkPads supported" claim

## Roadmap

### Phase 0 — foundation

Architecture, safety model, UI/service separation, device profiles, diagnostics rules, CI, logging, update/installer design and a theme system.

### Phase 1 — X9 daily driver

Quiet/Balanced/Performance, temperature, RPM, Lenovo Auto + EC levels 1-7, fan profiles, refresh rate, brightness, adaptive brightness, keyboard Off/Low/High/Auto, tray/undocking, Lenovo Software and update checking.

### Phase 2 — public ThinkPad beta

Anonymous diagnostics, unknown-model flow, more profiles, battery health, verified charge limits/Fn Lock where available, profile import/export.

### Phase 3 — broader support

More ThinkPads, validated user fan tables, optional profile sharing, additional backends, richer graphs and carefully gated experimental features.
