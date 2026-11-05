---
description: >-
  A model is a named tree of parts with their own transforms, pivots and envelopes, instances, and the levels each was refined through
---
# M-9 — Parts

**Status:** Planned

**One-liner:** *A model is a named tree of parts with their own transforms, pivots and envelopes, instances, and the levels each was refined through*

## What this milestone delivers

An agent builds a model as a named tree of parts rather than one mesh: a tank is a hull, a turret and two tracks, and a track is a loop and its road wheels. Each part has its own transform, a pivot that is its origin, and an envelope (its intended bounding box in metres). A repeated part is an instance: six wheels are one part. The edit tools act on a named part. Each part keeps every level it was refined through (blockout first), so that M-12 can build LODs from the tree. The Carve file holds the tree, and the `.glb` export writes it as glTF nodes, merging static subtrees.

Content: a sample model built as a part tree (a small vehicle), with its blockout and refined levels, and the exported `.glb` shown through M-3's viewer.

Plans against [D-2](../design/D-2-part-tree-and-hierarchical-modelling.md).

**Depends on:** M-3 (the Carve file and the export, which this extends). Plan it before M-10's primitives become parts, and before M-11, M-12, M-13 and M-15, which assume parts.

## Open at planning

- How the part tree extends M-3's version 1 Carve file (`{"carve": 1, "vertices", "faces"}`): a version 2 with a `parts` key, or a file per part (D-2).
- Whether every mesh lives in a part, or a model may still be a bare mesh.
- How a tool names its target part, and what happens to ids within a part when the tree changes.
- What a part's kept level is stored as: a full mesh per level, or a mesh plus the step it came from.
- Which subtrees the export merges: by a `rigid` flag on the part, by the absence of a pivot or socket, or both.
- How `render` and `get_mesh` show a tree: one part, a subtree, or the whole model.

## Required tasks

## Retrospective
