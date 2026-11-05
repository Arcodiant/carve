---
description: >-
  A skill walks a game's agent from brief to part tree, blockout, breadth-first refinement, checks and LODs with Carve
---
# M-15 — The modelling skill

**Status:** Planned

**One-liner:** *A skill walks a game's agent from brief to part tree, blockout, breadth-first refinement, checks and LODs with Carve*

## What this milestone delivers

A game's agent makes a model with a skill that walks it through the hierarchical method. It writes a part tree from the brief (parts, envelopes, relationships, which parts move), builds a blockout and has it reviewed, refines breadth-first so each finished level is a complete model, checks every level against its envelopes, relationships and budget, and exports the model with its LODs, collision and sockets. Sibling subtrees can go to subagents.

Content: the skill, its worked examples, and a model a fresh agent made with it from a brief.

Plans against [D-2](../design/D-2-part-tree-and-hierarchical-modelling.md), its decision on the walk.

**Depends on:** M-9, M-11 and M-12, and M-13 for sockets. M-8 (a real prop for a real game) uses this skill, so it comes after this milestone whatever their numbers.

## Open at planning

- Where the skill lives and ships: this repo, a plugin, or with each game.
- Where the person reviews: after the blockout only, or after every level.
- How subagents share one model: a part each, with the server holding the tree.
- Whether a style sheet per game (bevel width, density, palette) and a library of reusable parts belong here or later.

## Required tasks

## Retrospective
