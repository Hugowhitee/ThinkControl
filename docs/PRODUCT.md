# ThinkControl product specification

ThinkControl is a lightweight Windows hardware companion for Lenovo ThinkPads. It provides fast access to everyday performance, cooling, display, keyboard and battery controls while keeping model-specific hardware access explicit and safety-gated.

The ThinkPad X9-15 Gen 1 is the reference device for the first public alpha. Other laptops can use Windows-level capabilities where available, while Lenovo-specific or EC-backed features are enabled only when ThinkControl has enough evidence to use them safely.

## Product principles

ThinkControl is designed around five rules:

1. **Fast daily controls.** Common settings are available from a compact tray popup without opening a full OEM suite.
2. **Truthful telemetry.** Values are shown only when a real provider supplies them. Sensor names, fan states and compatibility levels are not invented.
3. **Capability-based hardware support.** Support is evaluated per feature and provider rather than inferred from a Lenovo model name alone.
4. **Least privilege.** The normal UI runs as the user; privileged hardware access belongs to the ThinkControl service.
5. **Safe fallback.** Direct hardware control always has a defined recovery path, including returning X9 fan control to Lenovo Auto.

## User interface

ThinkControl has two primary surfaces.

### Compact tray popup

The compact popup is the daily-driver interface. It contains:

- detected device name
- CPU temperature and 60-second history when available
- real fan RPM and current fan state when available
- Quiet / Balanced / Performance selection
- display refresh controls
- brightness and adaptive brightness
- keyboard backlight level
- compact battery status including charge, power and ETA when available
- direct links to Battery, Fans, System and Settings
- an expand control for the Advanced window

The popup stays small enough to behave like a system utility rather than a dashboard.

### Advanced window

The Advanced window contains:

- Home
- Performance
- Fans
- Display
- Keyboard
- Battery
- System
- Updates
- Settings

It is a normal resizable window. Closing it returns ThinkControl to the tray rather than terminating the application.

## Performance profiles

ThinkControl exposes three user-facing profiles:

### Quiet

Biases supported Windows and Lenovo policy providers toward efficiency and lower thermal activity.

### Balanced

Provides the normal everyday balance between responsiveness, power use and cooling.

### Performance

Biases supported providers toward maximum responsiveness under sustained load.

Profiles coordinate supported providers through one authority so Windows power mode, Lenovo thermal policy and ThinkControl hardware policy do not continuously fight each other.

ThinkControl does not display inferred wattage or acoustic values for these profiles. Any future dBA display must come from device-specific measured and documented data.

## Fan control

The verified X9 backend exposes the hardware states that actually exist:

- Lenovo Auto
- manual levels 1 through 7

It does not expose a fake 0-100% PWM slider.

The X9 backend reads fan state from EC register `0x2F` and tachometer data from `0x84/0x85`. Unsafe fan-off value `0x00` is blocked and unverified override state `0x40` is not exposed.

Direct fan control is designed around:

- immediate upward cooling transitions
- delayed downward transitions
- temperature hysteresis
- minimum state hold time
- duplicate-write suppression
- conservative tachometer sampling
- sleep/resume recovery
- service-stop and crash recovery to Lenovo Auto
- conflict detection for other EC fan controllers

RPM telemetry is not used as the control-loop clock.

## Display

Where Windows exposes the capability, ThinkControl provides:

- current and maximum refresh rate
- Auto refresh behavior
- explicit 60 Hz selection
- panel-maximum selection
- brightness control
- adaptive brightness

Auto refresh can use a lower refresh rate on battery and the panel maximum on AC when both modes are available.

## Keyboard

ThinkControl separates physical backlight levels from user-session effects.

Hardware levels:

- Off
- Low
- High

ThinkControl policies/effects:

- Auto
- Breathing
- Reactive
- Audio reactive

Hardware writes are available only through a compatible Lenovo provider with verification. Effects are implemented as policies over the real backlight levels; they are not presented as firmware capabilities.

## Battery

Battery telemetry uses Windows/ACPI data when available and can expose:

- charge percentage
- charging/discharging state
- instantaneous power
- filtered average power
- remaining and full-charge energy
- battery health
- estimated time remaining or time to full

ETA is intentionally smoothed so short power spikes do not make it jump continuously.

Charge-threshold control is not enabled until a compatible, verified write provider exists for the device.

## Compatibility model

Compatibility is evaluated per capability/provider:

- **Verified** — validated on the exact supported device/profile.
- **Experimental** — a known provider is present and safe checks pass, but the combination is not fully validated.
- **Not validated** — there is not enough evidence to trust model-specific hardware writes.

A device can therefore have verified display controls, experimental keyboard access and unavailable fan control at the same time.

Unknown devices never inherit X9 EC writes merely because they are ThinkPads or Lenovo laptops.

## Diagnostics and privacy

Compatibility diagnostics are designed for hardware validation, not advertising analytics.

Detailed upload is opt-in. Diagnostic data is built from an allowlisted schema and excludes serial numbers, usernames, hostnames, MAC addresses, disk serials, personal paths, typed text and audio samples. Users can preview, export and delete local diagnostic data from Settings.

## Updates and distribution

ThinkControl uses GitHub Releases as its public update channel.

The installation model consists of:

- ThinkControl UI
- ThinkControl Windows service
- required .NET runtime or self-contained payload, depending on release packaging
- device-conditional PawnIO hardware access where a verified backend requires it

Lenovo and Intel OEM components remain vendor-owned and are diagnosed or linked to rather than mirrored blindly by ThinkControl.

Update installation is explicit; ThinkControl does not require a permanent updater service.

## Scope boundaries

ThinkControl intentionally does not provide:

- arbitrary EC/register editing
- arbitrary IOCTL passthrough
- unverified fan-off or override states
- private Intel IPF calls
- custom PL1/PL2 controls
- undervolting
- universal all-ThinkPads hardware-write support

Features outside this list can be added when they have a supported API or a device-specific backend with a documented safety model.
