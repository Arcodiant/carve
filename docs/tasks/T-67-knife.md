---
description: >-
  knife cuts a path of surface points through faces; a closed path inside a face cuts out an inner face and leaves a hole
---
# T-67 — Knife

**Status:** Done

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

First agent: Opus (claude-opus-5-5). **The Core (`Knife`, and `FaceSplit`'s interior points), the `knife` tool, the unit and regression tests and their breaks.** The Cuts stage, its frames, the GPU run, the one-point rule and the `Reverses` case are the second agent's (below).

**`FaceSplit`'s addition** (`src/Carve.Core/FaceSplit.cs`, additive, on the orchestrator's ruling; every T-65 test passes unedited, and with no interior point nothing in its behaviour, vertex order or `split` changes):

- `CutPoint.Interior(Vector3 at)`, a new kind (`IsInterior`, its position in `At`, an init-only property; `B` is -2, so `IsVertex` is now `B == -1`, the same for every existing point). `ToString` names it.
- **Validation:** an interior point must lie in its face's plane within `Tolerance(mesh)`, inside the outer loop and in no hole, and further than the tolerance from every edge, else `cuts[i]: to: [0.5, 0.5, 0.5] is not inside faces[1], clear of its edges`.
- **Vertices:** each interior point is one new vertex, appended **after the edge vertices, in the order the cuts give them**; two interior points of one face within the tolerance of each other are one (so a path's shared point is one vertex).
- **Pieces:** in a piece's loop the interior vertices rank after the face's corners, by id (the loop-start rule). A closed loop of segments touching no boundary traces as an inner outline (the face's winding) and the same loop the other way (a hole); that hole goes to the smallest outline around it **other than the inner face it runs round** (`Reverses`), so the face keeps its id with a new hole, wound opposite, and the inner face is appended. The kept-id rule is unchanged, so the inner face is never the kept piece.
- **Weights:** a new interior vertex takes the barycentric blend of the corners of the face's triangle it lies in (`Triangulator.TriangulateFace`, the triangle it is least outside), through `Subdivision.Stencil` as the edge blend does, so capped at four and renormalised. `Carry` now takes one stencil per added vertex; the edge ones are built exactly as before. No UVs (M-6 has not landed).
- `HasEdge` and `Crosses` became `internal`, for `Knife`; and `InsideFace` is new, `internal`. (`Crosses`, and `Knife`'s `Length`, moved with the other geometry helpers to `PlaneGeometry` in the M-5 refactor pass.)

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
  | a closed inner path's hole goes to the face around it, not the inner face (`Reverses`) | the check dropped | 1 fails (first run: 0 failed, since the square's traced hole started at a vertex the ray test put outside the inner outline; the second agent added the square started at each of its four corners, and the rerun fails it) |
  | an interior point shared by two segments is one vertex | every interior point a new vertex | 7 fail |
  | an interior vertex blends its triangle's corners barycentrically | two corners' weights swapped | 1 fails |
  | an interior vertex is given weights | no blend | 1 fails |
  | `knife` is a tool | left out of `Program.cs`'s list | 2 fail (`KnifeToolTests`) |
  | `closed` reaches the knife | ignored | 1 fails (`KnifeToolTests`) |
  | refused: a closed path touching the boundary at one point only (second agent) | the check never fires | 1 fails (`AClosedPathTouchingTheBoundaryAtOnePointOnlyIsRefused`; the mesh's own repeated-index refusal comes back instead) |


**Second agent: Opus (claude-opus-5-5).** The one-point rule, the `Reverses` case, the Cuts stage and its local run; the GPU suites submitted (their run is in the third agent's notes).

- **The person's ruling (2026-11-12):** a closed path inside one face that touches the face's boundary (a vertex or an edge point) at exactly one point is refused in the knife's own words, facts only: `points[2]: the closed path touches the face's boundary at one point only, at [0.5, 0, 1]; touch it at two points to split the face, or keep it clear of the edge to cut a hole`. Before it, the mesh's own check refused it with `faces[1][4]: index 8 repeats faces[1][1]`. The point named is the vertex, or the point at the snap's fraction along the edge. The tool's description says so. Test: `AClosedPathTouchingTheBoundaryAtOnePointOnlyIsRefused` (an edge point and a vertex); break in the table above.
- **`Reverses` kept:** `AClosedPathInsideAFaceLeavesTheHoleInTheFaceWhicheverCornerItStartsAt` cuts the square from each of its four corners; with the check dropped, one start puts the traced hole's first vertex inside the inner outline by the ray test, and the hole goes to the inner face (break 12 above, rerun).
- **The Cuts stage** (`scenarios/Carve.Scenario.Cuts/KnifeStage.cs`, its line after `PlaneCutStage` in `CutsStages.cs`): the 2 x 2 x 0.2 slab (front at z = 0.2), the closed hexagon `[1.3,0.9] [1.6,0.9] [1.8,1.2] [1.6,1.5] [1.3,1.5] [1.1,1.2]` (14 vertices, 7 faces, closed, created face 6, the front one hole), then the open door `[0.3,0] [0.3,1] [0.7,1] [0.7,0]` from the bottom edge (18 vertices, 8 faces, closed). The knifed edges are found from get_mesh (an edge whose midpoint lies on a path segment, 9) and passed as `highlight`. Frame `knife-slab-wireframe` from `[0.6, 0.35, 1]`: caption, the ray-tested probes (`Look`, as `PlaneCutStage`), each knifed edge in sight orange and each other edge in sight not. Then `extrude` face 6 by -0.05 (24 vertices, 14 faces, closed, the hexagon at z = 0.15); frame `knife-slab-recess`, shaded: caption, the recess probes show face 6 (`Expect`), and `Look` with each face's shade.
- **Local run** (`scripts/run-scenario.ps1 cuts` on `f23c78f`): passed. Its 12 existing frames are byte-identical to T-65's `frames-aae1140/all.sha256`. New: `knife-slab-wireframe` `175724fa4cb2...`, `knife-slab-recess` `84957d9dc2db...`. Both looked at: the orange hexagon and door on the front; the pocket's walls showing at its left and bottom.
- **Stage breaks** (`C:/Users/andre/AppData/Local/Temp/t67/breaks3.py`, each applied alone, the Cuts scenario run here, restored; log appended to `breaks.log`): every one is caught, but by the stage's count and caption checks or by the knife refusing, **none by a pixel check**:

  | Check | Break | Result |
  |---|---|---|
  | Cuts: an interior point becomes a vertex where the path puts it | the vertex 0.05 off in x | 3 checks fail (knifed edges 3 not 9, the caption, edges in sight) |
  | Cuts: the hole is wound opposite the face | the hole wound as the face | the knife refused (1 problem) |
  | Cuts: an edge point is at its fraction along the edge | at 1 - t | the knife refused (2 problems) |
  | Cuts: `closed` reaches the knife | ignored | the knife refused (2 problems) |

- **The person's ruling (2026-11-13, RV-31 round 1):** knife segments that meet the face's boundary between their ends (a midpoint on the boundary, a grazed reflex vertex, a run along part of an edge) are cut as `plane_cut` cuts. The segment is split where it meets the boundary and each interior part cut; a part lying along an existing edge reuses that edge. The U-shaped face `(0,0) (3,0) (3,3) (2,3) (2,1) (1,1) (1,3) (0,3)` cut from (0,1) to (3,1) splits into three pieces, as `plane_cut` gives. Anything still impossible is refused in the knife's own facts-only words, naming the point or segment, never a face id that does not exist. Built in `Knife.Runs`: after the proper-crossing check, the segment is split at each corner of the face within the tolerance of it between its ends; each part's midpoint decides it (along an edge: kept, passed to `FaceSplit`, which skips it but makes its ends' vertices; inside: a cut; outside or in a hole: refused, `the segment from points[i] to points[j] leaves faces[f] at [x, y, z]`, the corner it leaves at). `FaceSplit`'s `cuts[c]` errors map back to the path segment the part is of, and `Edges` gives each path segment's two ends. Tests `ASegmentMeetingTheBoundaryBetweenItsEndsIsCutAsPlaneCutCutsIt` (the U, the grazed reflex vertex, the W's valleys, the off-centre notch, each against `plane_cut`'s pieces) and `ASegmentThatLeavesTheFaceThroughAVertexIsRefusedNamingWhere`; the break is in `artifacts/review/RV-31/breaks-A.log`.

**Third agent: Opus (claude-opus-5-5).** The render-path breaks, the GPU frames, the review (rejected; the person's rulings on it are in the fourth agent's notes).

- **GPU run of `f23c78f`** (the last code commit; every suite's `output.log` names `carve@f23c78f3352d`), fetched by job id into `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-67/frames-f23c78f/`, hashed in its `all.sha256` (135 frames): all 133 of T-65's `frames-aae1140/all.sha256` byte-identical; new `scenario-cuts/cuts/frames/knife-slab-wireframe.png` `175724fa4cb2...` and `knife-slab-recess.png` `84957d9dc2db...`, the same bytes as the local run. T-66 had not landed on `origin/milestone/M-5`. The local run's frames are kept in `.../T-67/local-f23c78f/`.
- **Render-path breaks** (`C:/Users/andre/AppData/Local/Temp/t67/breaks4.py`; each applied alone to the drawing code only, so get_mesh is right and the frame wrong, built with warnings as errors, the Cuts scenario run here, restored; log in `breaks.log`, the uncaught frames in `.../T-67/breaks-render/`):

  | Check | Break | Result |
  |---|---|---|
  | `knife-slab-wireframe`: each knifed edge in sight is orange (`Line`) | the highlight skips edges on a hole loop (`LookMeshes.OrangeLines`) | 6 fail (the hexagon's edges, e.g. `the knifed edge 8-9 ... the closest pixel to (255, 196, 89) is (85, 87, 89)`) |
  | `knife-slab-wireframe`: each other edge in sight is not orange (`NotLine`) | the highlight adds every edge of a holed face | 6 fail (e.g. `the unknifed edge 3-7 ... should not be drawn in that colour`) |
  | both frames: each probe is the mesh, in its face's colour (`Look`) | a holed face is not drawn (`MeshBridge`, its triangles collapsed) | wireframe 28 fail, recess 14 fail (`the front ... is the background, expected the mesh`) |
  | `knife-slab-recess`: `Look` and `Expect` | the drawn triangulation ignores the holes, the outline filled (`Shading.Triangles`) | **0 fail in either knife frame** (47 in other stages' frames). The wireframe frame is byte-identical to the good one (one fill colour); the recess frame changes (the pocket covered flat) |
  | `knife-slab-recess`: `Look` and `Expect` | the recessed face drawn at its old depth (z 0.15 drawn at 0.2, `MeshBridge`) | **not caught: the scenario passed.** The frame changes, 810 pixels round the hexagon (`breaks-render/R5-knife-slab-recess.png`; the same bytes as the filled-hole break's) |

  So each knife frame's pixel checks fail on at least one render break, but **the recess frame's pixel checks cannot see the pocket itself**: the probes at the recess and on the front all show faces of the same normal and so the same shade, and no probe lands on a pocket wall, so a pocket drawn flat passes. Not fixed here: a new probe or check is the person's call (ruled on: the fourth and fifth agents' notes).

**Fourth agent: Opus (claude-opus-5-5).** The person's rulings on the rejected review, the rebase onto T-66, the colours; stopped for a ruling on shade prediction (below).

- **The person's rulings (2026-11-12), on the rejected review:**
  1. `knife-slab-recess` must show the cuts: in `KnifeStage`, colour the faces the knife cut out with `set_colour`, through MCP like any other call, the door piece `#6b4423` (the gatehouse's door brown) and the hexagon's inner face `#4a7a9b`, before the recess so the pocket floor carries the colour. The frame stays shaded; no other frame changes.
  2. Probe the pocket's walls, each checked against the wall shade the ray test predicts, at the scenario's existing shade tolerance (no new threshold), shown failing on the flat-pocket breaks (R4 and R5). Also probes on the two coloured faces, each against the shade the ray test predicts for its colour at the existing tolerance, with a break (the colour not applied, or on the wrong face); if the existing ray test cannot predict a coloured face's shade, stop and report.
- **Rebased onto `origin/milestone/M-5`** (T-66's `loop_cut` and `LoopCutStage`): two conflicts, both kept: `Program.cs`'s tool list (`LoopCutTool`, then `KnifeTool`) and `CutsStages.cs` (`LoopCutStage`, then `KnifeStage`). Nothing else broke: built with warnings as errors, Core unit 715 passed, Server 666 passed, so no fix commit was needed.
- **Ruling 1 done** (`5dc6c9f`): after the wireframe frame, the door piece (the one face whose corners are all in the door's rectangle on the front) and face 6 are coloured, then the recess. The walls the extrude adds take face 6's blue (`set_colour`'s rule). The probes' checks are unchanged (each face's shade read at its first probe).
- **Local Cuts run of `5dc6c9f`:** passed. Against T-66's `frames-60ed146/all.sha256`, all 14 other Cuts frames byte-identical, `knife-slab-wireframe` kept `175724fa4cb2...`, `knife-slab-recess` now `7847fed2672b...` (the door brown, the hexagon pocket blue). Frames and hashes in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-67/local-5dc6c9f/`.
- **Stopped for a ruling (folded from its Hand-off):** the Cuts scenario's ray test predicts which face a pixel shows (`Solid.Seen`), not its shade; every face's shade was read from the frame at its first probe (`Shades`), so a wall or coloured probe checked that way calibrates itself and cannot fail on a flat pocket or a missing colour, and no grey face shares the normal of the pocket's two slanted walls. The one rule in the code that predicts a coloured shade is the Crate scenario's (T-30, `CrateColours`); on `5dc6c9f`'s local frame it held within `Exact`. No GPU suites were submitted by the fourth agent.

**Fifth agent: Opus (claude-opus-5-5).** The shade prediction on the person's rulings, its breaks, the GPU run of `8481a22`, the review.

- **The person's rulings on shade prediction (2026-11-12):**
  1. **Coloured faces** are predicted with the Crate scenario's rule (T-30, `CrateColours`): in linear light, the coloured pixel is the grey pixel of a face with the same normal in the same frame, times the colour, divided by the default grey (0.72, 0.72, 0.70). The tolerance is Cuts' own 2 steps per channel (`Exact`), not Crate's 8%. Applied to the door piece and the pocket floor (reference: the front face) and to the pocket's bottom wall (reference: the slab top). The Crate code reused or linked rather than copied, without touching the Crate scenario's behaviour or frames.
  2. **The two slanted pocket walls** have no same-normal reference: each slanted-wall probe must differ from the pocket floor's shade by more than 2 steps in at least one channel.
- **The rule, linked:** `scenarios/Carve.Scenario.Crate/ColourRule.cs` is new, holding the rule's parts (`Grey`, `Scale(hex)` = colour / grey per channel in linear light, `Linear`), lifted out of `CrateColours` with the same arithmetic (the same float division), and `CrateColours` now calls it; the Cuts project links the file (`Carve.Scenario.Cuts.csproj`). Crate's checks, tolerance and frames are unchanged (its frames hashed below).
- **The checks** (`KnifeStage.Predicted`, on `knife-slab-recess` only, after `Look`): the reference grey pixels are face 1 (the front) at the first probe `Seen` is sure of, and face 3 (the slab top) at the first top probe `SeenNarrow` is sure of; each prediction is that pixel through the rule, encoded back to sRGB. The door probes (face: the door piece, brown, predicted (97, 61, 31) here) and the recess probes (face 6, blue, (67, 111, 141)) against the front; three new probes on the bottom wall (y = 0.9, z = 0.175, a third, a half and two thirds along; blue, (69, 115, 146)) against the top; three on each slanted wall (lower left, from [1.1, 1.2] to [1.3, 0.9]; upper left, from [1.3, 1.5] to [1.1, 1.2]), each to differ from the pocket floor's predicted shade by more than 2 in a channel. Each probe the ray test is sure of must show the expected face (walls found from get_mesh by their normals), and each group needs at least one sure probe. The wall probes are in their own list, so the wireframe frame's checks are untouched.
- **`Solid.SeenNarrow`** (new, in `CutsGeometry.cs`; `Seen` unchanged): the slab's top and the pocket's walls are 3 to 6 pixels across here, and `Seen`'s three-pixels-round agreement was never sure of any of them (the first run failed on `no probe surely shows the top`). `SeenNarrow` asks the eight neighbouring pixels instead, and is used only for the top reference and the wall probes. **The person's ruling (2026-11-12): `SeenNarrow` is kept**, as it is, for the top reference and the wall probes only; noted with it, the lower left wall clears the pocket floor by only 3 to 4 steps (below).
- **Seen in the local frame:** the lower left wall (69, 114, 145) and the upper left wall (35, 62, 81) against the floor's (67, 111, 141): the lower left wall's margin is 3 to 4 steps, so it passes, but narrowly (it happens to be lit as the +Y wall is).
- **Breaks** (`C:/Users/andre/AppData/Local/Temp/t67/breaks5.py`, each applied alone, built with warnings as errors, the Cuts scenario run here on `8481a22`, restored; log appended to `breaks.log`, under `fifth agent`). Counts are of the new checks' failures in `knife-slab-recess`:

  | Check | Break | Result |
  |---|---|---|
  | the pocket floor and the bottom wall, predicted | R4 rerun: the drawn triangulation ignores the holes (`Shading.Triangles`) | 59 checks fail (before: 0 in the knife frames); the pocket floor 9 of 9 (`(201, 201, 198)`, expected `(67, 111, 141)`), the bottom wall 3 of 3; the slanted walls 0 (drawn grey, unlike the floor) |
  | the slanted walls differ from the floor; the bottom wall, predicted | R5 rerun: the recessed face drawn at its old depth (`MeshBridge`) | 7 fail (before: the scenario passed): the lower left wall 3, the upper left wall 1 (`(67, 110, 141), within 2 of the pocket floor's (67, 111, 141) in every channel`), the bottom wall 3 |
  | the door piece, the pocket floor, the bottom wall, predicted | the colour not applied (`MeshBridge`, every submesh grey) | 21 fail: the door 9, the floor 9, the bottom wall 3 (`(208, 208, 206)`, expected `(69, 115, 146)`) |
  | the door piece and the pocket floor, predicted | the colour drawn on the wrong face (`MeshBridge`, each face takes the next face's colour) | 18 fail: the door 9 (drawn blue), the floor 9 (drawn brown) |
  | the door piece, the pocket floor, the bottom wall, predicted | the stage colours the wrong faces (brown on the hexagon, blue on the door) | 21 fail: the door 9, the floor 9, the bottom wall 3 |

  So every new probe group fails on at least one break: the slanted walls on R5 only.
- **Local Cuts run of `8481a22`:** passed. Against T-66's `frames-60ed146/all.sha256`, all 14 other Cuts frames byte-identical; `knife-slab-wireframe` `175724fa4cb2...` and `knife-slab-recess` `7847fed2672b...`, the same as `5dc6c9f`'s (nothing in the render changed). Frames and hashes in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-67/local-8481a22/`.
- **GPU run of `8481a22`** (the last code commit; every suite PASS, jobs `J-20261112-181215-8d7e7a` to `J-20261112-181228-0119be`, viewer-cube carried; each scenario's `output.log` names `carve@8481a222c101`), fetched by job id into `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-67/frames-8481a22/`, hashed in its `all.sha256` (137 frames). Against T-66's `frames-60ed146/all.sha256` (135): all 135 byte-identical (Crate's 44 among them, so the `ColourRule` lift changed none), none missing; two new, the same bytes as the local runs: `cuts/cuts/frames/knife-slab-wireframe.png` `175724fa4cb2...` and `knife-slab-recess.png` `7847fed2672b...`. Nothing had landed on `origin/milestone/M-5` since T-66, so no rebase.

**Sixth agent: Opus (claude-opus-5-5).** The second verify, the review, the finish (the fifth agent's Hand-off folded in here).

- **The first verify (`285b092`) failed** one test outside this member: `server-regression` (job `J-20261112-182321-86f7f5`, on Xellotath, 20 m 8 s against about 5 m usually), 1 of 387, `HoledFaceToolTests.A_bad_hole_is_a_tool_error_naming_it` (the `faces[0].hole` case), `System.TimeoutException: No response to 1 after 00:00:30` at server start-up, a slow runner, not an assertion. No timeout changed.
- **The second verify (`285b092`): VERIFY PASS**, `server-regression` ran 299 s on Mantorok, 387 of 387; `scenario-cuts` and `scenario-character` ran too, the rest carried.
- **Review:** `knife-slab-wireframe` and `knife-slab-recess` from `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-67/frames-8481a22/`, through `mcp__waystone__review`, the labels saying the recess frame now colours the door brown and the hexagon's pocket blue, per the person's note: **approved**, no notes.
- Nothing had landed on `origin/milestone/M-5`, so no rebase and no re-run of the frames.
