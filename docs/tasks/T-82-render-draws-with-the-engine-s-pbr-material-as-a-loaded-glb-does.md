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

Second agent, on Opus 5.5 (the first was Opus 5.5 too). Stopped on a question: under the person's ruling of 2026-11-13 the margins it names now hold, but seven other checks fail, which the ruling says to bring back rather than loosen.

**The rulings of 2026-11-13** (recorded word for word from the options chosen):
1. "Keep light, lower margins": "Keep the scene's light as it is. MinimumStep goes from 10 to about 6, and the soft facet and crease thresholds are re-derived for PBR. Loosens checks, by your ruling."
2. On the highlight, "Yes, PBR-shaded tint": a highlighted face in the shaded look follows the surface's PBR shading, as the first agent built it.

**Done since (committed):**
- `MinimumStep` 10 to 6 in `CubeScenario.cs` and `CrateBuild.cs`, worked from the faces' n.l. The PBR model (grey 0.72, ambient 0.25 unchanged, directional 0.9 reaching a face as albedo / pi, sRGB-encoded readback) predicts the cube-front faces at 158 / 147 / 139 (measured 157 / 145 / 137; under LitMesh 216 / 190 / 171). The closest pair, +Z over +X, steps by 8, so 6 keeps 2 of headroom.
- The soft thresholds scaled by the same factor (a step from the light is about 0.43 of LitMesh's): torso facet step flat 20 to 9 or more, smooth 12 to 5 or less; brim rim stage 10 to 4, break 20 to 9. On a local run these all pass (flat rest, smooth rest, hat creased and uncreased).
- D-1 (*C# on Empyrean*), D-6 (*Two samplers, on purpose*) and `CLAUDE.md` (*Renders are Empyrean's*) record the ruling: the model's colours are drawn with the PBR material the loader makes from the export, the scene light unchanged, other looks keep their own materials, the highlight is a PBR-shaded tint.

**Checked:** a local run of every scenario on this commit, then cube, crate and soft again with `ScenarioDriver.Fail` made non-fatal for one uncommitted local run (reverted), so every failing check showed, not only the first. shed, rig, export, character and viewer-cube pass. The crate's colour check passes.

**The question: seven checks outside the ruling fail.** Values after (PBR):

| Scenario | Frame | Check | Seen | Needs |
|---|---|---|---|---|
| cube | cylinder-flat, cylinder-smooth, cylinder-subdivided-smooth (ortho, dir 2,2,1) | the cap is one shade (exact pixel equality) | (164,164,161) vs (164,164,162): blue differs by 1 | equal |
| cube | cylinder-flat, -smooth, -subdivided-smooth, -subdivided-soft-rims | the side at its two ends across the image (`MinimumStep`, the mirror guard) | 127 vs 131 (126 vs 130 soft rims): 4 | 6 |
| cube | cylinder-smooth | the smooth side grades across one facet (`least: 3`) | 136 vs 134: 2 | 3 |
| crate | crate-labels-both-front-1024, crate-labels-faces-dir-0.4-2-0.8 | the label backing darkens the lid by 60 or more | 157 to 103 (54), 166 to 109 (57); the backing's own rule, 0.4 of the linear value, gives exactly 103 and 109 | 60 |
| soft | soft-smooth-rest-front | the smooth torso is a ramp: a run of equal shades of 5 or fewer | 7 (the ramp is shallower under PBR, so it holds a shade longer) | 5 or fewer; flat 10 or more |

None is a break: each frame looks as PBR predicts, and the order of shades holds in every one. Ways out, for the person to choose per check:
1. **Lower these too**, worked from PBR as the ones above were: the cylinder's end-to-end step to about 3 (or `MinimumStep` to 3 there only), the facet grade to 1 or 2, the cap's one-shade check to compare green only or allow 1, the backing to "darker by 45 or more" or a margin worked from the face's own shade (the 0.4-of-linear rule already holds exactly), the smooth torso's run to 7 or fewer (against flat's 10). The cylinder's 4 and the grade's 2 are well below the ruling's "about 6", so this is beyond it.
2. **Move the cylinder's sampled points** to facets farther apart in n.l, so the end-to-end step comes back above 6 with the check's intent (a mirrored camera draws other facets) kept; and measure the torso's ramp over a span where the PBR gradient still steps every few pixels. Keeps the margins, changes what is sampled.
3. **Raise the scene's directional light** (about pi times), which the ruling of 2026-11-13 declined; it would bring every margin back.

**Left:** the person's answer; then the remaining checks, each changed check shown failing on its break (a table: `MinimumStep` 6 fails two faces drawn in one shade; the facet 9 fails a smooth torso, the smooth 5 a flat one; the rim break 9 fails an uncreased rim), `verify`, the waystone review of the new baselines (round-1, before and after), As built (the render vs render_glb table is in the first agent's notes below), remove `artifacts/base-src`, `finish`.

**First agent's hand-off, kept for its figures:**

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
