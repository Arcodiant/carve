---
description: >-
  Carve.Core holds an indexed polygon mesh: validation, stats, flat face normals and ear-clipped triangulation
---
# T-1 — Polygon mesh kernel

**Status:** In Progress

**Pri:** P1

**Milestone:** M-1

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

`Carve.Core` holds a validated polygon mesh and everything the tools need to know about it, tested with no GPU.

## Scope

In `src/Carve.Core`, engine-free:

- `PolygonMesh`: immutable, vertices (`Vector3`) and faces (index lists of any size of 3 or more). Plain indexed storage; M-2 decides the edge-connected structure.
- `PolygonMesh.Create(vertices, faces)` returns the mesh or a list of errors, each naming the element: a non-finite coordinate (`vertices[3]`), a face under 3 indices, an index out of range (`faces[2][1]`), a repeated index in a face, a zero-area face, no faces.
- `MeshReport`: vertex, face and edge counts, `closed`, bounds, and warnings for what is legal but likely a mistake: an open edge, an edge shared by more than two faces, two faces crossing an edge in the same direction (inconsistent winding), a non-planar face (with a tolerance relative to the face's size), a vertex no face uses.
- Face normal by Newell's method, outward for a face wound counter-clockwise seen from outside.
- `Triangulator`: ear clipping in the plane of the Newell normal, handling concave faces. Output triangles keep the face's winding and record which face each came from. A face it cannot clip is a validation error, not a bad draw.

Tests in `tests/Carve.Core.Tests`, a new class per concern: every error and warning above; a unit cube is closed with 8/6/12 and six axis normals; a concave L-shaped hexagon triangulates into four triangles of correct total area with all normals agreeing; triangle output for a quad; two adjacent faces wound oppositely warn. No test scenario here: nothing is on screen yet.

## Notes

- Winding is **counter-clockwise from outside** (the glTF convention). This is what agents write; only the render bridge (T-4) knows the engine's is the opposite.
- `CLAUDE.md` says the mesh is triangulated only on export. Drawing needs triangles too: triangulation is a derived view for render and export, never stored as the mesh. Keep it that way.
- No half-edge, no element ids, no edit operations: all M-2. Do not build ahead of it.
- No file outside `src/Carve.Core` and `tests/Carve.Core.Tests`.
- Writes down for T-3 and T-4: the public names of `PolygonMesh`, `MeshReport` and `Triangulator`, in the task's closing report.
- Moderately design-heavy: `MeshReport`'s fields become the tool's return shape. A rule this leaves open (such as what the planarity tolerance means) goes back to the person rather than landing.
