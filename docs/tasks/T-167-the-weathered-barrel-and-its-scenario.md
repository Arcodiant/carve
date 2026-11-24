---
description: >-
  samples/barrel-weathered.carve: the barrel with raised hoops, edge wear on the hoop rims from curvature and grime beside the hoops from occlusion, in suite scenario-weathered with re-bakes after mesh, UV and graph edits
---
# T-167 — The weathered barrel and its scenario

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-162, T-164, T-165, T-166

**Verify:** server-unit, scenario-materials, scenario-planks

## Goal

The milestone's content, seen: the barrel with raised iron hoops, bright wear on the hoop rims from curvature and
grime on the staves where they meet the hoops from occlusion, built and textured through the tools alone, re-baked
when its mesh, its UVs or a graph change, saved, reopened and exported.

## Scope

- **A new scenario**, `scenarios/Carve.Scenario.Weathered` (suite `scenario-weathered`, `regression`, in
  `tests.json` and `Carve.slnx`; run by `scripts/run-weathered-scenario.ps1` with load checks in
  `tests/Carve.WeatheredLoad.Tests`, in the manner of the planks scenario). Stages, one class a file, in the order of
  `WeatheredStages.cs`.
- **Research first, before any frame** (written into this file's Notes):
  1. *The UVs.* Planning's probe: after `extrude` of faces 0 to 7 and 24 to 31 by 0.015 and a plain `unwrap`, there are
     3 islands and no overlap, but a stave face is 0.115 to 0.130 wide in u with wavy borders, so the planks' eight
     staves (boundaries at u = k/8) drift off the barrel's eight facets. Preferred: the stave faces (8 to 23)
     projected as M-7 does into a band of the square so each facet is one plank, and the hoops, their walls and the
     caps unwrapped into the rest, with `check` reporting no overlap and no UV warning. Measure it; if it cannot be
     had through the tools, show the person both layouts before going on.
  2. *The numbers.* With T-162's graphs on that layout at 512, list for `wear` and `grime`: the texels on each hoop
     rim, on a hoop top's middle, on a stave beside a hoop and at a stave's middle (min, mean, max of each set), and
     the lit colour each gives from `front`, `back` and close up. Every threshold below is then proposed as `a - b`
     with both sets named and the statistic stated, and put to the person in one question.
  3. *What the mesh does not have.* The barrel has no stave-gap geometry, so occlusion finds nothing between staves
     (planning: exactly 1.000 on the plain barrel); the gaps are the planks graph's. Write no check that needs
     mesh-made grime in a gap.
- **Stage 1, build**: `open` `samples/barrel.carve`, `extrude` the two hoop bands 0.015, the UVs of step 1,
  `define_group` `hoops` and `staves`, `define_material` `weathered-iron` and `weathered-staves` from
  `samples/graphs/weathered-iron.texgraph` and `weathered-staves.texgraph` at 512 (base colour, metallic-roughness;
  the staves' normal and occlusion too) and `end-grain` from M-7's PNGs on the caps, `set_material`, `check` silent.
  The graphs are copied into the run folder and named from there, so a later stage can edit them.
- **Stage 2, seen**, every frame with a positive check worked from `get_mesh`'s geometry and UVs and from texels the
  scenario gets through the tools (`render mode=uvs image={graph…}` frames and patina-made PNGs), never from a product
  library: front, back, top, three-quarter; close on a hoop rim from two directions (zoom 4 or more); close on a stave
  where it meets a hoop; the `uvs` look with `wear` and with `grime` and a 4x crop of each; a six-view sheet; at 256,
  512 and 1024. Checks: a rim reads brighter than its hoop top's middle, and a stave beside a hoop darker than the
  stave's middle, by the margins ruled in step 2; the hoop tops' middles and the staves' middles match the lit colour
  of the graphs' clean outputs (M-7's `MaterialsSampler` and `ColourRule`, linked); the caps are M-7's.
- **Stage 3, re-bakes**, each asserting `baked` in the answer and the frame:
  - the same render again: nothing baked, the frame byte-identical;
  - `move` one hoop's rim vertices outward 0.01: the staves beside that hoop bake again and read darker, the other
    hoop's frame region is unchanged;
  - `undo`: nothing baked (a cache hit), the frame byte-identical to stage 2's;
  - the UVs changed (`unwrap` of the hoops with another margin): baked again, the shaded frame within D-6's rule of
    stage 2's and the `uvs` frame different;
  - `weathered-iron.texgraph`'s `wear_amount` rewritten to 0 in the run folder's copy (written by the scenario; the
    product is not told): baked again, the rim no brighter than the hoop top; restored, and the frame is stage 2's.
- **Stage 4, errors seen**: the iron graph renamed on disk, then `render`, `check` and `export` each answer with
  T-166's message listing the `.texgraph` files in the folder; renamed back, all three succeed.
- **Stage 5, saved and exported**: saved as the sample `samples/barrel-weathered.carve` (version 10, graph paths
  `graphs/…` reaching `samples/graphs/`), reopened with stage 2's frames byte-identical, exported into the run
  folder's `game/Content/models/`, and `render_glb` compared with `render` front and three-quarter under D-6's frame
  rule with a control that must fail. `tests/Carve.WeatheredLoad.Tests` reads the `.glb` through
  `IModelGeometryLoader.Load` (three materials, no warning, each texture byte-equal to the bake) and compares the
  saved file with the sample by content.
- **Breaks, on the scenario side** (`CARVE_WEATHERED_BREAK`, each shown failing, in a table with the count that
  caught it): `swap` (the two graphs exchanged), `neutral` (the prediction made from the neutral PNGs), `mirror` (the
  camera mirrored), `stale` (stage 3 compared with stage 2's frame after the `move`), `no-rebake` (expecting no
  `baked` after the graph edit).
- The person approves the frames in one set, after a review agent has opened each itself (cropped and scaled where
  the detail is small) and reported. A retune of the graphs' `levels` ranges asked for there is made in
  `samples/graphs/` with T-162's tests re-run.
- CLAUDE.md: Layout rows for the scenario, the load tests, the script and the sample.

## Notes

- **Decided at planning (2026-11-20, by the person):** the content is the barrel with raised hoops, built through the
  tools from `samples/barrel.carve`: wear on the hoop rims from curvature, grime beside the hoops from occlusion; the
  stave gaps stay the planks graph's own.
- **Planning's probe** (2026-11-20, Debug, a throwaway console): raised hoops at 1024, curvature width 0.02: hoop tops
  mean 0.628, 95th percentile 0.996; occlusion 32 rays, reach 0.15: staves min 0.500, 5th percentile 0.911, mean
  0.986, hoop walls 5th percentile 0.283. Only about 5% of stave texels are darkened, in a thin line by each hoop: the
  close frame is where grime shows, and the front frame may barely show it. Say so to the person with the frames
  rather than widening `reach` unasked.
- **The barrel is symmetric** about its axis and top to bottom, so it carries no mirror or up check on its own: the
  `mirror` break needs the asymmetric edit of stage 3 or the caps' end-grain to be caught. Work each frame's checks
  against that before writing them.
- A scenario references no product library, and breaks live in the scenario, never in the product (M-27).
- Draws many frames at three sizes: Opus, planned as three agents (research and build; frames and re-bakes; the frame
  review). Evidence goes to an absolute path under `D:\repos\carve\artifacts\T-167\`, and frames of a queued run are
  fetched by job id.
- This task creates suite `scenario-weathered`; add it to this file's `**Verify:**` line in the commit that adds it to
  `tests.json`, with every linked file among its inputs (`ColourRule.cs`, `MaterialsSampler.cs`, `CameraPixels.cs` and
  the rest: a missing linked input has been a review Major four times).
- **Stop and ask:** every threshold (step 2); a layout that cannot be had; a frame the checks cannot tell from a broken
  one; any check that would have to loosen.
