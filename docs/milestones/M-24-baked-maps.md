---
description: >-
  Carve bakes occlusion, bevel normals and id masks into a mesh's UV layout as PNGs a material can name
---
# M-24 — Baked maps

**Status:** Planned

**One-liner:** *Carve bakes occlusion, bevel normals and id masks into a mesh's UV layout as PNGs a material can name*

## What this milestone delivers

An agent asks Carve for maps computed from the mesh, and names them in a material.

- Ambient occlusion into the UV layout, with ray count and reach.
- A normal map that rounds hard edges by a given width.
- A mask per group or material, and a curvature map for wear.
- Padding past island borders, and output paths a material from [M-7](M-7-materials-and-textures.md) can use.

Content: one fps-arms tool textured from Carve bakes alone.

## Why

Raised by the fps-arms project (private repo `Arcodiant/fps-arms`, 2026-11-15), which built a rigged, textured pair of first-person arms and four tools with Carve 0.9.0 through script-written `.carve` files.

- [M-7](M-7-materials-and-textures.md) takes supplied PNGs and has no texture-making tool.
- fps-arms wrote its own bakes on Carve's UVs: ray-cast occlusion, a 1.6 mm edge bevel into the normal map, and group masks for colour, roughness and emission.
- Each needed the mesh, its hard edges and its UVs read back out of the Carve file.

## Open at planning

- Tangent convention and where tangents come from (glTF, green up).
- Resolution, padding and how occlusion, roughness and metallic are packed.
- Occlusion between separate meshes, and for a skinned mesh, in which pose?

## Required tasks

Not planned yet.

## Retrospective
