# Device support

This document describes the support model at **v0.1.0-alpha.43**. ThinkControl is intentionally capability-driven: a laptop model name alone does not grant direct write access or decide which setup/calibration/effect workflows appear. Immutable `v0.1.0-alpha.42` remains the low-level hardware baseline for this candidate.

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

ThinkControl can expose telemetry from a reviewed provider without implying that writes are safe. Fan RPM, temperature and OEM feature reads may therefore be available even when direct fan output remains blocked. The same applies to OEM battery-care state: a credible live read may be shown read-only when the provider does not expose a complete write contract.

### Verified semantic policy support

An OEM may expose reviewed semantic policy without exposing arbitrary low-level writes. ThinkControl may expose only the semantic states that provider actually proves.

### Verified narrow hardware semantic

A model/provider may expose a narrowly defined hardware semantic that is not a generic continuous writer. Current X9 examples are the known Lenovo global full-speed boolean and the Lenovo Standard/Long-Life battery charge type. Neither implies arbitrary Other Mode feature IDs or freeform values are safe.

### Verified direct write support

A direct-output control is enabled only when the active provider advertises the exact semantic capability and passes its provider/device validation gate **and any required physical acceptance gate**. A failed or unknown path must fall back to safe firmware/OEM ownership rather than guessing addresses, EC commands, vendor APIs or a larger numeric ceiling.

## ThinkPad X9 15 Gen 1

Machine types `21Q6` / `21Q7` are the current verified X9 development path. That identity is only one part of each gate: the relevant provider must also match the reviewed semantic contract. The X9 is a reference implementation, not the product boundary.

Current X9-oriented areas include:

- sensor discovery and CPU/control temperature sources;
- independent Fan 1 / Fan 2 telemetry where Lenovo-native or reviewed providers expose it;
- Lenovo `LENOVO_OTHER_METHOD` native dual-fan telemetry where real `fanX_input` channels pass live-read gates;
- built-in **Auto / Quiet / Balanced / Max cooling** through reviewed Lenovo firmware-policy semantics;
- alpha.41 exact-X9 support for Lenovo Other Mode's known global **full-speed boolean feature `0x04020000`** for Max cooling only when it live-reads safely and every transition verifies readback;
- alpha.42 persistence/reassertion of the selected firmware cooling profile across UI restart, Windows startup settle, AC/DC transitions and resume;
- alpha.43 exact-X9 **Battery care · 80% / Full charge · 100%** through Lenovo Other Mode charge-type attribute `0x03010001` only when the explicit capability row advertises VALID+GET+SET and post-write readback verifies the transition;
- the experimental per-fan Other Mode `fanX_target` writer kept **read-only** after physical testing reproduced repeated speed cycling/re-kick and weaker useful cooling than naturally hot Auto;
- read-only Lenovo `EnergyDrv` fan telemetry while its write contract remains unverified;
- the seven-step ThinkPad EC implementation retained as provider-specific investigation/diagnostic code, not silently re-authorized once native OEM fan telemetry has been confirmed;
- Lenovo keyboard backlight provider/readback and firmware Auto where verified;
- haptic/raw-touchpad discovery and the shared Touchpad gesture editor;
- alpha.43 Audio Safety, which is Windows-generic and does not alter any Lenovo provider boundary.

If two native Lenovo fan channels have been proven during a hardware-service lifetime, a transient native read failure—or a rejected per-fan writer—does not silently re-authorize the EC writer. Provider failure is not permission to guess a lower-level backend.

## Battery charge-protection semantics

The current X9 provider is intentionally **not** a generic numeric charge-limit backend. Upstream Lenovo WMI evidence defines `0x03010001` as the PSU charge type with two semantic states:

```text
Standard  = 0 -> Full charge · 100%
Long Life = 1 -> Battery care · 80%
```

ThinkControl enables the selector only when all product-write gates pass: exact `21Q6/21Q7` identity, active Lenovo Other Mode method, explicit capability row, VALID+GET+SET, a live `0/1` value immediately before writing, and matching post-write readback. If Lenovo omits the capability row, a live state may be shown read-only but ThinkControl does not write it.

The UI does not invent 60/70/85/90/95% options and does not promise an unsupported "x fewer cycles" multiplier. The factual benefit shown for Battery care is 20 percentage points of headroom from full charge and reduced time at high state of charge; firmware health/cycle telemetry remains separate.

Other OEMs or future Lenovo providers can expose their own supported threshold sets through a future semantic provider contract without changing the shared Battery page into a Lenovo-only page.

## Battery history semantics

Battery history is local-only product data. The current UI aggregates **day first, session second** rather than showing one raw endless event list. Recent detailed graphs are retained for a user-selectable **7 / 14 / 30 days**, while compact summaries remain for one year under the existing retention policy.

Automatic compaction is preferred to manual cleanup because summaries still support trends and learned estimates. Destructive reset lives behind **Manage history**, warns that learned charge/discharge estimates and the local health trend are cleared, and does not alter firmware battery health, cycle count or charge-protection state.

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

The UI seeds the service with the current Windows performance preference before enabling a cooling override. Lenovo's reviewed policy command differs by power source, so a Windows power-mode, AC/DC or resume event both updates the Auto restore baseline **and reasserts the active Quiet/Balanced/Max override for the current source**. Auto later restores the latest baseline.

A saved profile is not itself proof of applied state. During startup the Fans selector follows runtime/service state, so it may truthfully show Auto while a saved Quiet preference is still being restored. After capability discovery, the saved profile is actively reapplied. Alpha.42 added one bounded seven-second settle reassert to cover Lenovo login/service policy work that may complete just after the first successful request. This is not continuous polling.

