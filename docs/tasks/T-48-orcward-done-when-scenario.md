---
description: >-
  One suite proves the request's done-when: both exports, Load, LoadAnimationSet, and a loaded frame matching Carve's preview
---
# T-48 — Orcward done-when scenario

**Status:** Open

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-45, T-46, T-47

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Every line of Orcward's *Done when* passes in one suite.

## Scope

- **`scenarios/Carve.Scenario.Orcward`**, suite `scenario-orcward`, on the gardener with both looping clips:
  1. export in both modes, with no warnings;
  2. `IModelGeometryLoader.Load` gives the expected bone count and clip names;
  3. `LoadAnimationSet` reads the clips-only file with the same bones and clips;
  4. `render_glb` and `render` frames compared at the rest pose, and at several times of each clip, including times between keys.
- **The comparison:** measure the real difference first, as a table of per-pixel error by frame. **Then stop and ask the person for the tolerance.** Pick nothing yourself.
- **Show it failing:** a swapped bone order, wrong inverse binds, a dropped piece, reversed winding.

## Notes

- If the frames differ at a key, that is a bug and not a tolerance: find it.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
