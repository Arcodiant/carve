---
description: >-
  A new edit that cuts selected faces into triangles on their own plane no longer than max_edge, keeping holes and moving no existing vertex, with the border points added to the neighbours' loops so the mesh stays closed
---
# T-237 — tessellate: split selected faces into flat triangles down to max_edge

**Status:** In Progress

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

### Hand-off

Ran on Claude Opus 5.5; handed off at the 60-call checkpoint, everything below committed on `task/T-237`.

- **Done.** Core: `src/Carve.Core/Tessellation.cs` (`Tessellation.Tessellate(mesh, maxEdge, faces?)`) on a new
  `src/Carve.Core/ConstrainedDelaunay.cs` (incremental Delaunay with Lawson flips, Sloan constraint recovery by
  flipping, parity classification for holes, midpoint refinement of any inner edge over `max_edge`). Edge points by each
  edge's own length, carried into neighbours' loops with `FaceSplit.Insert` (made internal); inner points an
  equilateral lattice at `max_edge`, kept 0.5 `max_edge` clear of the loops, positioned and blended (UVs, weights) from
  the face's own ear-clip triangles; provenance as `FaceSplit`'s, so groups, colour, material, hard edges and seams
  carry. Server: `src/Carve.Server/TessellateTool.cs`, registered after `displace` in `CarveServer.cs`; `created`
  as subdivide's blocks. Fixture: `TestMeshes.Dais()` (with `DaisVertices`, `DaisFaces`, `DaisGroup`, `DaisMaxEdge`,
  `DaisDefineMesh()` for the server tests) in `tests/Carve.Core.Tests/TestMeshes.cs`, which `Carve.Server.Tests`
  links. Tests: `tests/Carve.Core.Tests/TessellateTests.cs` (11, pass: quad, 127 x 2 strip, hole, border, UVs,
  attributes and hard edges, refusals, the dais at 0.75 closed with no warning, every vertex unmoved, only 0 and 1
  split, no angle under 10 degrees) and `tests/Carve.Server.Tests/TessellateToolTests.cs` (10, pass: example and undo,
  each error leaves the mesh, the dais through the tools). `VerbosityToolTests` row and `PartToolsListTests`'
  part-space list take `tessellate`; those classes, `HelpToolTests` and `EditToolDefinitionTests` pass. Content:
  `scenarios/Carve.Scenario.Cuts/TessellateStage.cs` and its line in `CutsStages.cs`; it builds.
- **Left.** README's tool list (not yet touched); the `TessellateStage` run (`cairn task T-237 verify`, which runs
  `scenario-cuts`) and the frame review of `tessellate-slab-wireframe` by a subagent; any `CLAUDE.md` Layout line for
  the stage (the Cuts row lists each stage file); `### As built`; `cairn lint`; `finish`.
- **Not checked.** The full `core-unit` and `server-unit` suites (only the classes above were run); the stage has not
  run, so its probe count (Look notes under 40) and line checks are unproven.
- **Surprising.** On the strip the 26 edge-point rectangles are co-circular and their diagonals (5.28) exceed
  `max_edge` 5, so refinement puts one point on the middle line of each: the edges are cut along the length only, but
  points are added inside. Colour and material are exclusive on a face (`WithColour` drops the material).
