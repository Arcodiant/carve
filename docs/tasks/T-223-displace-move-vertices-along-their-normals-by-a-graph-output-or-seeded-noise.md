---
description: >-
  A destructive edit that offsets each selected vertex along its normal by a Patina greyscale output at its UVs, or by seeded 3D noise at its position, for rough and irregular geometry
---
# T-223 — displace: move vertices along their normals by a graph output or seeded noise

**Status:** Done

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

An agent can make a rock, a cliff, bark or a dented hull rough in its geometry, not only in its shading: one call offsets the vertices of a subdivided mesh by a height field, repeatably.

## Scope

- A new MCP edit `displace`, an edit on the mesh as `subdivide` and `extrude` are (undoable, journalled like the other edits), over the whole mesh or a selection.
- Each vertex moves along its vertex normal (the area-weighted average of its faces' normals, taken before any vertex moves) by `amount` x (h - `midlevel`), where h is 0..1:
  - `source: {graph, output}`: a Patina greyscale output sampled bilinearly at the vertex's UVs, wrapping. A vertex whose corners hold different UVs (a seam) takes the mean of its samples, so the seam does not tear. A mesh with no UVs is refused with a pointer to `project_uvs` or `unwrap`.
  - `source: {noise: {seed, scale, octaves}}`: a seeded 3D gradient noise (fBm) at the vertex's object-space position, needing no UVs and having no seams.
  - Parameters: `amount` (metres, may be negative), `midlevel` 0..1 (default 0.5), and an optional `axis` that replaces the normal with a fixed direction (for terrain).
- Ids, faces, UVs and groups are kept; only positions change. The output reports the largest offset and warns when the mesh is too coarse for the source to show (for example, fewer vertices across than the graph has features).
- Errors: a missing graph file lists the folder's graph files (as T-81 does for textures), a colour output is refused by name, and a selection that matches nothing says so.
- Tests: the same seed and arguments give bit-identical positions; a flat grid displaced by a known ramp matches hand-worked heights; a closed mesh stays closed and no vertex on a UV seam splits; `undo` restores every position exactly.
- Content: a rough boulder sample (a subdivided cube, then `displace` with noise) and its render.

## Notes

- Raised in a discussion on 2026-11-25 about whether Carve can make a rough, irregular surface. Answer: only with normal maps from Patina graphs, not in geometry. The person agreed to reserve this.
- D-7 says no procedural *geometry graph* is planned. This task stays inside that: it reads a texture graph's output as data and adds no geometry graph.
- If M-14 or D-5 makes finishing edits modifiers, follow whatever they decide. Until then this is a destructive edit.

### As built

- **Core** (`src/Carve.Core/Displace.cs`): `Displacement.Displace(mesh, vertices, source, amount, midlevel, axis, out DisplaceReport?, faces)` returns an `EditResult` (layers carried by `EditResult.From`, so UVs, groups, colours, weights, shading and targets are kept). Sources: `FieldSource(HeightField, name)` (a row-major field, top row first, sampled bilinearly and wrapping with texel centres at (x + 0.5) / w, as `BakeImage`) and `NoiseSource(seed, scale, octaves)`. `Noise` is Perlin's improved gradient noise in double with an integer hash of lattice point and seed (no `System.Random`); fBm doubles frequency and halves amplitude per octave, each octave shifted off the lattice by its own seed-derived fraction; h = clamp(0.5 + sum / (2 x amplitudes' sum)). Vertex normals: sum of `FaceNormal x FaceArea` over live faces, taken before any vertex moves; a vertex with a zero normal and no axis stays. `DisplaceReport` holds the largest offset and the coarseness warning.
- **Tool** `displace` (`src/Carve.Server/DisplaceTool.cs`, registered after `subdivide`): `source` `{graph, output}` or `{noise:{seed, scale, octaves}}`, `amount` (required), `midlevel`, `axis`, and `vertices`/`faces` (union) or `group`; none means every vertex on a live face. Result is the edit report plus `moved` and `largest`. Carve.Server now references `Patina.Graphs`. `DefineMaterialTool.Missing` takes an extension so the missing-graph error lists `.texgraph` files as T-81's lists `.png`; `SubdivideTool.Warnings` takes the tool name so displace folds not-planar warnings as "(expected after displace)".
- **Content**: `samples/boulder.carve` is the worked example as written (cube, subdivide 3, displace seed 7 scale 0.35 octaves 5 amount 0.6, set_shading smooth), kept equal by `DisplaceToolTests`; set `CARVE_WRITE_BOULDER=1` to rewrite it. `samples/boulder.png` is scenario-cuts' `boulder-front` frame. The Cuts scenario's `DisplaceStage.cs` shows the boulder before, front, back and after undo (byte-equal to before).
- **README**: a `displace` bullet after `subdivide`, naming the sample and its frame.

Decisions the Scope left open:
- The graph is evaluated at its own size, with no parameter overrides; `output` defaults to `height`.
- Noise defaults: seed 0, scale 1 m (the first octave's lattice cell), octaves 4 (1 to 8).
- Selection takes `vertices`, `faces` or `group` as move and set_weights do; "matches nothing" is an empty list or an empty group.
- A selected vertex on no mapped face is refused for a graph source (the vertices named, first 10).
- Coarseness: noise warns when the selected edges' mean length is over scale / 2; a graph warns when 1 / (mean UV edge length) is under 2 x the field's features across (mean-crossings per row and column, halved).
- The scenario stage does not use the Cuts `Solid.Seen` test: a displaced quad is not flat, so the stage casts against both triangle pairs of every face and counts a probe only where both agree (a 5 cm camera shift fails 49 probes).

Not verified: the `.glb` export of a displaced mesh read back through the engine (nothing new in export); a graph with `parameters` or nested `graph` nodes as a source (resolves by `TexGraphFile.ReadFile`, untested here); performance of an 8192 graph as a source (evaluated whole into memory, 256 MB at 8192).
