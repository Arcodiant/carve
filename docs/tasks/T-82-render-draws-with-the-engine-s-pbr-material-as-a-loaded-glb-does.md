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

### As built

Four agents, all on Opus 5.5: the first built the PBR path and the unit and regression tests, the second the margins under the person's first and third rulings, the third the last cube point, the derivations, the shown-failing table, the full run and the review, the fourth the two `server-regression` tests the scenarios do not cover (the last two rows of the table) and the finish.

**What changed.** `src/Carve.Server/SurfaceMaterial.cs`: `SurfaceMaterial.Of(colour)` is `Material.Pbr(base colour, GlbExport.Metallic, GlbExport.Roughness, doubleSided: false)`, the base colour the exported one; `RenderSystem` uses it for the shaded fill, each colour's submesh and a glb with no material. Wireframe, normals and weights keep `Material.SolidColor`; lines, bones and labels are untouched; a highlighted face in the shaded look is a vertex tint through PBR. D-1, D-6 and `CLAUDE.md` record the ruling.

**Rulings (2026-11-13), word for word.** 1. "Keep the scene's light as it is. MinimumStep goes from 10 to about 6, and the soft facet and crease thresholds are re-derived for PBR. Loosens checks, by your ruling." 2. On the highlight, "Yes, PBR-shaded tint". 3. "Re-pick points, else lower": "Where a check samples points, move them to facets or spans far enough apart in n·l that the existing margin holds (cylinder ends, cylinder grade, torso run). Where it can't (the cap's off-by-one, the label backing), lower it, worked from PBR: the cap allows 1, and the backing margin is worked from the face's shade. Each still shown failing on its break."

**The light model** the thresholds are worked from: the scene light turned with the camera (`ViewFraming.LightOrientation`, light along its -Z); a face's linear light is albedo x (ambient 0.25 + 0.9 n.l / pi), sRGB-encoded. Checked against the frames, not fitted to them: the cylinder cap (n.l 0.937) 164, facets 3 and 14 131 and 127, the cube front 158 / 147 / 139 (measured 157 / 145 / 137), the torso row within 1 luma.

