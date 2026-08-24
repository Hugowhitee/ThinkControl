# ThinkControl agent workflow

These rules apply to automated coding assistants and human contributors working in this repository.

## Branch and PR hygiene

1. Inspect `main`, open pull requests and existing feature branches before creating a new branch.
2. Reuse the active feature branch/PR when the requested work belongs to the same release or scope.
3. Never leave recovery/checkpoint branches as authoritative work. Before finishing, compare every non-main branch with `main` and the active PR.
4. If an old branch contains unique useful work, port only the missing changes into the active PR and validate them there. Do not merge stale branches wholesale.
5. After a PR is merged, its branch is disposable. The repository cleanup workflow deletes same-repository merged branches automatically.
6. A release task is not complete until the validated PR is merged to `main`, release assets are published and the published installer/assets are verified.

## Stabilization before expansion

1. Treat the current `main` as the implementation base. Older releases may be used as regression/performance references, never as automatic rollback targets.
2. Audit existing behavior before adding another helper, enhancer, timer, provider or visual layer. Prefer simplifying an existing path over stacking a second path on top of it.
3. Do not add features while a related regression is unresolved. A stabilization release prioritizes correctness, responsiveness and consistency over feature count.
4. Keep startup, navigation and compact-mode rendering independent of slow hardware discovery. Render cached/placeholder state first and refresh hardware asynchronously.

## Multi-OEM architecture

1. ThinkControl is a general laptop-control product. The ThinkPad X9-15 Gen 1 is the current verified low-level reference device, not the architectural product boundary.
2. Keep the UI capability-first. Fans, Sensors, Battery, Audio, Keyboard, Display and Touchpad must not be duplicated into vendor-specific pages.
3. Device support follows `Windows generic → OEM generic → family → exact model`. See `devices/README.md`.
4. Device profiles are data that select/provider-prioritize probes. Hardware implementations, readback, lifecycle and write allowlists belong to provider code.
5. Never put an X9 register, Lenovo IOCTL or other model-specific contract into generic/family code simply to make another model work.
6. New OEM support should first reuse Windows-safe capabilities, then an OEM provider, then family specialization, with model-specific writes last.
7. Vendor-specific actions in shared UI must be capability/identity gated. Do not make a generic page assume Lenovo Vantage, ASUS Armoury Crate, Dell software or another OEM application exists.
8. Keep provider boundaries replaceable. Future OEM/provider modules must be able to evolve without rewriting `ThinkControl.Core` or the main UI.

## Hardware reliability and safety

1. Model service reachability, driver installation, driver/device accessibility, provider readiness, telemetry availability and write capability as separate states. Never call the Windows service offline merely because a sensor/provider is unavailable.
2. Real telemetry only: never synthesize fan RPM, temperature, power or hardware state to make the UI look populated.
3. Unknown hardware stays read-only. Direct EC/IOCTL writes require an explicitly verified device/provider contract and readback where available.
4. Manual fan ownership must have a safe firmware/OEM fallback on failure, shutdown and disposal paths.
5. Provider probing must be bounded and recoverable. Failed PawnIO/LHM/OEM probes back off; explicit Repair/Retry may bypass that backoff once.
6. Per-frame or high-frequency input must never fan out into unbounded OS, driver or async calls. Gestures/media/telemetry use one state owner, coalescing and bounded write cadence.

## UI and performance consistency

1. Prefer shared XAML styles/resources/reusable controls over runtime visual-tree surgery. Runtime mutation is a last resort for genuinely dynamic content, not the default way to polish static pages.
2. All Advanced pages share the same left content rail, responsive width rules, spacing system, theme surfaces and selected/hover/disabled states.
3. A page must work at the documented minimum, normal and wide widths without clipping or horizontal escape. Reopening a scrollable page starts at the top unless preserving position is explicitly part of the UX.
4. Animations stay subtle and shared. Do not add per-page movement that makes content shift horizontally or delays interaction.
5. Do not attach duplicate event handlers, timers, polling loops or refresh workers. Any background loop needs an owner, bounded cadence and disposal path.

## Validation

Normal product changes must pass the Windows build/tests, WPF snapshot/visual QA and packaging/install/service lifecycle checks used by this repository. Hardware-control changes must remain capability-gated and preserve safe firmware fallback behavior.

For UI-affecting work, generated screenshots must be inspected visually, not merely generated successfully. Check dark/light where applicable and representative minimum/normal/wide layouts. For hardware work, validate both provider-ready and provider-unavailable states.

For updater/installer work, validation includes update availability, cancellation, verified download, one elevation handoff, existing-install upgrade, service restart, relaunch and stale-update cleanup.

## README and public claims

The top-level README is written for people evaluating or downloading ThinkControl. Do not write it as a private development log or in terms of one tester's conversation/context. Claims must reflect behavior actually implemented and validated; clearly distinguish physically verified hardware behavior from capability-gated or experimental support.

The README should describe ThinkControl as a general Windows laptop-control product while clearly identifying the currently verified low-level reference devices. Do not market a planned OEM/model as supported before its provider contract is implemented and validated.

## Source of truth

`main` plus the single active release PR are the source of truth. Do not assume an old branch contains newer functionality just because Git history reports it as ahead after a squash merge; compare actual file contents/capabilities first.
