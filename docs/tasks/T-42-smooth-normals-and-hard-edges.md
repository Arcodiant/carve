---
description: >-
  An agent marks a mesh smooth and marks hard edges, and render and export draw the same per-corner normals
---
# T-42 — Smooth normals and hard edges

**Status:** In Progress

**Pri:** P1

**Milestone:** M-18

**Batch:** — (standalone)

**Depends on:** T-36

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

A character's surface reads soft, with creases only where an agent puts them.

## Scope

- **Core:** per-corner normals (`src/Carve.Core/Shading.cs`) as D-6 says, one function for render and export. Flat meshes give exactly today's normals.
- **Tools:** `set_shading(mesh, flat | smooth)`, and `mark_edges(mesh, edges, hard)` with edges as vertex pairs.
- **Skinning:** posed normals come from the corner normals.
- **File:** version 3 adds `shading` and `hard_edges`; versions 1 and 2 still read.
- **Export:** corner normals in `NORMAL`. Render and loaded normals compared in a `unit` test.
- **Tests:** `unit` for a cube smooth, a cube with one hard edge, and non-manifold and open edges; frames of a smooth and a creased shape.

## Notes

- Flat stays the default so no existing frame or file changes.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
