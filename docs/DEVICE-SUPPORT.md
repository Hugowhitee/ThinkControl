# Device support

This document describes the support model at **v0.1.0-alpha.42**. ThinkControl is intentionally capability-driven: a laptop model name alone does not grant direct write access or decide which setup/calibration/effect workflows appear.

## Support levels

### Windows-generic

Available without vendor-specific write access where Windows exposes the information/action safely:

- shell/navigation and settings;
- update/install flow;
- Windows power-policy integration;
- display/audio pages backed by Windows-visible capabilities;
- battery and generic telemetry that Windows/providers expose;
- diagnostics/report preview and explicit sharing controls.

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
- the experimental per-fan Other Mode `fanX_target` writer kept **read-only** after physical testing reproduced repeated speed cycling/re-kick and weaker useful cooling than naturally hot Auto;
- read-only Lenovo `EnergyDrv` fan telemetry while its write contract remains unverified;
- the seven-step ThinkPad EC implementation retained as provider-specific investigation/diagnostic code, not silently re-authorized once native OEM fan telemetry has been confirmed;
- Lenovo keyboard backlight provider/readback and firmware Auto where verified;
- haptic/raw-touchpad discovery and the shared Touchpad gesture editor.

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

The UI seeds the service with the current Windows performance preference before enabling a cooling override. If Windows performance preference changes while a cooling profile is active, the service updates the restore baseline but keeps the selected cooling profile active. Auto later restores that latest baseline.

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

Track control remains one continuous visible edge lane: **Previous | Play/Pause | Next**. Standalone Play/Pause is not offered separately; legacy serialized PlayPause bindings sanitize into Track control.

In alpha.42 the center target is widened to **28%** of the Track edge (`0.36..0.64`). A quick press still commits on release, but a stationary center hold also commits after about **240 ms while the finger remains down**. Hold/release/skip share one guarded action state, so a single contact cannot double-toggle or both toggle and skip. The center movement envelope remains **8.75 mm** and the deliberate Previous/Next threshold remains **9 mm**.

When reverse close is enabled for a top corner, the reverse start target is now the **inner half of the already-visible diagonal lane**, not only the small rounded inner cap. The outer corner guard remains an inward-launch start, the right side remains an exact mirror, and no invisible reverse hit area exists beyond the rendered lane/cap geometry.

Occupied edge actions still swap instead of destructively clearing the previous edge. Sensitivity and inversion remain attached to their physical edges.

The Track OSD keeps familiar semantics: **Playing + pause bars**, **Paused + play triangle**; ambiguous fallback remains `Playback toggled`.

Visualized live input is coalesced for WPF while recognition receives the raw frame stream. At silent Windows startup, configured Raw Input is started from the earliest app Startup hook after cheap identity instead of being queued behind ordinary shell dispatcher work.

## Unknown/new hardware

Unknown hardware stays safe by default:

1. collect passive, non-sensitive identity/capability evidence;
2. expose read-only features with credible provider support;
3. keep risky writes unavailable;
4. allow explicit sanitized compatibility reporting;
5. promote write support only after reviewed evidence and explicit provider/profile changes.

ThinkControl never learns a new device by experimentally writing arbitrary EC/IOCTL/BIOS values merely for diagnostics.

## Physical validation

Hosted CI can prove source/build/lifecycle behavior but not physical hardware feel or firmware response.

Confirmed negative X9 evidence remains:

- alpha.38 per-fan target-RPM control repeatedly re-kicked/waved rather than settling;
- nominal target 100% was weaker than naturally hot Lenovo Auto;
- alpha.40 Performance-policy-only Max cooling improved behavior but still felt materially less forceful than Auto despite high-looking RPM telemetry;
- alpha.41 Track center remained physically harder to trigger than intended and reverse close was unreliable because its start target was too precise.

Alpha.42 therefore requires real-X9 checks for:

- repeated quick center presses reliably toggling exactly once;
- a stationary center hold toggling while the finger is still down without a second toggle on release;
- a center hold not later also producing Previous/Next;
- deliberate ~9 mm Track swipes still winning when performed as swipes;
- reverse close succeeding from multiple points in the inner half of either mirrored diagonal lane;
- the outer guard still launching inward and never being misclassified as reverse close;
- all alpha.41 fan/startup safety behavior remaining unchanged.

These physical checks belong in `docs/ALPHA-TESTING.md` and release-readiness notes; screenshots/CI alone must not mark them complete.
