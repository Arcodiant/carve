---
description: >-
  An agent cuts into a mesh: plane cuts, loop cuts, knife paths, face merge and delete
---
# M-5 — Cut

**Status:** Planned

**One-liner:** *An agent cuts into a mesh: plane cuts, loop cuts, knife paths, face merge and delete*

## What this milestone delivers

An agent cuts into a mesh: plane cuts, loop cuts and knife paths, with face merge and delete to tidy up, on a model visibly built this way.

Content: a sample model that needs cuts to exist, with a scenario showing each stage.

## Open at planning

- What "cut" means (D-1): which of plane, loop and knife matter, and whether boolean subtraction is wanted. A true boolean would need its own milestone.
- How a knife path is specified by coordinates, with no pointer.
- What happens to selections and element ids across a cut.
- Faces with holes, moved here from M-2 (decided at M-2's planning): a face as an outer loop plus inner loops, its `define_mesh` syntax, triangulation and how the edit tools treat it. M-2's edge table leaves room for it.
- What deleting a face or vertex does to ids, which M-2 promises are never reused.

## Required tasks

## Retrospective
