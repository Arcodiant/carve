---
description: >-
  Masks drawn as falloff volumes, strokes along the surface and projected decals, so an agent places wear and marks where it chooses
---
# M-31 — Strokes, volumes and decals

**Status:** Planned

**Depends on:** M-30

**One-liner:** *Masks drawn as falloff volumes, strokes along the surface and projected decals, so an agent places wear and marks where it chooses*

## What this milestone delivers

[M-30](M-30-layered-materials.md)'s masks follow the mesh. These follow the agent's choice of place, each still a call with exact arguments ([D-7](../design/D-7-texture-graphs-and-layered-surfaces.md), *Masks are calls, not brushes*).

- Volumes: a sphere, box or plane in object space with a falloff, for a scorch, a wet hem or a moss line.
- Strokes: a path of points on the surface with a width and falloff, as `knife` takes points, refused with the point and its distance when one is off the surface.
- Decals: a graph output or PNG projected onto the surface from a point and direction, for labels, scratches and blood.
- `render` with `highlight` marks a stroke's path and a decal's frame, as it marks named elements.

Content: the layered prop from M-30 given a scorch, a painted stripe and a stencilled label.

## Estimated size

About **5 tasks**:

1. Volume masks.
2. Surface strokes.
3. Projected decals.
4. Highlighting strokes and decals, and the prop's content and scenario.
5. Acceptance: fresh agents place marks where a prompt describes them.

## Why

D-7: some wear and marks follow no property of the mesh, only where the agent wants them. Without these, the only way to put them there is a hand-made mask PNG.

## Open at planning

- Whether a stroke crosses UV seams and hard edges, and how it wraps around a corner.
- Whether decals are layers of their own or masks on a layer.

## Required tasks

Not planned yet.

## Retrospective
