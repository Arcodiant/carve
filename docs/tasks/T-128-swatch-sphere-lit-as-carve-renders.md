---
description: >-
  patina's sphere swatch lit by carve's ambient and directional lights as well as the fixed sky, so a swatch shows a material as carve's render does
---
# T-128 — Swatch sphere lit as carve renders

**Status:** In Progress

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
