---
description: >-
  How a model is held as a tree of parts, how it maps to glTF, how LODs and collision come from it, and the walk that builds it
---
# D-2 — Part tree and hierarchical modelling

**Status:** Reserved

**Raised by:** discussion of Carve against Blender MCP and of a hierarchical modelling skill, 2026-11-04. It takes up the parts, hierarchy, LOD and collider items in D-1's *Scope of a model*.

**Default firmness:** Leaning · Rule

## Intent

An agent models best the way hard-surface modellers already work: lay out the main blocks, then add detail under each. A tank is a hull, a turret and two tracks. A track is a loop and its road wheels, and so on down. Carve should hold a model in that shape, so a skill can walk an agent through it one small, checkable level at a time, and so the levels it passes through are kept and become the model's LODs and collision mesh for free.

The tree is for authoring. The `.glb` is the game's view of it: one source, two shapes.

Milestones planned against this doc: [M-9](../milestones/M-9-parts.md) (parts), [M-12](../milestones/M-12-lods-and-collision.md) (LODs and collision), and [M-15](../milestones/M-15-the-modelling-skill.md) (the skill).

## Decision

### A model is a named tree of parts

**Certainty:** Leaning · **Strictness:** Rule

A model is a tree of named parts. Each part has its own transform relative to its parent, its own geometry, and an envelope: its intended bounding box, in metres (D-1 and M-3: 1 unit = 1 metre). A repeated part is an instance: six road wheels are one part placed six times.

**Alternatives:** one mesh per file, as M-3 ships; a flat list of parts with no hierarchy.
**Would change if:** agents building with the skill rarely go deeper than one level, or holding the tree costs more than it gives.

### A part maps to a glTF node

**Certainty:** Leaning · **Strictness:** Rule

A part exports as a glTF node with its name and transform. Its geometry is the node's mesh, and instances are nodes that share one mesh. glTF's scene is already a named node tree, Y up with counter-clockwise front faces, the same as Carve, so this needs no conversion beyond triangulating.

**Alternatives:** flatten the whole model into one mesh on export.
**Would change if:** Empyrean's `IModelLoader` ignores or flattens the node hierarchy. Checked at M-9's planning (2026-11-17): Empyrean 0.119.0's `SplitParts` load keeps every mesh node, every animated node and their ancestors, and shares one `Mesh` between nodes on one glTF mesh. It refuses a skinned file, morph targets and a mirrored node.

### A part's origin is its pivot

**Certainty:** Leaning · **Strictness:** Rule

glTF has no separate pivot: a node turns about its own origin. A part's pivot is therefore its origin, and its geometry is stored relative to it, so the export writes nothing extra.

**Alternatives:** a pivot field apart from the origin, with the export inserting an extra node to reconcile them.
**Would change if:** agents often need a pivot away from where a part's geometry is natural to author.

### The export merges static subtrees

**Certainty:** Leaning · **Strictness:** Guideline

The Carve file keeps the full tree. The `.glb` keeps a node only where something moves, has a socket, or is an instance, and merges static subtrees into their parent's mesh, so bolts and track links do not each cost a draw call and a transform.

At M-9's planning (2026-11-17) the person ruled the details: a part keeps its node when it is marked `moves` (M-13 adds sockets); one root node named after the file holds every static part; and static instances are merged too, so only moving instances are nodes sharing one mesh. A model with nothing marked `moves` exports as before.

**Alternatives:** mirror the tree one to one.
**Would change if:** draw calls do not matter for Empyrean games at prop scale, or a merge loses something a game needs.

### Each part keeps its levels, and LODs are cuts through the tree

**Certainty:** Leaning · **Strictness:** Rule

A part keeps every level it was refined through, from its blockout up. An LOD is a cut through the tree: below a depth, or at named parts, a subtree is replaced by its coarser kept level. LODs are authored, never decimated, so they keep their silhouette and clean topology.

**Alternatives:** decimating LOD0, as Blender's Decimate modifier does.
**Would change if:** kept levels cost agents too much to maintain, or seams where coarse proxies meet refined siblings make the LODs unusable.

### M-3 exports one LOD

**Certainty:** Settled · **Strictness:** Guideline

M-3's `.glb` export is single-LOD. The LOD chain is M-12's (the person, 2026-11-04).

### Collision comes from the tree

**Certainty:** Leaning · **Strictness:** Guideline

The collision mesh is the blockout level, or a level the agent marks.

**Alternatives:** a convex hull or a hand-built mesh.
**Would change if:** blockouts turn out too coarse, or too fine, for a game's physics.

### The walk is breadth-first

**Certainty:** Leaning · **Strictness:** Guideline

The modelling skill goes: brief, then a part tree (parts, envelopes, relationships, which parts move), then a blockout and a review, then refinement one whole level at a time, with every level checked against envelopes, relationships and budget. Breadth-first means every finished level is a complete model, and therefore an LOD. Sibling subtrees are independent, so they can go to subagents.

**Alternatives:** depth-first, finishing one part completely before starting the next.
**Would change if:** agents lose the whole model's proportions working level by level, or breadth-first costs noticeably more calls.

## Open questions

Settled at M-9's planning (2026-11-17), by the person; the full list is in [M-9](../milestones/M-9-parts.md)'s *Decided at planning*:

- **The file.** Version 7 adds `parts`, plus `levels` on a mesh. It is written only when the model needs it, and every mesh is a part.
- **Ids.** Part names are unique in one flat namespace. Edit tools keep `mesh`, which names a part with geometry. Instances are `instance_of` a part that owns geometry. Ids stay in each part's own mesh, in part-local coordinates, so changes to the tree never change them.
- **Kept levels.** A level is a full mesh snapshot with an explicit number. "Level N" means each part's newest kept level ≤ N.
- **Rigid or moving.** A `moves` flag, with the merge rules above.
- **Envelopes.** An envelope is in the part's own local space and covers its subtree. Leaving one is a warning in `check` and `get_parts`, never a refusal.

Still open:

- **LOD and collision encoding.** `MSFT_lod`, naming conventions (`_LOD1`, `_col`) or something else. This turns on what Empyrean loads: 0.119.0 has a `LodGroups` load of sibling `<stem>_LODn` nodes. Decided at M-12's planning.
