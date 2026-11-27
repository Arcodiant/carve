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
