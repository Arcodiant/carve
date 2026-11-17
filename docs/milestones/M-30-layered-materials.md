---
description: >-
  A Carve material is a stack of layers, each masked by selections, groups or the mesh's baked maps, and seen layer by layer
---
# M-30 — Layered materials

**Status:** Planned

**Depends on:** M-29

**One-liner:** *A Carve material is a stack of layers, each masked by selections, groups or the mesh's baked maps, and seen layer by layer*

## What this milestone delivers

A material is no longer one set of textures but a stack: each layer is a graph output or flat PBR values, laid over the ones below it through a mask.

- The layer stack in `Carve.Core`: kept through edits and undo, and saved in the Carve file.
- Tools to add, edit, reorder, remove and read back layers.
- Masks from a `select` result, a group or material, and from [M-29](M-29-graphs-on-a-mesh.md)'s mesh-input maps, combined with a graph for breakup.
- `render` shows one layer alone, or a layer's mask, so an agent sees what each contributes.
- Export flattens the stack into the material's PNGs.

Content: a crate or prop with base wood, painted planks chosen by `select`, and dirt from occlusion, as separate layers.

## Estimated size

About **6 tasks**:

1. The layer stack in the Core, the Carve file and undo.
2. Layer tools.
3. Masks from selections, groups and mesh maps.
4. Rendering a layer or mask alone, and flattening on export.
5. The layered prop and its scenario.
6. Acceptance: fresh agents layer a material and change one layer without touching the others.

## Why

[D-7](../design/D-7-texture-graphs-and-layered-surfaces.md): Painter's value to an agent is its layer stack, not its brush. Stored layers mean an edit to one never means repainting the rest.

## Open at planning

- Whether a layer's mask from `select` keeps face ids, so it follows the faces through later edits, or is evaluated once.
- Blend modes beyond normal and multiply.
- Whether the stack is per material or per mesh.

## Required tasks

Not planned yet.

## Retrospective
