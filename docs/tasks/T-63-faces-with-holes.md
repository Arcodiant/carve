---
description: >-
  A face is an outer loop plus holes: define_mesh's object form, topology, triangulation, check, select, render, export and Carve file version 4
---
# T-63 — Faces with holes

**Status:** In Progress

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

### As built

First agent: Opus (claude-opus-5-5). The Core half is in; tests of the holed paths, and the server half, are not done yet (see Hand-off).

- **The face type.** `Carve.Core.Face` (`src/Carve.Core/Face.cs`), immutable: `Loop` (outer, CCW from outside), `Holes` (each CW from outside), `HasHoles`, `Corners` (every vertex id, outer loop first, then each hole in order: the face's corner numbering), `LoopAt(hole)` (-1 is the outer loop), `Locate(corner) -> (Hole, Index)`, `CornerAt(hole, index)`, `CornerOf(vertex)`, `Neighbours(corner)` (the previous and next vertex in the corner's own loop), `Map(f)`, and the names `LoopName(face, hole)` (`faces[n]` or `faces[n].holes[k]`) and `CornerName(face, hole, i)` (`faces[n][i]` or `faces[n].holes[k][i]`). It is deliberately not a list of ints, so every consumer of `PolygonMesh.Faces` (now `IReadOnlyList<Face>`) failed to compile and was visited. `int[]` converts implicitly to a plain `Face`; equality is structural. `PolygonMesh.Create` keeps its plain-loop overload and gains one taking `Face`s; `Edit` takes `Face`s for reshaped and added faces. New `PolygonMesh.FaceArea` (outer less holes); `FaceNormal` and `FaceCentre` use the outer loop.
- **`EdgeUse`** is `EdgeUse(Face, Hole, Corner, LoToHi)`: `Hole` is -1 for the outer loop, else the hole's index; `Corner` is the index within that loop; `CornerName` gives `faces[n][i]` or `faces[n].holes[k][i]`. Hole edges' direction is the hole's own (clockwise), so a hole edge shared with an inner face is manifold. `FaceEdges(f)` lists the outer loop's edges, then each hole's, in corner order.
- **Validation** (`PolygonMesh.Create`, helpers `Triangulator.HoleProblems` and `Triangulator.Bridge`): per loop, fewer than 3 (`faces[n].holes[k]`), out of range and a vertex repeated across the face's loops (`faces[n].holes[k][i]`, message `index v repeats <first corner's name>`); then the outer loop takes the plain face's crossing and zero-area checks; then each hole: winding the same way as the outer loop (2D signed area in the outer loop's plane > 0), not strictly inside the outer loop (an edge meeting an outer edge, or its first vertex outside), and overlapping or touching an earlier hole; then the whole face, bridged, takes the plain face's crossing, zero-area, triangulation and covers-twice checks, an edge named by its starting corner. **Touching uses the existing `Triangulator.CollinearTolerance` "on" band (relative to the outer loop's size), the one `FindCrossing` uses; no new tolerance was added. The spec marks "a tolerance for strictly inside" as open: this needs the person's ruling.** A self-crossing hole is not a new rule: it is caught by the plain face's crossing check on the bridged outline. A hole corner off the plane is a non-planar warning (`MeshReport`, the plane is the outer loop's).
- **Triangulation.** Plain faces take exactly today's path (the whole existing core-unit suite passes unchanged on geometry). A holed face is bridged: holes rightmost-vertex first, each rightmost vertex joined to the nearest vertex of the outline so far that it sees (inside that vertex's corner, the join meeting no edge of the outline or of a remaining hole); the bridged outline is then ear-clipped by the existing clipper, which already took bridged outlines.
- **Query, health, shading.** Area is outer less holes; the faces table's `vertices` is the corner count; bounds, duplicate faces and inside-out use every corner or the outer loop as fits. Smooth corner normals: a hole corner joins its neighbours along its own loop, like any corner; corner arrays (`Shading.Corners`, posed corners, `SkinnedExport`) are by `Face.Corners`. `Shading.CornerOf(loop, v)` is gone, for `Face.CornerOf(v)`.
- **Edits.** `move` moves hole corners as any vertex; a move after which a hole leaves its face or crosses a loop is refused by `Create`'s validation. `extrude` on a holed face makes a tube: walls on the outer loop then on each hole (`[old k, old k+1, new k+1, new k]`; on a clockwise hole they face into the hole), hole corners lifted along the outer loop's normal, and the cap keeps the face id with its holes on the new rings; walls take colour and groups by `FaceStyle.Carry` as before. `extrude` with `inset > 0` on a holed face is refused, naming `faces[n]`. **`subdivide` refuses any mesh with a holed face**, one error per such face (`faces[n]: this face has holes, and subdivide does not take a face with holes`): Catmull-Clark of a holed face has no single obvious answer.
- **The Carve file: version 4** (4 was free: `main` and `origin/milestone/M-5` both at 3). A face is a plain array or `{"loop": [...], "holes": [[...], ...]}` (both keys required on the object; written on one line, a plain face as the plain array). A `null` in `vertices` or `faces` is reserved as T-64's tombstone and refused in a version 4 file (`CarveFile.TombstoneMessage`); the format's doc comment says so. Versions 1 to 3 still read. **Open: the spec says `Write` always writes the new version, but T-42's rule and its tests (`A_model_with_no_smooth_mesh_and_no_hard_edge_still_writes_version_2_with_no_new_key`, `WriteIsStable`, `Shading_and_hard_edges_write_as_version_3...`, `TheCubeIsOneMeshNamedMesh`) hold that a model older versions can hold is written as before, byte for byte. Dropping that would remove a check, so as an interim `Write` gives 4 only when a face has holes, and 2 or 3 otherwise. The person rules which.** The tests that named 4 as an unread version now use 5, and the "reads versions" messages list 4.
- **Server (mechanical only so far).** `get_mesh` reports a holed face's `vertices` as `{loop, holes}` (a plain face as before); labels, highlight spikes, `set_weights` groups and the subdivide triangle count use every corner. Nothing else in the server half is done.

### Hand-off

**Done (committed):** the Core half above; the solution builds; `core-unit` and `server-unit` pass. The baseline frames (136 of cube, crate, shed, export, character, soft) are in `D:\repos\carve\branches\milestone\M-5\artifacts\review\T-63\before\` with `frames-before.sha256`, taken by `cairn test artifacts <suite>` from the last finished jobs (cube and crate at 3b51d88, shed and soft at e7b42d5, export and character at 8e89b67), not from this branch's base: if any differs after, re-check against a fresh run of the base before blaming T-63.

**Not done:**
1. Every unit test of the holed paths in Scope's Tests list (validation errors one by one, triangulation of one hole, two holes and a concave outer loop with a hole, the plate topology with and without the inner face, the tube extrude, inset refused, the move refused, subdivide refused, the file round trip of a holed face and the tombstone refusal), each with a break shown failing and the breaks table here. **None of the hole code has been exercised by a test yet.**
2. Server: `define_mesh`'s object form and the schema `anyOf`; the description (hole winding, the 2 x 2 plate with a 1 x 1 hole) and its worked-example test; `render` labels on the outer loop's area less its holes (check `LabelLayout`'s face-label placement; it may use the centre, which for the plate sits in the hole); the wireframe's hole edges (topology gives them, so likely already drawn: check); the regression tests (`define_mesh` object form, `get_mesh`, the plate's export triangle count).
3. The Cuts scenario (`scenarios/Carve.Scenario.Cuts`, `CutsStages.cs`, `HolesStage.cs`), suite `scenario-cuts`, `Carve.slnx`, the CLAUDE.md layout row, its pixel checks and their breaks, and a review of its frames.
4. The six scenario suites after, hashed against `frames-before.sha256`.

**For the person before finish:** the "strictly inside" tolerance (the existing `CollinearTolerance` band is used), and whether `Write` always writes 4 (the spec) or only for holed models (the interim, which keeps T-42's checks).
