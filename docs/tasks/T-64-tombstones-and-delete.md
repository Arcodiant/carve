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

First agent: Opus (claude-opus-5-5). **Core only** (the member's own checkpoint: "hand off after Core with the sweep list"). The consumer sweep, the server and the content are left; see `**Second agent: Opus (claude-opus-5-5). The consumer sweep, the server and the tests** (commit `cbd4644`):

- Core: `Triangulator.TriangulateFace` gives a tombstone no triangles (so `FlatShading`, `Shading`, render, export and `Skinning` draw none); `PolygonMesh.FaceArea`/`FaceCentre`/`FaceLabelPoint`/`FaceNormal` refuse a tombstone plainly (`InvalidOperationException("faces[n]: deleted")`, callers skip through `LiveFaces`); `Shading.Corners` gives a tombstone no corners; `MeshHealth` (shells, area, duplicate faces, coincident vertices) and `MeshQuery.Select` (vertices and faces; `Of` is the live row count) skip tombstones; `MeshReport` counts live elements and gives no unused-vertex warning for a tombstone; `Model.Combined` carries the deleted vertices at their offsets (`OffsetOf` stays slot counts, so ids map); `subdivide` keeps tombstones, and the face point each level makes for a tombstone is a tombstone vertex, not a live stray.
- Server: the `delete` tool (`DeleteTool.cs`, worked example `{"faces":[3]}` on `define_mesh`'s cube, the top at y = 1); `deleted: {vertices, faces}` in `MeshToolSupport.Edit`'s body (move, extrude, delete), in `subdivide`'s and in `define_mesh`'s; `get_mesh` writes `null` in a tombstoned slot and says so; labels, spikes and the highlight skip tombstones; `set_colour`/`define_group` and `set_weights` use the mesh-aware `FaceProblem`/`VertexProblem` (`deleted`); the skinned export's unweighted list, bound-to-root count, height and materials, and `export`'s bounds line, take live elements only. The `.glb` compacts with no code of its own: it is built from triangles, so no tombstone's vertex is written.
- `select edges where open` on an open mesh works (4 edges on the topless cube, tested); the first agent's surprise was not a bug.
- Tests: `TombstoneSweepTests` (Core unit, 9: triangulation, smooth shading, face geometry refusal, topology, report, check, select, subdivide, combined), `TombstoneExportTests` (Server unit, 2: skinned export of a rigged cube with vertex 6 deleted, every written vertex used and carrying its own two weights; a plain export of the topless cube through the engine's parser), `DeleteToolTests` (Server regression, 2: the worked example, `get_mesh` nulls, vertex delete, `deleted` errors, refusals, ids appended after the last slot, undo; and the topless cube exported through the client and read back through the engine's parser). All pass by `dotnet test` here; Core 621, Server unit 279.

### Hand-off

**Done:** Core (first agent), the consumer sweep, the `delete` tool, the report's `deleted`, `get_mesh` nulls, export compaction, and their tests (second agent, commit `cbd4644`). `dotnet build Carve.slnx` is clean with warnings as errors.

**Left:**

1. `cairn test run server-regression` was queued as `J-20261112-131058-0d01aa` on this machine's runner and its result was **not** seen; check it (the new `deleted` field or the new tool may trip a test that lists tool names or report keys exactly).
2. **Breaks for the second agent's checks** are not yet run: one per row of the sweep (e.g. `TriangulateFace` not skipping a tombstone, `MeshQuery` not skipping, `MeshReport` live counts, `Model.Combined` dropping the mask, `subdivide` leaving the face point live, `get_mesh` writing the face, `deleted` missing from the report, the skinned `Unweighted` counting the tombstone), each shown failing, with the table added to As built and the log appended to `D:epos\carveranches\milestone\M-5rtifactseview\T-64reaks.log`.
3. **Content:** `DeleteStage.cs` in `scenarios/Carve.Scenario.Cuts` and its line in `CutsStages.cs`: the cube with its top (face 3) deleted from `[0.6, 0.35, 1]`, and the cube with vertex 6 (1, 1, 1) deleted (faces 1, 3, 5) from the same direction in the wireframe; pixels by ray test (`CutsGeometry.cs`), each shown failing on a break.
4. **Frames:** the GPU suites once on a near-final commit; every existing frame byte-identical to T-63's `frames-after-99c846e.sha256` (121) and `cuts\cuts-frames.sha256` (4), listed, into the evidence folder; the new frames' hashes too. The member's spec asks for no review by the person.
5. `render` captions: `RenderSystem` takes its counts from `MeshReport`, so they are live counts now, but no test shows it yet.
6. `cairn task T-64 verify` until clean, `cairn lint`, then `finish`. Remove this section or fold it in.

**Checked:** Core and Server unit tests and the two new regression tests pass by `dotnet test`. Not checked: the full regression suites, any GPU suite, any frame.
