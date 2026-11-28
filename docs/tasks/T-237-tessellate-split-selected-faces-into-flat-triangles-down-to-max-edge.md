---
description: >-
  A new edit that cuts selected faces into triangles on their own plane no longer than max_edge, keeping holes and moving no existing vertex, with the border points added to the neighbours' loops so the mesh stays closed
---
# T-237 — tessellate: split selected faces into flat triangles down to max_edge

**Status:** Done

**Pri:** P2

**Milestone:** M-41

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, server-unit, scenario-cuts

## Goal

An agent can make part of a hard-surface mesh dense enough to displace without changing its shape: `tessellate`
cuts the selected faces into triangles lying on their own planes, none with an edge longer than `max_edge`, moves no
existing vertex, and keeps the mesh closed.

## Scope

- A new MCP tool `tessellate`, one edit and one undo step, built on a Core operation in `src/Carve.Core`, in the
  style of the other edit tools (`MeshToolSupport`: `mesh`, `verbosity`, part space, the shared report fields).
- Arguments: `max_edge` (required, metres, above 0, no cap on how fine); `faces` or `group` (a face group from
  `define_group`), as `displace` takes them, not both; neither means every face.
- Every edge of a selected face is split into equal segments no longer than `max_edge`, by its own length, so a
  long thin face (a 127 x 2 strip) is split along its length only.
- Points are added inside each selected face (a lattice at about `max_edge` spacing in the face's plane, clear of
  its loops, is the obvious choice) and the face is triangulated with those points, its edge points and its holes
  kept as holes. Carve's `Triangulator` only ear-clips existing corners and there is no Delaunay code in `src`; a
  constrained Delaunay triangulation, or an equivalent that gives no triangle edge over `max_edge` and no sliver
  that `check` warns about, is this task's to build.
- Output is triangles, not quads: `displace` moves corners by different amounts, and only a triangle stays planar.
- An edge shared with an unselected face gets the same new points inserted into that face's loop, in order, so
  there is no T-junction. That face is not split and none of its vertices move.
- Each face keeps its id on its first piece (reported in `split`, as other edits do); every piece keeps the face's
  groups, colour and material; UVs at new points are interpolated from the face's own corners.
- `created` is reported as `subdivide` reports it, `{vertices:{first, count}, faces:{first, count}}`, since it can
  be thousands.
- Errors, each a tool error that changes nothing: `max_edge` missing, not a number or not above 0; an unknown face
  or group; both `faces` and `group`; an argument it does not take.
- Tool description and schema, with a worked example; README's tool list.
- Content: a stage in the Cuts scenario (`TessellateStage.cs`, one line in `CutsStages.cs`): a holed slab with a group tessellated, seen in the wireframe, its pixels worked from the geometry as the other stages' are.
- Tests (Core and Server): a planar quad, a long thin strip, a face with a hole, a selection bordering unselected
  faces (border loops gain points, nothing else changes, mesh stays closed), UV interpolation, attributes kept,
  undo. And the milestone's fixture: a slab shaped like Shoalward's `D-DAIS` (flat top with holed faces, pegs,
  sharp edges, in mm), top faces in group `wear`, at `max_edge` 0.75: closed, no new warnings, every vertex
  outside the group unmoved, no face outside the group split.

## Notes

- Raised from the Shoalward terrain project's request (2026-11-27); planned in discussion with the person, who
  chose to reserve it as part of M-41.
- A new tool rather than a `subdivide` mode: `subdivide` is whole-mesh, set by `levels`, and smooths; this takes a
  selection and `max_edge` and does not smooth. Recommended in discussion; not separately ruled on.
- Triangles are required, not just allowed (see Goal).
- Not tested by the requester: `subdivide` with every edge hard as a stand-in, and `subdivide` on holed faces.
- Ruled by the person on 2026-11-27 (RV-100, round 1): **`split` is short at summary.** At `summary` each face's
  `pieces` in `split` is `{count, first}` (the first 10 ids, ascending), as T-239 made `moved`; the whole list only at
  `full`.
- Ruled by the person on 2026-11-27 (RV-100, round 1): **a budget and a faster filter.** `tessellate` refuses, as a
  tool error that changes nothing, a call predicted to add over 200,000 new vertices, the error and the description
  giving the predicted count and the budget. The prediction is worked before any cut, from edge lengths and face areas
  over `max_edge`, so a `max_edge` too fine for a double (1e-14 on a quad at 1000..1001) is refused at once. The
  lattice filter is made about linear, keeping exactly the points it kept before. This replaces the Scope's "no cap
  on how fine".
