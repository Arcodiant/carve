---
description: >-
  delete removes faces and vertices, leaving tombstoned slots so ids are never reused, through get_mesh, the file, render, select, check and export
---
# T-64 — Tombstones and delete

**Status:** In Progress

**Pri:** P1

**Milestone:** M-5

**Batch:** — (standalone)

**Depends on:** T-63

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent removes faces and vertices, and every id it holds still means what it did, or is plainly gone. D-1's rule that ids are never renumbered or reused holds through a delete.

## Scope

**Core: tombstones.**

- A deleted vertex or face keeps its slot in `PolygonMesh`, marked deleted. Its id is never given out again along the current chain of edits. An undone step frees its ids as today.
- New elements are still appended at the end.
- `Vertices.Count` and `Faces.Count` stay slot counts, so ids remain positions. Add live counts and an `IsDeleted` check.
- Visit every consumer: `MeshTopology`, `Triangulator`, `MeshHealth`, `MeshQuery`, `FlatShading` and T-42's corner normals, `FaceStyle`, `VertexWeights`, T-42's hard edges, `Model.Combined` and `OffsetOf`, `Skinning`, the server's render, labels, highlight, export, skinned export and `get_mesh`. Each one skips tombstones. None of them throws on one, or draws or exports one.
- A tombstoned face leaves its colour and its groups, and a tombstoned vertex its weights.
- Naming a tombstoned id in any edit is an error saying it was deleted: `faces[7]: deleted`, distinct from an id that never existed.

**Core: `MeshDelete`** (its own file).

- Deleting a vertex deletes every face that uses it, in its outer loop or in a hole. Deleting a face also deletes every vertex no live face uses afterwards.
- `EditResult` gains `Deleted` (vertex and face ids, ascending), the field every later M-5 edit reports.
- Deleting nothing is refused. So is deleting every face (a mesh keeps at least one face, as `define_mesh` requires).
- A face whose hole loop loses a vertex is deleted whole. The rule is never to reshape a face because of a delete.

**Server.**

- **The `delete` tool:** `delete(vertices?, faces?, mesh?)`, with the description and a worked example on `define_mesh`'s unit cube: deleting the top face leaves an open box.
- **The report.** Every edit's report gains `deleted: {vertices: [], faces: []}`, written in the shared report body in `MeshToolSupport`. Counts in every report are live counts.
- **`get_mesh`** writes `null` in a tombstoned slot of `vertices` and `faces`. Its description says so.
- **`render`** captions show live counts.
- **Export compacts.** The `.glb` holds live elements only, with indices remapped and weights following their vertex. Nothing about ids reaches a game.
- **The Carve file.** The version T-63 introduced now accepts the reserved `null` in `vertices` and `faces` and writes it, bit-exact. A file whose live face uses a `null` vertex is refused, naming the face.
- **`undo`** of a delete restores the slots and what was in them.

**Tests.**

- `unit`, each with a break shown failing:
  - a delete of a face, of a vertex and its faces, and the orphan rule;
  - ids after a delete then an extrude (appended, none reused);
  - undo then a new edit reusing the undone ids;
  - an edit naming a deleted id;
  - topology, triangulation, `check` and `select` skipping tombstones;
  - the file round trip with nulls;
  - export compaction (indices and weights) on a rigged cube with one vertex deleted.
- `regression`: `delete` and `get_mesh` through the client, and export of a cube with its top deleted read back through the engine's loader, as `scenario-export` does.

**Content.** `DeleteStage.cs` in the Cuts scenario, a line in `CutsStages.cs`:

- the unit cube with its top face deleted, from `[0.6, 0.35, 1]`: the inside of the far walls is culled and shows the background;
- the same cube with one top corner vertex deleted, which takes its three faces, from the same direction and in the wireframe.

Pixel checks are worked by ray test, each shown failing on a break.

## Notes

- **Decided by the person at planning (2026-11-09):**
  - tombstones, not renumbering, and no `compact` tool;
  - deleting a face takes vertices left with no face, and deleting a vertex takes its faces;
  - everything removed is listed in `deleted`.
