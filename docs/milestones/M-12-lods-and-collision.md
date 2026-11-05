---
description: >-
  LODs as cuts through the part tree with per-LOD budgets and a silhouette comparison render, a collision mesh, and an LOD chain in the .glb
---
# M-12 — LODs and collision

**Status:** Planned

**One-liner:** *LODs as cuts through the part tree with per-LOD budgets and a silhouette comparison render, a collision mesh, and an LOD chain in the .glb*

## What this milestone delivers

An agent gets its LODs from the part tree it built, without decimating anything. An LOD is a cut through the tree: below a depth, or at named parts, a subtree is replaced by its coarser kept level. LOD0 is the fully refined model. Each LOD has a triangle budget, and a comparison render shows an LOD against LOD0 from the same view with the silhouette difference marked, so "does it still read at distance" is measured. The collision mesh comes from the blockout, or from a level the agent marks. The `.glb` carries the LOD chain and the collision mesh in a form Empyrean loads.

Content: M-9's sample vehicle with three LODs and a collision mesh, their comparison renders, and the exported chain shown through the viewer.

Plans against [D-2](../design/D-2-part-tree-and-hierarchical-modelling.md), its LOD and collision decisions.

**Depends on:** M-3 (export) and M-9 (the part tree and its kept levels). M-3's export is single-LOD; the chain is this milestone's (the person, in discussion, 2026-11-04).

## Open at planning

- How LODs and collision are encoded in the `.glb`: `MSFT_lod`, naming conventions (`_LOD1`, `_col`) or something else. This turns on what Empyrean's `IModelLoader` reads, which has not been checked.
- Whether Empyrean switches LODs and imports collision at all, or this raises engine needs.
- How the silhouette difference is measured and drawn.
- What happens at seams, where a coarser proxy meets refined siblings.

## Required tasks

## Retrospective
