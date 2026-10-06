# ThinkControl UI design handoff

This file is the durable handoff for future ThinkControl UI/design chats. Read it together with `docs/DESIGN.md` and the current `main`/active release PR before changing visual structure.

## Product direction

Ordinary status is a short sentence stating availability or a failed action and its next step. Never bind raw provider/error dumps into page summaries or ellipsis footers. System's explicit technical disclosure keeps full diagnostic evidence as separate lines. Auto/Max-only providers expose only those choices, hide unavailable advanced controls, and keep Home's Max control usable directly from Auto. The sidebar fades into its surface at the bottom only when content remains below; it does not cover the scrollbar or block input.

Use a colon for a label and value, parentheses for a short qualifier, and normal sentences for an outcome and next action. Avoid middle-dot chains in ordinary controls, statuses, tooltips and notifications. History summaries name the quantity (average power, energy, drain rate); longer captions wrap beneath their heading rather than competing on one row. Raw diagnostic encoding remains unchanged behind the technical disclosure.

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
- the alpha.55+ Modes ownership model: sparse settings, one active mode, manual-wins-per-facet behavior and separate automation rules.

Motion is allowed only when it improves state comprehension. A future Battery motion concept may animate real charging/limit state, but it must not add decorative movement or replace accurate static information.

October 5 owner feedback supersedes the earlier Photopea percentage-inside-gauge experiment: Battery now follows Home with a separate charge value beside the small gauge, and status and ETA beneath it. This composition is implemented and inspected in installed dev.2026100507. Preserve its compact hierarchy and wrapping for longer status text; the separate Battery Preservation threshold gauge is unchanged.

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

- one mode dropdown selects/applies the mode; vertical rows summarize saved modes;
- Edit remains a separate secondary action on the row;
- active/automatic/modified/applying state is obvious without a separate Activate button;
- Focus, Battery saver and Performance are seeded once as normal editable saved modes, not shown as three permanent starter CTA buttons;
- New mode opens a blank editor directly;
- Modes contains saved settings; Automation contains linked conditions and its own editor. Both pages use shared components and one engine;
- automation should feel like laptop context, not a generic IFTTT builder.
- the mode editor **saves without activating** away from its trigger context; users can select a mode manually when needed;
- automation evaluates a stable condition for at least 5 seconds, supports Any/All conditions, highest priority then visible list order, and restores the prior manual mode or ordinary settings when the trigger ends;
- Wi-Fi suggestions show the currently connected and a short list of saved networks with custom SSID entry, never unrelated scans or credentials;

Starter direction:

- **Focus** — Efficiency, Quiet, 60 Hz, keyboard Low, ThinkControl Touchpad/edge gestures off;
- **Battery saver** — Efficiency, Quiet, 60 Hz, keyboard Off, automatic below 25%;
- **Performance** — Performance power preference, Balanced cooling, max refresh, keyboard Auto.

### Battery Preservation

Preserve the compact gauge itself, but make the decision surface explicit:

- present the selectable value as the **charge limit** first;
- current presets are 80% Strong protection, 85% Recommended, 90% More runtime and 95% Light protection;
- explain the lower threshold as “charging starts again below X%” instead of making the user decode a range;
- keep existing non-preset Lenovo pairs visible as Custom and do not overwrite them automatically;
- comparative wear copy should use a stable 0%→selected-limit percentage against the 0%→100% wear reference; explain in the tooltip that real top-ups starting above 0% are lower and the upper end contributes disproportionately.

### Header actions and Windows links

Title-level actions stay direct and contextual. Do not hide a common destination behind a generic **Windows settings** dropdown. Display may link directly to Windows display settings; Battery may link directly to Power & battery. Page Defaults remains a quiet secondary direct action rather than becoming another menu/list item. External links, Defaults and contextual actions share one fixed header rail, style and right alignment. Feature switches sit beside the related setting rather than in the header. Settings uses flat general rows and a collapsed advanced diagnostics/recovery section.

### Home quick controls

Improve only weak alignment/hierarchy problems. Preserve useful information density.

Pay special attention to:

- segmented-control baselines across sibling cards;
- equal title/control vertical rhythm;
- Mode as a quick selector, not another settings editor;
- a system telemetry strip only if it stays visually secondary;
- no duplicate subsystem explanations.

## Current editable Fans design source

- Figma file: https://www.figma.com/design/Dcl5mMTiWUYwMcwiB0oVdy
- dark Fans target: node `3:2`
- light Fans target: node `3:77`
- fan state studies: node `3:152`
- production typography remains `Segoe UI Variable Text, Segoe UI`. The Figma host did not expose Segoe, so the editable mockup uses Inter only as a representational fallback; do not change production typography to match the mockup.
- runtime behavior, WPF tokens and rendered screenshots remain authoritative; this Figma file owns the selected Fans composition, not hardware semantics.

## Figma workflow

Use installed Figma/Product Design tooling rather than rebuilding screens blindly:

- Product Design `audit` for screenshot-grounded critique;
- Product Design `get-context` before broad redesign exploration;
- Figma `figma-use` for direct file edits;
- Figma `figma-generate-library` when establishing/reconciling reusable tokens/components;
- Figma `figma-design-to-code` before implementing a selected Figma node back into production;
- Figma `figma-implement-motion` only when a selected design actually contains meaningful motion.

Impeccable is an external specialist resource rather than a guaranteed native host skill. When an Impeccable pass is requested, use a native installed skill only if one is actually exposed; otherwise retrieve the current upstream `pbakaus/impeccable` skill and only the task-relevant references. ThinkControl’s normal desktop control surfaces are **Operate** interfaces, so favor its craft-floor, critique, layout, clarify and polish guidance. Never claim the pass ran without loading/retrieving the source.

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
- generic dropdowns that hide one or two obvious Windows destinations;
- turning a simple mode editor into a settings-table/admin builder with repeated separator rows and Remove labels;
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
