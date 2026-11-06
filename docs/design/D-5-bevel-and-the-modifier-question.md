---
description: >-
  Whether bevel, subdivide and similar finishing edits are destructive edits or a modifier stack, and which come first
---
# D-5 — Bevel and the modifier question

**Status:** Reserved

**Raised by:** discussion of Carve against Blender MCP, 2026-11-04. Blender does bevel, subdivide, solidify and array as modifiers, and no Carve milestone covered them.

**Default firmness:** Leaning · Guideline

## Intent

Bevel is what stops a game prop looking like a grey box, and Carve has no plan for it or for the other finishing edits. This doc decides how they work: as destructive edits on the mesh like `extrude`, or as a modifier stack kept in the file and applied on render and export. It also decides which come first.

Milestone planned against this doc: [M-14](../milestones/M-14-bevel-and-finish.md).

## Decision

### Bevel comes first

**Certainty:** Leaning · **Strictness:** Guideline

Of the finishing edits, bevel is first, with control over hard and smooth edges alongside it.

**Alternatives:** subdivision first, which suits softer shapes Carve does not target.
**Would change if:** the props agents make for real games need something else first (M-8).

## Open questions

- **Destructive or modifier.** A destructive edit keeps D-1's one-representation kernel and stable ids simple. A modifier stays editable and keeps the base mesh low-poly for LODs ([D-2](D-2-part-tree-and-hierarchical-modelling.md)). Decided at M-14's planning.
- Which of subdivide, solidify and array follow, and when.
- Smoothing control: *moved to [D-6](D-6-characters-rigging-animation-and-character-export.md).* Edges are marked with a tool (M-18); by angle may follow.
- Subdivide: *D-6 makes it a destructive edit in M-18,* because Orcward's soft characters met this doc's *Would change if*. Bevel still comes first among the hard-surface edits.
