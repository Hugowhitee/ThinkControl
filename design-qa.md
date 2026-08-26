# Design QA — focused prerequisite prompts and layout switch

## Comparison targets

- Source visual truth: `C:\Users\hugoj\AppData\Local\Temp\codex-clipboard-0e59c21a-acb7-44cb-97b9-5b8eaf4bc4aa.png` (579×407 px), used as an interaction-density reference for the PawnIO prerequisite prompt.
- Implementation: `C:\Users\hugoj\Documents\ChatGPT\ThinkControl\artifacts\visual-qa-alpha19-final\hardware-setup-pawnio-repair.png` (560×360 px at 560×360 WPF device-independent pixels, 96 DPI).
- Combined evidence: `C:\Users\hugoj\Documents\ChatGPT\ThinkControl\artifacts\visual-qa-alpha19-final\comparison-pawnio-popup.png`.
- Source visual truth: `C:\Users\hugoj\AppData\Local\Temp\codex-clipboard-f9b79216-8567-4b0b-9d8c-88851703a2b9.png` (343×284 px), used as an interaction reference for the compact/normal layout switch.
- Implementation: `C:\Users\hugoj\Documents\ChatGPT\ThinkControl\artifacts\visual-qa-alpha19-final\compact-dark.png` (390×480 px at 390×480 WPF device-independent pixels, 96 DPI).
- Combined evidence: `C:\Users\hugoj\Documents\ChatGPT\ThinkControl\artifacts\visual-qa-alpha19-final\comparison-layout-switch.png`.
- States additionally inspected: PawnIO repair/ready/persistent error at 560×360 and 500×330 in dark and light; service, sensors, fan and keyboard focused prompts; Inbox; diagnostics ready/discovering.

The references are inspiration rather than pixel-clone targets. Crops and viewport proportions therefore remain product-owned; the comparison focuses on interaction structure, hierarchy, density and control affordance.

## Findings

No actionable P0/P1/P2 differences remain.

- Fonts and typography: Segoe UI Variable preserves ThinkControl's Windows hierarchy. Prompt title, body, metadata and button text remain readable at normal and minimum sizes with no truncation.
- Spacing and layout rhythm: the focused prompt uses one message, one relevant primary action and a restrained secondary cancel action. Ready and persistent-error states remove irrelevant controls. The compact/full switch is grouped with the existing shell controls and retains a 32×32 hit target.
- Colors and visual tokens: all new surfaces use existing `Tc.*` theme resources. Warning, success and error states remain distinct in dark and light without introducing reference-brand colors.
- Image and icon fidelity: no raster imagery was needed. New icons come from the same curated Material Symbols geometry library used by ThinkControl; supplied screenshots were not embedded into the product.
- Copy and content: PawnIO is named plainly and tied specifically to fan control and sensor data. Service, sensors, fan and keyboard prompts each describe only their own operation and safety boundary.
- Interaction states: the first stable actionable prerequisite opens once per issue/release; Inbox cards open the matching focused prompt; success shows “You're all set” and auto-closes; persistent failure remains until closed; installer opt-out and explicit GitHub submission behavior are unchanged.

## Comparison history

1. Earlier P2: the generic Hardware Setup surface combined unrelated repair paths and permanently showed explanatory/footer controls. Fixed by routing each Inbox item to a focused prerequisite prompt.
2. Earlier P2: diagnostics showed a “Ready” step while discovery was incomplete. Fixed with an honest `Waiting`/`Ready to share` state and capability-specific discovery summary.
3. Earlier P2: the compact/full switch used a one-off drawn glyph. Fixed by using the shared Material Symbols layout icon and existing ThinkControl shell button behavior.
4. Post-fix evidence: the combined comparison images and 76-snapshot WPF matrix show no clipping, transparent roots, mismatched state actions or unresolved P0/P1/P2 visual issues.

## Implementation checklist

- [x] Focused service/PawnIO/sensors/fan/keyboard prompts.
- [x] One-time proactive prerequisite opening.
- [x] Auto-closing success and persistent-error states.
- [x] Inbox routing and naming.
- [x] Shared compact/full layout icon.
- [x] Dark/light and minimum/normal WPF snapshot inspection.
- [x] Diagnostics readiness inspection.

## Follow-up polish

No blocking polish remains for alpha.19.

final result: passed
