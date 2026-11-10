---
description: >-
  delete removes faces and vertices, leaving tombstoned slots so ids are never reused, through get_mesh, the file, render, select, check and export
---
# T-64 — Tombstones and delete

**Status:** Open

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
