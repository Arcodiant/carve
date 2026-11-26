---
description: >-
  Point and spot lights and an environment in render, lights and alpha blend and mask in the exported .glb
---
# M-38 — Light and glass

**Status:** Planned

**Depends on:** M-22

**One-liner:** *Point and spot lights and an environment in render, lights and alpha blend and mask in the exported .glb*

## What this milestone delivers

An agent lights a scene and gives it glass, and sees both in Carve's own render.

- Point and spot lights in the model: placed, named, saved in the Carve file, drawn by `render`, and exported as glTF `KHR_lights_punctual`.
- An environment for `render` that a polished or metal surface can reflect.
- Alpha blend and alpha mask on a material, drawn by `render` and exported as glTF `alphaMode` and `alphaCutoff`.

Content: the stone room's window with real glass and a light inside, its polished floor reading as polished; a cut-out material (a grille or leaves) for the mask.

## Estimated size

About **6 tasks**:

1. Lights in the Core and the Carve file (a new file version), with their tools.
2. Lights in `render`.
3. The environment.
4. Alpha on a material, in `render`.
5. Lights and alpha in export, read back.
6. The lit room's scenario, and acceptance.

## Why

The stone-room project's feature request (2026-11-22, items 5 and 6). Its window glass had to be an opaque, faintly glowing pane, and its polished floor barely read as polished with nothing to reflect.

## Open at planning

- **Alpha the engine does not read.** M-7 left materials opaque because Empyrean's loader reads no `alphaMode` (D-1, engine need 7, Low). Does Carve write alpha for other consumers, raise the engine need, or both? The person's call.
- Does the engine's loader read `KHR_lights_punctual`? If not, the same question.
- Renders are Empyrean's with a fixed sky (D-1, D-6): an environment an agent can change is a change to that rule. Is it per render call, or part of the model?
- `render_glb` must agree with `render` under D-6's frame rule with lights and alpha in the file.
- Draw order for blended faces.
- This milestone adds a Carve file version. So do M-29, M-35 and M-37: none of them runs beside another.

## Required tasks

Not planned yet.

## Retrospective
