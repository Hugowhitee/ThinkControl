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
- diagnostics/report preview and explicit sharing controls.

Audio Safety is a Windows-user-session policy and does **not** grant any low-level device write capability. Media lock only suppresses ThinkControl Touchpad media/output actions. Silent additionally mutes the current Windows render endpoint and blocks ThinkControl output writes while leaving microphone input independent.

### Provider-backed read-only

ThinkControl can expose telemetry from a reviewed provider without implying that writes are safe. Fan RPM, temperature and OEM feature reads may therefore be available even when direct fan output remains blocked.

### Verified semantic policy support

An OEM may expose reviewed semantic thermal policy such as Quiet/Balanced/Performance without exposing safe direct RPM/PWM control. ThinkControl may use that policy for named built-ins while leaving manual percentages, custom curves and raw hardware states unavailable.

### Verified narrow hardware semantic

A model/provider may expose a narrowly defined hardware semantic that is not a generic continuous writer. Alpha.41's X9 full-speed boolean is one example: the known Lenovo feature can be used only behind exact identity, live read, capability/readback and bounded-value gates. It does not imply that arbitrary Other Mode feature IDs or per-fan targets are safe.

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
- the experimental per-fan Other Mode `fanX_target` writer kept **read-only** after physical testing reproduced repeated speed cycling/re-kick and weaker useful cooling than naturally hot Auto;
- read-only Lenovo `EnergyDrv` fan telemetry while its write contract remains unverified;
- the seven-step ThinkPad EC implementation retained as provider-specific investigation/diagnostic code, not silently re-authorized once native OEM fan telemetry has been confirmed;
- Lenovo keyboard backlight provider/readback and firmware Auto where verified;
- haptic/raw-touchpad discovery and the shared Touchpad gesture editor;
- alpha.43 Audio Safety, which is Windows-generic and does not alter any Lenovo provider boundary.

If two native Lenovo fan channels have been proven during a hardware-service lifetime, a transient native read failure—or a rejected per-fan writer—does not silently re-authorize the EC writer. Provider failure is not permission to guess a lower-level backend.

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

The current UI uses semantic `SetCoolingProfile` for firmware-backed built-ins and only exposes `SetCoolingCurve`, `SetFanPercent` or raw EC behavior when the matching direct provider capability exists.

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

Audio Safety does not imply any cooling, EC, keyboard or OEM support. Future composed presets would need separate explicit ownership and restore semantics for every subsystem they change.

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
- pre-freeze alpha.42 testing feedback made clear that automatic/quick center activation was too risky for a global media command;
- during alpha.41/early-alpha.42 restart testing, a saved Quiet preference could remain visibly selected while physical airflow behaved like a harder Auto/base policy.

Alpha.43 therefore carries forward the alpha.42 real-X9 checks and adds Audio Safety / optional-center checks:

- with Track Play/Pause enabled, quick center tap does nothing;
- deliberate roughly half-second center hold toggles exactly once **on release**;
- nothing auto-fires while the finger stays down after the hold threshold;
- >3 mm movement permanently disarms Play/Pause for that contact;
- ~9 mm Track swipes still produce Previous/Next without overlap;
- disable Track Play/Pause and confirm the center visual disappears and center contacts never toggle media while Previous/Next still work;
- re-enable it and confirm the center visual/recognizer return together;
- reverse close works from multiple points in the inner half of either mirrored diagonal lane;
- outer guard remains inward launch;
- Quiet/Balanced/Max persistence and reassertion remain truthful across UI restart, reboot, AC/DC and resume as described in alpha.42 testing;
- on real Windows audio, Media lock blocks ThinkControl Touchpad media/output actions without muting deliberate app audio;
- Silent mutes the current default output, blocks ThinkControl output changes, follows a default-output change, leaves the microphone independent and restores only prior endpoint mute states that ThinkControl owned/recorded.

These physical checks belong in `docs/ALPHA-TESTING.md` and release-readiness notes; screenshots/CI alone must not mark them complete.