Closing or restarting only the normal-user UI keeps a firmware-policy profile active in the privileged service. Direct/manual output remains a separate safety class and is released to Auto when the UI exits. Normal service shutdown still performs its ownership-aware cleanup.

### X9 Other Mode details

Known per-fan attributes remain `0x04030001` onward. Independently live channels can be native telemetry evidence, but VALID+GET+SET metadata plus sane Fan Test ranges no longer authorizes the X9 per-fan target writer after its physical rejection. Target `0` remains available only for cleanup/reassertion of previously owned stale state.

Alpha.41's `0x04020000` path is intentionally separate. It is treated only as boolean full speed:

- exact `21Q6/21Q7` identity required;
- active `LENOVO_OTHER_METHOD` required;
- current feature value must live-read as `0` or `1`;
- if a capability row is explicitly present, it must advertise the required valid/read/write contract;
- only values `0` and `1` are ever written;
- every transition is verified by reading the same feature back;
- ThinkControl records ownership only when its own call actually changed the state;
- readback/probe failure fails closed rather than falling back to per-fan target RPM, raw EC or unknown IOCTLs.

RPM telemetry is not treated as a proxy for airflow intensity. Physical alpha.40 evidence showed a high-looking RPM report while Performance-policy-only Max cooling still felt materially weaker than naturally hot Lenovo Auto. The full-speed semantic exists to address that exact distinction without lying about direct percentage control.

`EnergyDrv` `QueryFanSpeed 0x83102570` remains read-only evidence. `ChangeFanSpeed 0x8310257C` remains blocked until exact X9 encoding and rollback semantics are recovered. Maintenance/dust/high-speed IOCTL families are not substituted for a reviewed product contract.

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

Alpha.43 adds a **Play / Pause** switch only inside the selected Track-control editor. Existing alpha.42 settings default to enabled for backward compatibility. When enabled, the lane remains **Previous | Play/Pause | Next** and the center target stays **28%** of the Track edge (`0.36..0.64`). Play/Pause is deliberately **hold-to-release**, not timer-auto-fire: the contact must remain down at least **450 ms**, stay within **3 mm** maximum radial movement, and then release. Quick taps are ignored. Release is the final intent confirmation.

When the Track-local switch is disabled, Track itself remains assigned but the center target is not active or visible: no center fill, no separators and no Play/Pause icon. The edge is then a clean Previous/Next lane. The preference survives temporarily moving/removing Track and is reused if Track is assigned again.

The recognizer preserves maximum excursion so moving away and back cannot re-arm an enabled center hold. Once the 3 mm hold slop is exceeded, normal Track direction recognition resumes; Previous/Next still requires the unchanged **9 mm** threshold. A Track swipe can never also become Play/Pause on release.

When reverse close is enabled for a top corner, the reverse start target is the **inner half of the already-visible diagonal lane**, not only the small rounded inner cap. The outer corner guard remains an inward-launch start, the right side remains an exact mirror, and no invisible reverse hit area exists beyond the rendered lane/cap geometry.

Occupied edge actions still swap instead of destructively clearing the previous edge. Sensitivity and inversion remain attached to their physical edges.

The Track OSD keeps familiar semantics: **Playing + pause bars**, **Paused + play triangle**; ambiguous fallback remains `Playback toggled`.

Visualized live input is coalesced for WPF while recognition receives the raw frame stream. At silent Windows startup, configured Raw Input is started from the earliest app Startup hook after cheap identity instead of being queued behind ordinary shell dispatcher work.

## Audio Safety semantics

Audio Safety is available anywhere the normal Windows output endpoint can be accessed; it is not an OEM capability.

- **Normal** — ThinkControl audio/media controls behave normally.
- **Media lock** — ThinkControl Touchpad Volume, Media scrub and Track media commands are blocked. It does not mute the Windows output and does not stop audio deliberately started elsewhere.
- **Silent** — includes Media lock, semantically mutes the current Windows render/multimedia endpoint and blocks ThinkControl output-volume/unmute writes.
- **Microphone** — capture/input state remains independent in all three modes.

Alpha.43 keeps Audio Safety **session-only**. Restart starts in Normal because the new process cannot safely claim ownership of mute state created by the old process. While Silent is active, ThinkControl records the prior mute state of each default output endpoint it actually encounters and restores only those states when leaving Silent/orderly exit. A default-output change reuses the existing app status cadence rather than starting a new polling loop.

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
- alpha.40 Performance-policy-only Max cooling improved behavior but still felt materially less forceful than Auto despite high-looking RPM telemetry;
- alpha.41 Track center remained physically harder to trigger than intended and reverse close was unreliable because its start target was too precise;
- during alpha.41/early-alpha.42 restart testing, a saved Quiet preference could remain visibly selected while physical airflow behaved like a harder Auto/base policy.

Alpha.43 adds these physical battery-care checks:

- initial dropdown matches the actual firmware-reported Standard/Long-Life state rather than a saved preference;
- selecting Battery care returns verified Long-Life/80% state and the real machine stops/holds normal charging around Lenovo's intended 80% boundary;
- selecting Full charge returns verified Standard/100% state and ordinary charging can continue above 80%;
- app/service restart reads actual firmware state again instead of painting a remembered desired value;
- missing/ambiguous capability remains read-only/unavailable and never writes guessed thresholds.

Touchpad, fan persistence and Audio Safety real-device checks remain listed in `docs/ALPHA-TESTING.md`. These physical checks must not be marked complete from screenshots/CI alone.
