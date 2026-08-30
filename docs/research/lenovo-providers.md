# Lenovo provider research

Lenovo laptops do not expose one universal hardware-control interface. This reference records provider families that ThinkControl may probe and the boundaries that keep one model's implementation from leaking into another.

A profile selects reasonable provider candidates. The provider itself owns probing, readback, lifecycle and write safety. Installed-file/class presence alone is never sufficient to make a provider writable.

## Windows-first capabilities

Use Windows APIs when the OS already owns the feature: power preference, display refresh/brightness, battery status, system audio and machine identity. These paths are vendor-neutral and should remain usable independently of Lenovo hardware providers.

## Keyboard backlight

Known Lenovo candidates include:

- `IBMPmDrv` on many ThinkPads;
- `EnergyDrv` on multiple Lenovo families;
- the installed Lenovo ThinkKeyboard/Vantage component as a bounded fallback where its semantic contract validates.

A candidate must return a recognized state before writes are enabled. A requested write succeeds only under the provider's readback/response contract. ThinkControl user-session effects are separate from hardware backlight capability; Auto therefore does not require unrelated effect capabilities when normal backlight writes are valid.

## Fan telemetry versus fan control

Read-only RPM has a lower risk boundary than writable fan ownership. Telemetry candidates may include real hardware-monitor providers, Lenovo WMI/CIM data where exposed, Windows `CIM_Tachometer`, Lenovo `EnergyDrv` query contracts where their semantics are known, and exact-model verified read-only EC tachometer fallbacks.

Writable backends are modeled separately, for example:

- OEM-native thermal policy;
- OEM semantic target-RPM control;
- discrete EC fan states;
- telemetry-only provider.

A generic `FanControl` label is not enough to infer EC, PWM or target-RPM semantics. A native OEM telemetry path does not become writable merely because a nearby driver IOCTL exists: the write command format, rollback and readback contract must be recovered and validated independently.

## EnergyDrv fan contracts are not interchangeable

Public Lenovo reverse-engineering exposes several **different** `\\.\EnergyDrv` fan-related contracts. Similar names or the fact that all of them reach the same driver are not evidence that their payloads can be mixed:

| IOCTL | Publicly observed semantic | Known payload evidence | ThinkControl policy |
| --- | --- | --- | --- |
| `0x831020C0` | legacy clean-dust / temporary high-speed action | three DWORDs `[6, 1, mode]`, with public tools using `0` normal and `1` fast | never use as a curve/target backend; it is a pulsing/time-limited family |
| `0x831020C4` | companion legacy fan-state query | one DWORD input `14`, one DWORD result | read-only research only |
| `0x8310213C` | family-specific legacy ITS/Geek full-speed overlay in public ThinkBook tooling | public code uses `0x001F100B` enable and `0x000F100B` disable | evidence for that family only; never generalize to X9 |
| `0x83102570` | `QueryFanSpeed` | one DWORD zero-based fan index, one DWORD speed result; dual-fan code queries indices `0` and `1` | allowed read-only X9 candidate |
| `0x8310257C` | `ChangeFanSpeed` | one DWORD `dwFanCtrlCmd`, one DWORD action-status result | blocked until the exact X9 command encoding and Auto/rollback semantics are recovered |

This separation matters because the legacy dust implementation explicitly re-issues its fast request as the driver times out/reverts; that behavior matches a temporary maintenance action, not stable target-RPM ownership. The X9 product goal is smooth Lenovo-like control, so a maintenance/full-speed overlay cannot be renamed into a percentage curve.

A decompilation of Lenovo Energy Management's `LenovoEmExpandedAPI` gives stronger evidence for the `0x83102570`/`0x8310257C` pair: `CAtmDriverLibrary.QueryFanSpeed` sends a zero-based DWORD fan index to `0x83102570`, while `CAtmDriverLibrary.ChangeFanxSpeed` sends exactly one DWORD `dwFanCtrlCmd` to `0x8310257C` and receives one DWORD action status. The dual-fan getter explicitly queries indices `0` and `1`. That validates the read-side shape now used by ThinkControl, but the public decompilation does **not** expose the caller-side values placed into `dwFanCtrlCmd`; it therefore does not authorize a writer on the X9.

`tools/research/Analyze-LenovoOemFanBinaries.ps1` is the offline/static next step after `Capture-LenovoAuto.ps1 -BundleRelevantOemBinaries`. It never opens a driver or executes an OEM binary. It scans only the supplied ZIP/directory for the known IOCTL DWORDs and fan/LITS-related strings, records bounded file offsets, nearby printable strings and a small hex window around each hit, and hashes the files. The purpose is to locate the exact installed X9 call sites/data around `0x8310257C` and correlate them with Lenovo's thermal-policy binaries without trying command values on the hardware.

## ThinkPad X9-15 Gen 1

Machine types `21Q6` and `21Q7` are the current physically reviewed low-level reference. Physical testing has shown that the classic ThinkPad EC path can expose both fan tachometers, but its seven manual states do not reproduce Lenovo Auto's useful high-cooling range or acoustic behavior. That path is therefore investigation/fallback evidence rather than the desired product fan-control boundary.

