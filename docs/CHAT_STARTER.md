# ThinkControl project start / new-chat starter

This is the canonical bootstrap for a fresh or rolled-over ThinkControl coding chat. Paste the block below at the start of the chat, then add the actual bug, improvement, redesign or research request underneath it.

The starter is deliberately **version-agnostic**. It tells the next agent how to recover the current state from the repository instead of freezing assumptions about a release number, branch name, workflow count or implementation that may change later.

```text
Continue development of my GitHub repo `Hugowhitee/ThinkControl`.

Treat the CURRENT repository state as the source of truth. Do not assume a version number, release baseline, branch, PR, workflow layout, device-support claim or old chat implementation from this prompt. Recover those from GitHub first.

Before changing code, orient yourself properly:

1. Read `AGENTS.md` first. This repository is **`Hugowhitee/ThinkControl`**; do not accidentally work in an old X9-Helper fork, a local copy with unknown drift, a Figma prototype, or another project.
2. For substantial work, retrieve the CURRENT Drive router **once** and load only the smallest relevant canonical dependency set:
   - router: `Digitaal/AI/AI werkinstructies/Overzicht.md` — https://drive.google.com/file/d/193JhDRldBKEa-kdJQzfQ8u92LaFhbGEx/view
   - normal repository implementation: `Softwareontwikkeling/Repositorywerk met Codex.md` — https://drive.google.com/file/d/1SYSD__QP4OAURM5yPAcZ5u41mezKkgnR/view
   - user-facing UI/UX: `Softwareontwikkeling/Interfaces ontwerpen en valideren.md` — https://drive.google.com/file/d/1Gyz_9MwPFYR6-xVY65kow_yw3dCWHOT9/view
   - larger/release-critical execution when routed there: `Kern/Projectuitvoering.md` — https://drive.google.com/file/d/1KBWj_iupOp9yhQrDNbrSoDSZP5TgPCw_/view
   - supporting capability selection: `Digitaal/AI/Bronnen/AI tools en skills.md` — https://drive.google.com/file/d/17GDUb1Glm-DtgPKlMm_bXFfHPboe2Cqj/view
   Do not claim these instructions or skills were used unless you actually retrieved/read the relevant current source.
3. For a substantial ThinkControl UI redesign, visual review or cross-page interaction change, the Drive interface skill is the **primary owner**. Then select supporting capabilities deliberately:
   - use **Product Design** when live and appropriate for a screenshot-first flow/UX audit or design brief;
   - use **Figma** when an editable design source, deliberate visual exploration or component/token round outside WPF code is useful. Before Figma writes, load the live Figma prerequisite skills (notably `figma-use`; for composed page/panel work also `figma-generate-design`; before implementing a Figma design back into code, `figma-design-to-code`);
   - use **pbakaus/Impeccable** as the targeted anti-template/anti-AI critique layer when I explicitly ask for Impeccable or when a substantial visual redesign needs that critique before finalization;
   - use specialist craft resources such as **emilkowalski/skills** only for a concrete motion/perceived-quality need after hierarchy and product flow already work.
   If a requested supporting skill is not live/installed in the current host, say so instead of pretending it ran. Do not substitute generic “clean/minimal” taste language for a requested Impeccable/Figma/Product-Design pass.
4. Figma, Product Design and Impeccable are **supporting design tools**, not product sources of truth. Current runtime behavior, current screenshots, shared WPF primitives, tests and the repository remain authoritative. Use Figma for deliberate design work; do not force every tiny alignment/bug fix through Figma.
5. Read `docs/RELEASE_READINESS.md` as the persistent roadmap/handoff, then the task-relevant parts of `docs/ARCHITECTURE.md`, `docs/PRODUCT.md`, `docs/DEVICE-SUPPORT.md`, `docs/ALPHA-TESTING.md`, `docs/DESIGN.md`, installer/update docs and provider research where relevant.
6. Inspect current `main`, `version.json`, the latest published release/tag, open PRs, active branches, recent merged PRs, relevant issues/crash reports and the current GitHub Actions workflows.
7. If I supplied screenshots, logs, crash reports or reproduction details, compare them with the CURRENT implementation instead of assuming an older fix is still missing or still correct.
8. Determine whether there is already one active branch/PR for the work. Reuse it when the requested change belongs to that scope; do not casually create parallel branches or duplicate PRs.

This file is a ThinkControl entrypoint, not a second universal skill. If work on ThinkControl exposes a reusable prompt/workflow lesson, follow the current Drive instruction system and land that lesson in its canonical universal owner rather than copying the rule into this starter.

Understand the implementation before editing it. Trace the real path end-to-end: UI/control/event -> shared state/service/client -> IPC/provider/backend -> readback/refresh/lifecycle. Search usages and call sites so you know which component actually owns the behavior.

Do not solve problems by stacking another implementation on top of the existing one. Before adding a helper, timer, event handler, polling loop, cache, visual layer, compatibility wrapper, provider, state owner or background worker, check whether one already exists and improve the canonical path instead. Anything new must be genuinely wired into the running app, reachable from the intended flow, have a clear owner/lifecycle/disposal path and not leave dead or parallel code behind.

Be especially careful with old-looking compatibility code. Distinguish current-client dead code from intentionally retained service/updater compatibility before deleting anything. Do not weaken installer/updater compatibility, privilege boundaries, hardware safety gates or firmware fallback just to make the code look cleaner.

ThinkControl is capability-driven and multi-OEM. The currently physically reviewed laptop(s) are reference devices, not the product boundary. Keep generic UI and Core logic vendor-neutral. Put OEM/model-specific behavior behind providers, capabilities, identity gates, readback and documented safety rules. Unknown hardware stays conservative/read-only. Never invent fan RPM, temperatures, sensor values, hardware support or successful writes.

For UI work, preserve the shared design system and existing UX DNA. Start from the actual rendered/runtime state and the user flow, not from a list of controls. Prefer shared XAML/resources/components over runtime visual-tree hacks or one-off overlays. For substantial redesigns, run the routed design/critique workflow above before implementation is considered final. Check ownership of animations, high-rate input, timers and dispatcher work so a visual fix does not introduce lag, duplicate handlers or hidden work after navigation. Inspect generated screenshots yourself at representative minimum/normal/wide sizes and relevant light/dark/error/unavailable states; a green renderer alone is not visual QA.

For performance/cleanup work, measure first. Use workflow/job logs, timings, traces or code-path evidence. Remove duplicated work rather than merely moving it. Do not add a cache unless measured end-to-end wall-clock time improves. Keep safety/coverage equivalent or stronger after optimization.

For installer/update/release work, inspect the CURRENT workflow definitions instead of assuming old workflow names. Preserve the repository's current release discipline: validate the exact candidate/head, exercise real install/service/IPC/update/uninstall behavior, keep the oldest-supported immutable upgrade regression unless the support floor is deliberately changed, and verify the published release/tag/assets/checksums. Never move an existing immutable release tag to a different commit.

Before merging:
- review the complete diff and changed-file list for accidental/unrelated changes;
- confirm new code is actually referenced/reachable and obsolete duplicate paths are intentionally removed or retained with a reason;
- run the repository's CURRENT required CI/package/release gates on the exact final PR head;
- inspect visual artifacts when UI changed;
- preserve honest separation between hosted-CI evidence and physical-hardware validation;
- use the expected-head guard when merging if supported;
- verify post-merge `main` and any release/promotion workflow that should run.

Keep documentation current when architecture, validation or user-visible behavior changes, especially `docs/RELEASE_READINESS.md`. Do not create competing handoff/checklist files for release state.

Do not start redesigning or refactoring unrelated areas just because you notice them. You may report important nearby issues, but keep the implementation coherent with the task I give you next.

After recovering the current state, continue with the request I write below this starter and keep working through implementation, validation and cleanup instead of stopping at a status report.
```

## Why this is intentionally not version-specific

The repository already records the mutable facts a new chat needs: `main`, `version.json`, releases/tags, active PRs/branches, workflows, tests and the persistent release-readiness handoff. A reusable starter should point the agent to those sources rather than duplicate facts that will become stale.
