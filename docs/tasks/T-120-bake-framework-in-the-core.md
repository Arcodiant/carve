---
description: >-
  The Core lays a mesh's UV layout onto texels with a position, shading normal and engine-matched tangent frame per texel, refuses overlapping UVs, pads past island borders, finds nearby shading-break edges, and bakes a mask per group or material
---
# T-120 — Bake framework in the Core

**Status:** In Progress

**Pri:** P1

**Milestone:** M-24

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit

## Goal

An engine-free bake kernel in `Carve.Core` that every M-24 map is built on, and that M-29's mesh-input nodes wrap.
For each texel it gives the surface the texel shows, in the tangent frame the engine uses to read a normal map. It also
pads a baked image past its islands and finds the edges near a texel. The first map, a mask per group and per
material, proves the kernel end to end.

## Scope

- **`src/Carve.Core/Baking/`, public API** (M-29 builds on it, so name and document it with care):
  - `BakeImage`: width, height, a channel count of 1 to 4, and `float` values, row-major with the top row first.
    Texel (x, y) has its centre at uv ((x + 0.5) / width, (y + 0.5) / height), so uv (0, 0) is the top-left corner,
    as in M-7's textures and the `uvs` look.
  - `BakeLayout`, the coverage of one mesh at one square power-of-two size (64 to 4096, M-7's `MaterialTexture.MaxSize`).
    It covers every mapped face, or only the faces given. For each covered texel it holds the face, the triangle of
    `Triangulator`'s triangulation, barycentric weights, and:
    - the world position at rest, from the mesh's own positions, never a pose;
    - the interpolated shading normal, which is what render lights: the face normal on a flat mesh, `Shading`'s corner
      normals on a smooth one;
    - the geometric face normal;
    - the tangent frame (below).
  - **Coverage rule.** A texel is covered by the triangle whose UV triangle holds its centre. A tie on a shared edge is
    settled by a top-left fill rule, so no texel is counted twice. A texel whose centre lies outside every triangle but
    within half a texel's diagonal of one is also covered: it is clamped to the nearest point of the nearest
    triangle, so a face a few texels wide still samples. A texel within reach of two faces takes the nearer; a tie
    goes to the lower face id. The result records which texels were covered by their centres and which by the clamp.
  - **Overlap is refused.** When two baked faces' triangles both hold one texel centre, `BakeLayout` returns an error
    naming the face pairs, at most the first eight with a count of the rest. A tool passes that error through as it
    stands. Faces with no UVs are skipped and reported in a list, never baked. A mesh with no UVs is an error.
  - **Padding.** `Pad(image, layout, texels)` fills texels that no face covers, out to `texels` from the nearest
    covered texel (Euclidean distance in texels). Each takes the value of that nearest covered texel; a tie goes to
    the lowest row-major index. A padded texel never changes a covered one, so padding never bleeds into another
    island. Padding goes from 0 to `size / 2`.
  - **Tangent frame, matching the engine (decided at planning, from a probe; see Notes).** Empyrean's `applyNormalMap`
    uses no vertex tangent. It builds a cotangent frame per fragment from `ddx`/`ddy` of the world position and the
    uv, about the interpolated normal N. Over one triangle that frame is constant up to N:
    - with `dp1`, `dp2` the triangle's edges and `duv1`, `duv2` their UV deltas,
      `T = cross(dp2, N) * duv1.x + cross(N, dp1) * duv2.x` and `B = cross(dp2, N) * duv1.y + cross(N, dp1) * duv2.y`;
    - both are scaled by `1 / sqrt(max(|T|², |B|²))`.

    Store T and B per texel. Give `ToTangentSpace(worldNormal)` and `FromTangentSpace(encoded)`, with glTF's encoding
    `127.5 + 127.5 * n`, green up. Whether the engine flips the sign of B for the image's downward v is worked out by
    this task and held by a unit test that reproduces the probe: on the +Z face of a unit box with u = x and v = 1 − y
    (`MaterialRenderTests.FrontUvs`), red is world +x and green is world +y. If the cotangent formula above gives the
    other sign for G, the formula is wrong for this engine and the test says which. Do not guess.
  - **Shading-break edges.** The edges render draws hard:
    - on a smooth mesh, its marked hard edges (`MeshShade.Hard`);
    - on a flat mesh, every edge between two faces that are not coplanar, by the coplanarity tolerance Carve already
      uses (name it in the code). If none fits, stop and ask.

    Boundary edges, with one face, are not breaks. `NearEdges(texel, width)` lists each break edge of the texel's own
    face within `width` metres of the texel's world position, distance measured in 3D to the edge segment. For each it
    gives the distance, the other face's normal, the dihedral bend angle (0 when flat) and whether the edge is convex
    or concave. It works across UV seams, since it never looks at UVs.
  - **Masks.** `Masks(layout, mesh)` gives one single-channel image per group and per material the mesh's faces use.
    A texel is 1 where its face is in the group or has the material, else 0, before padding. The order is the groups'
    stored order, then the materials' first-use order.
- **Determinism.** No `System.Random`, no clock. Parallel loops are allowed only where the result cannot depend on
  scheduling. Two runs give bit-identical floats.
- **Tests** (`unit`, `tests/Carve.Core.Tests`, each well under a second, so use sizes of 64 to 256):
  - a unit quad at 64 covers exactly the texels whose centres it holds;
  - a sliver face two texels wide is covered by the clamp;
  - two islands sharing an edge in UV give no overlap and no texel twice;
  - a stacked island is refused, naming both faces;
  - unmapped faces are listed;
  - padding: a ring of `n` texels, nearest island first, covered texels untouched, the tie rule, and a padding of 0
    leaves the image as it was;
  - the frame test above, plus a mirrored island (u = 1 − x), where red flips and green does not;
  - `NearEdges` on a flat box (every edge a break, 90° convex), on an L-block's inner edge (concave), and on a smooth
    mesh with one marked hard edge;
  - masks on a two-group box;
  - determinism;
  - `samples/barrel.carve` lays out at 256 with no overlap. That is the barrel's own unwrap, a real layout.
- **Writes down:** `CLAUDE.md`'s architecture rules gain one line: bakes are computed in `Carve.Core`,
  deterministically, at rest, and are never stored in the Carve file. D-7's *Open questions* entry on M-24's outputs is
  answered: M-24 gives this Core API and M-29 wraps it as nodes.

## Notes

- Decided at planning (2026-11-18), by the person: the tangent frame matches the engine (green up, the per-triangle
  derivative frame, no `TANGENT` export); bevel and curvature use shading-break edges measured in 3D; occlusion and
  every bake are at rest; overlapping UVs are refused with the faces named; unmapped faces are skipped with a warning;
  sizes are powers of two from 64 to 4096; padding fills only uncovered texels, nearest island first.
- **The probe** (2026-11-18, a throwaway regression test, deleted). A constant normal map was put on the +Z face of a
  unit box with `FrontUvs`, base colour #808080, and read at the face centre in `render`'s front view. There the top
  face is the brightest and +X is darker than +Z. Results, as (R, G, B):
  - flat (128, 128, 255): (112, 118, 129);
  - G+ (128, 218, 218): (118, 128, 144);
  - G− (128, 38, 218): (106, 108, 111);
  - R+ (218, 128, 218): (108, 115, 126);
  - R− (38, 128, 218): (114, 120, 130).

  So G+ leans the normal toward world +y, which is image up, and R+ toward +x, which is +u. This is glTF's convention,
  and the engine reads it that way.
- Chosen by the orchestrator, each a stop if it proves wrong: the clamp ring of half a texel's diagonal; nearest-texel
  padding rather than averaging; the tie rules.
- **Stop and ask** before landing: a coverage or padding rule other than the above, a frame that disagrees with the
  probe, or a public type M-29 could not wrap without reaching into internals.
- Design-heavy and decides what T-122 and T-124 to T-126 build on, and M-29 after them. Opus. The layout, frame and
  edge query are the hard part; the masks are small.
- Runs beside T-121, which touches only `Carve.Server`'s `uvs` look. This task owns `src/Carve.Core/Baking/` and
  creates it. Later tasks add one file each there.
- T-71 (off-milestone, in progress) moves groups, colours and hard edges onto attribute layers, with their public
  behaviour unchanged. Read groups, materials and hard edges through `FaceStyle` and `MeshShade` as they stand, and
  rebase onto T-71 if it lands first.

### As built

Built by Claude Opus 5.5 (2026-11-19). `src/Carve.Core/Baking/`, namespace `Carve.Core.Baking`:

- `BakeImage(width, height, channels)`: `float` values row-major, top row first (`Values`, `this[x, y, c]`, `Texel`,
  `CentreUv`, `Clone`).
- `BakeLayout.Create(mesh, size[, faces])` gives a `BakeLayoutResult` (`Layout`, `Error`, `Unmapped`). The layout has
  `Mesh`, `Size`, `Triangles` (`Triangulator.Triangulate`'s list), `Faces`, `Unmapped`, `CoveredCount`,
  `ClampedCount`, `CoverageAt`, `IsCovered`, `FaceAt`, `TryGetTexel`, `Texels()` (row-major), `NewImage` and
  `NearEdges(texel, width)`. Its constants are `MinSize`, `MaxSize`, `ClampReach` (√0.5 texels) and `NamedOverlaps`
  (8), and its messages `SizeProblem`, `OverlapMessage` and `NoUvsMessage`. A `BakeTexel` holds the face, the triangle
  index, the barycentric weights for corners A, B and C, the `BakeCoverage` (`Centre` or `Clamp`), the uv, the rest
  position, the unit shading normal (from `Shading.Corners`, never `PosedCorners`), the face normal and a
  `TangentFrame`. The layout stores only the triangle, the weights and the clamp flag for each covered texel. The rest
  is interpolated from the triangle each time a texel is read, the same way every time, so a 4096 layout stays small.
- `TangentFrame(T, B, N)`: `Of(...)`, `Cotangent(...)` (the formula as written), `ToTangentSpace(world)` and
  `FromTangentSpace(encoded)`, both in glTF's 0 to 255 encoding `127.5 + 127.5 * n`. Decoding is
  `normalize(T x + B y + N z)` and encoding inverts it exactly (Cramer's rule in double), so T and B can be non-unit.
- `Bake.Pad(image, layout, texels)` returns a padded copy. `Bake.Masks(layout, mesh)` returns `BakeMask(Source, Name,
  Image)` in order: groups, then materials. `Bake` is a `static partial class`, so each later map adds one file.
- `EdgeShape` (`Flat`, `Convex`, `Concave`) and `NearEdge(Lo, Hi, OtherFace, Distance, Closest, OtherNormal, Bend,
  Shape)`. `Bend` is in radians. `BakeLayout.AreCoplanar(mesh, f, g)` is public.

Readings landed:

- **Green is the formula's second vector negated.** On `FrontUvs`, the cotangent formula as written gives B' along
  world −y (along +v, down the image). The probe says +G leans toward world +y. So `TangentFrame.B = −B'` and T is as
  written. `The_frame_matches_the_probe_...` holds both facts: it checks the raw `Cotangent` along −y and the frame's B
  along +y. The mirrored island flips red and keeps green. The frame agrees with the probe. `Cotangent` is public so
  the test can show the sign.
- **Coplanarity uses `PolygonMesh.PlanarityTolerance`**, the planarity rule of the non-planar check. It is applied to
  the pair: two faces are coplanar when their normals point the same way and every corner of each lies within
  `PlanarityTolerance` × the bounding-box diagonal of both faces' corners of the other face's plane. Each plane is
  taken through the mean of its outer loop, as `MeshReport` does. So two faces are coplanar exactly when the check
  would pass them merged into one face. Carve has no tolerance of its own for comparing two faces. This is the one it
  uses for "planar".
- **On a smooth mesh, breaks are the edges `Shading` never smooths across:** marked hard edges, plus non-manifold
  edges (three or more faces, or two wound the same way). That is "the edges render draws hard". An edge of 3 or more
  faces is listed once for each other face. On a smooth mesh, a marked edge between coplanar faces is `Flat` with
  bend 0.
- **Convex or concave** is read from the side of the texel's face plane that the other face's outer-loop mean lies
  on, measured from the edge's midpoint.
- **Overlap** names every pair of faces whose triangles hold one centre. Pairs are sorted, the first eight are named
  and the rest counted. A face whose own triangles overlap in UV is named as `faces[f] and faces[f]`.
- **Top-left rule.** Coverage is worked in texel space with y down. A centre on a shared vertical edge goes to the
  island on its right, and one on a horizontal edge goes to the island below it. The edge functions are exact in
  double: float uv × a power of two gives 24-bit operands.
- **Clamp ties** go to the lower face and then to the lower triangle. Distances are squared and compared against 0.5.
- **Padding** is exact Euclidean, with the tie at the lowest row-major index. It uses a column pass (the upper of two
  equidistant rows) and then a row scan bounded by the padding and the best found. Rows run in parallel, but each row
  writes only its own texels.
- **Masks** have one image for every stored group, so a group with no faces gives an all-zero mask, then one for each
  material in order of its lowest live face id. The mesh argument supplies the style. It may be the layout's mesh
  restyled, but its face count must match.
- **`faces` that name no live face are an error** (`faces[n]: ...`). A mapped face with a zero-area uv triangle covers
  nothing.

Tests: `tests/Carve.Core.Tests/BakeLayoutTests.cs` (15, unit, about 1 s in total) covers every item in Scope's test
list, and `samples/barrel.carve` lays out at 256 with no overlap. Not checked: timings at 4096. There is no frame or
engine run, since this member is Core-only.
