---
description: >-
  A material states its texture's physical size, and a world projection maps any face at that scale with an anchor and a grain direction
---
# M-37 — Real sizes

**Status:** Planned

**Depends on:** M-29

**One-liner:** *A material states its texture's physical size, and a world projection maps any face at that scale with an anchor and a grain direction*

## What this milestone delivers

A texture has a size in the world, and a face is mapped at that size without the agent working out a scale for each call.

- A material states its texture's physical size, for example one repeat to 2.4 m. It is set once, on the material.
- `project_uvs` gains a `world` mode that maps any face at its material's scale.
- An anchor (such as y = 0 for walls), so stone courses line up across adjacent walls and into a window's reveals.
- A grain direction for a face, a group or a part, so beams, door boards and ceiling boards run the right way.
- A patina graph states its physical size, `export` writes it beside the PNGs, and `define_material` takes an export folder and fills every slot and the size from it in one call.

Content: two walls meeting at a corner with a window between them, their courses continuous across the corner and the reveal; a beam and a door with their grain along their length.

## Estimated size

About **5 tasks**:

1. The physical size on a material and in the Carve file (a new file version).
2. `project_uvs world`, with the anchor.
3. Grain direction.
4. The size in a patina graph and its export, and `define_material` from an export folder.
5. The corner sample, its scenario, and acceptance: fresh agents texture a room without a seam in the courses.

## Why

The stone-room project's feature request (2026-11-22, items 2 and 8). Its script worked out every UV scale and offset by hand so that the stone would be one size everywhere.

## Open at planning

- [D-7](../design/D-7-texture-graphs-and-layered-surfaces.md) says every graph parameter is relative to the image. A physical size is new metadata on a graph, not a parameter; confirm that reading with the person.
- How this sits with [M-29](M-29-graphs-on-a-mesh.md), where a slot names a graph output directly: the size then comes from the graph, with no export folder.
- Shared UVs against anchored UVs (D-8, Open): an anchored part cannot be an instance.
- The unit is metres, as the model's is. Does the size differ in u and v?
- The UV-overlap warning on a tiling material ([M-23](M-23-quiet-on-dense-meshes.md)'s intent flags) should be settled before this, or every world projection warns.
- This milestone adds a Carve file version. So do M-29, M-35 and M-38: none of them runs beside another.

## Required tasks

Not planned yet.

## Retrospective
