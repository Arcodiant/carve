---
description: >-
  render draws with the engine's PBR material, as a loaded .glb does
---
# T-82 — render draws with the engine's PBR material, as a loaded .glb does

**Status:** In Progress

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-47, T-84

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Carve's preview looks as the game will. T-47 found `render_glb` frames uniformly darker than `render`'s, about 0.73 of the brightness, with about 12 % of pixels past 8/255: the exported file's material loads as the engine's PBR material (metallic 0, roughness 1), while `render` draws with `Material.LitMesh`. The person ruled (2026-11-12) that `render` goes PBR: the preview draws with the same PBR material the game gets.

## Scope

- **`render`** (every look that draws the model's surfaces in colour) draws with the same PBR material, and the same parameters, that the engine's loader makes from Carve's exported `.glb`, so a `render` and a `render_glb` of the same model at the same pose agree in shading. Pose, framing and the CPU skinning stay as they are (D-6, *Two samplers*).
- **Measure** the agreement: a `render` and a `render_glb` of the gardener at rest and at one clip time, with the table of differing pixels under D-6's rule (8/255, 0.5 %). T-48 owns the full comparison; this shows the shading gap is gone.
- **Existing frames:** the byte-identical guard is lifted once, for this task only. Every scenario's frames will change; re-measure each pixel check that reads a colour from the model (worked from the geometry and the new material, not copied from the new frames), keep every check's intent, and keep each frame's positive and mirrored/flipped checks.
- **Review:** put the new baselines to the person, before and after, through `mcp__waystone__review`.
- **D-6 and `CLAUDE.md`:** record the ruling where they say how renders and the preview draw.

## Notes

- The person's ruling, 2026-11-12, on T-47's finding: "render goes PBR".
- Looks that are not the model's own colour (wireframe, normals, UVs, highlight, bones, weights, labels) keep their own materials unless the person rules otherwise; ask if one cannot.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### Hand-off

First agent, on Opus 5.5. Stopped on a question (below); nothing about the frames is decided.

**Done (committed):**
- `src/Carve.Server/SurfaceMaterial.cs`: `SurfaceMaterial.Of(colour)` is `Material.Pbr(base colour, GlbExport.Metallic, GlbExport.Roughness, doubleSided: false)`, the base colour the exported one (grey, or the linear form of the hex). `RenderSystem` uses it for the shaded fill, each colour's submesh, and a glb with no material. Wireframe, normals and weights keep `Material.SolidColor`; the orange lines, bones and labels are not touched. A highlighted face in the shaded look is still a vertex tint on the fill, so it is now shaded through PBR. I read that as the model's own surface, not as the highlight's own material.
- Unit test `FaceColourTests.Render_draws_each_colour_with_the_PBR_material_the_loader_makes_from_the_export`: each of render's submesh colours, in render's order, against the `ParsedMaterial` the engine's glTF parser reads from the exported file (Pbr set, base colour, metallic, roughness, emissive, occlusion, double-sided, no maps). Shown failing with `LitMesh` ("grey: SolidColor, Lit, LitWithVertexNormals is not lit PBR") and with roughness 0.5 ("Expected 1, Actual 0.5").
- Regression test `RenderGlbTests.Render_and_render_glb_of_the_gardener_agree_in_shading` (D-6's rule: past 8/255, at most 0.5 %). The figures come from a local run:

| Frame | LitMesh (before) | PBR (now) |
|---|---|---|
| rest, front (+z) | 39,409 px (15.03 %), brightness 0.755 of render's | 0 px (0.00 %), 1.000 |
| Walk 0.5 s, side (+x) | 31,784 px (12.12 %), 0.755 | 0 px (0.00 %), 1.000 |

**Baselines:** from a local run of the milestone head eded55f, in a detached worktree at `artifacts/base-src`, run by `artifacts/run-frames.sh`. The hashes are in `artifacts/review/T-82/hashes-before.txt` and `hashes-after.txt`. On the T-82 commit, character, rig and shed ran to the end. Every `.glb` and `.carve` is byte-identical, and so are the shed's normals and wireframe frames and rig's seven weights frames. Every shaded frame changed: character 21, rig 2, shed 8.

**The question: the PBR path compresses the light, and four checks' fixed margins no longer hold.** Under PBR the directional light's share of a lit face is much smaller (roughly divided by pi, as Cook-Torrance's diffuse term is), while a face lit by ambient alone is unchanged. The cube's face pixels (G):

| Frame | top / +Z / +X (or -Z / -X / underside) before | after |
|---|---|---|
| cube-front | 216 / 190 / 171 | 157 / 145 / 137 |
| cube-back | 219 / 196 / 118 | 159 / 148 / 118 |

The face order still holds, but `CubeScenario.CheckOrder`'s step of 10 does not: "cube-front: +Z (145) should be at least 10 brighter than +X (137)". cube, crate and export stop on it. soft stops on "soft-flat-rest-front: across the torso's facet edge at (224, 242) the shade steps by 13; a flat mesh steps by 20 or more". Each scenario stops at its first failure, so the checks after these are not yet known (cube, crate, export, soft).

Two ways out. Neither is settled by the spec or D-6:
1. **Keep the scene's light and lower the margins**: `MinimumStep` 10 down to about 6, and the soft facet steps (flat 20 or more, smooth 12 or less, the 10 at the brim crease) re-derived for PBR. Each of those loosens a check, so it needs the person's ruling.
2. **Raise the scene's directional light** (about pi times), so that PBR's lit faces come back near their old brightness and contrast. `render_glb` draws in the same scene, so the two still agree, and the margins can stay. This changes how the preview is lit, which is a D-6/D-1 decision.

**Left:** after the ruling, re-measure every colour-reading check (the crate colour check's grey-ratio rule assumes a pure Lambert factor and may not hold with PBR's albedo-independent specular), run `verify`, the review through `mcp__waystone__review`, and the D-6 / `CLAUDE.md` wording, then As built.