The preferred X9 hierarchy is now:

1. Lenovo `LENOVO_OTHER_METHOD` target-RPM control, but only if exact-device capability data exposes at least two VALID+GET+SET channels with sane constraints and live reads;
2. Lenovo-native fan telemetry through Other Mode or the known read-only `EnergyDrv` `QueryFanSpeed` contract;
3. read-only EC thermal/tachometer evidence only when richer native telemetry is unavailable;
4. writable EC steps only on machines where no native Lenovo fan surface has been established and the exact-model EC contract remains explicitly allowed.

On the physically tested X9 candidate, Other Mode did not become writable. PR #71 therefore also probes `EnergyDrv` `QueryFanSpeed` (`0x83102570`) for fan indices 0/1 read-only. When two native Lenovo fan channels are present, ThinkControl prioritizes those RPMs and deliberately stops advertising the known-inferior EC writer while the exact OEM target-RPM command is recovered. Public Lenovo reverse-engineering proves a separate `ChangeFanSpeed` IOCTL (`0x8310257C`) exists, but does not define the X9 `dwFanCtrlCmd` encoding; ThinkControl must not brute-force it.

That native two-fan proof is a **safety boundary**, not just a momentary UI result. Within a service lifetime, once the exact X9 has successfully exposed two native Lenovo fan channels, ThinkControl latches that evidence. A later transient EnergyDrv/Other Mode read failure may make live RPM temporarily unavailable, but it must not silently re-authorize the known-inferior EC writer. Provider refresh preserves the latch. Likewise, merely reading an EC manual-looking value does not make ThinkControl the owner of another utility's EC state; automatic cleanup is restricted to the provider this controller actually took ownership of.

Exact registers, transport observations and physical test evidence are maintained in [x9-15-gen1.md](x9-15-gen1.md).

## Lenovo Intelligent Thermal Solution

X9 research established a Lenovo policy path through `LITSSvc`. ThinkControl exposes only semantic **Efficiency / Balanced / Performance** behavior in product UI; the service selects the previously reviewed source-specific command only after exact provider/identity checks.

Recovered IL from the exact X9 ThinkSmartSense add-in contains a wider command family than the three production modes:

| Command | Recovered name | Current status |
| ---: | --- | --- |
| `500` / `501` | enable / disable AC Cool | research-only |
| `502` / `503` / `504` | AC Eco / Balanced / Performance | reviewed production policy mapping |
| `505` / `506` | enable / disable DC Cool | research-only |
| `507` / `508` / `509` | DC Eco / Balanced / Performance | reviewed production policy mapping |
| `510` / `511` | enable / disable Improved Cooling Efficiency | research-only |
| `31` / `32` | enable / disable Balanced Mode LCM | research-only |
| `33` / `34` | enable / disable Performance Mode LCM | research-only |

The same add-in implements `ChangeITSsetting(UInt32)` by writing one UInt32 to `com.lenovo.its.pipe.setting` and reading one Int32 response. The names above are valuable evidence for understanding how Lenovo Auto/high-cooling policy may differ from the basic Eco/Balanced/Performance mapping, especially `Cool` and `Improved Cooling Efficiency`, but **their presence in Lenovo's add-in is not permission to send them from ThinkControl**. Their capability conditions, enable/disable lifecycle, interaction with AC/DC and actual fan behavior still need exact-X9 correlation. They remain static/read-only research clues until that evidence exists.

This is thermal-policy coordination, not a direct fan PWM/RPM interface. Lenovo's returned integer semantics are not guessed; the implementation uses only the protocol boundary supported by observed evidence. A source regression test explicitly keeps the unvalidated 500/501/505/506/510/511 commands out of the production `LenovoThermalPolicyService` mapping.

## GameZone and family providers

Some Legion/LOQ-family systems expose `LENOVO_GAMEZONE_DATA` or related OEM WMI contracts. ThinkControl treats those as provider candidates, not family-wide guarantees: support queries/current state/readback must validate before any semantic write is exposed.

Typical candidate hierarchy:

| Family | Conservative candidates |
| --- | --- |
| ThinkPad | Windows APIs, `IBMPmDrv`, Lenovo telemetry, exact-model providers when verified |
| ThinkBook / Yoga / IdeaPad | Windows APIs, `EnergyDrv`/compatible Lenovo PM paths, read-only Lenovo telemetry |
| Legion / LOQ | Windows APIs, Lenovo telemetry, GameZone-style providers only where validated |

Family membership never authorizes X9 EC, X9 LITS or X9 EnergyDrv writes.

## OEM software launching

ThinkControl may open an installed Lenovo application through registered protocols/AUMIDs when useful. The presence of Lenovo Vantage is not a prerequisite for Windows-generic ThinkControl features and does not by itself prove any low-level provider capability.

## Validation principle

Profiles identify candidates; providers prove capabilities. Unknown values fail closed. Risky writes require stronger physical evidence than read-only telemetry, and conflicting reports prevent automatic promotion.