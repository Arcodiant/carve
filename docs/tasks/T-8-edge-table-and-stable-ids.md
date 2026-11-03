---
description: >-
  Carve.Core derives an edge table from each mesh, and vertex and face ids stay the same across every edit
---
# T-8 — Edge table and stable ids

**Status:** In Progress

**Pri:** P1

**Milestone:** M-2

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

`Carve.Core` knows how a mesh's faces join: every edge, the faces on it and which way each crosses it. It also has a written id contract that the edit operations (T-10) and tools (T-11) build on.

## Scope

In `src/Carve.Core`, engine-free:

- **`MeshTopology`**: derived from one `PolygonMesh`, immutable, built once per mesh (lazily, held by the mesh) and never stored apart from it:
  - edges as unordered vertex pairs `(lo, hi)`, each with the faces that use it and, for each, whether the face runs `lo->hi` or `hi->lo`. An edge may have 1, 2 or more faces.
  - for each vertex, the faces that use it; for each face, its edges in loop order.
  - `TryGetEdge(a, b)` in either order; `IsManifoldEdge` (exactly two faces, crossing it in opposite directions).
- **`MeshReport` is rebuilt on `MeshTopology`**, with the same fields, the same warnings and the same element names (`edges[a-b]`, `faces[n]`, `vertices[n]`). Every existing `MeshReportTests` case passes unchanged.
- **Ids.** A vertex's id and a face's id are its position in `Vertices` and `Faces`, the same numbers `define_mesh` takes. Write the contract on `PolygonMesh` as XML docs: an edit never renumbers, never reuses an id within a mesh's history, adds new vertices and faces at the end, and keeps a face's id when it reshapes that face. Add the one Core entry point edits need: `PolygonMesh.Edit(...)`, or a builder of your naming, that takes the old mesh, replaced positions, replaced face loops and appended vertices and faces, and runs `Create`'s validation on the result. It returns the same `MeshResult`, so a bad result is refused by element exactly as `define_mesh` is.
- **Face centre** (the mean of its vertices) beside `FaceNormal`, for `get_mesh` (T-11).

Tests in `tests/Carve.Core.Tests`, a new `MeshTopologyTests` class:

- The unit cube has 12 edges, each with two faces in opposite directions.
- An open quad has 4 one-face edges.
- Three faces on one edge are all listed.
- A flipped face's edges show the same direction twice.
- Vertex-to-face and face-to-edge lookups are right on the L-block from `TestMeshes`.
- `Edit` keeps every old id at its place, appends at the end, and refuses a result `Create` would refuse, naming the element.
- **Show each new test failing** on a deliberately broken implementation, such as directions swapped or an edge dropped, and say so in the closing report.

No scenario: nothing new is on screen.

## Notes

- **Decided by the person:** faces plus a derived edge table, not half-edge. Non-manifold, open and mixed-winding input stays legal with warnings, as in M-1. Stable integer ids are array positions, and nothing deletes in M-2.
- **Open rules: stop and ask, do not land a reading of your own.** Any change to a warning's text, element name or tolerance. Any change to what `Create` accepts or refuses. Any id rule beyond the contract above, such as what a future delete does to ids, which belongs to M-5.
- Faces with holes are **not** in M-2; they moved to M-5. Do not add inner loops.
- Files owned: `src/Carve.Core/*` except triangulation, `tests/Carve.Core.Tests/*`, and D-1's *Polygon meshes* decision. Update that decision: name the edge table as the structure, Certainty Settled, with half-edge as the rejected alternative, and remove its M-1 exception. T-9 touches none of these, so the two run side by side.
- **Decides what others build on**: `MeshTopology` and the `Edit` entry point are what T-10 is written against. Moderately design-heavy. Put their public names in the closing report.
- Leave the worktree (`cd` out of it) before `finish`, so `finish` can remove it.
