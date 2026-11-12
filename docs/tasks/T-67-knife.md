---
description: >-
  knife cuts a path of surface points through faces; a closed path inside a face cuts out an inner face and leaves a hole
---
# T-67 — Knife

**Status:** In Progress

**Pri:** P1

**Milestone:** M-5

**Batch:** — (standalone)

**Depends on:** T-65

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent cuts any shape it can give as coordinates into a mesh, with no pointer: a door outline up from a floor edge, or a window wholly inside a wall.

## Scope

**Core: `Knife`** (its own file), on T-65's `FaceSplit` and T-63's holed faces. `knife(points: [[x, y, z], ...], closed = false)`.

- **Points.** Each point must lie on the mesh's surface, within 1e-4 of the mesh's bounds diagonal.
  - **Snapping.** A point within that distance of a vertex is that vertex. Otherwise, a point within it of an edge is on that edge. Otherwise it is inside a face.
  - **Errors.** A point off the surface is an error naming it (`points[3]`) with its distance and the nearest surface point, so an agent can correct it from the text alone. So is a point that falls on two faces it cannot choose between, such as a point inside a coplanar overlap.
- **Segments.**
  - Each pair of consecutive points must share a face, and with `closed`, so must the last and the first.
  - A segment runs across that face. It may pass through several faces sharing its plane only by way of points on their shared edges, which the agent lists.
  - A segment that leaves its face is refused, naming both points.
- **What a path does:**
  - a path from boundary to boundary of a face (vertex or edge) splits it, through `FaceSplit`;
  - interior points become new vertices inside the face;
  - a closed path wholly inside one face, touching no edge, cuts out an inner face (appended) and leaves the face with a new hole on the same loop, wound opposite (T-63);
  - a closed path that touches the boundary splits the face as usual;
  - an open path that ends inside a face is refused: a dangling cut is not a face.
- **Ids, `split` and what carries** follow `FaceSplit`. The inner face of a closed path is listed among the face's pieces in `split`. It is not the kept piece. A knife vertex inside a face gets the barycentric blend of its triangle's corner weights, renormalised and capped at 4 (UVs likewise, if M-6 has landed).
- A segment through an existing hole of the face is refused in this task, naming it.

**Server.** The `knife` tool, with the description and worked examples on `define_mesh`'s unit cube:

- a door outline from the front face's bottom edge;
- a closed square inside the front face, which leaves it holed.

The description gives the tolerance and the snapping order. One `undo` step.

**Tests.**

- `unit`, each with a break shown failing:
  - an open path vertex to vertex, edge to edge, and through interior points;
  - a closed path inside a face (the hole and the inner face, winding, topology closed);
  - a closed path touching an edge;
  - a path across two faces by a shared-edge point;
  - snapping at the tolerance's edge (inside and out);
  - every refusal with its text, above all the off-surface point's distance and nearest point;
  - ids, determinism and barycentric weights.
- `regression`: `knife` through the client, the off-surface error read back, then `check` closed.

**Content.** `KnifeStage.cs` in the Cuts scenario:

- a 2 x 2 x 0.2 slab with a closed hexagon knifed into its front face and an open door path from its bottom edge, from `[0.6, 0.35, 1]` in the wireframe with the new edges highlighted;
- the hexagon's inner face recessed by 0.05 with `extrude`, shaded.

Ray-test pixel checks, each shown failing on a break.

## Notes

- **Decided by the person at planning (2026-11-09):**
  - points on the surface within 1e-4 of the bounds diagonal, snapping to a vertex, then an edge, then a face;
  - consecutive points share a face;
  - a closed path inside a face leaves a hole.
- The off-surface error is T-70's planted mistake. Its text must give the point's distance and the nearest surface point, and must not tell the agent what to do (facts only, as the person ruled for `move` on RV-20).
- Uses `FaceSplit` as T-65 wrote it down. A need in it goes back to the orchestrator, because T-66 shares it.
- T-66 runs beside this task. Keep both sides in `Program.cs` and `CutsStages.cs`.
- **Open rules: stop and ask.**
  - A different tolerance.
  - Cutting through an existing hole.
  - A dangling open path.
  - A point that could belong to two non-coplanar faces at an edge, beyond the snapping order.
- Every existing frame stays byte-identical by hash, listed.
- Opus. The geometry of snapping and of the inner face is the hard part, and it gets the plant's error text right.
- Evidence goes to the milestone worktree's `artifacts/review/T-67/` by absolute path.
- Run `finish` from inside the worktree and leave the folder.
