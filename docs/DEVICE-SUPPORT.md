# Device support

This document describes the support model at **v0.1.0-alpha.43**. ThinkControl is intentionally capability-driven: a laptop model name alone does not grant direct write access or decide which setup/calibration/effect workflows appear. Immutable `v0.1.0-alpha.42` remains the release baseline underneath this candidate.

## Support levels

### Windows-generic

Available without vendor-specific write access where Windows exposes the information/action safely:

- shell/navigation and settings;
- update/install flow;
- Windows power-policy integration;
- display/audio pages backed by Windows-visible capabilities;
- alpha.43 **Audio Safety** (`Normal` / `Media lock` / `Silent`) using Windows semantic audio APIs;
- battery and generic telemetry that Windows/providers expose;
- local battery-history aggregation/retention management;
- diagnostics/report preview and explicit sharing controls.

Audio Safety is a Windows-user-session policy and does **not** grant any low-level device write capability. Media lock only suppresses ThinkControl Touchpad media/output actions. Silent additionally mutes the current Windows render endpoint and blocks ThinkControl output writes while leaving microphone input independent.

### Provider-backed read-only

ThinkControl can expose telemetry or state from a reviewed provider without implying that writes are safe. Fan RPM, temperatures, charge thresholds and OEM feature reads may therefore be visible while the corresponding write capability remains unavailable.

### Verified semantic policy support

An OEM may expose reviewed semantic policy without exposing arbitrary low-level writes. ThinkControl exposes only states the provider proves.

### Verified narrow hardware semantic

A model/provider may expose a narrowly defined hardware semantic that is not a generic writer. Current X9 examples are Lenovo's known global full-speed boolean and the Lenovo PM Device charge-threshold contract. Neither implies arbitrary Other Mode features, EC registers or IOCTL payloads are safe.

### Verified direct write support

A direct-output control is enabled only when the active provider advertises the exact semantic capability and passes its provider/device validation gate **and any required physical acceptance gate**. A failed or unknown path stays read-only rather than guessing addresses, commands or larger numeric ranges.

## ThinkPad X9 15 Gen 1

Machine types `21Q6` / `21Q7` are the current verified X9 development path. That identity is only one part of each gate: the relevant provider must also match the reviewed semantic contract. The X9 is a reference implementation, not the product boundary.

Current X9-oriented areas include:

- sensor discovery and CPU/control temperature sources;
- independent Fan 1 / Fan 2 telemetry where Lenovo-native or reviewed providers expose it;
- Lenovo `LENOVO_OTHER_METHOD` native dual-fan telemetry where real `fanX_input` channels pass live-read gates;
- built-in **Auto / Quiet / Balanced / Max cooling** through reviewed Lenovo firmware-policy semantics;
- alpha.41 exact-X9 support for Lenovo Other Mode's known global **full-speed boolean feature `0x04020000`** for Max cooling only when it live-reads safely and every transition verifies readback;
- alpha.42 persistence/reassertion of the selected firmware cooling profile across UI restart, Windows startup settle, AC/DC transitions and resume;
- alpha.43 exact-X9 **battery preservation start/stop thresholds** through the installed Lenovo `PWRMGRV` + `IBMPmDrv` Windows stack when that provider is present and writable;
- the experimental per-fan Other Mode `fanX_target` writer kept **read-only** after physical testing reproduced repeated speed cycling/re-kick and weaker useful cooling than naturally hot Auto;
- read-only Lenovo `EnergyDrv` fan telemetry while its write contract remains unverified;
- the seven-step ThinkPad EC implementation retained as provider-specific investigation/diagnostic code, not silently re-authorized once native OEM fan telemetry has been confirmed;
- Lenovo keyboard backlight provider/readback and firmware Auto where verified;
- haptic/raw-touchpad discovery and the shared Touchpad gesture editor;
- alpha.43 Audio Safety, which is Windows-generic and does not alter any Lenovo provider boundary.

If two native Lenovo fan channels have been proven during a hardware-service lifetime, a transient native read failure—or a rejected per-fan writer—does not silently re-authorize the EC writer. Provider failure is not permission to guess a lower-level backend.

## Battery charge-protection semantics

The current X9 provider follows Lenovo's Windows start/stop-threshold model rather than pretending every laptop has one generic charge-limit slider.

The provider reads the actual Lenovo battery configuration under `PWRMGRV` and requires the privileged service to open `\\.\IBMPmDrv`. It exposes an ordered start/stop pair plus whether threshold control is enabled. Product writes are bounded to five-percent steps with start below stop; the normal page offers a small named preset list rather than the driver's raw value range.

Alpha.43 presets are:

```text
Daily         75–85%   recommended general-purpose window
Desk          55–80%   stronger high-charge avoidance
Maximum care  40–60%   for mostly-plugged-in use
Full charge   100%     disable thresholds / ordinary charging
```

ThinkControl does **not** silently apply one of these on first run. Existing Lenovo state is authoritative. If the machine already has a different valid pair, the page shows `Custom · start–stop%` until the user deliberately selects a named preset.

A write is authorized only on the verified X9 identity when the PWRMGRV battery configuration exists, `IBMPmDrv` is writable, the semantic pair passes ThinkControl's bounded range rules, the fixed Lenovo PM Device commands succeed without the rejection bit, and PWRMGRV readback matches. A failure requests rollback to the state observed before the change. ThinkControl does not alter the Lenovo driver service start type and does not try an EC/ACPI fallback.

The UI deliberately does not promise “x fewer cycles”. A lower upper threshold reduces time at high state of charge, but real wear also depends on chemistry, temperature, calendar time, depth of discharge and workload. Firmware cycle count and ThinkControl health trend remain separate measurements.

Other OEMs or future Lenovo providers can expose their own semantic threshold set without changing the shared Battery page into a vendor-specific page.

## Battery history semantics

Battery history is local-only product data. The normal UI aggregates **day first, session second** rather than showing one raw endless event list.

- default visible range: latest **7 days**;
- `Show older`: latest 14 days, without changing retention;
- detailed graph retention: user-selectable **7 / 14 / 30 days**;
- compact summaries: one year under the existing retention policy;
- detailed points compact automatically instead of forcing manual cleanup;
- destructive reset lives behind **Manage history** and warns that local learned estimates/trends are reset while firmware health, cycle count and charge thresholds are untouched.

## Fan semantics

Fan features are kept semantically distinct:

- **Firmware/OEM Auto**: firmware owns cooling and the current OEM power-policy baseline applies;
- **OEM firmware-policy profile**: a reviewed Quiet/Balanced/Performance transition while firmware still owns the closed-loop fan algorithm;
- **OEM global full speed**: a narrowly known boolean semantic, separate from per-fan RPM targets and only available where exact provider/device gates pass;
- **OEM target RPM**: a provider may advertise a real per-fan target only after capability/range and required physical behavior have both been accepted;
- **named direct curves**: routed through an active physically accepted direct provider only;
- **discrete output**: provider/model-specific states, not fake continuous PWM;
- **calibration**: a provider-advertised direct-output mapping workflow;
- **telemetry-only**: RPM/state can be shown without enabling direct writes.

On the current X9 backend the built-ins map as follows:

```text
Auto         -> release ThinkControl-owned full speed if any; clear cooling override; restore latest Lenovo power-policy baseline
Quiet        -> ensure ThinkControl-owned full speed is released; Lenovo Quiet policy
Balanced     -> ensure ThinkControl-owned full speed is released; Lenovo Balanced policy
Max cooling  -> Lenovo Performance policy + verified 0x04020000 full-speed boolean when safely exposed
```

The UI seeds the service with the current Windows performance preference before enabling a cooling override. Lenovo's reviewed policy command differs by power source, so Windows power-mode, AC/DC and resume events both update the Auto restore baseline and reassert the active Quiet/Balanced/Max override for the current source. Auto later restores the latest baseline.

A saved profile is not itself proof of applied state. During startup the Fans selector follows runtime/service state; after capability discovery, the saved profile is actively reapplied. Alpha.42 adds one bounded seven-second settle reassert to cover late Lenovo login/service policy work. Closing/restarting only the normal-user UI keeps a service-owned firmware profile active.

### X9 Other Mode details

Known per-fan attributes remain `0x04030001` onward. Independently live channels can be native telemetry evidence, but VALID+GET+SET metadata plus sane Fan Test ranges no longer authorizes the X9 per-fan target writer after its physical rejection. Target `0` remains available only for cleanup/reassertion of previously owned stale state.

Alpha.41's `0x04020000` path is intentionally separate and treated only as boolean full speed. It requires exact identity, live boolean state, bounded 0/1 writes, readback and ownership tracking. Probe/readback failure fails closed rather than falling back to per-fan target RPM, raw EC or unknown IOCTLs.

RPM telemetry is not treated as a proxy for airflow intensity. Physical evidence showed high-looking RPM while some firmware-policy states still felt weaker than naturally hot Lenovo Auto; the full-speed semantic addresses that distinction without pretending to be continuous percentage control.

`EnergyDrv` `QueryFanSpeed 0x83102570` remains read-only evidence. `ChangeFanSpeed 0x8310257C` remains blocked until exact X9 encoding and rollback semantics are recovered.

