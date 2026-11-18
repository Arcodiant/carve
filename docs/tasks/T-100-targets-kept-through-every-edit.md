---
description: >-
  move, extrude, delete, merge_faces, bridge, plane_cut, loop_cut and knife keep a mesh's targets, new vertices taking their deltas from the vertices they come from
---
# T-100 — Targets kept through every edit

**Status:** In Progress

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-99

**Verify:** core-unit

## Goal

No edit that keeps vertex identity loses a target. An agent can give the cage its dials first and go on
modelling.

## Scope

All in `src/Carve.Core`. The rules the person agreed (2026-11-16):

| Edit | Targets |
|---|---|
| `move` | deltas unchanged: the target moves with the base |
| `extrude` (and its inset) | each new vertex copies the delta of the vertex it was made from |
| `delete` | a deleted vertex's delta goes with it; the rest are kept |
| `merge_faces`, `bridge` | kept; a vertex these remove loses its delta |
| `plane_cut`, `loop_cut`, `knife` | each new vertex blends deltas by the same `Subdivision.Stencil` that blends its weights (`FaceSplit.CarryRest`) |
| style-only tools (colour, groups, material, shading, hard edges, UVs, weights) | untouched |

- `EditResult` carries the targets through like `Weights`. Find every edit through `MeshEdits`, `FaceSplit`,
  `FaceMerge`, `Bridge`, `PlaneCut`, `LoopCut` and `Knife`, and leave none out: list them in As built.
- **Tests** (unit), one per edit: a target defined, the edit made, and the morphed mesh at weight 1 compared with
  the same edit applied to the morphed mesh. Where that is exact (move, delete, extrude of a flat face, cuts along
  an edge), compare within 1e-6 of the diagonal. Where it is not (a knife point inside a face), assert the stencil
  rule directly. Show each failing on a broken product, such as a new vertex given a zero delta, and record the
  table.

## Notes

- Runs beside T-101 and T-102, which do not touch these files.
- **Goes back to the person, stop and ask:** an edit that cannot keep targets by the rules above. The plan says
  none exists. If one does, it must be refused before it runs with a text naming the targets, not silently
  dropped.
- Mechanical once T-99 is in: Sonnet.
