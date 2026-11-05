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
**Would change if:** Empyrean's `IModelLoader` ignores or flattens the node hierarchy. Not checked yet.

### A part's origin is its pivot

**Certainty:** Leaning · **Strictness:** Rule

glTF has no separate pivot: a node turns about its own origin. A part's pivot is therefore its origin, and its geometry is stored relative to it, so the export writes nothing extra.

**Alternatives:** a pivot field apart from the origin, with the export inserting an extra node to reconcile them.
**Would change if:** agents often need a pivot away from where a part's geometry is natural to author.

### The export merges static subtrees

**Certainty:** Leaning · **Strictness:** Guideline

The Carve file keeps the full tree. The `.glb` keeps a node only where something moves, has a socket, or is an instance, and merges static subtrees into their parent's mesh, so bolts and track links do not each cost a draw call and a transform.

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

- **The file.** How the tree extends M-3's version 1 Carve file (`{"carve": 1, "vertices", "faces"}`): a version 2 with a `parts` key, or a file per part. Decided at M-9's planning.
- **Ids.** How tools name a part, and what happens to vertex and face ids within a part when the tree changes.
- **Kept levels.** Stored as a full mesh per level, or as a mesh plus the step it came from.
- **Rigid or moving.** Whether merging uses a `rigid` flag on the part, the absence of a pivot or socket, or both.
- **LOD and collision encoding.** `MSFT_lod`, naming conventions (`_LOD1`, `_col`) or other. This turns on what Empyrean loads. Decided at M-12's planning.
- **Envelopes.** Whether a child outside its parent's envelope is a warning, an error, or only reported.
