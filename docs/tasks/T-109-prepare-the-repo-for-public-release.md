---
description: >-
  Make the repo buildable and presentable from a public clone: engine from nuget.org, MIT license, .github, README setup.
---
# T-109 — Prepare the repo for public release

**Status:** Done

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

### As built

- `EmpyreanVersion` is 0.129.0. No code changed: with `NuGet.config` gone, a restore from a config naming only nuget.org into an empty package folder pulled every `Arcodiant.*` package and the solution built with no warnings; `core-unit` (1030) and `server-unit` (363) passed.
- `LICENSE` is MIT. `.github/workflows/ci.yml` builds on `windows-latest` and runs `core-unit` and `server-unit` (hosted runners have no Vulkan device); not yet run on GitHub; its first run, on the push to `main` at `finish`, is its check. `.github/SECURITY.md` and `.github/CONTRIBUTING.md` added.
- README: the local-feed paragraph is replaced by nuget.org, plus *Build and test* and *License* sections. CLAUDE.md's engine section no longer names the feed or its folder.
- 31 docs files: a drive's `repos\carve\branches` prefix became `branches`, the cairn checkout `<cairn>`, the user's temp folder `%TEMP%` and the user's profile `~`. Records only; nothing reads them.
- The comment at `src/Carve.Server/SkinnedExport.cs:36` still cites 0.119.0's `HumanoidRoleMap.Synty` as where the bone list came from; it records provenance and is left.
