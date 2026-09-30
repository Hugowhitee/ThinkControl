# ThinkControl UI design handoff

This file is the durable handoff for future ThinkControl UI/design chats. Read it together with `docs/DESIGN.md` and the current `main`/active release PR before changing visual structure.

## Product direction

ThinkControl should feel like a precise Windows hardware instrument, not a generic SaaS dashboard. The visual language is restrained, compact and technical with Dieter Rams/Braun-style clarity: strong alignment, thin separators, few surfaces, clear hierarchy, deliberate states and little permanent helper copy.

The ThinkControl logo/brand, existing dark/light tokens, shared Advanced page header, current navigation grammar and compact Windows-native interaction style remain product identity. Figma is a design workspace, not a reason to replace those foundations with a new design language.

## Preserve unless evidence says otherwise

Do not redesign these merely to make a Figma file look more complete:

- the current Battery Preservation gauge/threshold visual and its information hierarchy;
- the current Battery history/chart direction;
- the shared `AdvancedPageHeader` geometry and action rail;
- the Compact/Advanced product split;
- the current ThinkControl logo and restrained accent system;
- semantic disabled/selected/focus states already defined in shared resources;
- the alpha.55+ Modes ownership model: sparse settings, one active mode, manual-wins-per-facet behavior and context triggers.

Motion is allowed only when it improves state comprehension. A future Battery motion concept may animate real charging/limit state, but it must not add decorative movement or replace accurate static information.

## Priority redesign surfaces

### Fans

Highest-priority Figma surface. Improve composition and clarity around:

- current fan profile and ownership;
- real RPM/telemetry;
- Lenovo firmware-policy profiles versus direct custom curves;
- provider unavailable/recovery states;
- temporary/manual test controls where actually supported;
- status/error presentation that helps recovery without implementation dumps.

Do not imply arbitrary RPM/percentage control when the active provider does not safely expose it. On the verified X9, Auto / Quiet / Balanced / Max may use Lenovo firmware policy while custom curves remain unavailable until a physically accepted direct writer exists.

### Modes

Keep the context-engine model, but continue improving interaction.

Desired interaction:

- clicking the mode row selects it directly;
- Edit remains a separate secondary action;
- active/automatic/modified/applying state is obvious without a separate Activate button;
- starter presets are useful starting points, not immutable built-ins;
- editor remains Settings + Turn on automatically;
- automation should feel like laptop context, not a generic IFTTT builder.

Starter direction:

- **Focus** — Efficiency, Quiet, 60 Hz, keyboard Low, ThinkControl Touchpad/edge gestures off;
- **Battery saver** — Efficiency, Quiet, 60 Hz, keyboard Off, automatic below 25%;
- **Performance** — Performance power preference, Balanced cooling, max refresh, keyboard Auto.

### Home quick controls

Improve only weak alignment/hierarchy problems. Preserve useful information density.

Pay special attention to:

- segmented-control baselines across sibling cards;
- equal title/control vertical rhythm;
- Mode as a quick selector, not another settings editor;
- a system telemetry strip only if it stays visually secondary;
- no duplicate subsystem explanations.

## Figma workflow

Use installed Figma/Product Design tooling rather than rebuilding screens blindly:

- Product Design `audit` for screenshot-grounded critique;
- Product Design `get-context` before broad redesign exploration;
- Figma `figma-use` for direct file edits;
- Figma `figma-generate-library` when establishing/reconciling reusable tokens/components;
- Figma `figma-design-to-code` before implementing a selected Figma node back into production;
- Figma `figma-implement-motion` only when a selected design actually contains meaningful motion.

There is no assumption that an external skill named **Impeccable** is installed. If a later session wants that exact external tool, discover/verify it first rather than claiming it is available.

For this WPF application, do not use webpage capture as the source of truth. Start from actual WPF screenshots plus existing code/tokens, build/edit the screen in Figma, then translate selected changes back to shared XAML/resources.

## Required design loop

1. Read `docs/DESIGN.md`, this handoff and the current relevant WPF source.
2. Capture/inspect the actual runtime screenshot first.
3. State the problem being solved and preserve already-good mechanisms.
4. Explore only the affected screen/components in Figma.
5. Compare dark/light and minimum/normal/wide where relevant.
6. Translate selected mechanisms back to real WPF components/styles, not runtime visual-tree patches.
7. Run WPF visual QA and inspect the rendered implementation against the selected Figma target.
8. Hardware/status surfaces must also be tested in provider-ready and provider-unavailable states.

## Anti-patterns

Avoid:

- gratuitous rounded cards;
- permanently visible paragraphs explaining implementation;
- “AI dashboard” metric tiles with decorative gradients;
- unrelated redesign of good Battery visuals during a Fans task;
- replacing precise controls with abstract illustrations;
- separate one-off styles that drift from shared tokens;
- Figma-only polish that cannot map cleanly to WPF;
- invented telemetry or enabled-looking hardware controls without capability support.

## Current hardware boundary

The design file must not blur hardware safety:

- verified X9 Lenovo firmware-policy profiles are semantic cooling controls;
- the Other Mode per-fan target writer remains read-only after failed physical smoothness/range validation;
- the classic X9 seven-step EC writer remains non-production after physical cycling/range failure;
- the narrow Lenovo full-speed boolean remains separately capability/readback gated;
- custom fan curves/manual percentages require a physically accepted direct-output provider.

When new physical evidence changes those boundaries, update hardware/product docs first, then the Figma state model.
