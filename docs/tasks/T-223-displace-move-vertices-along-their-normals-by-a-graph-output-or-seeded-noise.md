---
description: >-
  A destructive edit that offsets each selected vertex along its normal by a Patina greyscale output at its UVs, or by seeded 3D noise at its position, for rough and irregular geometry
---
# T-223 — displace: move vertices along their normals by a graph output or seeded noise

**Status:** In Progress

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
