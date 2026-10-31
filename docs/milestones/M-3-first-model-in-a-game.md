---
description: >-
  An agent saves its work to a Carve file and exports a .glb that loads through Empyrean
---
# M-3 — First model in a game

**Status:** Planned

**One-liner:** *An agent saves its work to a Carve file and exports a .glb that loads through Empyrean*

## What this milestone delivers

The end-to-end slice is complete: an agent saves its work to a Carve file, opens it again in a later session, and exports a `.glb` that loads through Empyrean's `IModelLoader` and looks right.

Content: the crate exported and shown through the engine, and a worked example taking a model from an empty file to a game's content folder.

## Open at planning

- The Carve file: text or binary, geometry or a list of operations (D-1).
- How normals are made on export, and what controls hard and smooth edges.
- Scale, axes and naming conventions a game expects, and whether Carve checks them.
- `SharpGLTF` or a writer of our own.
- How a scenario proves the export loads, without Carve taking on a game's job.

## Required tasks

## Retrospective
