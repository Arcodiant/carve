---
description: >-
  A Patina node that layers a detail normal map over a base one by reoriented normal mapping, so a baked bevel normal and a noise bump live in one normal texture
---
# T-224 — normal_blend: combine two tangent-space normal maps in a Patina graph

**Status:** In Progress

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** [T-240](T-240-image-read-a-png-into-a-patina-graph.md)

## Goal

One normal texture can carry both a mesh's rounded edges (from `bake`'s `normal` map) and a surface's bumps (from `height_to_normal`). Today a material gets one or the other.

## Scope

- A new Patina node `normal_blend` in `src/Patina.Graphs/Nodes/`, registered in `NodeRegistry` and listed in the server's node catalogue and the node-library README (T-180 keeps that README current).
  - Inputs `base` and `detail`, both colour tangent-space normals in -1..1, the form `height_to_normal` outputs. Either may be a PNG read through an input node, such as a `bake` normal map.
  - Parameter `strength` 0..1 (default 1) scales the detail's tilt (it lerps detail toward (0, 0, 1) and renormalises) before blending.
  - The method is reoriented normal mapping (Barré-Brisson and Hill): the detail is rotated into the base's frame. The output has unit length and passes the existing `NormalNotUnitRule`.
- Tests in `tests/Patina.Graphs.Tests`:
  - A flat base (0, 0, 1) returns the detail unchanged.
  - A flat detail returns the base.
  - Hand-worked texels for a tilted base with a tilted detail.
  - `strength` 0 returns the base.
  - The output is unit length everywhere.
  - A table of broken copies (inputs swapped, renormalisation skipped) is each caught, as T-135 and T-136 did for their nodes.
- A sample graph: a noise `height_to_normal` blended over a baked bevel normal PNG, and its swatch.

## Notes

- Raised on 2026-11-25 in a discussion about rough surfaces, alongside [T-223](T-223-displace-move-vertices-along-their-normals-by-a-graph-output-or-seeded-noise.md) and [T-225](T-225-bake-a-detailed-mesh-s-normals-onto-a-simpler-one.md). The person agreed to reserve it.
- Reoriented normal mapping was chosen over "UDN" (adding the xy components), which flattens strong detail on a tilted base. The bevel normal is exactly the tilted-base case.
- A height map as its own material slot (parallax or displacement) was set aside: glTF has no standard slot for one, so height stays an in-between step.
