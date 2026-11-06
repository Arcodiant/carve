---
description: >-
  subdivide smooths a mesh with Catmull-Clark, keeping ids, colours, groups, hard edges and weights
---
# T-43 — Subdivide

**Status:** Open

**Pri:** P1

**Milestone:** M-18

**Batch:** — (standalone)

**Depends on:** T-42

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

Extruded blocks become soft shapes in one call.

## Scope

- **Core:** Catmull-Clark in `MeshEdits`, as D-6's *Subdivide is a destructive edit* says. Hard edges stay creased. Open edges follow the boundary rule. Colours, groups, hard edges and weights carry through; weights use the same stencil, capped at four.
- **Tool:** `subdivide(mesh, levels)`, with `levels` 1 to 3. The result gives counts and warns past a stated face count.
- **Tests:** `unit` for cube counts, a limit surface check, id rules, weights summing to 1, and determinism; undo in one step; a frame before and after.

## Notes

- D-1's id rule is the hard part: stop and ask if it cannot hold. Stronger model.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
