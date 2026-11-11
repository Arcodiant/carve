---
description: >-
  weigh gives every vertex up to four weights from bone proximity, deterministically
---
# T-37 — Automatic weights

**Status:** In Progress

**Pri:** P1

**Milestone:** M-17

**Batch:** — (standalone)

**Depends on:** T-44

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent rigs a whole mesh with one call, not a weight per vertex.

## Scope

- **Core:** weights by distance to each bone's segment, as D-6 says: a falloff by distance, the top four kept, normalised. A leaf bone's segment runs along its parent's direction, for half its parent's length. No randomness, and a fixed order for ties.
- **Tool:** `weigh(mesh, bones?)`, optionally limited to named bones. It leaves explicit weights alone unless given `replace: true`.
- **Sample:** the gardener weighted automatically, compared with its hand weights.
- **Tests:** `unit` for a vertex on a bone being fully that bone's, a point midway sharing it, determinism, and the cap of four; a bent-arm frame with no vertices left at rest.

## Notes

- The falloff is a design choice; record it and its constant in D-6. Stronger model.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
