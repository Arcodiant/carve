---
description: >-
  A bake map that casts rays from the low mesh's surface to a high-detail source mesh and writes the difference as a tangent-space normal map, so displaced detail ships on low-poly geometry
---
# T-225 — bake a detailed mesh's normals onto a simpler one

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

Detail modelled on a dense mesh (for example one made rough by [T-223](T-223-displace-move-vertices-along-their-normals-by-a-graph-output-or-seeded-noise.md)'s `displace`) ships as a normal map on the low-poly mesh a game can afford. This is the usual "bake high to low" workflow.

## Scope

- `bake` gains a map, `detail_normal`, with a block `{source: <mesh name>, reach: metres, required; inward: metres, default reach}`. It writes `<folder>/<mesh>.detail_normal.png`.
  - For each covered texel of the low mesh, a ray goes from its surface point along the interpolated shading normal, both outward up to `reach` and inward up to `inward`. It takes the nearest hit on `source` and reads the source's interpolated shading normal there.
  - That normal is written in the low mesh's tangent frame: the one `BevelNormal` and `TangentFrame` (src/Carve.Core/Baking) use, glTF green up, matching the engine's own frame because no tangents are exported. So a normal that matches the low mesh's shading normal writes (128, 128, 255).
  - A texel whose ray misses writes the flat normal. The result reports `missed` (the fraction of texels that missed) and warns above a few percent, naming `reach`.
- It reuses `RayCaster` (as occlusion does) and the existing padding. The source mesh needs no UVs. The source can be a mesh in the same model kept for baking (for example the T-223 copy), and the low mesh's UVs must not overlap, as for every bake.
- Errors (listed together, as the other bake errors are): an unknown `source` (the error lists the meshes); `source` equal to `mesh`; a missing or non-positive `reach`; the block given without the map (ignored with a warning, as `bevel` is).
- Tests:
  - A low mesh baked against an identical source gives the flat normal everywhere.
  - A flat quad against a source plane tilted by a known angle gives the hand-worked normal.
  - A source with one bump shows the bump at the right texels, and green-up orientation is checked as T-125 checked it.
  - Two bakes are byte-identical.
  - A source wholly out of reach gives `missed` 1 and the warning.
- Content: the T-223 boulder. A low-poly cube is unwrapped, then baked against its displaced subdivided copy, and renders of the low mesh with and without the map go in the task's artifacts.

## Notes

- Raised on 2026-11-25 in a discussion about rough surfaces, alongside T-223 and [T-224](T-224-normal-blend-combine-two-tangent-space-normal-maps-in-a-patina-graph.md). The person agreed to reserve it.
- It is most useful after T-223 but does not depend on it: any dense source mesh works. Blending its output with the bevel normal is T-224's job.
- Not checked yet: whether `RayCaster` is fast enough for a dense source at 2048 texels. If it is not, an acceleration structure belongs in this task.
