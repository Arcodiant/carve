---
description: >-
  A Carve material names a texture graph output, graphs read the mesh's baked maps, and Carve re-bakes them when the mesh or UVs change
---
# M-29 — Graphs on a mesh

**Status:** Planned

**Depends on:** M-27, M-24

**One-liner:** *A Carve material names a texture graph output, graphs read the mesh's baked maps, and Carve re-bakes them when the mesh or UVs change*

## What this milestone delivers

Carve uses the shared texture-graph package from [M-27](M-27-a-first-texture-graph.md) directly. A material can name a graph file and an output in place of a PNG, and the graph can read the mesh.

- `define_material` takes a graph path and output for any texture slot. The Carve file keeps it, relative to the file as texture paths are.
- Mesh-input nodes, as a Carve extension to the package: curvature, occlusion, an id mask per group or material, and position, from [M-24](M-24-baked-maps.md)'s bakes.
- `render` and `export` evaluate the graph on the mesh's UVs. An edit to the mesh or its UVs makes the next render or export bake again, never a stale texture.
- Errors: a missing graph file lists the graph files in its folder, as a missing texture does (T-81), and a graph error names the node.

Content: the barrel with edge wear on its iron hoops from curvature, and grime in its stave gaps from occlusion.

## Estimated size

About **6 tasks**:

1. A material slot names a graph output, with a new Carve file version.
2. Mesh-input nodes from M-24's bakes.
3. Evaluation in `render` and `export`, with caching and re-baking after edits.
4. Errors and `check` for graph materials.
5. The weathered barrel and its scenario.
6. Acceptance: fresh agents weather a model and recover from a planted wrong graph path.

## Why

[D-7](../design/D-7-texture-graphs-and-layered-surfaces.md): mesh-driven masks are where a layered material gets its value. M-24's bakes are those masks. A graph that reads them belongs in Carve, so it is re-baked when the mesh changes.

## Open at planning

- Whether M-24 already writes its bakes in a form the nodes can read, or this milestone wraps its PNGs (D-7, *Open questions*).
- Evaluation resolution per material, and how long a bake may take before `render` should warn.
- Does M-28 need to land first, or are M-27's nodes enough for the content?

## Required tasks

Not planned yet.

## Retrospective
