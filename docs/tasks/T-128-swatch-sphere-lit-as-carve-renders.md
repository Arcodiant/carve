---
description: >-
  patina's sphere swatch lit by carve's ambient and directional lights as well as the fixed sky, so a swatch shows a material as carve's render does
---
# T-128 — Swatch sphere lit as carve renders

**Status:** Done

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** T-114

**Verify:** patina-unit, patina-regression, scenario-patina, server-regression, scenario-cube, scenario-crate, scenario-sky, scenario-materials, scenario-planks

## Goal

patina's `swatch` sphere is lit the way carve's `render` is: the fixed sky plus carve's ambient light and its
directional light. Today the sphere has the sky alone, which has no sun disc in its probe, so the sphere shows no
highlight and roughness can only blur a gradient (the person's note on T-114's frames, and T-117's two rejected
rounds).

## Scope

- **Share carve's lights.** Carve's render adds an ambient light (0.25) and a directional light (0.9) that turns
  with the camera (`src/Carve.Server/RenderLights.cs`). Move what a second server needs of it into
  `src/Carve.Hosting` beside `RenderSky`, public, and have both carve and patina use it. Carve's behaviour must not
  change: **every carve frame stays byte-identical** by hash (every scenario suite carve draws, and
  `server-regression`'s frames), as T-111's frame guard did; take the before table from a run before the first
  commit, compare like runner with like.
- **Light the sphere.** `src/Patina.Server/SwatchRendering.cs` adds the same lights, with the directional light
  placed relative to the swatch camera as carve places it relative to its camera.
- **Checks.** Update the sphere checks in `scenarios/Carve.Scenario.Patina/SwatchStage.cs` (and `SwatchSphere.cs`
  if the prediction needs the light) so they hold under the new light, and add one that a roughness-0 graph
  shows a highlight a roughness-1 graph does not (measured, e.g. peak luma on the sphere). The four breaks of
  T-114's As built must still be caught. A new threshold, or a changed one, goes to the person with its probed
  value before it lands.
- **Frames.** Only the sphere frames (sphere 0, 90 and 200, and the sheets' sphere cell) may change; the flat and
  tiled cells and frames stay byte-identical. The changed set goes to the person for approval.
- The README's patina section and `swatch`'s description, where they describe the sphere's lighting.

## Notes

- Raised 2026-11-18 after T-117: the person ruled the fix goes in M-27. Research by an Opus 5.5 agent found no
  roughness defect: the map reaches `Material.Pbr` correctly in both servers; the sphere's sky-only lighting and
  the planks' narrow roughness (0.57 to 0.78) make it look weak. The planks graph stays as it is (the person).
- This task may edit `Carve.Hosting` and `Carve.Server` (T-114 could not), only to share the lights.
- **Stop and ask** if any carve frame changes by one byte, if a guard would need loosening, or for any threshold.
- **Draws new frames, so plan the frame review as its own agent**, as T-114 did. Treat Waystone's "Commented" as
  unanswered until the person says approved.
- Opus: it touches the shared host library and carve's frame guard.

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`), first agent, to the frame-review checkpoint.

- **Shared lights.** `src/Carve.Hosting/RenderLights.cs` (public): `AmbientIntensity` 0.25, `DirectionalIntensity` 0.9,
  `Add(scenes, entities, scene, orientation)` (ambient, `RenderSky.Sky`, directional, in carve's order), `Orientation(cameraDirection)`
  (carve's front light, turned by whatever turns carve's front camera into this one: the code moved as it was), `UpFor`,
  `VerticalCosine`. `src/Carve.Server/RenderLights.cs` went; `ViewFraming.LightOrientation`, `UpFor` and `VerticalCosine`
  delegate to it; `RenderSystem` and `PbrEqualityTests` pass `ViewFraming.LightOrientation(RenderView.Front)`; the test
  project no longer links the old file. Nothing else in `Carve.Hosting` or `Carve.Server` changed.
- **After finish: the milestone's refactor pass.** Run by the cairn process before review and verified by `cairn milestone
  M-27 verify`, three commits changed what the bullet above describes, keeping every value: `cabbfad` dropped
  `ViewFraming.VerticalCosine` (unused once `UpFor` moved; the cone is read only inside `RenderLights`); `ee8608e` dropped
  `PbrMaterials.Sampler`, leaving `TextureUpload.Sampler` the one sampler (T-111's As built); `01cb4b2` made
  `RenderLights.FrontDirection` (1.6, 1.2, 2.2) public, and `ViewFraming.FrontDirection` now reads it rather than holding its
  own copy. RV-68's answer since made `AmbientIntensity` and `DirectionalIntensity` `internal const` and `VerticalCosine`
  `private` (f44e507). So `ViewFraming.LightOrientation` and `UpFor` delegate to `RenderLights`, and `FrontDirection` reads it.
- **The sphere.** `SwatchSystem.Build` calls `RenderLights.Add` with `SwatchCamera.Light` = `RenderLights.Orientation(Eye)`
  (the camera on +z, +y up), so the light sits where carve keeps it relative to its camera. The sheet's sphere label is now
  `sphere, Pbr, carve lights, angle 0`; `swatch`'s description and the README's patina paragraph say how it is lit.
- **Checks** (`SwatchStage.cs`; `SwatchSphere.cs` unchanged, it predicts texels, never a lit shade). Probed on Xellotath
  (local runs `artifacts/scenarios/patina/1..3`), put to the person (Waystone round 1, `G:\My Drive\artifacts\carve\M-27\T-128\round-1\thresholds.txt`)
  and **approved**: *dark* 52 -> 66 (gap luma max now 50.0 / 43.0 / 42.3 at 0 / 90 / 200), *in band* 58..110 -> 80..128
  (staves now 92.0..124.0, 92.3..115.8, 94.5..122.3), and a new *highlight* rule: a #808080 sphere at roughness 0 has a peak
  luma at least 40 above the same at roughness 1. **Measured: 255.0 against 128.1, a step of 126.9.** It adds two frames,
  `swatch-sphere-roughness-0` and `-1`. The four breaks are still caught (the scenario fails otherwise): `roughness-as-base`'s
  staves read 132.1..193.0 and its gaps far above 66.
- **Frame guard: holds.** Before: the start commit 4384202, every GPU suite (`artifacts/T-128/before-jobs.txt`), all on
  Mantorok. After: 439b8b8, every suite also on Mantorok (`after-jobs.txt`), so like runner against like. Tables by
  `artifacts/T-128/compare.py before after` (SHA-256 of every PNG). All 15 carve scenario suites' frames byte-identical
  (483 rows: sky 138, orcward 76, crate 69, cube 32, gatehouse 26, materials 23, uvs 22, character 21, planks 17, cuts 16,
  soft 11, shed 10, rig 9, export 7, viewer-cube 6; none changed, none missing). `server-regression`: 527 against 524
  rows; every differing (request, frame) row is in T-111's two timing races (the `lids_ids ... is_refused` test, whose
  missing frame `e4dc71b7...` is one of its own renders); outside them the table is identical.
- **Patina frames**: flat normal 256, tiled base 128 and the seam frame byte-identical to the carried pass before;
  the sheets differ only inside the sphere cell (x >= 1074, y 27..494: the sphere and its label); spheres 0, 90, 200 changed;
  the two roughness frames new. The verify run's frames equal local run 3's by hash.

- **Verify** `VERIFY PASS` at 439b8b8 (patina-unit, server-unit, scenario-cube, -crate, -sky, -materials, -planks, -patina,
  server-regression on Mantorok; patina-regression on Xellotath).
- **Frame review** by Claude Opus 5.5 (`claude-opus-5-5`), second agent, of job `J-20261118-174636-f1be3b` (`scenario-patina`,
  Mantorok). By hash against the pass before: flat normal 256, tiled base 128 and the seam frame the same; spheres 0, 90, 200
  and both sheets changed; the two roughness frames new. Each changed frame opened once (the sheets as a crop of the sphere
  cell, x 1032..1543):
  - *sphere 0*: lit, broad shading brightest top-centre and darker toward the lower right; the three gaps dark and crisp, the
    staves' grain readable; whole on the slate (x 43..468), facets at the limb as before. No sharp glint, as the planks'
    roughness (0.57..0.78) would give.
  - *sphere 90*: as 0, the stave tones shifted with the turn; gaps and staves read.
  - *sphere 200*: two gaps either side of a wide stave, the same light; reads, whole.
  - *grey, roughness 0*: a small white highlight, 21 pixels at luma 250 or more, x 251..255, y 197..201, just above centre on
    the vertical midline. Carve's light worked through `RenderLights.Orientation` puts the half vector at (-0.01, 0.24, 0.97)
    in camera space, a highlight at about (254, 205) on this sphere's 212 px radius: where carve would put it. The peak clips
    at 255; a light Fresnel rim of sky at the limb.
  - *grey, roughness 1*: smooth top-to-bottom gradient, no highlight (peak 128.1).
  - *sheet 256 and 64, sphere cell*: the label `sphere, Pbr, carve lights, angle 0` reads; the sphere matches sphere 0, the 64
    one a little softer in grain; whole, slate round it.
- **Person**: Waystone round 2 (`G:\My Drive\artifacts\carve\M-27\T-128\round-2`, the five spheres and both cell crops,
  told the roughness-0 peak clips at 255): **approved**, no notes.
- **Not checked**: the highlight's size or shape (clipped); carve's own frames were not opened, only held by hash.
