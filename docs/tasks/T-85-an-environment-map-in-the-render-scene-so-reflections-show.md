---
description: >-
  An environment map in the render scene, so reflections show
---
# T-85 — An environment map in the render scene, so reflections show

**Status:** Done

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

The person asked, on T-82's review (2026-11-13): "do we get a sky texture to show reflections?" Today the render scene, shared by `render` and `render_glb`, has no environment map, and Carve's exports are non-metallic at roughness 1, so a sky would show little until a model asks for a shinier material.

## Scope

To plan: whether Empyrean's packaged docs give the render scene an environment map or image-based light; whether Carve's materials gain metallic/roughness so a reflection can show; and the cost: every shaded frame changes again, and `render` and `render_glb` must still agree under D-6's frame rule.

## Notes

- **Closed by the person's ruling (2026-11-17, RV-52 tracker finding 3): "Close it".** The premise was overtaken: M-7's fixed sky (T-78, `src/Carve.Server/RenderSky.cs`, the engine's `ProceduralSky.Default`) gives the render scene, shared by `render` and `render_glb`, an environment: its diffuse ambient and its specular probe reach every PBR face, and since T-82 every colour face is a PBR face. The cost the Scope named was paid in M-19's third rebase, when every shaded frame changed and was approved, and `render` and `render_glb` still agree under D-6's frame rule. Nothing was built here. A shinier material than metallic 0, roughness 1, so that a reflection shows more, would be a task of its own if a model asks for one.
