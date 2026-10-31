---
description: >-
  Carve.Core holds an indexed polygon mesh: validation, stats, flat face normals and ear-clipped triangulation
---
# T-1 — Polygon mesh kernel

**Status:** Done

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

### As built

Ran on Claude Sonnet 5.5. All in `src/Carve.Core`, namespace `Carve.Core`; no file outside Core and its tests.

- `PolygonMesh` (sealed, immutable): `Vertices: IReadOnlyList<Vector3>`, `Faces: IReadOnlyList<IReadOnlyList<int>>`, `static MeshResult Create(IEnumerable<Vector3>, IEnumerable<IEnumerable<int>>)`, `Vector3 FaceNormal(int face)` (Newell, unit, outward for CCW-from-outside). Consts `PlanarityTolerance = 1e-4f`, `ZeroAreaTolerance = 1e-9f`.
- `MeshResult`: `Mesh` (null on failure), `Errors: IReadOnlyList<MeshError>`, `Succeeded`. `MeshError(Element, Message)`, Element like `vertices[3]`, `faces[2]`, `faces[2][1]`. A face the triangulator cannot clip is reported as an error on `faces[i]`.
- `MeshReport.Create(PolygonMesh)`: `VertexCount`, `FaceCount`, `EdgeCount` (distinct undirected), `Closed` (every edge in exactly two faces), `BoundsMin`/`BoundsMax` (of vertices faces use), `Warnings: IReadOnlyList<MeshWarning>`. `MeshWarning(Kind, Element, Message)`; `MeshWarningKind`: `OpenEdge`, `NonManifoldEdge`, `InconsistentWinding`, `NonPlanarFace`, `UnusedVertex`. Edge elements are named `edges[lo-hi]`.
- `Triangulator` (static): `Triangulate(PolygonMesh) -> IReadOnlyList<MeshTriangle>`, `TriangulateFace(PolygonMesh, int)`. `MeshTriangle(int A, int B, int C, int Face)` keeps the face's winding and records its source face. Triangles are never stored on the mesh.
- Planarity meaning (the spec said "relative to the face's size"): a face of 4+ vertices warns when any vertex is further than `1e-4 x` the face's bounding-box diagonal from the plane through its centroid along the Newell normal. Zero area: area below `1e-9 x` diagonal squared. These constants are a reading of the spec, easy to change if the person wants other values.
- A collinear vertex in a face is tolerated (dropped during clipping, no degenerate triangle). Only a straight-through one (neighbours on opposite sides) is dropped; an outline that folds back (adjacent edges turning back 180 degrees, or collinear edges overlapping along a length) is an error on `faces[i]` ("folds back"), and a crossing or fold is checked before clipping so the message names the edges. A vertex merely touching another edge is accepted. `cannot be triangulated (degenerate outline)` is what remains. One face with several faults (length, out-of-range indices, repeats) reports all of them in one call.
