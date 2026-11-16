---
description: >-
  A mesh carries named shape variants on one topology, keeps them through subdivision, previews them and exports them as glTF morph targets
---
# M-21 — Morph targets

**Status:** Planned

**One-liner:** *A mesh carries named shape variants on one topology, keeps them through subdivision, previews them and exports them as glTF morph targets*

## What this milestone delivers

An agent gives a mesh named targets (a second set of positions on the same vertices), sees the mesh at any weights, and exports them as glTF morph targets a game can drive.

- Define a target from a full or sparse position set, or from a second file with the same topology. A topology mismatch fails and names the first difference.
- Targets survive `subdivide` and the edits that keep vertex identity. An edit that cannot keep them says so before it runs.
- `render` and `check` take weights. `check` reports a target that flips faces or opens the mesh at weight 1.
- Export writes `targets` with position and normal deltas and the target names.

Content: an arm with a weight dial, loaded in an Empyrean game with its slider.

## Why

Raised by the fps-arms project (private repo `Arcodiant/fps-arms`, 2026-11-15), which built a rigged, textured pair of first-person arms and four tools with Carve 0.9.0 through script-written `.carve` files.

- The Carve file (version 5) has no field for a second position set.
- fps-arms wants build, weight, muscle and age dials. Each is its cage script re-run with other ring sizes: 600 vertices in the cage, 2,376 after one `subdivide`.
- Without this, the project must repeat Carve's subdivision in its own script to get matching vertex order, and add the targets to the `.glb` after export.
- Empyrean reads `POSITION` and `NORMAL` deltas and applies morphs before skinning. It drops `TANGENT` deltas.

## Open at planning

- Which edit tools keep targets, and which drop or refuse?
- Dense or sparse storage in the file and in the `.glb`?
- Are normal deltas stored or computed at export?
- Corrective targets (driven by two others) and morphs in clips: in or out?

## Required tasks

Not planned yet.

## Retrospective
