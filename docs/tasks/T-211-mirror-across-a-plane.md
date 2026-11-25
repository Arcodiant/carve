---
description: >-
  mirror copies faces across an axis plane, welds the vertices on the plane, appends the new ids in source order with loops reversed, and carries UVs, colours, groups and seams
---
# T-211 — Mirror across a plane

**Status:** Open

**Pri:** P1

**Milestone:** M-10

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, server-unit

## Goal

An agent builds one half of a shape and mirrors it into a whole: the copy is welded to the original on the plane,
faces outward, and has ids an agent can predict.

## Scope

- **Core, `src/Carve.Core/MirrorPlane.cs`** (new): an axis (0, 1 or 2) and `at`, with `Reflect(Vector3)` and
  `OnPlane(Vector3)`. A vertex is on the plane when its distance to the plane is at most 1e-5 m. T-212 reuses this.
- **Core, `src/Carve.Core/MeshMirror.cs`** (new), `MeshMirror.Mirror(mesh, faces, plane)`. `faces` defaults to every
  live face.
  - **Welding:** a vertex of the copied faces that lies on the plane is reused, keeping its id and position. Every
    other vertex of those faces gets a new vertex at its reflection, appended in ascending source-id order after the
    mesh's last id (tombstones included).
  - **Faces:** each copy is appended in ascending source-face order. Its loop is reversed from its first corner,
    `[c0, c(n-1), …, c1]`, and its holes are reversed too, so its normal faces out.
  - **A copied face lying wholly in the plane** (every corner on it) is refused, naming the face: delete it first.
    The wheel (T-215) does exactly that.
  - **Layers through T-71's `EditProvenance`:**
    - each new vertex comes from its source;
    - each new face takes its source's face values (colour, material, groups), and its corners are mapped by stencils
      onto the reversed order, so UVs follow their corners;
    - seams and hard edges are copied onto the mirrored edges.
    If the provenance cannot carry an edge layer onto a new edge, extend it in Core; do not work around it in the tool.
    The copied UVs wind the other way, and `check` reporting them flipped is expected and correct. The description
    says so and names `unwrap`.
  - **Refused on a mesh with vertex weights or morph targets**, naming them (decided at planning): an L/R bone swap and
    reflected deltas are rules for later.
- **Server, `src/Carve.Server/MirrorTool.cs`** (new), tool `mirror`: `axis` (`"x" | "y" | "z"`, read in this file),
  `at` (default 0, in the mesh's own space), optional `faces`, and `mesh`. It returns define_mesh's report fields with
  `created: {vertices, faces}`, `welded: [ids]`, and `deleted` and `split` empty. Register it in
  `CarveServer.AddTools` after `move` (T-210 adds rotate and scale there too; keep both sides).
- **Refusals:** an unknown face id, a bad axis, a non-finite `at`, a face in the plane, weights or targets, an argument
  `mirror` does not take. Every problem is named, and the mesh is left unchanged.
- **The description**, at most 3,686 characters. Its worked example is on define_mesh's unit cube: `delete
  {"faces":[4]}` (the x = 0 face), then `mirror {"axis":"x"}`, which gives a closed 2 x 1 x 1 box of 12 vertices and
  10 faces, with 4 welded. Both calls are run as written by a test.
- **Tests** (`tests/Carve.Core.Tests/MirrorTests.cs`, `tests/Carve.Server.Tests/MirrorToolTests.cs`):
  - by hand on the cube;
  - an asymmetric, non-axis-aligned shape mirrored at a non-zero `at`;
  - a face with a hole;
  - after a delete (new ids after tombstones);
  - closed and consistent winding after a welded mirror;
  - UVs, colours, groups and seams on the copies;
  - mirror twice (a copy of a copy);
  - every refusal, and the worked example.
- **`README.md`**: `mirror`, with its worked calls.

## Notes

- **Decided at planning (2026-11-25, by the person):** a copy, welded on the plane, ids appended in source order with
  loops reversed, and refused on weighted or morph-targeted meshes. Only axis planes are allowed; a general plane was
  not offered.
- **Design-heavy:** the id order and `MirrorPlane` are what T-212's pairing and T-215's sample counts rest on. Write the
  rule into the description and `### As built`.
- Ask, don't land: snapping welded vertices exactly onto the plane, welding off-plane coincident vertices, and any rule
  this does not settle.
