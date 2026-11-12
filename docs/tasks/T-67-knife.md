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

### As built

First agent: Opus (claude-opus-5-5). **The Core (`Knife`, and `FaceSplit`'s interior points), the `knife` tool, the unit and regression tests and their breaks.** The Cuts stage, its frames and the GPU run are left (see Hand-off).

**`FaceSplit`'s addition** (`src/Carve.Core/FaceSplit.cs`, additive, on the orchestrator's ruling; every T-65 test passes unedited, and with no interior point nothing in its behaviour, vertex order or `split` changes):

- `CutPoint.Interior(Vector3 at)`, a new kind (`IsInterior`, its position in `At`, an init-only property; `B` is -2, so `IsVertex` is now `B == -1`, the same for every existing point). `ToString` names it.
- **Validation:** an interior point must lie in its face's plane within `Tolerance(mesh)`, inside the outer loop and in no hole, and further than the tolerance from every edge, else `cuts[i]: to: [0.5, 0.5, 0.5] is not inside faces[1], clear of its edges`.
- **Vertices:** each interior point is one new vertex, appended **after the edge vertices, in the order the cuts give them**; two interior points of one face within the tolerance of each other are one (so a path's shared point is one vertex).
- **Pieces:** in a piece's loop the interior vertices rank after the face's corners, by id (the loop-start rule). A closed loop of segments touching no boundary traces as an inner outline (the face's winding) and the same loop the other way (a hole); that hole goes to the smallest outline around it **other than the inner face it runs round** (`Reverses`), so the face keeps its id with a new hole, wound opposite, and the inner face is appended. The kept-id rule is unchanged, so the inner face is never the kept piece.
- **Weights:** a new interior vertex takes the barycentric blend of the corners of the face's triangle it lies in (`Triangulator.TriangulateFace`, the triangle it is least outside), through `Subdivision.Stencil` as the edge blend does, so capped at four and renormalised. `Carry` now takes one stencil per added vertex; the edge ones are built exactly as before. No UVs (M-6 has not landed).
- `HasEdge` and `Crosses` became `internal`, for `Knife`; and `InsideFace` is new, `internal`.

**`Knife`** (`src/Carve.Core/Knife.cs`): `Knife.Cut(mesh, points, closed = false) -> KnifeResult(EditResult Edit, IReadOnlyList<(int From, int To)> Edges)`, `Edges` the vertex ids of each segment's ends, in path order, the closing one last.

- **Snapping**, with `FaceSplit.Tolerance(mesh)` (1e-4 of the live bounds diagonal, the person's ruling on T-65): the nearest vertex used by a live face within the tolerance (lowest id on a tie); else the nearest edge of a live face's loops within it (lowest `(lo, hi)` on a tie), `t` the projection's fraction from `lo`; else every live face whose plane is within it and whose outline (less holes) holds the projection: one is an interior point, **projected onto that plane**; two or more are refused naming them; none is off the surface.
- **The off-surface error** (T-70's plant), facts only: `points[1]: [0.5, 0.5, 1.25] is not on the surface: it is 0.25 from it, more than the tolerance 0.000173205; the nearest surface point is [0.5, 0.5, 1], on faces[1]`. The nearest point is the projection when it falls inside a face, else the nearest point of its loops' edges, over every live face (the lowest face on a tie). Numbers are `G6`.
- **Segments:** the faces both ends lie on (a vertex's faces, an edge's two faces, an interior point's one face). Each is judged: along its boundary (the midpoint within the tolerance of a loop edge), across it, leaving it (crossing an outer edge the ends do not lie on, or the midpoint outside), or through hole k (crossing a hole's edge, or the midpoint in it). One face across is the cut's face; two across are refused as overlapping; none across but one along is a cut along an edge (FaceSplit skips it); otherwise refused with the first reason.
- **Refusals**, each naming its points: `points: an open path needs at least 2 points, got 1` (3 for closed); `points[1]: must be a finite point`; the off-surface error; `points[1]: [1.5, 1.5, 0] lies inside faces[0] and faces[1], which overlap there`; `points[1]: [0.5, 0.5, 1] is inside faces[1], not on an edge or a vertex, and an open path must end on one: a cut that stops inside a face is not a face`; `points[0], points[1]: points[0] and points[1] share no face: a segment runs across one face`; `... the segment from points[0] to points[1] leaves faces[0]`; `... passes through hole 0 of faces[1]`. A `FaceSplit` refusal comes back with `cuts[k]` renamed `points[k], points[k+1]`.
- Ids, `split` and what carries are `FaceSplit`'s.

**The `knife` tool** (`src/Carve.Server/KnifeTool.cs`, in `Program.cs`'s list after `plane_cut`): `points` (array of `[x, y, z]`), `closed`, `mesh`. The description gives the tolerance and the snapping order, and two worked examples on `define_mesh`'s cube: the door outline `[[0.35,0,1],[0.35,0.7,1],[0.65,0.7,1],[0.65,0,1]]` (12 vertices, 7 faces) and the closed square `[[0.25,0.25,1],[0.75,0.25,1],[0.75,0.75,1],[0.25,0.75,1]]` with `closed` (the front holed, 12 vertices, 7 faces). One `undo` step (the store's one `Edit`).

**Tests.** `KnifeTests` (Core unit, 17): an open path vertex to vertex, edge to edge (the neighbours take the new vertices), and through an interior point; a closed square inside the front (hole `[8, 11, 10, 9]`, inner face `[8, 9, 10, 11]`, areas 0.75 and 0.25, normals equal, closed); a closed triangle with two corners on an edge (split, no hole); a path across two faces by a shared-edge point; snapping at 0.9 and 1.1 of the tolerance from a vertex, an edge and the plane; both off-surface texts; every other refusal; a segment leaving an L-block's face; a segment through a knifed hole; a point in two overlapping faces; determinism; barycentric weights on a tetrahedron's base, capped at four; FaceSplit refusing an interior point outside its face. `KnifeToolTests` (Server regression, 2): both worked examples through the client, `check` closed, `undo` one step each, the holed front read back from `get_mesh`; the off-surface error read back, bad arguments refused, the mesh unchanged. Core unit 693 passed, server unit 279, `KnifeToolTests` 2, by `dotnet test` here.

**Breaks** (each applied alone by `C:/Users/andre/AppData/Local/Temp/t67/breaks.py`, built with warnings as errors, `KnifeTests` or `KnifeToolTests` run, restored; output in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-67/breaks.log`):

  | Check | Break | Result |
  |---|---|---|
  | a point within the tolerance of a vertex is that vertex | the vertex snap never fires | 2 fail |
  | a point within the tolerance of an edge is on it | the edge snap never fires | 9 fail |
  | a point inside a face is projected onto its plane | the point kept as given | 1 fails |
  | the off-surface error gives the distance | the distance doubled | 2 fail |
  | the off-surface error gives the nearest surface point | the point itself named | 2 fail |
  | refused: a point inside two overlapping faces | the first face taken | 1 fails |
  | refused: consecutive points sharing no face | the check never fires | 1 fails |
  | refused: a segment leaving its face | leaving counted as across | 1 fails |
  | refused: a segment through a hole | a hole counted as across | 1 fails |
  | refused: an open path ending inside a face | the check never fires | 2 fail |
  | refused: too few points | the check never fires | 1 fails |
  | refused (FaceSplit): an interior point not inside its face | the check never fires | 1 fails |
  | a closed inner path's hole goes to the face around it, not the inner face (`Reverses`) | the check dropped | **0 fail: not yet shown** (see Hand-off) |
  | an interior point shared by two segments is one vertex | every interior point a new vertex | 7 fail |
  | an interior vertex blends its triangle's corners barycentrically | two corners' weights swapped | 1 fails |
  | an interior vertex is given weights | no blend | 1 fails |
  | `knife` is a tool | left out of `Program.cs`'s list | 2 fail (`KnifeToolTests`) |
  | `closed` reaches the knife | ignored | 1 fails (`KnifeToolTests`) |

### Hand-off

First agent (Opus, claude-opus-5-5), handing off at the tool-call checkpoint.

- **Done and committed:** `FaceSplit`'s interior points, `Knife`, the `knife` tool, `KnifeTests` (17), `KnifeToolTests` (2), the breaks above. Core unit (693), server unit (279) and `KnifeToolTests` pass locally. T-65's `FaceSplitTests` pass unedited.
- **Left:**
  1. `KnifeStage.cs` in the Cuts scenario and its line in `CutsStages.cs` (after `PlaneCutStage`; T-66 adds one too, keep both): the 2 x 2 x 0.2 slab, the closed hexagon and the open door path on its front, the wireframe from `[0.6, 0.35, 1]` with the new edges highlighted (`KnifeResult.Edges` is not in the tool's report; find the new edges from `get_mesh` as `PlaneCutStage` does), and the hexagon's inner face extruded by -0.05 (recessed), shaded. Ray-test pixel checks on the existing `CutsGeometry.cs`, each with a break. New frames go to the person.
  2. The GPU run, once, on a near-final commit; every existing frame byte-identical against `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-65/frames-aae1140/all.sha256` (133) plus T-66's if landed, listed under `artifacts/review/T-67/`.
  3. `cairn task T-67 verify` (not run yet), then `finish`.
  4. The `Reverses` break failed nothing: the test's inner square happens to put the hole's first point where the ray test already says "in the outer outline only". Either add a case that needs it (an inner path whose first vertex's ray test falls inside the inner outline) and rerun break 12, or drop the check. Not settled here.
- **Open rule for the person (not decided here):** a closed path wholly inside a face that touches the boundary at **one point only** (e.g. a triangle with one corner on an edge). The face around it would be a loop that passes through that point twice, which a face cannot be, so it is refused today by the mesh's own check with `faces[1][4]: index 8 repeats faces[1][1]`, a fact but not one in the knife's words. The spec's "a closed path that touches the boundary splits the face as usual" does not cover it. Ask whether it is refused (and in what words) or cut some other way. The test of a closed path touching an edge uses two corners on the edge, which splits cleanly.
- **Surprising:** none beyond the two points above. The piece loop rule (a piece starts at its corner first in the face's corners) puts an edge vertex ahead of an old corner where it comes first in the face's loop (`[8, 5, 6, 9, 10]`), as T-65 wrote it.
