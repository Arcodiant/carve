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

Second agent, on Opus 5.5 (the first was Opus 5.5 too). Stopped twice on questions; the second is open (below).

**The rulings of 2026-11-13** (recorded word for word from the options chosen):
1. "Keep light, lower margins": "Keep the scene's light as it is. MinimumStep goes from 10 to about 6, and the soft facet and crease thresholds are re-derived for PBR. Loosens checks, by your ruling."
2. On the highlight, "Yes, PBR-shaded tint": a highlighted face in the shaded look follows the surface's PBR shading, as the first agent built it.

**Done since (committed):**
- `MinimumStep` 10 to 6 in `CubeScenario.cs` and `CrateBuild.cs`, worked from the faces' n.l. The PBR model (grey 0.72, ambient 0.25 unchanged, directional 0.9 reaching a face as albedo / pi, sRGB-encoded readback) predicts the cube-front faces at 158 / 147 / 139 (measured 157 / 145 / 137; under LitMesh 216 / 190 / 171). The closest pair, +Z over +X, steps by 8, so 6 keeps 2 of headroom.
- The soft thresholds scaled by the same factor (a step from the light is about 0.43 of LitMesh's): torso facet step flat 20 to 9 or more, smooth 12 to 5 or less; brim rim stage 10 to 4, break 20 to 9. On a local run these all pass (flat rest, smooth rest, hat creased and uncreased).
- D-1 (*C# on Empyrean*), D-6 (*Two samplers, on purpose*) and `CLAUDE.md` (*Renders are Empyrean's*) record the ruling: the model's colours are drawn with the PBR material the loader makes from the export, the scene light unchanged, other looks keep their own materials, the highlight is a PBR-shaded tint.

**Checked:** a local run of every scenario on this commit, then cube, crate and soft again with `ScenarioDriver.Fail` made non-fatal for one uncommitted local run (reverted), so every failing check showed, not only the first. shed, rig, export, character and viewer-cube pass. The crate's colour check passes.

**Third ruling, 2026-11-13, on the checks outside the first ruling**, "Re-pick points, else lower", word for word as offered: "Where a check samples points, move them to facets or spans far enough apart in n·l that the existing margin holds (cylinder ends, cylinder grade, torso run). Where it can't (the cap's off-by-one, the label backing), lower it, worked from PBR: the cap allows 1, and the backing margin is worked from the face's shade. Each still shown failing on its break." If a re-picked point cannot hold its existing margin either, stop and ask again rather than lower it.

**Done under it (committed with this hand-off):**
- The light model, checked: the scene light turned with the camera (`ViewFraming.LightOrientation`, light along its -Z), worked in Python, gives the cube front n.l 0.783 / 0.513 / 0.351 and the cylinder (view 2,2,1) cap 0.937, predicted 164 (measured 164); facets 3 and 14 predicted 131 and 127 (measured 131 and 127). Per facet k (centre angle (k + 0.5) x 22.5 degrees), n.l: 0 0.33, 1 0.35, 2 0.31, 3 0.23, 4 0.11, 13 0.03, 14 0.16, 15 0.27; 5 to 12 face away.
- `CubeScenario.cs`: the side's two ends are now facet 13 (n.l 0.03, 119) against facet 3 (131), 12 apart under `MinimumStep` 6; the smooth grade is taken on facet 14 (t 0.2 against 0.8, n.l 0.09 to 0.21, about 5 apart against `least: 3`), with a flat plateau check added on the same facet; the three cap checks go through `CheckCapOneShade`, within 1 per channel.
- `CrateLabels.cs`: the backing's least darkening is worked from the face's shade, `shade - Backed(shade) - 10` (44 and 47 for the lid at 157 and 166), unless a caller sets it (the +Z frame keeps 0).
- `SoftScenario.cs`: the torso's ramp span moves to -0.195 to -0.115 m, by the -X silhouette, where the normal turns fastest per pixel and n.l = 0.35 sin + 0.51 cos changes fastest: the smooth ramp holds a shade 3 pixels at most (5 allowed), flat holds 14 (10 needed).
- Local run of this tree: **crate passes, soft passes; cube fails** on the one point below.

**The new question (cube, `cylinder-subdivided-soft-rims-dir-2-2-1-ortho`):** the twice-subdivided soft cylinder rounds in, so facet 13's point (384, 267) is background there ("the soft cylinder's side, on one side of the image at (384, 267) is the background, expected the mesh"). Along that row the pillow's shade runs 130 at x 376, 126 at 378, 123 at 380, then the edge (92 at 382): no point at the image's right end on that row is 6 or more below the other end (130) with any headroom. The other three cylinder frames hold with facet 13. Ways out: (1) for the soft-rims frame only, sample the mirror point lower on the side, where the pillow is upright, and show it holds 6 (not yet tried, needs a run); (2) compare facet 14 against a facet nearer the image's centre (facet 1, about 137) in that frame, which keeps 6 but not "the two ends"; (3) lower that one check to 3 or 4, which the ruling says to ask about.

**Left:** the answer and that one check; then the shown-failing table for every changed check (`MinimumStep` 6, facet 9 / smooth 5, rim stage 4 / break 9, the cylinder ends and grade, the cap's 1, the backing, the torso span), a full local run with hashes against `artifacts/review/T-82/hashes-before.txt` (every `.glb`, `.carve` and non-colour look byte-identical), `verify`, the waystone review (round-1, before and after), As built, remove `artifacts/base-src`, `finish`. Second agent stopped at the 60-call checkpoint.

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