- Answered by Waystone's filter, not the person, on 2026-11-27 (RV-101, round 2), for the person to confirm or
  overturn: **`split` itself is short at summary.** At `summary` `split` is `{count, first}`, the number of faces cut
  and the first 10 entries, each `{face, pieces:{count, first}}`; the whole list only at `full`.
- Answered by Waystone's filter, not the person, on 2026-11-27 (RV-101, round 2), for the person to confirm or
  overturn: **thin rotated strips.** In M-41 a test pins the refusal of a thin face turned off the axes and its message
  is a plain one ("is too thin for max_edge ..."), a tool error that changes nothing; the triangulation's tolerance is
  not changed here. The cause goes to an off-milestone bug, B-15.
- RV-101's third finding (the prediction under-counting narrow faces) was fixed within the person's ruling 2 above:
  the prediction counts the strip refinement adds along the loops, and a hard stop refuses a call whose made count
  passes the budget anyway, before anything is committed.

### As built

Ran on Claude Opus 5.5 (two agents; the first handed off at its 60-call checkpoint).

- Core: `src/Carve.Core/Tessellation.cs` (`Tessellation.Tessellate(mesh, maxEdge, faces?)`) on a new
  `src/Carve.Core/ConstrainedDelaunay.cs`. That file does incremental Delaunay with Lawson flips, recovers the
  constraints by Sloan's flipping, classifies holes by parity, and splits any inner edge over `max_edge` at its
  midpoint. Each edge's points are spaced by its own length and carried into the neighbours' loops by
  `FaceSplit.Insert`, which is now internal. The inner points are an equilateral lattice at `max_edge`, on the plane
  basis of `PlaneGeometry.Basis` (so on a face square to z its rows run along y), kept half a spacing clear of the
  loops. Positions, UVs and weights are blended from the face's own ear-clip triangles. Provenance works as in
  `FaceSplit`, so groups, colour, material, hard edges and seams carry over.
- Server: `src/Carve.Server/TessellateTool.cs`, registered after `displace`; `created` is reported in subdivide's
  blocks. `ToolHarnessTests` now counts 45 tools, and the `VerbosityToolTests` row and `PartToolsListTests`' part-space
  list include `tessellate`.
- Fixture for T-238: `TestMeshes.Dais()` in `tests/Carve.Core.Tests/TestMeshes.cs`, with `DaisVertices`, `DaisFaces`,
  `DaisGroup` (`wear`), `DaisMaxEdge` (0.75) and `DaisDefineMesh()` for the server tests, which link the file.
- Tests: `tests/Carve.Core.Tests/TessellateTests.cs` (quad, 127 x 2 strip, hole, border, UVs, attributes, refusals,
  the dais: closed, no warning, every vertex unmoved, only faces 0 and 1 split, no angle under 10 degrees) and
  `tests/Carve.Server.Tests/TessellateToolTests.cs` (the example and undo, each error leaves the mesh, the dais
  through the tools).
- Content: `scenarios/Carve.Scenario.Cuts/TessellateStage.cs` and its line in `CutsStages.cs`. A holed slab has its
  front group tessellated at 0.25 and is seen in the wireframe frame `tessellate-slab-wireframe`. At some slopes
  (upright, 45 degrees, 2:1, flat) 4x MSAA draws a 1-px line on 3 of its 4 samples at most, so the shared `Line` rule
  (within 10 of 237) failed 17 piece edges at 212. Tipping the slab only moved the failures to other edges. The person
  chose option A (Waystone review, round 1, 2026-11-27): in this stage only, a piece edge also passes within 10 of the
  3/4-coverage blend over the fill. `ShedChecks.Line` is unchanged; `ShedChecks.Closest` became internal so that the
  stage can use it.
- Frame review (a subagent): the frame does what is meant, with no triangles in the socket, no lines across the sides
  or walls, and no gaps. It pointed out uneven density (finer between the socket and the slab's edge, where the lattice's
  clearance leaves room for refinement only) and a few narrow triangles by the socket. These are looks only: the stage
  and the dais both pass `check` with no warning.
- README: `tessellate` added to the edit tools that take `verbosity`, and a bullet with its worked example before
  `displace`'s. CLAUDE.md's Cuts Layout row names `TessellateStage.cs`.
