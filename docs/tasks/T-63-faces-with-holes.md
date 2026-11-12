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
  - a hole not strictly inside the outer loop, or touching it ("touching" within the kernel's existing `CollinearTolerance`, no new tolerance: ruled by the person, 2026-11-11);
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
- Every older version still reads. `Write` writes the lowest version that holds the model: the new one only when the model needs it (a holed face now, tombstones once T-64 lands), otherwise 2 or 3 as T-42's tests require, byte for byte. Files read back bit-exact. (Ruled by the person, 2026-11-11.)

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
- **Open rules: stop and ask.** Any validation rule beyond the list above. (The tolerance for "strictly inside" is settled: the existing `CollinearTolerance`.) A change to how a plain face triangulates. Holes in subdivide, if refusing them does not fit.
- Every existing frame must stay byte-identical by hash: cube, crate, shed, export, character and the soft character. Check by hash before and after, and list them.
- Plan for two agents. If the topology and triangulation fill the first, hand off at the server half with the As built notes written.
- Evidence (frames, `breaks.log`) goes to the milestone worktree's `artifacts/review/T-63/` by its absolute path, given in the brief, not to this task's worktree.
- Run `finish` from inside the worktree and leave the folder.

### As built

First to fourth agents: Opus (claude-opus-5-5). The Core half, its tests, the server's define_mesh, get_mesh and labels, and the Cuts scenario are in.

- **The face type.** `Carve.Core.Face` (`src/Carve.Core/Face.cs`), immutable: `Loop` (outer, CCW from outside), `Holes` (each CW from outside), `HasHoles`, `Corners` (every vertex id, outer loop first, then each hole in order: the face's corner numbering), `LoopAt(hole)` (-1 is the outer loop), `Locate(corner) -> (Hole, Index)`, `CornerAt(hole, index)`, `CornerOf(vertex)`, `Neighbours(corner)` (the previous and next vertex in the corner's own loop), `Map(f)`, and the names `LoopName(face, hole)` (`faces[n]` or `faces[n].holes[k]`) and `CornerName(face, hole, i)` (`faces[n][i]` or `faces[n].holes[k][i]`). It is deliberately not a list of ints, so every consumer of `PolygonMesh.Faces` (now `IReadOnlyList<Face>`) failed to compile and was visited. `int[]` converts implicitly to a plain `Face`; equality is structural. `PolygonMesh.Create` keeps its plain-loop overload and gains one taking `Face`s; `Edit` takes `Face`s for reshaped and added faces. New `PolygonMesh.FaceArea` (outer less holes); `FaceNormal` and `FaceCentre` use the outer loop.
- **`EdgeUse`** is `EdgeUse(Face, Hole, Corner, LoToHi)`: `Hole` is -1 for the outer loop, else the hole's index; `Corner` is the index within that loop; `CornerName` gives `faces[n][i]` or `faces[n].holes[k][i]`. Hole edges' direction is the hole's own (clockwise), so a hole edge shared with an inner face is manifold. `FaceEdges(f)` lists the outer loop's edges, then each hole's, in corner order.
- **Validation** (`PolygonMesh.Create`, helpers `Triangulator.HoleProblems` and `Triangulator.Bridge`): per loop, fewer than 3 (`faces[n].holes[k]`), out of range and a vertex repeated across the face's loops (`faces[n].holes[k][i]`, message `index v repeats <first corner's name>`); then the outer loop takes the plain face's crossing and zero-area checks; then each hole: winding the same way as the outer loop (2D signed area in the outer loop's plane > 0), not strictly inside the outer loop (an edge meeting an outer edge, or its first vertex outside), and overlapping or touching an earlier hole; then the whole face, bridged, takes the plain face's crossing, zero-area, triangulation and covers-twice checks, an edge named by its starting corner. **Touching uses the existing `Triangulator.CollinearTolerance` "on" band (relative to the outer loop's size), the one `FindCrossing` uses; no new tolerance was added. The person ruled (2026-11-11) that "strictly inside" uses this existing `CollinearTolerance`, with no new tolerance.** A self-crossing hole is not a new rule: it is caught by the plain face's crossing check on the bridged outline. A hole corner off the plane is a non-planar warning (`MeshReport`, the plane is the outer loop's).
- **Triangulation.** Plain faces take exactly today's path (the whole existing core-unit suite passes unchanged on geometry). A holed face is bridged: holes rightmost-vertex first, each rightmost vertex joined to the nearest vertex of the outline so far that it sees (inside that vertex's corner, the join meeting no edge of the outline or of a remaining hole); the bridged outline is then ear-clipped by the existing clipper, which already took bridged outlines.
- **Query, health, shading.** Area is outer less holes; the faces table's `vertices` is the corner count; bounds, duplicate faces and inside-out use every corner or the outer loop as fits. Smooth corner normals: a hole corner joins its neighbours along its own loop, like any corner; corner arrays (`Shading.Corners`, posed corners, `SkinnedExport`) are by `Face.Corners`. `Shading.CornerOf(loop, v)` is gone, for `Face.CornerOf(v)`.
- **Edits.** `move` moves hole corners as any vertex; a move after which a hole leaves its face or crosses a loop is refused by `Create`'s validation. `extrude` on a holed face makes a tube: walls on the outer loop then on each hole (`[old k, old k+1, new k+1, new k]`; on a clockwise hole they face into the hole), hole corners lifted along the outer loop's normal, and the cap keeps the face id with its holes on the new rings; walls take colour and groups by `FaceStyle.Carry` as before. `extrude` with `inset > 0` on a holed face is refused, naming `faces[n]`. **`subdivide` refuses any mesh with a holed face**, one error per such face (`faces[n]: this face has holes, and subdivide does not take a face with holes`): Catmull-Clark of a holed face has no single obvious answer.
- **The Carve file: version 4** (4 was free: `main` and `origin/milestone/M-5` both at 3). A face is a plain array or `{"loop": [...], "holes": [[...], ...]}` (both keys required on the object; written on one line, a plain face as the plain array). A `null` in `vertices` or `faces` is reserved as T-64's tombstone and refused in a version 4 file (`CarveFile.TombstoneMessage`); the format's doc comment says so. Versions 1 to 3 still read. **Ruled by the person (2026-11-11): `Write` writes the lowest version that fits**: 4 only when the model needs it (a holed face now; tombstones once T-64 lands), otherwise 2 or 3 byte for byte, so T-42's tests (`A_model_with_no_smooth_mesh_and_no_hard_edge_still_writes_version_2_with_no_new_key`, `WriteIsStable`, `Shading_and_hard_edges_write_as_version_3...`, `TheCubeIsOneMeshNamedMesh`) keep their targets. The spec's Scope line and the milestone's file-version line now say so. The tests that named 4 as an unread version now use 5, and the "reads versions" messages list 4.
- **Server.** `define_mesh` takes a face as a plain array (as before) or `{"loop": [...], "holes": [[...], ...]}`, both keys required and no other, as the file form takes it (**ruled by the person, 2026-11-11**: the object form requires both `loop` and `holes` and refuses any other key, matching the file form; a face with no holes is a plain array, and `get_mesh` and the file write it so) (`faces[n].holes: required but missing`, `faces[n].<key>: a face takes only loop and holes`), each loop read as a plain face is, a bad index named `faces[n].holes[k][i]`; `{"loop": [...], "holes": []}` is a plain face. The schema's face item is an `anyOf` of the index list and that object (`additionalProperties: false`). The description says holes wind clockwise seen from outside, lie strictly inside and apart, share no vertex, and show as open edges unless filled, and gives the worked example `Worked example (a face with a hole):`, the 2 x 2 plate with a 1 x 1 hole (`DefineMeshTool.PlateExample`), before the cube's, which stays last as the tests that take the rest of the text need. `get_mesh` reports a holed face's `vertices` as `{loop, holes}` (its text says so; `centre` is the outer loop's mean); highlight spikes, `set_weights` groups and the subdivide triangle count use every corner. **Labels:** a face's id goes at the new `PolygonMesh.FaceLabelPoint`: `FaceCentre` for a plain face (so no existing label frame moves), and for a holed face the centroid of the largest triangle of its triangulation, which is on the face, not in a hole. Normal spikes stay at `FaceCentre` (the spec: position comes from the outer loop). The wireframe draws `Topology.Edges`, so hole edges are drawn; render draws the derived triangles, so a hole draws nothing.
- **Tests.** `tests/Carve.Core.Tests/HoleTests.cs` (unit, 28): every validation error, the non-planar warning, the three triangulations (area outer less holes, no centroid in a hole or outside, each facing +Z), the filled box closed and every edge manifold (16 edges), a hole edge's `EdgeUse` named `faces[0].holes[0][1]`, the box without the inner face with exactly its four hole edges open, the tube extrude (16 vertices, 9 faces, 8 walls, the cap keeps id 0 with its hole on the new ring, outer walls out and hole walls into the hole, only the base rings open), inset refused, the move refused and an inner move allowed, subdivide refused, the label point, the version 4 round trip (text equal on rewrite), a plain model still version 2, and the `null` tombstone refused in `vertices` and in `faces`. Versions 1, 2 and 3 still reading are the existing `CarveFileTests`, `CarveFileV2Tests` and `ShadingFileTests`, unchanged and passing. `tests/Carve.Server.Tests/HoledFaceToolTests.cs` (regression): the worked plate parsed from the description and defined through the client (8 vertices, 1 face, 8 edges, open), `get_mesh` returning the object form, its export of 8 triangles; the empty-holes object being a plain face; seven bad holes each named. `DefineMeshTests`' description phrases and published schema follow the new text and `anyOf`.
- **The Cuts scenario** (`scenarios/Carve.Scenario.Cuts`, suite `scenario-cuts`, in `Carve.slnx` and the CLAUDE.md layout table). `CutsScenario` runs `CutsStages.All` in order, one `ICutsStage` per file; a later cut adds its file and one line there. `HolesStage.cs`: the 2 x 2 plate, z from -0.1 to 0.1, its +Z front face `{"loop": [4, 5, 6, 7], "holes": [[8, 9, 10, 11]]}` with the inner face `[8, 11, 10, 9]` (7 faces, 16 edges, closed), then redefined without it (6 faces, open, `select edges where open` the hole's four). Four frames: `holes-plate` and `holes-open` from `[0.6, 0.35, 1]`, `holes-open-ortho` orthographic from `[0, 0, 1]`, `holes-open-wireframe` from `[0.6, 0.35, 1]`. The pixel checks are worked by a ray test that knows holes (`CutsGeometry.cs`, `Solid.FirstHit`: the plane crossing inside the outer loop and in no hole, culled from behind); the shed's `ShedChecks.cs` and `ShedGeometry.cs` (its `Camera`) are linked, not copied, and are in the suite's inputs. 41 probes: the hole's centre, inside the hole by its corners and sides, the plate by its corners, its sides' middles and the hole's corners, the four sides, and outside. The geometry itself must give the inner face at the hole's centre in frame 1, the background there and inside the hole in the others, and the front at the plate's corners; every +Z face is the plate's shade within `Exact` (frame 2 the same shade as frame 1); the wireframe's hole edges and the front's outer edges are each checked as four stretches, since `Line`'s 17 samples missed the fully covered pixels on the steep edge 5-6 (the line is where it is projected: 71 of its rows read 237). The four frames' hashes are in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-63/cuts/cuts-frames.sha256`, with the frames.
- **Breaks**, each applied alone, built, the tests run, then restored; output in `D:\repos\carve\branches\milestone\M-5\artifacts\review\T-63\breaks.log` (trust each line's `Failed: N`):

  | Check | Break | Result |
  |---|---|---|
  | hole of fewer than 3 | count tested on the outer loop only | 1 fails |
  | winding | the winding test never fires | 1 fails |
  | crosses or touches the outer loop | `LoopsMeet(outer, hole)` dropped | 2 fail (crossing, touching) |
  | lies outside | the first-vertex-inside test dropped | 1 fails |
  | holes apart | the hole-hole test never fires | 2 fail (overlap, touch) |
  | vertex used twice across loops | repeats checked in the outer loop only | 2 fail |
  | non-planar warning | planarity over the outer loop only | 1 fails |
  | triangulation | holes not bridged | 4 fail (three shapes, label point) |
  | topology | hole edges not listed | 6 fail |
  | hole edges' direction | taken the outer loop's way | 1 fails (manifold) |
  | area | holes not subtracted | 3 fail |
  | tube extrude | no walls on the holes | 1 fails |
  | inset refused | the refusal never fires | 1 fails |
  | subdivide refused | the refusal never fires | 1 fails |
  | lowest version | always 4 | 6 fail (with T-42's) |
  | holed face in the file | written as its plain loop | 1 fails (round trip) |
  | tombstone refused | the refusal never added | 2 fail |
  | label point | always the centre | 1 fails |
  | define_mesh object form | holes dropped | 5 fail |
  | define_mesh `loop` missing refused | the refusal never fires | 1 fails (`A_bad_hole_is_a_tool_error_naming_it`, `faces[0].loop`) |
  | define_mesh schema, an `anyOf` | `anyOf` renamed `oneOf` | 2 fail (`The_published_input_schema_is_the_typed_one`, `Tools_list_shows_the_schema_and_the_description_rules`) |
  | Cuts frame 1: the hole's centre is the inner face, in the plate's shade | the inner face's triangles not drawn | 9 checks fail (hole centre and inside the hole: background) |
  | Cuts frames 2 to 4: the opening is the background | holes not bridged | 27 checks fail (shaded, orthographic and wireframe) |
  | Cuts: the plate's corners and middles in the one shade | a holed face's first triangle dropped | 55 checks fail |
  | Cuts frame 4: the hole's edges drawn | the wireframe skips a hole's edges | 16 checks fail (each hole edge in four stretches) |
  | Cuts: get_mesh's object form | get_mesh gives a holed face as its plain loop | 56 checks fail |

### Hand-off

Fourth agent (Opus, claude-opus-5-5), stopped at the checkpoint "code committed, a real run still to come". **Done:** the break for the `loop`-missing refusal (row in the table, output appended to `breaks.log`); the Hand-off's earlier items 1 and 2 folded into As built. No code changed: the code commit is `99c846e`, and no sibling commit is on `origin/milestone/M-5` beyond it (fetched 2026-11-12 02:1x UTC).

**Run under way:** `cairn task T-63 verify` on `99c846e` submitted these jobs; `server-unit` (J-20261112-020526-612880) and `core-unit` (J-20261112-020531-e3b074) show `done`; the GPU ones were still `queued` behind about 17 Empyrean, voidmarches and waystone jobs, with no progress in 11 minutes: scenario-cube J-20261112-020528-0bcf14, scenario-crate J-20261112-020529-7b8d94, scenario-shed J-20261112-020532-4ed2c0, viewer-cube J-20261112-020534-608b99, scenario-cuts J-20261112-020535-c446b1, scenario-export J-20261112-020537-fcb988, scenario-character J-20261112-020538-6c7b37, scenario-soft J-20261112-020539-16528f, server-regression J-20261112-020541-2871c4. These are for the code commit `99c846e`; the commit holding this hand-off changes only this file, so their results stand for the code (re-run only if code changes). The older J-20261112-015231-945854 is stale.

**Left:**
1. Wait for those jobs (or re-run `cairn task T-63 verify`, which picks them up by commit), and read their results.
2. Hash every frame of that run against `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-63/frames-before.sha256` (136 frames, `sha256sum -c` format, paths relative to the run's artifacts) and list them in As built; any that differ, compare with a fresh run of the base before blaming T-63. Hash the four Cuts frames against `cuts/cuts-frames.sha256`.
3. The frames' review through `mcp__waystone__review`: the four Cuts frames in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-63/cuts/`.
4. Remove this Hand-off, `cairn task T-63 verify` to `VERIFY PASS`, `cairn lint`, `finish`.

**Surprising:** `cairn task T-63 verify` writes nothing to its stdout while it waits on queued jobs, so a redirected log stays empty; `cairn test status` shows the jobs. `cairn test report <job>` does not take a job id.
