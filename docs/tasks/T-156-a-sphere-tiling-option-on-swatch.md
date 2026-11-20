---
description: >-
  swatch takes a sphere tiling (whole repeats, default 1), so a material can be judged on the sphere at a texel density nearer a game's; the samples' committed sheets use 3
---
# T-156 — A sphere tiling option on swatch

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-139

**Verify:** patina-unit, scenario-patina

## Goal

`swatch` takes an optional sphere tiling, so the sphere wears the texture repeated n times in u and v. At 1, the
default, every sheet and sphere is byte-identical to what it is now. The samples' committed sheets (T-142) use 3.

## Scope

- **The argument** on `swatch`: a whole number of repeats, 1 to 8, default 1, applying to the sphere view and the
  sheet's sphere cell. It is named in the sheet's title or caption when it is not 1, as T-139's overrides are. The
  description says what it is for and gives it in a worked example, which a test runs as written.
- **How:** the sphere's UVs scaled by n, through the material's texture transform or the mesh's UVs, whichever
  `SwatchSphere.cs` and the engine allow without a new engine feature. If the engine is missing something, record it as
  an engine need, as `CLAUDE.md` says.
- **Tests and scenario:**
  - At tiling 1, every existing swatch frame and sheet is byte-identical, by hash against `main`'s, fetched by job id.
  - A scenario-patina check predicts the sphere at tiling 3 from `SwatchSphere.cs`'s geometry and the exported texels,
    as the sphere at tiling 1 is predicted now. A break (the tiling ignored, or applied to one axis) is caught, its
    switch in the scenario, never in the product.
- **Review:** the painted metal's sphere at tiling 1 and 3, shown to the person.

## Notes

- **Decided (2026-11-20, by the person):** approving T-137's painted metal, the person noted the sphere is low
  resolution for the object and asked to tile it 3 x 3. Offered every sphere tiled, an option on `swatch`, or filing it
  off-milestone, they chose an option on `swatch`, default 1 so existing frames stay as they are, with the samples'
  sheets using 3.
- T-139 owns `swatch`'s arguments until it lands; this task starts after it. T-142, which commits each sample's swatch
  sheet, waits on this task and draws its sheets at tiling 3.
- Sonnet: the scope is concrete.

### As built

Built by Sonnet 5.5 (`claude-sonnet-5-5`).

- `swatch` takes `tiling`, a whole number 1 to 8, default 1, for sheet and sphere (refused for flat and tiled, as `angle` is). Done through `MeshInstance.AtlasRect = UvRect(0, 0, n, n)` with the texture sampler already Repeat: no new engine feature, no engine need. The Empyrean docs say a `UvRect`'s components are in [0, 1], but 0.129.0 draws a size above 1 as a repeat, on every map (base colour, normal, metallic-roughness, occlusion), and the scenario holds it. Not guaranteed by the docs, so a later engine bump is caught by that check.
- The sheet's title gains `   tiling 3`, the sphere's caption `tiling 3,`, the sheet's caption `, tiling 3`; at 1 nothing is added.
- Worked examples in the description: `{"view":"sphere","tiling":3}` (drawn by the scenario, since the sphere needs an engine) and `{"view":"sphere","tiling":9}`, refused, run as written by `SwatchTests`.
- Scenario (`SwatchTiling.cs`): a 4 x 2 stripe grid (u and v differ, which the planks' vertical staves cannot show) exported at 64, the sphere at tiling 1, 3 (angles 0 and 90) and 8 predicted from `SwatchSphere`'s geometry and those texels (0 wrong of 10 to 54 thousand pixels; white at luma 220 or more, black at 29 or less, rule 100 and 60). Breaks, all on the scenario side: tiling ignored, in u only, in v only, 2 for 3, 3 for 1, axes swapped; each fails thousands of pixels. 90 degrees equals 0 at tiling 3 for this grid (12 periods), so the angle does not distinguish there.
- Byte identity: all 16 frames of T-139's `scenario-patina` job (J-20261120-070631-363baf) equal by SHA-256 to this run's; plus the sphere at tiling 1 equals the sphere with no argument.
- Review: painted metal at tiling 1 and 3 approved by the person, who also asked to see 6; shown, and 3 chosen for the samples' sheets (T-142).
- Unit tests: refusals, the description, the worked refusal.