- On the strip, the 26 edge-point rectangles are co-circular and their diagonals (5.28) exceed `max_edge` 5, so
  refinement puts one point on the middle line of each. The edges are still cut along the length only. Colour and
  material are exclusive on a face (`WithColour` drops the material).
- RV-100, round 1 (Claude Opus 5.5, one agent), after the person's two rulings of 2026-11-27 above:
  - The Cuts scenario's `TessellateStage` reads every piece, so it now asks for `verbosity: "full"`; its mesh and
    frames are as before. `DefineMeshDefinitionTests`' published schema carries the new verbosity text.
  - `split` at summary: `MeshToolSupport.Split(split, level)` gives each face's `pieces` through `Moved`, used by
    `tessellate` only (`subdivide` and `displace` answer `split:[]` or few pieces and keep the whole list).
  - Budget: `Tessellation.Budget` (200,000) and `Tessellation.Predicted(mesh, faces, maxEdge)`: each distinct edge's
    `ceil(length / max_edge) - 1` points plus each face's area over `max_edge^2 * sqrt 3 / 2`, in doubles. Over it the
    call is refused on `max_edge` with "about N new vertices, over the budget of 200,000". On a unit quad the budget
    falls between `max_edge` 0.0025 (about 186,000 predicted, 191,085 made) and 0.002.
  - Filter: `Tessellation.Lattice` works each lattice row's loop crossings once (the same expression
    `PlaneGeometry.InPolygon` uses, so the same parity) and buckets the loop segments in a grid of half the spacing,
    testing `SegmentDistance` only on the 5 x 5 cells round a point. The unit quad at 0.0025 in Release took 9.7 s
    before and 3.1 s after (the rest is the triangulation and blending). A test holds it to the old rule, copied into
    the test, on a holed square, two holes, a concave comb and a slanted pentagon at five spacings. Core reaches
    `Carve.Core.Tests` through `InternalsVisibleTo` for this.
  - Tests added: an unselected face's hole on the border (the dais's bottom around a peg), UVs across a seam, the
    budget refusal (Core and tool), the empty `faces: []`. The "cannot be tessellated" refusal is in the description
    but untested: no face `PolygonMesh.Create` accepts was found to trigger it (a repeated corner is refused there).
- RV-101, round 2 (Claude Opus 5.5, one agent):
  - `split` at summary (the filter's answer above): `MeshToolSupport.Split(split, level)` gives `{count, first}` with
    the first 10 entries through the same `Moved` for each entry's `pieces`; at `full` the whole list as before. The
    Cuts `TessellateStage` already asks for `full` and is unchanged. `TessellateToolTests` tessellates a whole mesh of
    40 unit quads at 0.3 and holds the summary result to 2,000 characters; `VerbosityToolTests` reads the short `split`
    on tessellate only (other tools' `split` is the same at both levels).
  - Prediction: `Predicted` adds `Tessellation.StripPoints` (3.5) points per `max_edge` of each selected face's loops.
    Measured (Release) the strip's points per `max_edge` of loop are 0.8 (strips 1.6 `max_edge` wide) to 3.0 (the unit
    quad at 0.0025). On RV-101's table the prediction is now 191,948 (made 191,085), 217,933 (196,325), 542,387
    (268,339) and 408,596 (368,134), the made counts RV-101's, the algorithm being unchanged; so the 40 x 0.01 strip,
    which makes 196,325, is now refused. `The_prediction_is_not_under_the_vertices_made` holds it on scaled-down copies
    (a 0.25 quad and strips 2 x 0.01, 2 x 0.004, 3 x 0.01 at 0.0025, each under 0.7 s in Debug).
  - Hard stop: `Tessellate` stops once its new vertices would pass the budget (after the edge points, the lattice, or
    inside `ConstrainedDelaunay.Refine(limit, maxPoints)`, which now returns false at its cap) and refuses on
    `max_edge` with "more than 200,000 new vertices, over the budget of 200,000" before anything is committed. An
    internal overload takes the stop so a test can set it under a call's made count.
  - Thin faces (the filter's answer above): any `InvalidOperationException` from the triangulation is now
    `faces[N]: is too thin for max_edge M; give a larger max_edge, or cut the face across first`, pinned on a 20 x 0.002
    quad turned 10° at 0.002 (`A_thin_strip_turned_off_the_axes_is_refused_plainly`). Bug B-15 holds the cause.