The classic EC states are not generic laptop controls. **Raw EC diagnostics** appear only if an active provider explicitly exposes the verified discrete-EC semantic contract. Manual percentage/raw-state interactions use bounded temporary-test safety where applicable.

## Keyboard semantics

- Off / Low / High are static hardware states when available.
- Auto means a verified firmware/OEM mode where supported; ThinkControl does not imitate it with a software idle loop.
- Breathing / Reactive / Audio are separate ThinkControl user-session effects.
- Effects appear only when the active provider advertises `KeyboardEffects`.
- A saved effect is restored only after that capability has been observed.

## Touchpad semantics

The Touchpad editor exposes six selectable zones: Top, Bottom, Left, Right, Top-left and Top-right. Edges and corners share one selection/rendering system while runtime recognition remains deliberately strict.

Enabled top-corner launch geometry remains the canonical **guard → diagonal lane → rounded end-cap** shape. The right side is an exact horizontal mirror of the left. Rejected corner candidates stay locked out until lift rather than falling through into nearby edge gestures.

Track control remains one continuous edge action. Standalone current Play/Pause is not offered separately; legacy serialized PlayPause bindings sanitize into Track control.

Alpha.43 adds a **Play / Pause** switch only inside the selected Track-control editor. Existing alpha.42 settings default to enabled for backward compatibility. When enabled, the lane remains **Previous | Play/Pause | Next** and the center target stays **28%** of the Track edge (`0.36..0.64`). Play/Pause is deliberately **hold-to-release**: the contact must remain down at least **450 ms**, stay within **3 mm** maximum radial movement, and then release. Quick taps are ignored.

When the Track-local switch is disabled, Track remains assigned but the center target is neither active nor visible: no center fill, separators or Play/Pause icon. Previous/Next keeps the unchanged **9 mm** threshold. The preference survives temporarily moving/removing Track.

When reverse close is enabled for a top corner, the reverse start target is the **inner half of the already-visible diagonal lane**, not only the small rounded inner cap. The outer guard remains an inward-launch start, the right side remains mirrored, and no invisible reverse hit area exists beyond rendered geometry.

Occupied edge actions swap instead of destructively clearing the previous edge. Sensitivity and inversion remain attached to physical edges.

## Audio Safety semantics

Audio Safety is available anywhere the normal Windows output endpoint can be accessed; it is not an OEM capability.

- **Normal** — ThinkControl audio/media controls behave normally.
- **Media lock** — ThinkControl Touchpad Volume, Media scrub and Track media commands are blocked. It does not mute Windows output or stop audio started elsewhere.
- **Silent** — includes Media lock, mutes the current Windows render endpoint and blocks ThinkControl output-volume/unmute writes.
- **Microphone** — capture/input state remains independent in all three modes.

Alpha.43 keeps Audio Safety **session-only**. Restart starts in Normal because a new process cannot safely claim ownership of mute state created by the old process. While Silent is active, ThinkControl records the prior mute state of each default output endpoint it actually encounters and restores only those states when leaving Silent/orderly exit. Default-output convergence reuses the existing app status cadence rather than adding another polling loop.

## Unknown/new hardware

Unknown hardware stays safe by default:

1. collect passive, non-sensitive identity/capability evidence;
2. expose read-only features with credible provider support;
3. keep risky writes unavailable;
4. allow explicit sanitized compatibility reporting;
5. promote write support only after reviewed evidence and explicit provider/profile changes.

ThinkControl never learns a new device by experimentally writing arbitrary EC/IOCTL/BIOS values merely for diagnostics.

## Physical validation

Hosted CI can prove source/build/lifecycle behavior but not physical hardware feel, Windows endpoint acoustics or firmware response.

Confirmed negative X9 evidence remains:

- alpha.38 per-fan target-RPM control repeatedly re-kicked/waved rather than settling;
- nominal target 100% was weaker than naturally hot Lenovo Auto;
- alpha.40 Performance-policy-only Max cooling improved behavior but still felt less forceful than Auto despite high-looking RPM telemetry;
- alpha.41 Track center remained physically harder to trigger than intended and reverse close was unreliable because its start target was too precise;
- during alpha.41/early-alpha.42 restart testing, a saved Quiet preference could remain visibly selected while physical airflow behaved like a harder Auto/base policy.

Alpha.43 battery threshold behavior still needs physical confirmation on the reference X9: verify a selected window such as 75–85% actually stops/holds near the stop boundary, does not immediately top up again while above the start boundary, and returns to ordinary charging after Full charge is selected. A successful driver call/registry readback is not by itself proof of the physical charge boundary.

Touchpad, fan persistence and Audio Safety real-device checks remain listed in `docs/ALPHA-TESTING.md`. These physical checks must not be marked complete from screenshots/CI alone.
