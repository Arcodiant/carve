---
description: >-
  plane_cut splits faces along a plane, keeping both sides or one with the cut filled, on Core's shared face-splitting step that reports each split face's pieces
---
# T-65 — Plane cut

**Status:** Open

**Pri:** P1

**Milestone:** M-5

**Batch:** — (standalone)

**Depends on:** T-64

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent slices a mesh with a plane: it splits it in place, or cuts one side away and caps the cut. It does this on the face-splitting step that the loop cut and the knife reuse.

## Scope

**Core: `FaceSplit`** (its own file). This is the shared step: given segments across faces, it inserts vertices on edges and splits faces into pieces.

- **Vertices.**
  - A cut point within `PolygonMesh`'s planarity tolerance of an existing vertex is that vertex.
  - A point on an edge becomes one new vertex, shared by both faces on that edge.
  - New vertices are appended in a deterministic order (by edge, low id then high), so the same call always gives the same ids.
- **Faces.**
  - A split face keeps its id for the piece that holds its first outer-loop corner. If that corner lies on the cut, the next corner off the cut decides.
  - The other pieces are appended.
  - A neighbouring face that only gains a vertex on its edge keeps its id, with the vertex in its loop.
- **Holes.** A segment that crosses a hole splits the face through the hole: the hole's loop is cut and becomes part of the pieces' outlines. A hole wholly on one side stays a hole of the piece that contains it.
- **What carries:**
  - every piece takes the split face's colour and groups;
  - each piece of a split hard edge stays hard, and new edges are not hard;
  - a new vertex on an edge blends the ends' weights by its fraction along it, renormalised and capped at 4 as `subdivide` does (T-43);
  - if M-6's UVs have landed, they blend the same way.
- `EditResult` gains `Split`: one entry per face split, `{Face, Pieces}`, its pieces ascending with the kept id first.

**Core: `PlaneCut`.** `plane_cut(point, normal, keep: both | above | below = both, fill = true, faces?)`.

- `above` is the side the normal points to.
- `faces`, when given, limits the cut to those faces. A limited cut that keeps one side is refused.
- **`keep: both`** only splits.
- **Keeping one side** deletes every face wholly on the other side, through T-64's tombstones and orphan rule.
- **`fill`.** Each closed loop of cut edges is capped with a new face, wound outward on the kept side. Two nested loops give a cap with a hole (T-63). A cut loop that is not closed (an open mesh) is left open, with a warning naming its ends.
- On a smooth mesh, the fill's border edges are marked hard.
- **Refused:**
  - a zero or non-finite normal;
  - a plane that touches no face;
  - a cut that would leave nothing.

  A face lying in the plane is not split: with `keep`, it goes with the side its normal points to.

**Server.**

- **The `plane_cut` tool,** with the description and a worked example on `define_mesh`'s unit cube: a cut at y = 0.5 keeping `below` with fill gives a half-height closed box.
- Every edit's report gains `split: [{face, pieces}]`, written once in `MeshToolSupport`'s shared report body.
- One `undo` step per call.

**Tests.**

- `unit`, each with a break shown failing:
  - `FaceSplit` on a quad, on a triangle through a vertex, and through a holed face (the hole cut and the hole kept);
  - the id rule, including corner 0 on the cut;
  - shared edge vertices made once;
  - determinism over two runs;
  - colours, groups, hard edges and weights carried;
  - `PlaneCut` keeping both, above and below, with and without fill, on a cube, an L-block and an open mesh (the warning);
  - a cut in a face's plane;
  - each refusal.
- `regression`: `plane_cut` through the client on the cube, its `split` and `deleted` fields, then `check` closed.

**Content.** `PlaneCutStage.cs` in the Cuts scenario:

- the unit cube cut diagonally, keeping `below` with fill, from `[0.6, 0.35, 1]` and from `[-0.6, 0.35, -1]`;
- the same cut with `keep: both`, in the wireframe from `[0.6, 0.35, 1]`, the cut edges highlighted.

Ray-test pixel checks, each shown failing on a break.

## Notes

- **Decided by the person at planning (2026-11-09):**
  - the piece holding corner 0 keeps the id;
  - `keep` and `fill`, as in Scope;
  - weights blend;
  - split hard edges stay hard, new edges are soft, and the fill border on a smooth mesh is hard.
- **Design-heavy: `FaceSplit` is the mechanism T-66 and T-67 build on.** Write its API, the vertex order and the `Split` report into As built. Opus.
- T-68 runs beside this task and also adds a tool to `Program.cs`'s list and a stage line to `CutsStages.cs`. Settle those conflicts by keeping both sides. T-68 does not touch the report body; this task owns `split`.
- **Open rules: stop and ask.**
  - A tolerance other than `PolygonMesh`'s.
  - How UV seams carry, if M-6 has landed.
  - A cut through a non-manifold edge whose result is not obvious.
  - Filling a loop that is not planar.
- Every existing frame stays byte-identical by hash, listed.
- Plan for two agents: `FaceSplit` with its tests, then `PlaneCut` and the tool.
- Evidence goes to the milestone worktree's `artifacts/review/T-65/` by absolute path.
- Run `finish` from inside the worktree and leave the folder.
