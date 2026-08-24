# ThinkControl agent workflow

These rules apply to automated coding assistants and human contributors working in this repository.

## Branch and PR hygiene

1. Inspect `main`, open pull requests and existing feature branches before creating a new branch.
2. Reuse the active feature branch/PR when the requested work belongs to the same release or scope.
3. Never leave recovery/checkpoint branches as authoritative work. Before finishing, compare every non-main branch with `main` and the active PR.
4. If an old branch contains unique useful work, port only the missing changes into the active PR and validate them there. Do not merge stale branches wholesale.
5. After a PR is merged, its branch is disposable. The repository cleanup workflow deletes same-repository merged branches automatically.
6. A release task is not complete until the validated PR is merged to `main`, release assets are published and the published installer/assets are verified.

## Validation

Normal product changes must pass the Windows build/tests, WPF snapshot/visual QA and packaging/install/service lifecycle checks used by this repository. Hardware-control changes must remain capability-gated and preserve safe firmware fallback behavior.

## Source of truth

`main` plus the single active release PR are the source of truth. Do not assume an old branch contains newer functionality just because Git history reports it as ahead after a squash merge; compare actual file contents/capabilities first.
