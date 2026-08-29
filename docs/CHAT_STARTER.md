# ThinkControl new-chat starter

Paste the block below at the start of a new coding chat, then add the actual bug, improvement or research request underneath it.

The starter is deliberately **version-agnostic**. It tells the next agent how to recover the current state from the repository instead of freezing assumptions about a release number, branch name, workflow count or implementation that may change later.

```text
Continue development of my GitHub repo `Hugowhitee/ThinkControl`.

Treat the CURRENT repository state as the source of truth. Do not assume a version number, release baseline, branch, PR, workflow layout, device-support claim or old chat implementation from this prompt. Recover those from GitHub first.

Before changing code, orient yourself properly:

1. Read `AGENTS.md` first.
2. Read `docs/RELEASE_READINESS.md` as the persistent roadmap/handoff, then the task-relevant parts of `docs/ARCHITECTURE.md`, `docs/PRODUCT.md`, `docs/DEVICE-SUPPORT.md`, `docs/ALPHA-TESTING.md`, `docs/DESIGN.md`, installer/update docs and provider research where relevant.
3. Inspect current `main`, `version.json`, the latest published release/tag, open PRs, active branches, recent merged PRs, relevant issues/crash reports and the current GitHub Actions workflows.
4. If I supplied screenshots, logs, crash reports or reproduction details, compare them with the CURRENT implementation instead of assuming an older fix is still missing or still correct.
5. Determine whether there is already one active branch/PR for the work. Reuse it when the requested change belongs to that scope; do not casually create parallel branches or duplicate PRs.

Understand the implementation before editing it. Trace the real path end-to-end: UI/control/event -> shared state/service/client -> IPC/provider/backend -> readback/refresh/lifecycle. Search usages and call sites so you know which component actually owns the behavior.

Do not solve problems by stacking another implementation on top of the existing one. Before adding a helper, timer, event handler, polling loop, cache, visual layer, compatibility wrapper, provider, state owner or background worker, check whether one already exists and improve the canonical path instead. Anything new must be genuinely wired into the running app, reachable from the intended flow, have a clear owner/lifecycle/disposal path and not leave dead or parallel code behind.

Be especially careful with old-looking compatibility code. Distinguish current-client dead code from intentionally retained service/updater compatibility before deleting anything. Do not weaken installer/updater compatibility, privilege boundaries, hardware safety gates or firmware fallback just to make the code look cleaner.

ThinkControl is capability-driven and multi-OEM. The currently physically reviewed laptop(s) are reference devices, not the product boundary. Keep generic UI and Core logic vendor-neutral. Put OEM/model-specific behavior behind providers, capabilities, identity gates, readback and documented safety rules. Unknown hardware stays conservative/read-only. Never invent fan RPM, temperatures, sensor values, hardware support or successful writes.

For UI work, preserve the shared design system and existing UX DNA. Prefer shared XAML/resources/components over runtime visual-tree hacks or one-off overlays. Check ownership of animations, high-rate input, timers and dispatcher work so a visual fix does not introduce lag, duplicate handlers or hidden work after navigation. Inspect generated screenshots yourself at representative minimum/normal/wide sizes and relevant light/dark/error/unavailable states; a green renderer alone is not visual QA.

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
