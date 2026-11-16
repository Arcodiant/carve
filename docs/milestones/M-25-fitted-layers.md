---
description: >-
  A shell is grown from chosen faces of a skinned mesh and keeps its weights, UV islands and morph targets, for sleeves, gloves and armour
---
# M-25 — Fitted layers

**Status:** Planned

**One-liner:** *A shell is grown from chosen faces of a skinned mesh and keeps its weights, UV islands and morph targets, for sleeves, gloves and armour*

## What this milestone delivers

An agent selects faces on a skinned mesh and gets a fitted layer over them that moves and reshapes with the body.

- A shell at a thickness from a face selection, with open or closed rims and a cuff.
- Skin weights, UVs and morph targets ([M-21](M-21-morph-targets.md)) carried to the shell.
- Export as its own skinned mesh on the same skeleton.
- A check for the body showing through the shell in a clip's poses.

Content: a sleeve and a glove for the fps-arms rig, worn in an Empyrean game.

## Why

Raised by the fps-arms project (private repo `Arcodiant/fps-arms`, 2026-11-15), which built a rigged, textured pair of first-person arms and four tools with Carve 0.9.0 through script-written `.carve` files.

- fps-arms plans clothing as shells over the arm's rings. In Empyrean, several meshes can share one skinning palette, so a sleeve is a second skinned mesh on the same skeleton.
- A shell made outside Carve needs weights, UVs and every dial's deltas copied by hand, and falls out of step when the arm changes.

## Open at planning

- Thickness that varies along the shell.
- How the body under a layer is hidden: a mask, or removed faces?
- One file per piece, or parts of one model ([M-9](M-9-parts.md))?

## Required tasks

Not planned yet.

## Retrospective
