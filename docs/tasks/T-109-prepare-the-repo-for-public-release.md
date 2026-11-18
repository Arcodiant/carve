---
description: >-
  Make the repo buildable and presentable from a public clone: engine from nuget.org, MIT license, .github, README setup.
---
# T-109 — Prepare the repo for public release

**Status:** In Progress

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

Anyone can clone Carve from a public GitHub repo, restore from nuget.org alone and build it.

## Scope

- `EmpyreanVersion` moves from 0.119.0 to 0.129.0, the newest version every Empyrean package Carve uses has on nuget.org. Fix whatever the bump breaks so the solution builds with warnings as errors. Orcward follows on its own side (the person's call, 2026-11-17).
- Delete `NuGet.config`; restore uses nuget.org only.
- Add `LICENSE` (MIT).
- Add `.github/`: a CI workflow that builds and runs the suites that need no GPU, `SECURITY.md`, `CONTRIBUTING.md`.
- README setup: no local feed; the license.
- Drop private folder paths (a local drive's repos folder, a user profile folder) from the current files. History is not rewritten.

## Notes

Not in scope: the GitHub description (set directly on GitHub), the visibility switch, a history rewrite.
