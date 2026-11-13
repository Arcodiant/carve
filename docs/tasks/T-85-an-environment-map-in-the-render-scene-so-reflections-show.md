---
description: >-
  An environment map in the render scene, so reflections show
---
# T-85 — An environment map in the render scene, so reflections show

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

The person asked, on T-82's review (2026-11-13): "do we get a sky texture to show reflections?" Today the render scene, shared by `render` and `render_glb`, has no environment map, and Carve's exports are non-metallic at roughness 1, so a sky would show little until a model asks for a shinier material.

## Scope

To plan: whether Empyrean's packaged docs give the render scene an environment map or image-based light; whether Carve's materials gain metallic/roughness so a reflection can show; and the cost: every shaded frame changes again, and `render` and `render_glb` must still agree under D-6's frame rule.

## Notes
