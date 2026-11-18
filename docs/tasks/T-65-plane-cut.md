---
description: >-
  plane_cut splits faces along a plane, keeping both sides or one with the cut filled, on Core's shared face-splitting step that reports each split face's pieces
---
# T-65 — Plane cut

**Status:** Done

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

  A face lying in the plane is not split: with `keep`, it goes with the side its normal points to (superseded by the ruling of 2026-11-13 in As built: it stays whichever side is kept).

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

### As built

First agent: Opus (claude-opus-5-5). **`FaceSplit` with its tests, `EditResult.Split` and the `split` report field** (the member's own checkpoint: "`FaceSplit` with its tests, then `PlaneCut` and the tool"). `PlaneCut`, the tool and the content were left to the second agent, below.

**`FaceSplit`** (`src/Carve.Core/FaceSplit.cs`), the API T-66 and T-67 build on:

- `CutPoint` (record struct): `CutPoint.AtVertex(v)`, or `CutPoint.OnEdge(a, b, t)`, a point on edge `a`-`b` at fraction `t` from `a` (0..1). `IsVertex`.
- `FaceCut(int Face, CutPoint From, CutPoint To)`: one straight segment across one live face, from a boundary point to a boundary point (a corner of the face, in its outer loop or a hole, or a point on one of its edges). Interior points are not taken yet: the knife's path through a face's interior (T-67) needs them, and adds them here (the tracing below already handles a closed inner loop as a hole of the piece around it, but nothing makes interior vertices yet).
- `FaceSplit.Split(mesh, cuts) -> FaceSplitResult(EditResult Edit, IReadOnlyList<(int From, int To)> Segments)`: `Segments` gives, per cut in the order given, the vertex ids its two ends became (empty on failure), which is what a caller needs to know its cut edges (the plane cut's fill loops, the loop cut's ring).
- `FaceSplit.Tolerance(mesh)`: `PolygonMesh.PlanarityTolerance` (1e-4) times the bounding-box diagonal of the mesh's live vertices. `PlanarityTolerance` is a fraction of a face's bounding-box diagonal, and the spec says only "within `PolygonMesh`'s planarity tolerance". A per-face length would let the two faces on an edge disagree about one point, so it is one length for the mesh (the knife's 1e-4 of the bounds diagonal is the same number). **Ruled by the person (2026-11-12): 1e-4 of the whole mesh's bounds diagonal, as built.**
- **Vertex order.** An edge point within the tolerance of an edge end is that vertex; two points on one edge within it of each other are one. Every other edge point is one new vertex, shared by both faces on that edge. New vertices are appended **by edge, low id then high (the `(lo, hi)` pair ascending), and along each edge from its low end**, position `lo + t (hi - lo)` in doubles. So the same call always gives the same ids.
- **Faces.** Every live face with a new vertex on an edge takes it into that loop (outer or hole), in the loop's direction, keeping its id. A split face's pieces are traced as a planar subdivision in the face's plane (the loops in their own winding, each segment both ways; at each vertex the next half-edge is the first clockwise from the way back): counter-clockwise loops are outlines, clockwise ones untouched holes. **The kept id** goes to the piece holding the face's first outer-loop corner, or, if that corner is a segment's end, the next corner of the original outer loop that is not. The other pieces are appended: faces in id order, a face's pieces in the order found from its corners (outer loop then holes). Each piece's outer loop starts at its vertex that comes first in the face's corners (with the new vertices in place), so the kept piece of a face whose corner 0 is off the cut starts at corner 0; winding is the face's.
- **Holes.** A segment ending on a hole cuts the hole's loop into the pieces' outlines; a hole no segment meets stays a hole of the piece containing its first vertex (the smallest such, ready for T-67's nested case), its loop rotated the same way.
- **Skipped, not refused:** a segment from a vertex to itself, or one running along the boundary (its midpoint within the tolerance of a boundary edge). It splits nothing, though its edge points still become vertices. A repeated segment counts once.
- **Refused**, each named `cuts[i]`: no cuts (`cuts: nothing to split: give at least one segment`); a face that is not live (`cuts[0]: faces[0]: deleted`); a vertex not a corner of the face; an edge not the face's; a fraction not finite or not in 0..1; a segment whose midpoint is outside the face (in no outline, or in a hole); one properly crossing a boundary edge or another segment; segments that leave the same piece on both sides of one (`FaceSplit.OnePieceMessage`, e.g. a single bridge from the outer loop to a hole). On failure the mesh is untouched, `Split` and `Segments` empty.
- **What carries:** pieces take the face's colour and groups (`FaceStyle.Carry`); a hard edge with new vertices on it becomes hard pieces, and the segments' edges are soft; a new vertex's weights are `Subdivision.Stencil`'s blend of its edge's ends, `(1 - t)` and `t`, capped at four and renormalised as `subdivide` does; an old vertex's weights are unchanged. M-6's UVs have not landed (no UV in the Core), so nothing about UVs.

**`EditResult.Split`** (`src/Carve.Core/EditResult.cs`): `IReadOnlyList<SplitFace>`, `SplitFace(int Face, IReadOnlyList<int> Pieces)`, one entry per face split in face id order, its pieces the kept id (the face's own) first, then the appended ones ascending; empty for every other edit and on failure. `EditResult.Made` takes it as `split:`.

**The `split` report field** (`src/Carve.Server/MeshToolSupport.cs`, `MeshToolSupport.Split`): `split: [{face, pieces}]` in the shared `Edit` body (so every edit tool), and in `subdivide`'s and `define_mesh`'s bodies, which are written apart, as `deleted` is. Always present, `[]` when nothing was split.

**Tests.** `tests/Carve.Core.Tests/FaceSplitTests.cs` (unit, 15): the cube's top split in two with its neighbours taking the new vertices and keeping their ids, closed; a triangle cut through a vertex (corner 0 on the cut, corner 1 decides); the id rule on a square both ways off corner 0 and on both diagonals, and on a hexagon cut 0-3 and 1-3, where corners 0 and 1 are both on the cut and corner 2 decides; a holed plate cut through the hole (the hole loop in both outlines, areas 6 and 6) and beside it (the hole kept in the piece around it, both ways round); a vertex on a shared edge made once when the two faces give it from opposite ends; the tolerance (a point at half of it is the vertex, at twice it new, two points closer than it one vertex); determinism over two runs of three cuts; colour, groups, smooth mode and split hard edges carried, the new edge soft; weights blended by fraction, capped at four and renormalised; a segment along an edge or to itself splitting nothing; every refusal's text, the one-piece refusal for a lone bridge to a hole and for the same bridge beside a cut that does divide the face; a failed split leaving the mesh and reporting no split, and `move` reporting none. `DeleteToolTests` (server regression) checks `split` is `[]` in delete's report. Core 636 passed, server unit 279 and `DeleteToolTests` 2 passed, by `dotnet test` here.

**Breaks** (each applied alone by `%TEMP%/t65/breaks.py`, built with warnings as errors, `FaceSplitTests` or `DeleteToolTests` run, restored; output in `branches/milestone/M-5/artifacts/review/T-65/breaks.log`):

  | Check | Break | Result |
  |---|---|---|
  | a point within the tolerance of a vertex is that vertex | the low-end snap never fires | 1 fails |
  | two points on one edge are one vertex (a shared edge's vertex made once) | every point a new vertex | 3 fail |
  | new vertices by edge, low id then high | edges in the order met | 2 fail |
  | the same call gives the same ids | every other call takes the edges in reverse | 5 fail |
  | corner 0 on the cut: the next corner off it decides | corner 0 always decides | 1 fails (first run: 0 failed, because the tracing order agreed with the rule in every case; the hexagon test was added and the break rerun) |
  | the piece holding the deciding corner keeps the id | the last piece keeps it | 5 fail |
  | a neighbour on the edge takes the new vertex | only cut faces take it | 3 fail |
  | a hole wholly on one side stays a hole of its piece | every hole to the kept piece | 1 fails |
  | a cut through a hole cuts the hole loop into the outlines | the holes left out of the tracing | 2 fail |
  | pieces take colour and groups | the style not carried | 1 fails |
  | each piece of a split hard edge stays hard | only every other piece | 1 fails |
  | new edges are not hard | the first new edge made hard | 1 fails |
  | a new vertex blends weights by its fraction | `t` and `1 - t` swapped | 1 fails |
  | the new vertex's weights carried | no blend | 1 fails |
  | a segment along an edge splits nothing | the along-boundary test dropped | 1 fails |
  | refused: a face that is not live | the check never fires | 1 fails |
  | refused: a vertex not a corner of the face | the check never fires | 1 fails |
  | refused: a fraction not in 0..1 | the check never fires | 1 fails |
  | refused: an edge not of the face | the check never fires | 1 fails |
  | refused: a segment outside the face | the check never fires | 1 fails |
  | refused: a segment crossing an edge | the check never fires | 1 fails |
  | refused: segments crossing | the check never fires | 1 fails |
  | refused: a cut leaving the face in one piece | the per-segment check never fires | 1 fails (first run: 0 failed, because the outline count caught the lone bridge on its own; the bridge-beside-a-cut case was added and the break rerun) |
  | `Split` lists the kept id first | the kept id left out | 3 fail |
  | every report carries `split` | the key left out of `MeshToolSupport.Edit` | 1 fails (`DeleteToolTests`) |

**Frames:** none taken. Nothing here changes a frame: the report gains a key, and no stage cuts yet. The hash check against T-64's 127 (`branches/milestone/M-5/artifacts/review/T-64/frames-737853a/all.sha256`) is for the agent that runs the GPU suites.

**The person's rulings (2026-11-12), on the first agent's hand-off:**

1. `FaceSplit`'s snap tolerance is 1e-4 of the whole mesh's bounds diagonal, as built.
2. With `keep` above or below, a piece the split appends and the cut removes is listed in both `split` and `deleted`, as built.
3. T-64's `Drawn` pixel check (a nearby pixel at least half way from the background to the line colour, along an edge over the background) is accepted as the rule for edges over the background. This member's stage checks no edge over the background, so it does not use it.

**Second agent: Opus (claude-opus-5-5). `PlaneCut`, the `plane_cut` tool, the Cuts stage, the breaks and the GPU run.**

- **`PlaneCut`** (`src/Carve.Core/PlaneCut.cs`): `PlaneCut.Cut(mesh, point, normal, keep = PlaneKeep.Both, fill = true, faces = null) -> PlaneCutResult(EditResult Edit, IReadOnlyList<string> Warnings, IReadOnlyList<(int Lo, int Hi)> CutEdges)`.
  - A vertex within `FaceSplit.Tolerance(mesh)` of the plane is on it (the one tolerance, ruling 1). Per face (the `faces` given, else every live face): its crossings are the corners on the plane and the edges whose ends are strictly on opposite sides (`CutPoint.OnEdge(a, b, da / (da - db))`), sorted along the line where the plane meets the face. Each gap between consecutive crossings whose middle is inside the face, and not within the tolerance of its boundary, is one `FaceCut`. So a concave face gives several segments (the L-block's arms), and a corner or edge that only touches the plane gives none. A face with every corner on the plane is not split. All the segments go to one `FaceSplit.Split`. With no segment at all the mesh is unchanged (not refused, if the plane touches a face).
  - **keep above/below:** each live face of the split mesh is above when every corner is >= -tol, below when every corner is <= tol, and a face with every corner on the plane goes with its normal. The other side's faces go through `MeshDelete.Closure` (T-64's orphan rule) and one `Edit` with the caps.
  - **fill:** the cap border is each kept face's edge that a deleted face has the other way round, with both ends on the plane, taken reversed, so the caps wind outward on the kept side. The border chains into loops. Open chains are traced first, from each vertex with more border edges out than in, and each is a warning, `fill: the cut from vertex 11 to vertex 10 is not a closed loop (the mesh is open there), so it is left open`, which the tool appends to the report's `warnings`. A closed loop wound counter-clockwise about the kept side's outward normal is a cap. One wound the other way is a hole of the smallest cap around it (T-63's clockwise holes), so a tube cut across gives one holed cap. Each loop starts at its lowest vertex id, and caps are appended in order of their lowest id. On a smooth mesh every cap edge is made hard (`MeshShade.WithEdges`).
  - **The report:** `Split` is the split's. `Deleted` is the closure, which with `keep` includes the appended pieces of the far side (ruling 2). `NewVertices` and `NewFaces` (`created`) list what the split and the fill made **and is still live**. This follows `subdivide`'s precedent of leaving tombstoned ids out of `created` (ruled as built, below). Caps take no colour and no group, since they are no face's pieces (ruled as built, below).
  - **Refused:** `point: must be a finite point`; `normal: must be a finite, non-zero vector`; `faces: a cut limited to some faces keeps both sides: leave faces out to keep one side`; `faces` empty, unknown or deleted (`MeshEdits.UnknownElements`); `point, normal: the plane touches no face` (no target face with a corner on it or corners on both sides); `keep: this would leave nothing: every face is below the plane`.
  - `FaceSplit`'s geometry helpers (`Vector2D`, `Vector3D`, `D`, `Dot`, `Cross`, `Basis`, `SegmentDistance`, `SignedArea`, `InPolygon`, `Inside`) became `internal` so `PlaneCut` uses the same geometry. Nothing else in `FaceSplit` changed. (The M-5 refactor pass moved them, `Vector3D` aside, unchanged to `src/Carve.Core/PlaneGeometry.cs`.)
  - **A non-manifold cut** (a border that meets itself at a vertex): at a vertex with more than one way on, the chaining takes the lowest vertex id (ruled as built, below). `ANonManifoldCutBorderIsChainedByLowestVertexId` covers it.
- **The `plane_cut` tool** (`src/Carve.Server/PlaneCutTool.cs`, in `Program.cs`'s list after `bridge` since the rebase on T-68; after `delete` before it): the description, the worked example `{"point":[0,0.5,0],"normal":[0,1,0],"keep":"below"}` on `define_mesh`'s cube (a half-height closed box, 8 vertices, 6 faces), and one `undo` step (the store's one `Edit`). A `keep` other than both, above or below is refused (`keep: expected both, above or below, got "left"`).
- **Tests.** `PlaneCutTests` (Core unit, 18):
  - keeping both on the cube (ids 8..11, faces 6..9, the four cut edges);
  - keeping below with fill: the half-height box, deleted `[2, 3, 6, 7]` and `[3, 6, 7, 8, 9]`, split `[0, 6] [1, 7] [4, 8] [5, 9]`, the cap `[8, 10, 11, 9]` facing up, area 1, closed;
  - keeping above with fill (the cap facing down), and without fill (four open edges);
  - the L-block cut by x + y = 2.5: front and back each in three pieces, two caps below, eight open edges above without fill;
  - a square tube: a cap with one hole, area 3;
  - the cube with its front deleted: the warning naming vertices 11 and 10;
  - a cut in the top face's plane: not split, the top going above with its normal, the cap `[2, 3, 7, 6]` below;
  - a smooth cube's cap border hard, beside a hard edge it had;
  - a cut limited to face 4;
  - each refusal;
  - two cubes meeting on edge 5-6, cut at y = 0.5 keeping below (added by the third agent): the border meets itself at 17, the new vertex on 5-6, and the lowest way on gives each cube its own cap, `[14, 16, 17, 15]` and `[17, 19, 20, 18]`, both facing up, area 1, no warning.

  `PlaneCutToolTests` (Server regression, 2):
  - the worked example through the client: `split` (faces 0, 1, 4, 5, each its own id first), `deleted` (the upper pieces in it) and `created`, then `check` closed, then `undo` back to the cube in one step;
  - keep both, the open-mesh warning in the report, and five refusals.
- **The Cuts stage** (`scenarios/Carve.Scenario.Cuts/PlaneCutStage.cs`, one line in `CutsStages.cs`).
  - **The cut.** The diagonal cut is the plane x + 2y - z = 1.2, through `[0.5, 0.6, 0.5]` with normal `[1, 2, -1]`, chosen so that no cube corner lies on it. It crosses five edges and leaves a pentagonal cap, seen from both directions.
  - **Keeping below with fill.** The report is 10 vertices, 7 faces, closed, split faces 0, 1, 3, 4, 5, deleted vertices 2, 3, 6, and a five-cornered cap. The frames are `plane-cut-below-front` (from `[0.6, 0.35, 1]`) and `plane-cut-below-back` (from `[-0.6, 0.35, -1]`). The probes are a 4 x 4 grid on each cube plane, six points outside the cube, and the grid points of the cut plane. In each frame:
    - every probe on the cut plane inside the cube that the ray test is sure of shows the cap, and at least 8 are sure;
    - every sure probe where the ray meets nothing shows the background;
    - every other sure probe shows the mesh, within `Exact` of the shade read at that face's first sure probe.
  - **Keeping both.** 13 vertices, 11 faces, closed, nothing deleted. The cut edges are found from get_mesh (face edges with both ends on the plane, five of them) and passed as render's `highlight`. Frame `plane-cut-both-wireframe` (from `[0.6, 0.35, 1]`) checks:
    - the caption's `highlight 5 edges`;
    - the faces `Fill` away from lines;
    - on the faces turned to the camera, the three cut edges in sight are `Line` orange and every other edge in sight is `NotLine` orange. These are the crate's highlight checks, over the faces' fill, not over the background.

**Breaks** (each applied alone by `%TEMP%/t65/breaks2.py`, built with warnings as errors, `PlaneCutTests`, `PlaneCutToolTests` or the Cuts scenario run on this machine, then restored; output appended to `branches/milestone/M-5/artifacts/review/T-65/breaks.log`):

  | Check | Break | Result |
  |---|---|---|
  | keep both only splits | keep both goes on to delete | 2 fail |
  | above is the side the normal points to | above and below swapped | 9 fail |
  | keeping one side deletes the other | no face deleted | 9 fail |
  | the orphan rule (T-64's closure) | only the faces deleted | 4 fail |
  | fill caps each closed loop | no cap | 7 fail |
  | the cap is wound outward on the kept side | the outward normal reversed | 6 fail |
  | nested loops give a cap with a hole | holes dropped | 1 fails |
  | an open chain is a warning naming its ends | the ends named the wrong way round | 1 fails |
  | a face in the plane goes with its normal | against its normal | 1 fails |
  | a smooth mesh's fill border is hard | not marked | 1 fails |
  | `faces` limits the cut | every face cut | 2 fail (first written so it did not build, CS8604; rewritten and rerun) |
  | a concave face's gaps outside it are not cut | the inside test dropped | 1 fails (first written so it did not build, CS8321; rewritten and rerun) |
  | `split` reported beside `deleted` | `split` left out | 1 fails |
  | `created` lists only what is left | the deleted pieces listed | 3 fail |
  | refused: a zero or non-finite normal | the check never fires | 3 fail |
  | refused: a non-finite point | x not checked | 1 fails |
  | refused: a limited cut keeping a side | the check never fires | 1 fails |
  | refused: a plane touching no face | the check never fires | 1 fails |
  | refused: a cut leaving nothing | the check never fires | 1 fails |
  | a non-manifold cut border is chained by lowest vertex id (third agent) | the highest way on taken (`outs[^1]` in `Caps`) | 1 fails (`ANonManifoldCutBorderIsChainedByLowestVertexId`; the other 17 pass) |
  | tool: the open-chain warning reaches the report | not appended | 1 fails |
  | tool: `keep: below` is read | read as both | 2 fail |
  | tool: `fill` defaults to true | defaults to false | 2 fail |
  | Cuts: the fill caps the cut | no cap | 35 checks fail |
  | Cuts: the cap faces out of the kept side | the outward normal reversed | 35 checks fail |
  | Cuts: keep below deletes the corner above | no face deleted | 31 checks fail |
  | Cuts: the cut is where the plane is, and the highlighted edges with it | each crossing at 1 - t along its edge | 24 checks fail |

  The one `undo` step per call has no break of its own. It is the store's one `Edit`, as for every edit tool, and `PlaneCutToolTests` checks that one `undo` restores the cube.

**Frames:** the Cuts scenario was run on this machine (`scripts/run-scenario.ps1 cuts`, outcome passed) on `69f9afa`. Its six existing frames are byte-identical to T-64's `cuts-frames.sha256`. The run's nine frames and their list are in `branches/milestone/M-5/artifacts/review/T-65/cuts-local-69f9afa/`.

**The person's rulings (2026-11-12), on the second agent's hand-off:**

1. `created` lists only live ids, so a far-side piece appears in `split` and `deleted` but not in `created`, as built.
2. Fill caps get no colour and no groups, as built.
3. A non-manifold cut border is chained by lowest vertex id, as built, with a test and a break showing it failing (both above).

**The person's ruling (2026-11-13), RV-31 round 1:** a face lying in a `plane_cut`'s plane stays untouched. It keeps its id and colour, no cap is laid over it, and it stays whichever side is kept, since it is the border. This replaces "a face with every corner on the plane goes with its normal" (Scope and *keep above/below* below record the earlier reading). Built in `PlaneCut.Cut`: a face in the plane is never deleted; for the border, one facing into the kept side counts with the deleted faces (it is the cut-away side's surface), one facing out of it as any kept face, so the cap fills only what the face leaves open. The cube cut at y = 1 keeps face 3 with either side and adds no cap; the L-block cut at its step keeps the step's face 4 and caps only the tower's section. Tests `AFaceInThePlaneIsNotSplitAndStaysUntouchedWhicheverSideIsKept` (replacing the earlier `...GoesWithTheSideItsNormalPointsTo`) and `ACapBesideAFaceInThePlaneFillsOnlyTheCutAndNotThatFace`; the break is in `artifacts/review/RV-31/breaks-A.log`.

**Third agent: Opus (claude-opus-5-5).** Recorded the rulings, added the non-manifold test and its break, and ran the GPU suites on the final code commit.

**Frames (GPU run of `3a809ec`, whose last code commit is `e3c1ca9`):** `cairn task T-65 verify` passed, ran 8, carried 3. The shed, cuts, export, character and soft runs (jobs `J-20261112-151319-1f83cc` to `-151327-d2c189`, each `carve@3a809ec`) were fetched with `cairn test artifacts` into `branches/milestone/M-5/artifacts/review/T-65/frames-e3c1ca9/`, with their list in `all.sha256` there. Their 51 frames that T-64's 127 also has are byte-identical. The three new ones match the local run: `plane-cut-below-back` `fc88fdeb...`, `plane-cut-below-front` `81c9a77d...`, `plane-cut-both-wireframe` `d1bcda5a...`. T-68 had landed nothing on `origin/milestone/M-5`, so no T-68 frames are in the baseline. **Not hash-checked on this commit:** cube (32 frames) and crate (44). Verify carried them from `40a51d5` (their inputs unchanged), and `cairn test artifacts scenario-cube` returns the latest run, which was T-68's (`1cccc9a`), not this member's. The `40a51d5` job ids are not in the local queue records. A forced fresh run was not tried, since `cairn test submit` carries them again.

**Fourth agent: Opus (claude-opus-5-5).** Rebased onto `origin/milestone/M-5` after T-68 landed (`merge_faces`, `bridge`, `MergeBridgeStage`). Two add-only conflicts, both sides kept: `Program.cs`'s tool list (`PlaneCutTool` after `BridgeTool`) and `CutsStages.cs` (`PlaneCutStage` after `MergeBridgeStage`). The rebased tree built with no warnings and the unit suites passed here (Core 676, Server 662, ModelViewer 15), so the rebase needed no fix commit.

**Frames (GPU run of `aae1140`, the rebased last code commit):** `cairn task T-65 verify` passed, ran 10, carried 1 (viewer-cube, from `99c846e`). Cube and crate ran fresh, which settles the third agent's open question. The seven scenario runs (jobs `J-20261112-153155-852077` to `J-20261112-153206-0bbf28`, the `job` line of each `output.log` reading `carve@aae1140...`) were fetched by job id into `branches/milestone/M-5/artifacts/review/T-65/frames-aae1140/`, 133 frames, listed in `all.sha256` there. The baseline is T-64's 127 (`T-64/frames-737853a/all.sha256`) plus T-68's three new Cuts frames (`bridge-slab`, `bridge-slab-ortho`, `merge-plate-wireframe`, from `T-68/frames-990b937/all.sha256`), 130 in all. All 130 are byte-identical, none is missing, and there are three new ones, each the same hash as the local and `e3c1ca9` runs:

  | Frame | sha256 |
  |---|---|
  | `cuts/frames/plane-cut-below-back.png` | `fc88fdeb93fcd33cd017158128f927882847649e59dd119f7f35ca83b04e521f` |
  | `cuts/frames/plane-cut-below-front.png` | `81c9a77ddc9b3df96ce0f6ef9ce45005e84dd64ecfee9311ffc5ce8e18aa7185` |
  | `cuts/frames/plane-cut-both-wireframe.png` | `d1bcda5ac6641859eccf991efcc6ad1623f4739ea698e05b037f185c6a937b34` |

**The person's review (2026-11-12):** the three new frames from the `aae1140` run were sent through Waystone, and the person approved them with no notes.
