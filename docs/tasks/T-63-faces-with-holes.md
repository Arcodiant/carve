---
description: >-
  A face is an outer loop plus holes: define_mesh's object form, topology, triangulation, check, select, render, export and Carve file version 4
---
# T-63 — Faces with holes

**Status:** Open

**Pri:** P1

**Milestone:** M-5

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

A face can have holes. An agent defines a plate with a window in it, sees the opening in a render, and saves, reopens and exports it. Every later cut builds on this face.

## Scope

**Core: the face.**

- A face is an outer loop plus zero or more hole loops, each of 3 or more vertex ids.
  - Make the shape explicit in `PolygonMesh`: for example a `Face` with `Loop` and `Holes`.
  - Do not add a side table that old code can ignore. Every consumer of `Faces` is visited, and none of them silently drops the holes.
- **Validation in `PolygonMesh.Create`.** Each of these is an error naming `faces[n].holes[k]`, or `faces[n].holes[k][i]` for one corner:
  - a hole of fewer than 3 vertices;
  - a hole wound the same way as the outer loop (seen from outside, holes are clockwise);
  - a hole not strictly inside the outer loop, or touching it;
  - two holes overlapping or touching;
  - a vertex used twice across the loops of one face.

  As today, a hole corner off the face's plane is a non-planar **warning**.
- **`MeshTopology`.** Hole edges are edges of their face. `EdgeUse` says which loop and which corner (hole edges are crossed opposite to the outer loop, so a hole edge shared with an inner face is manifold). Each face's edges are listed outer loop first, then each hole in order.
- **`Triangulator`.** A holed face triangulates by bridging each hole into the outer loop (the rightmost hole vertex to a visible outer vertex, in the standard way), then ear clipping. The triangles cover the outer loop minus the holes. Plain faces give exactly the triangles they give today.
- **`MeshQuery` and `MeshHealth`.**
  - A face's area is the outer loop's area minus its holes'. Normal and position come from the outer loop.
  - `select` and `check` see hole edges like any other edge, so a hole with no face inside it shows as open edges.
- **`MeshEdits`.**
  - `move` moves hole corners like any other vertex, and refuses a move after which a hole would leave its face or cross another loop.
  - `extrude` on a holed face makes walls on the outer loop and on every hole loop: a tube. The cap keeps the face's id, with its holes on the new rings.
  - `extrude` with `inset > 0` on a holed face is refused, naming the face.
  - Walls take the face's colour and groups (`FaceStyle.Carry`).

**Server.**

- **`define_mesh`.** A face is either a plain array of vertex ids, or the object form `{"loop": [...], "holes": [[...], ...]}`. Plain arrays mean exactly what they mean today. The schema allows both (an `anyOf` of the two).
- **`get_mesh`** reports a holed face in the same object form.
- **The text.** The description says how holes are wound and gives a worked example: a 2 x 2 plate with a 1 x 1 square hole. The worked-example test parses it, as for every tool.
- **`render`.** Draws the derived triangles, so the opening shows the background. The wireframe draws hole edges. Labels put a face's id on its outer loop's area minus its holes, not inside a hole.
- **Export.** Triangles go to the `.glb` as now.

**The Carve file.** This task owns the new file version for all of M-5.

- Take the next version after the newest on `main` at dispatch. At planning that is 4, after T-42's 3. If M-6's UVs have taken 4 by then, take 5. Say which in As built.
- In this version a face is a plain array or the `{"loop", "holes"}` object, as `define_mesh` takes it.
- Reserve the tombstone form that T-64 fills: a `null` in `vertices` or `faces`. Write it into the format's doc comment as reserved, and refuse it with an error until T-64 lands.
- Every older version still reads. `Write` always writes the new one. Files read back bit-exact.

**Tests.**

- `unit`, each with a break shown failing and the table of breaks in As built:
  - each validation error;
  - triangulation of a square with one square hole, two holes, and a concave outer loop with a hole: triangle area equals outer minus holes, and no triangle centroid lies in a hole;
  - topology of a plate whose hole is filled by an inner face: closed, every edge manifold;
  - the plate without the inner face: the four hole edges are open;
  - extrude of a holed face: the walls and counts;
  - inset refused;
  - a move refused because a hole leaves its face;
  - the file round trip, plus a version-1, -2 and -3 file still reading.
- `regression`: `define_mesh` with the object form through the client, and `get_mesh` returning it. Also an export of the plate whose triangle count is checked.

**Content: a new scenario, `scenarios/Carve.Scenario.Cuts`,** and suite `scenario-cuts` in `tests.json`, added to `Carve.slnx`.

- It is built for later tasks to add to. Each stage is a class in its own file (`HolesStage.cs` here) and starts from its own `define_mesh`, so stages share no ids. The stages run in the order of one registration list, `CutsStages.cs`. A later task adds its own file and one line to that list.
- **This task's stage, the holed plate:** a 2 x 2 plate in the XY plane, 0.2 thick (a box with a 1 x 1 square hole through the front face, and the inner square kept as its own face).
  1. The plate with the inner face, from direction `[0.6, 0.35, 1]`.
  2. The inner face then removed by redefining the plate without it, so the opening shows the back face's inside culled and the background beyond. From `[0.6, 0.35, 1]` and orthographic `[0, 0, 1]`.
  3. The wireframe from `[0.6, 0.35, 1]`.
- Every frame has positive pixel checks worked from the geometry by a ray test, as `ShedGeometry.cs` does: the hole's centre is background in frame 2 and plate shade in frame 1, and the corners are checked as well as the middles. Each check is shown failing on a break.

## Notes

- **Decided by the person at planning (2026-11-09):**
  - the object form `{"loop", "holes"}`;
  - holes wound opposite to the outer loop, with a wrong winding refused;
  - a holed face extrudes as a tube, and an inset on one is refused;
  - the file version is the next free one at dispatch.
- **Design-heavy, and it decides the face shape every M-5 task builds on.** Write down in As built:
  - the face type and its API;
  - how `EdgeUse` names a hole corner;
  - the file form, and the reserved `null`.

  Opus.
- **Before writing code, read what has landed since planning:** T-42's `hard_edges` and shading, T-43's `subdivide`, and anything from M-6. Every edit that has landed must handle holed faces:
  - subdivide on a holed face: if Catmull-Clark of a holed face is not obvious, refuse it with an error naming the face, and say so;
  - smooth shading's corner normals: hole corners join their neighbours like any corner.
- **Open rules: stop and ask.** Any validation rule beyond the list above. A tolerance for "strictly inside". A change to how a plain face triangulates. Holes in subdivide, if refusing them does not fit.
- Every existing frame must stay byte-identical by hash: cube, crate, shed, export, character and the soft character. Check by hash before and after, and list them.
- Plan for two agents. If the topology and triangulation fill the first, hand off at the server half with the As built notes written.
- Evidence (frames, `breaks.log`) goes to the milestone worktree's `artifacts/review/T-63/` by its absolute path, given in the brief, not to this task's worktree.
- Run `finish` from inside the worktree and leave the folder.