- **Decides how every later M-5 edit removes elements.** Write the tombstone API and the `Deleted` field into As built. T-65, T-68 and their siblings use them and add nothing of their own. Opus.
- Update D-1's *Polygon meshes* decision: a deleted element's slot is kept, and its id is never reused.
- **Open rules: stop and ask.** Whether an empty mesh is allowed. A delete that would reshape a face rather than remove it. Any change to D-1's id rule beyond tombstones.
- Every existing frame stays byte-identical by hash, listed.
- Plan for two agents. The consumer sweep is long, so hand off after Core with the sweep list in As built.
- Evidence goes to the milestone worktree's `artifacts/review/T-64/` by absolute path.
- Run `finish` from inside the worktree and leave the folder.

### As built

First agent: Opus (claude-opus-5-5). **Core only** (the member's own checkpoint: "hand off after Core with the sweep list"). The consumer sweep, the server and the content were left to the second agent, below.

**The tombstone API** (for T-65, T-68 and every later M-5 edit; add nothing of your own):

- `Face.Deleted` (`src/Carve.Core/Face.cs`): the one tombstone a deleted face's slot holds. It has no corners (`Loop`, `Holes`, `Corners` empty), `face.IsDeleted` is reference identity with it, so a face built from an empty list is still refused as `faces[n]: face has 0 indices`, never taken for a tombstone. `Map` returns it unchanged; `ToString()` is `deleted`.
- `PolygonMesh` (`src/Carve.Core/PolygonMesh.cs`): `Vertices.Count` and `Faces.Count` stay **slot counts**, so an id is still a position. A deleted vertex's slot holds `Vector3.Zero` (canonical, so a file round trip compares equal), a deleted face's `Face.Deleted`. New: `LiveVertexCount`, `LiveFaceCount`, `HasTombstones`, `IsVertexDeleted(id)`, `IsFaceDeleted(id)` (both false for an id out of range), `LiveVertices`, `LiveFaces` (ids ascending), `VertexProblem(id)` / `FaceProblem(id)` (null for a live id, `there is no vertex n: ids run 0..m` for one that never existed, `deleted` for a tombstone), `DeletedMessage = "deleted"`, and internal `DeletedVertexMask`. `FaceStyle.FaceProblem(mesh, id)` and `VertexWeights.VertexProblem(mesh, id)` are the mesh-aware forms of the old count-only checks.
- `PolygonMesh.Create(vertices, faces, deletedVertices)`: the deleted ids are tombstones (positions not checked, set to zero); a `Face.Deleted` slot is not checked; a live face using a deleted vertex is refused at its corner (`faces[3][2]: index 6 is a deleted vertex`); a mesh whose every face is a tombstone is refused (`faces: mesh has no faces: every face is deleted`). The old two-argument overloads are unchanged.
- `PolygonMesh.Edit(..., deletedVertices, deletedFaces)`: tombstones exactly these ids (no cascade); existing tombstones always carry and added elements append after the last slot, so no id is given twice. A moved vertex, reshaped face or deleted id that is a tombstone is refused as `vertices[n]: deleted` / `faces[n]: deleted`. **An edit that removes elements** computes what goes with `MeshDelete.Closure` (or its own rule if the spec gives one), passes it to `Edit`, and reports it through `EditResult.From(..., deleted)` / `EditResult.Made(..., deleted)`.
- `EditResult.Deleted` (`src/Carve.Core/EditResult.cs`): a `DeletedElements(Vertices, Faces)` record, ids ascending, `DeletedElements.None` for every edit that deletes nothing and on failure. `EditResult.From` (the `Carried` step) strips, for any edit that leaves tombstones, a tombstoned face's colour and groups (`FaceStyle.Without`; a group left empty goes) and a tombstoned vertex's weights (`VertexWeights.Without`); a hard edge no live face has is dropped as before.
- `MeshDelete` (`src/Carve.Core/MeshDelete.cs`): `Closure(mesh, vertices, faces)` and `Delete(mesh, vertices?, faces?)`. A listed vertex takes every live face using it in any loop (outer or hole: a face is deleted whole, never reshaped); a deleted face takes every vertex of its own that no live face uses afterwards. **The person's ruling (2026-11-12), confirming the first agent's reading of "every vertex no live face uses afterwards":** a face delete (and later a merge) removes only the deleted faces' own vertices left with no live face, per the person's "deleting a face takes vertices left with no face"; a vertex no face used before the delete (a stray, as in the leaky shed) is not taken unless listed. Refused: nothing listed (`vertices, faces: nothing to delete: list at least one vertex or face`), an id out of range (`MeshEdits.UnknownElements`'s message) or already deleted (`faces[7]: deleted`), and a delete that would take every live face (`vertices, faces: this would delete every face, and a mesh keeps at least one face (as define_mesh requires)`).
- `MeshEdits`: `UnknownElements` (used by `move`, `render`'s highlight and `delete`) and `extrude`'s face check give `deleted` for a tombstone, so `move` and `extrude` already refuse one (`faces[1]: deleted`).
- **The Carve file** (`src/Carve.Core/CarveFile.cs`, version 4): a `null` in `vertices` or `faces` is a tombstone, read and written bit-exact; `Write` takes version 4 when a mesh `HasTombstones` or a face has holes, else 2 or 3 as before. A live face on a `null` vertex is refused by `Create` at its corner, which names the face (`meshes[0].faces[3][2]: index 6 is a deleted vertex`). A colour, group or weight naming a tombstone in a file is refused (`deleted`). Versions 1 to 3 still refuse `null` as before. T-63's `HoleTests.ANullTombstoneIsRefusedUntilT64` is replaced by the round-trip and refusal tests below.
- **D-1's *Polygon meshes*** now says a deleted slot is kept and its id never reused.

**Tests** (`tests/Carve.Core.Tests/TombstoneTests.cs`, unit, 14): a face deleted (slot kept, others unchanged, the original mesh untouched), a vertex deleted with its three faces, the orphan rule (only vertex 6 when faces 1, 3, 5 go; a stray kept), a hole losing a vertex deletes its face whole, ids after a delete then an extrude (vertices 8..11, faces 6..9), undo (the earlier mesh untouched, the undone ids given out again), every edit naming a deleted id (`move` by vertex and by face, `extrude`, `delete` again, `Edit`'s reshape) distinct from one that never existed, nothing and every face refused, colour, groups and weights left, topology with no edge of a tombstone, `Create`'s live-face-on-deleted-vertex, tombstones-only and empty-face-is-not-a-tombstone, the file round trip with nulls (version 4, text equal on rewrite), a file's live face on a null vertex refused naming it, and a plain model still version 2. Core: 612 passed.

**Breaks** (each applied alone by `C:\Users\andre\AppData\Local\Temp\t64\breaks.py`, Core tests built and run, restored; output in `D:\repos\carve\branches\milestone\M-5\artifacts\review\T-64\breaks.log`):

  | Check | Break | Result |
  |---|---|---|
  | a deleted face keeps its slot as a tombstone | `Edit` never writes `Face.Deleted` | 11 fail |
  | a vertex takes every face on it | `Closure` adds no face for a listed vertex | 8 fail |
  | a face takes the vertices left with no face | no orphan taken | 2 fail (orphan rule, holed face) |
  | only the deleted faces' own vertices are taken | every vertex no live face uses taken | 1 fails (the stray) |
  | a hole losing a vertex deletes the face whole | the outer loop only searched | 1 fails |
  | tombstones carry through a later edit (ids not reused) | `Edit` passes only the new deleted ids to `Create` | 1 fails (delete then extrude) |
  | a deleted vertex id in an edit is `deleted` | `UnknownElements` skips the tombstone test | 1 fails |
  | a deleted face id in an edit is `deleted` | `UnknownFace` always says there is no face | 1 fails |
  | deleting every face refused, with its own message | the check never fires | 1 fails |
  | deleting nothing refused | the check never fires | 1 fails |
  | a deleted face leaves its colour and groups | the style not stripped | 1 fails |
  | a deleted vertex leaves its weights | the weights not stripped | 1 fails |
  | the file writes `null` for a deleted vertex | its position written | 1 fails (round trip) |
  | the file reads a `null` face as a tombstone | read as an empty face | 2 fail |
  | tombstones need version 4 | the version chosen by holes only | 2 fail |
  | a live face on a deleted vertex refused | `Create`'s check never fires | 2 fail (`Create`, file) |
  | a mesh of tombstones only refused | the check never fires | 1 fails |
  | an empty face is not a tombstone | `IsDeleted` is an empty loop | 1 fails |

  Three breaks first written as `if (false)` did not build (CS0162, unreachable code, a warning made an error); they were rewritten as `if (x < 0)` and rerun, and the log keeps both runs.

**Frames:** none taken; Core alone changes no frame (no existing mesh has a tombstone, and every changed path is the old one when `HasTombstones` is false). The hash check against T-63's 121 + 4 is for the agent that runs the GPU suites.


**Second agent: Opus (claude-opus-5-5). The consumer sweep, the server and the tests** (commit `cbd4644`):

- Core: `Triangulator.TriangulateFace` gives a tombstone no triangles (so `FlatShading`, `Shading`, render, export and `Skinning` draw none); `PolygonMesh.FaceArea`/`FaceCentre`/`FaceLabelPoint`/`FaceNormal` refuse a tombstone plainly (`InvalidOperationException("faces[n]: deleted")`, callers skip through `LiveFaces`); `Shading.Corners` gives a tombstone no corners; `MeshHealth` (shells, area, duplicate faces, coincident vertices) and `MeshQuery.Select` (vertices and faces; `Of` is the live row count) skip tombstones; `MeshReport` counts live elements and gives no unused-vertex warning for a tombstone; `Model.Combined` carries the deleted vertices at their offsets (`OffsetOf` stays slot counts, so ids map); `subdivide` keeps tombstones, and the face point each level makes for a tombstone is a tombstone vertex, not a live stray.
- Server: the `delete` tool (`DeleteTool.cs`, worked example `{"faces":[3]}` on `define_mesh`'s cube, the top at y = 1); `deleted: {vertices, faces}` in `MeshToolSupport.Edit`'s body (move, extrude, delete), in `subdivide`'s and in `define_mesh`'s; `get_mesh` writes `null` in a tombstoned slot and says so; labels, spikes and the highlight skip tombstones; `set_colour`/`define_group` and `set_weights` use the mesh-aware `FaceProblem`/`VertexProblem` (`deleted`); the skinned export's unweighted list, bound-to-root count, height and materials, and `export`'s bounds line, take live elements only. The `.glb` compacts with no code of its own: it is built from triangles, so no tombstone's vertex is written.
- `select edges where open` on an open mesh works (4 edges on the topless cube, tested); the first agent's surprise was not a bug.
- Tests: `TombstoneSweepTests` (Core unit, 9: triangulation, smooth shading, face geometry refusal, topology, report, check, select, subdivide, combined), `TombstoneExportTests` (Server unit, 2: skinned export of a rigged cube with vertex 6 deleted, every written vertex used and carrying its own two weights; a plain export of the topless cube through the engine's parser), `DeleteToolTests` (Server regression, 2: the worked example, `get_mesh` nulls, vertex delete, `deleted` errors, refusals, ids appended after the last slot, undo; and the topless cube exported through the client and read back through the engine's parser). All pass by `dotnet test` here; Core 621, Server unit 279.

**Third agent: Opus (claude-opus-5-5). The content, the sweep's breaks and the frames.** The second agent's queued `server-regression` (`J-20261112-131058-0d01aa`, on `cbd4644`) passed, 378 of 378.

- **The Cuts delete stage** (`scenarios/Carve.Scenario.Cuts/DeleteStage.cs`, one line in `CutsStages.cs`). `define_mesh`'s unit cube, then `delete {"faces":[3]}`: the report (8 vertices, 5 faces, open, `deleted` `{vertices: [], faces: [3]}`), get_mesh's slots (8 and 6, null exactly at face 3), and frame `delete-top` from `[0.6, 0.35, 1]`: the top's probes are the background (the back, left and bottom face away and are culled), the front's and the right's are each one shade within `Exact`. Then the cube again and `delete {"vertices":[6]}`: 7 vertices, 3 faces, `deleted` `{vertices: [6], faces: [1, 3, 5]}` (no other vertex goes; each is still on a live face), get_mesh null at vertex 6 and faces 1, 3, 5, and frame `delete-corner-wireframe` from the same direction: every probe the background (all three faces left face away), each of the 9 live edges drawn in four stretches, and the middles of the deleted edges 2-6, 5-6 and 6-7 the background. The captions give live counts (`8 vertices, 5 faces`; `wireframe, 7 vertices, 3 faces`), which is the test the second agent's item on `render` captions asked for. 96 probes (a 4 x 4 grid on each of the cube's six planes, and six outside it), worked by `Solid.FirstHit`. `CutsGeometry.cs`'s `Solid` now reads a null slot: a null face has an empty loop, which no ray meets (`Solid.IsDeleted`), and a null vertex sits at the origin. The stage reads get_mesh's nulls from the raw JSON, so a tombstone written as an empty face is not taken for one.
- **The wireframe's lines over the background** are not checked with `Checks.Line`. Its one pixel within 10 of the line's colour does not fit here: a near-vertical edge at x = 219.2 reads 210 and 133 side by side, and the 45-degree edge 1-5 reads 177 and 177, with no pixel at 237. Instead, `DeleteStage.Drawn` needs each of 17 points along each stretch to have, within one pixel, a grey at least half way from the background to `Edge`. This is a new check of this stage, not a change to `Line`, which every other scenario still uses.

**Breaks for the sweep, the server and the stage.** Each was applied alone by `C:/Users/andre/AppData/Local/Temp/t64/breaks2.py`, built with warnings as errors and run (Core tests, `TombstoneExportTests`, `DeleteToolTests`, or the Cuts scenario), then restored. The output is appended to `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-64/breaks.log`.

  | Check | Break | Result |
  |---|---|---|
  | triangulation gives a tombstone no triangles | `TriangulateFace`'s tombstone test removed | 3 fail |
  | smooth shading gives a tombstone no corners | `Shading`'s tombstone test removed | 1 fails |
  | select never returns a tombstone | `MeshQuery.Select` skips none | 1 fails |
  | the report counts live elements | slot counts | 2 fail |
  | no unused-vertex warning for a tombstone | the warning for any unused slot | 2 fail |
  | check skips tombstones | the area summed over every slot | 1 fails |
  | the combined model keeps the mask | every vertex marked live | 1 fails |
  | subdivide's face point for a tombstone is a tombstone | only old tombstones kept | 1 fails (first applied to the wrong file and logged `BREAK NOT APPLIED`, then rerun) |
  | the skinned export's unweighted list is live vertices | every slot | 1 fails |
  | the skinned export's bound-to-root count is live vertices | every slot | 1 fails |
  | get_mesh writes null for a deleted face | the face written | 1 fails |
  | get_mesh writes null for a deleted vertex | the vertex written | 1 fails |
  | every edit report carries `deleted` | the key left out | 1 fails |
  | Cuts: captions and reports give live counts | slot counts | 5 checks fail |
  | Cuts: get_mesh nulls exactly the deleted faces, and the frame matches | a live front written null | 69 checks fail |
  | Cuts: get_mesh writes the deleted face null | the tombstone written as a face | get_mesh fails, run fails |
  | Cuts: the report lists what went | `deleted` left out | the stage throws, run fails |
  | Cuts: a vertex delete takes all its faces, nothing of vertex 6 drawn | the tool deletes only face 1 | 45 checks fail, incl. the middles of 2-6 and 5-6 |
  | Cuts: every live edge drawn | edges at vertex 0 dropped from the wireframe | 12 checks fail |

  The vertex-delete break first did not build (`List<int>` has no `Length`), so the runner recorded no run. It was fixed and rerun, and the log keeps both.

**Frames:** the GPU suites ran on `737853a` (`cairn task T-64 verify`: scenario-cube, crate, shed, cuts, export, character and soft ran there; viewer-cube and server-regression were carried). All 121 frames of `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-63/frames-after-99c846e.sha256` (cube 32, crate 44, shed 10, export 7, character 17, soft 11) are byte-identical, and so are the four Holes frames of `T-63/cuts/cuts-frames.sha256`. The run's frames and their list are in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-64/frames-737853a/` (`all.sha256`, 127 frames). The two new frames, `delete-top` (`880e7e0a...`) and `delete-corner-wireframe` (`8c7c3cb3...`), are byte-identical to this machine's local run. They and the Cuts list are in `.../T-64/cuts/` (`cuts-frames.sha256`). The person approved both new frames in review (2026-11-12).
