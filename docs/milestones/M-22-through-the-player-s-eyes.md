---
description: >-
  Render from a camera placed at a point with a field of view, in a clip's pose at a time, with textures, so a first-person asset is judged as the game shows it
---
# M-22 — Through the player's eyes

**Status:** Planned

**One-liner:** *Render from a camera placed at a point with a field of view, in a clip's pose at a time, with textures, so a first-person asset is judged as the game shows it*

## What this milestone delivers

An agent renders a model from a camera it places, in the pose a clip gives at a time, and gets the picture the player would see.

- `render` takes an eye point, a target, an up vector, a vertical field of view and an aspect ratio.
- It takes a clip and a time, and several named shots in one call.
- A contact sheet across a clip from one camera.
- With [M-7](M-7-materials-and-textures.md) in place, the shot uses the model's textures.

Content: the five fps-arms poses rendered from the eye by Carve, beside the same poses from the exported `.glb`.

## Why

Raised by the fps-arms project (private repo `Arcodiant/fps-arms`, 2026-11-15), which built a rigged, textured pair of first-person arms and four tools with Carve 0.9.0 through script-written `.carve` files.

- `render` could not place a camera at a point and drew no textures, so every first-person check went through F3D 3.5.0 on the exported `.glb` (camera at the origin looking down +Z, 60 degrees, a clip index and a time).
- For fast pose tuning the project wrote its own z-buffer renderer.
- A wrist bent 119 degrees went unseen through three review rounds because no view showed the forearm and hand from the side.

## Open at planning

- Near plane and clipping for geometry close to the eye.
- Lighting for a judged shot: fixed rig, or the game's?
- Does a comparison against F3D or the game stay part of the check, as in [M-19](M-19-a-clip-library-for-a-game.md)?

## Required tasks

Not planned yet.

## Retrospective