| Changed check | How it was derived | Passes on (measured) | Its break, and the result there |
|---|---|---|---|
| `MinimumStep` 10 to 6 (cube and crate face order) | cube front n.l 0.783 / 0.513 / 0.351, predicted 158 / 147 / 139; the closest pair steps by 8 | cube-front 157 / 145 / 137 | mirrored camera (frame mirrored): +Z 137 against +X 145, fails; flipped: 145 / 145, fails |
| Torso facet step: flat 9 or more, soft 5 or less | ruling 1 (scaled 0.43, a step from the light under PBR against LitMesh); the model over the samples' geometry gives flat 13 and 14, soft 5 and 5 | flat 13, 13; soft 4, 5 | each frame against the other's rule (smooth drawn flat, flat drawn smooth): the flat frame's 13 fails "5 or less", the soft frame's 4 and 5 fail "9 or more" |
| Torso ramp span, -0.16..-0.08 to -0.195..-0.115 (runs: flat 10 or more, smooth 5 or fewer, unchanged) | a ray cast per pixel through `samples/gardener.carve` and `gardener-soft.carve` with the model: flat crosses the facet edge onto one shade for 16 pixels; smooth ramps 89 to 101, longest run 5 (at the bound, no headroom). Spans further out leave the soft torso (background past about -0.21) or cut the flat plateau to 10 | flat 16, smooth 4 | the flat frame's 16 fails "5 or fewer"; the smooth frame's 4 fails "10 or more" |
| Brim rim: stage 10 to 4, break 20 to 9 | ruling 1, scaled 0.43 as above; not re-worked over the hat's geometry | creased and uncreased frames pass, each with its own rule | the scenario applies the uncreased rule (no step of 9) to the uncreased frame and the creased rule (a step of 9 or more) to the creased one, so each frame fails the other's |
| Cylinder ends (`MinimumStep`), facet 14 / 3 to 13 / 3 | n.l 0.03 (119) against 0.23 (131), 12 apart | flat 10, smooth 11, subdivided 10 | the side drawn one shade (no directional light): both 118, 0, fails. A mirrored camera ((1,2,2), worked in the model) still differs by 7 flat and 11 smooth, so this check alone does not catch a mirror; the normals-look channel checks on the same cylinder do |
| Soft-rims frame's far end (third agent): facet 13 at mid height to the cage's vertex 14 at y -0.3, at 0.9 of the radius | with no hard rim the subdivided cylinder rounds in and facet 13's point is background; lower and inside, the normal faces away from the light (n.l at or below 0), ambient alone: 0.72 x 0.25 = 0.18, sRGB 118, against facet 3's 131 | 118 against 130, 12 | one shade, as above, fails |
| Cylinder grade: facet 2 to facet 14, `least: 3` | facet 14's n.l runs 0.09 to 0.21 across it, about 5 at t 0.2 and 0.8 | smooth 125 / 129 | smooth drawn flat (the flat frame at the same points): 127 / 127, fails |
| Cap one shade, within 1 (was exact) | ruling 3 | 164 / 164 on the three hard-rimmed frames (blue 161 / 162) | the rim not hard (the soft-rims frame): 165 against 149, 16, fails |
| Label backing: least darkening `shade - Backed(shade) - 10`, was 60 | the 0.6 black backing leaves 0.4 of the face's linear light (`Backed`), less the 10 the backed value may be off; a top face at the model's 158 goes to 103, least 45 | lid labels pass | a label with no backing darkens by 0 (the plain frame), fails |
| `RenderTests.MinimumStep` 10 to 6 (fourth agent; the cube in `server-regression`, ruling 1) | the same cube and views as the scenario's: front +Z n.l 0.513 (147) over +X 0.351 (139), 8 apart; back 159 / 148 / 118 measured, 11 and 30; the darkest face 118 or more stays past `53 + 6` | front 157 / 145 / 137 (the failing run under 10) | the front frame mirrored: +Z 137 against +X 145, fails; flipped: 145 / 145, fails |
| `NamedMeshTests.IsOrange`: red 150 to 120, red over green 40 to 20, blue under 90 kept (fourth agent, ruling 2) | the tint (1, 0.55, 0.1) as UNORM vertex colour, which the engine's shader multiplies in after lighting, over the lit base (0.72, 0.72, 0.70): the tall box's front face (n.l 0.513) is (145, 110, 47), at the darkest n.l 0.351 (137, 103, 44); the untinted face is (145, 145, 143). Before, red 145 and red over green 35 failed the LitMesh-era rule | `verify` passes; the pixel is in the assertion's message now | the face not highlighted (the cube's front, or the tall box with no tint): red over green 0 and blue 143, fails both; so the cube's face fails `IsOrange`, which the test's second assertion requires |

The break column was worked on this run's frames (a frame mirrored or flipped, or the scenario's own frame of the other shading or crease) and in the light model, not by breaking the product and rerunning.

**Measured, render against render_glb** (D-6's rule, 8/255 and 0.5 %): rest front 39,409 px (15.03 %) under LitMesh, 0 px under PBR; Walk 0.5 s side 31,784 px (12.12 %), 0 px. `RenderGlbTests.Render_and_render_glb_of_the_gardener_agree_in_shading` keeps it; `FaceColourTests.Render_draws_each_colour_with_the_PBR_material_the_loader_makes_from_the_export` holds the material to the parsed export.

**Full local run** (every scenario and viewer-cube, all passing) hashed against the milestone head eded55f's (`artifacts/review/T-82/hashes-before.txt`, `hashes-final.txt`): 157 files, 35 byte-identical (all 12 `.glb` and `.carve`, the viewer's two frames of the exported crate, and every normals, wireframe and weights frame), 122 frames changed, each one that draws the model's colours. In the bones, labels and highlight frames the changed pixels are the surface: no white text or orange line pixel changed but a few edge-blended ones.

**Review:** the person approved both sets (round 1, `C:\Users\andre\artifacts\carve\M-19\T-82\round-1`), with a note on the first: "These all look good - do we get a sky texture to show reflections?" Not done here: the scene has no environment map, and at metallic 0, roughness 1 the material reflects little; left to the person as a question.

**Not checked:** the brim thresholds over the hat's own geometry; the break column by product breaks.
