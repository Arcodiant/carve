---
description: >-
  The texture server gains the noises, patterns and filters most game materials need, a check tool, and graphs reusable as nodes
---
# M-28 — A node library

**Status:** Planned

**Depends on:** M-27

**One-liner:** *The texture server gains the noises, patterns and filters most game materials need, a check tool, and graphs reusable as nodes*

## What this milestone delivers

An agent can make most stylised game materials (stone, brick, metal, fabric, wood) from [M-27](M-27-a-first-texture-graph.md)'s server, and can measure what it made.

- More noises: Worley (cells), fBm, and domain warp.
- Patterns: tiles and bricks with gaps and jitter, stripes, and simple shapes.
- Filters: blur, transform and tile, gradient map, and distance from an edge.
- A `check` tool, as Carve's lists mesh faults: an output that does not tile, base colour outside a plausible sRGB range, metallic far from 0 or 1, normals not of unit length, and a mask of one value.
- Exposed parameters, so one graph gives variants, and a graph used as a node in another.

Content: four or five sample materials, each a committed graph with its swatch renders, built on the new nodes.

## Estimated size

About **7 tasks**:

1. Noises: Worley, fBm, domain warp.
2. Patterns: tiles, bricks, stripes, shapes.
3. Filters: blur, transform and tile, gradient map, distance.
4. The `check` tool.
5. Exposed parameters and graphs as nodes.
6. The sample materials and their scenario.
7. Acceptance: fresh agents make a named material and fix what `check` reports.

## Why

[D-7](../design/D-7-texture-graphs-and-layered-surfaces.md): M-27 is a thin slice with only enough nodes for one material. Value checks are D-7's answer to an agent judging textures mostly through renders.

## Open at planning

- Whether the new nodes outgrow one task each and should be split.
- How a nested graph's parameters are shown to the graph that uses it.

## Required tasks

Not planned yet.

## Retrospective
