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

**Verify:** server-unit, scenario-materials, scenario-planks, scenario-weathered

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

### Research (2026-11-24, Opus 5.5 `claude-opus-5-5`, through the tools in run J-20261124-144517-60af7a)

Measured by `ResearchStage.cs` in suite `scenario-weathered`, written to the run's `research.txt` (a copy in the
worktree's ignored `artifacts/T-167/`). Everything below comes from `get_mesh`, `check` and `render` answers.

**The mesh after `extrude` of faces 0 to 7 and 24 to 31 by 0.015:** 98 faces, 104 vertices, closed, one shell. `extrude`
moves each face along its own normal, so every hoop face gets four walls of its own (64 in all, ids 34 to 97). Two kinds:
the rim walls toward the stave or the cap, and thin wedge walls at the octagon's corners between neighbouring hoop
faces. The base drops to y = -0.0027, because the hoop faces' normals lean 0.18 downward. `check` gives that as a note.

**1. The UVs. The preferred layout is had, and taken.** The tool calls:
- `project_uvs {method: cylindrical, faces: 8..23, into: [0, 0, 1, 0.34]}`;
- `unwrap {faces: every face but 8..23, into: [0, 0.34, 1, 1]}`.

What it gives:
- 3 islands.
- Every stave face is exactly 0.1250 wide in u, at k/8 to (k+1)/8. Faces 8 to 15 and 16 to 23 share the same u columns.
- The staves take v 0.0081 to 0.3319. The projection keeps their aspect, so a band taller than 0.34 only wastes room;
  the first try at 0.625 left v 0.15 to 0.47 used.
- `check`: `ok`, no problems, no warnings, 98 of 98 faces mapped.

The plain `unwrap` of every face gives the same answer from `check` (no overlap, no warning) and also 3 islands. Its stave
faces are 0.1222 to 0.1367 wide in u and drift off k/8 (for example stave 8 at u 0.7446 to 0.8746), as planning found.

`define_material` warns on each graph slot that "the graph reads the mesh, so it is baked onto each mesh". That is
expected, and stage 1 fails on any other warning.

**2. The numbers, at 512 on that layout.**

The texel sets:
- The texels are read from `render mode=uvs image={graph, output, size: 512}` at frame size 1024, the largest `render`
  takes. A 512 texel is 1.75 px there. A texel is used only if its centre lies at least 2 px from its face's UV edges,
  which the look draws.
- **hoop rim:** a hoop face (0 to 7, 24 to 31) within the outer 15% of its height, which is within 0.015 m of its two rim
  edges.
- **hoop top middle:** the middle 20% of a hoop face's height.
- **stave beside hoop:** a stave texel within 0.02 m in y of the staves' ends.
- **stave middle:** a stave texel within 0.03 m of the staves' mid-height.

The texel values (0 to 1):

| set | n | wear min / mean / max | grime min / mean / max |
|---|---|---|---|
| hoop rim | 5014 | 0.000 / 0.459 / 1.000 (p5 0, p95 0.976) | 0 / 0 / 0 |
| hoop top middle | 4779 | 0.000 / 0.040 / 1.000 (p95 0.404) | 0 / 0 / 0 |
| stave beside hoop | 2976 | 0 / 0.001 / 0.153 | 0.055 / 0.327 / 0.565 (p5 0.165) |
| stave middle | 4960 | 0 / 0.009 / 0.478 | 0 / 0 / 0 |
| all stave texels | 80352 | — | mean 0.019, p95 0.110; 14.7% above 0.02, **6.3% above 0.1** |

The lit colour of each set: the set's points projected into shaded 512 frames. Only points facing the camera and in
sight are used, every third one. The table gives the mean RGB, then the mean luma (Rec. 709).

| set | front | back | close (zoom 4 on the rim facing front) |
|---|---|---|---|
| hoop rim | (105,111,120), 110.6 | (92,112,140), 109.6 | (102,106,113), 105.9 |
| hoop top middle | (84,90,99), 89.3 | (79,95,119), 93.1 | (82,86,94), 85.8 |
| stave beside hoop | (97,75,60), 78.4 | (103,80,66), 83.8 | (103,76,56), 80.5 (n 62) |
| stave middle | (120,88,65), 93.1 | (128,95,75), 100.7 | (129,94,66), 99.2 (n 34) |

Per-set minimum luma reaches 27 to 30 on the staves. Those are the planks graph's dark gaps. A check on a minimum would
catch the gaps, not the grime, so the statistic proposed below is the mean.

**Grime, as planning expected.** Grime is a thin line by each hoop: 6.3% of stave texels have grime above 0.1, all
within about 0.02 m of a hoop. It is nothing at a stave's middle. In the shaded frames the staves beside a hoop are still
14.7 (front), 16.9 (back) and 18.7 (close) luma darker than the staves' middles, so the front frame does show it,
narrowly. Neither `reach` nor the graph was changed.

**3.** The barrel has no stave-gap geometry, so no check needs mesh-made grime in a gap. Grime on the hoops is exactly 0.

**Proposed thresholds, for the person to rule (not yet ruled).** Each is `a - b` of means, over the sets named:
1. **Shaded rim:** mean luma of the visible hoop-rim points minus that of the hoop-top-middle points, in each of front,
   back and close, **at least 8**. Measured 21.3 / 16.5 / 20.1; 8 is half the smallest.
2. **Shaded grime:** mean luma of the stave-middle points minus that of the stave-beside-hoop points, in each of front,
   back and close, **at least 7**. Measured 14.7 / 16.9 / 18.7.
3. **Wear texels:** mean `wear` of the hoop-rim texels minus that of the hoop-top-middle texels, in the 512 `uvs` look,
   **at least 0.2**. Measured 0.419.
4. **Grime texels:** mean `grime` of the stave-beside-hoop texels minus that of the stave-middle texels, **at least
   0.15**. Measured 0.327. Also, the stave-middle and hoop texels' `grime` max **at most 0.02**; measured 0.000.
5. **Stage 3's `wear_amount = 0`:** "no brighter" taken as the shaded rim minus hoop-top-middle mean luma **at most 2**.
   Not yet measured, so this one is a proposal of form only.

**Ruled (2026-11-24, by the person): all five as proposed**, each a difference of set means. Declined: thresholds at
three-quarters of the measured differences, and another statistic than set means. (5) was ruled as a form before it was
measured: if 2 luma does not hold when stage 3 runs, stop and report the number rather than move it.

Alternatives: a margin of 0.75 of the smallest measured gap (12 / 11 / 0.31 / 0.25) is tighter. A test on p95 instead of
the mean would be noisier on the rim, whose p5 is 0 because of the `breakup` mask.

### Hand-off (2026-11-24, agent 1 of 3, Opus 5.5 `claude-opus-5-5`)

**Done:**
- The scenario's skeleton: `scenarios/Carve.Scenario.Weathered` (`WeatheredScenario.cs` with `WeatheredRun`,
  `WeatheredStages.cs`, `BuildStage.cs`, `ResearchStage.cs`).
- Suite `scenario-weathered` in `tests.json`, with every linked file among its inputs, and in `Carve.slnx`.
- `scripts/run-weathered-scenario.ps1`, scenario step only. The load-test step, as in `run-planks-scenario.ps1`, comes
  with `tests/Carve.WeatheredLoad.Tests` in stage 5, and that project goes into the suite's inputs then.
- The `**Verify:**` line.
- Stage 1: open, extrude, the layout above (`Barrel.StaveBand` and `Barrel.RestBand`), the groups `hoops` (every
  non-stave, non-cap face, walls included) and `staves`, and the three materials at 512 with the graphs copied into the
  run folder's `graphs/`. The end grain is on the caps. `check` is silent and stage 1 holds it.
- The research above.

**Checked:** `cairn test run scenario-weathered` passes, in 25.7 s including the research. No frame was opened, and no
`verify` was run.

**Left:**
- The threshold ruling: the question above, which this agent could not ask itself (no `AskUserQuestion`). Record it here
  with its date.
- Stages 2 to 5, the breaks table, `tests/Carve.WeatheredLoad.Tests`, the sample, the README and CLAUDE.md rows, the
  frame review and the person's approval.
- `ResearchStage` checks nothing. Fold its set code into stage 2's checks, or drop it once stage 2 measures the same
  sets. Its last step does a plain `unwrap` and then `undo`s it.

**Surprising:**
- The per-face extrude's corner wedge walls (32 of them). They may read as thin vertical lines in the wear, since
  curvature finds those corners. Look for this in the close rim frames.
- The 1024 uvs frame is render's largest size. 2048 is refused.

**Sizing (agent 1):** the run takes 25.7 s now, with bakes of 0.4 s (wear) and 1.0 s (grime) at 512. The 1024 frames add two
materials' bakes at about 6 to 7 s each, both drawing the slow-bake warning, on top of every frame of stage 2 at three
sizes. Expect about 90 to 150 s in all, so splitting stage 2's 1024 frames into their own suite may be needed.

### Hand-off (2026-11-24, agent 2 of 3, Opus 5.5 `claude-opus-5-5`)

**The ruling** is recorded above, under the proposed thresholds.

**Done and committed:**
- `ResearchStage` is gone. Its set code is now `WeatheredSets.cs`, and stage 2's checks use it, with `Camera` (the checks' camera, and the
  `mirror` break). The numbers each check measures go to the run's `sets.txt`.
- **Stage 2** (`SeenStage.cs`) **passes.** It runs at bake sizes 256, 1024 and 512, in that order, so the stages after it start at 512. Each
  size has front, back, three-quarter, top, close on a rim (the research's camera), close on the rim from the side, close on the stave below the
  top hoop, the `uvs` looks of `wear` and `grime` with a 4x crop of each (written by the scenario into `frames/`), and a six-view sheet. The
  checks:
  - The five ruled thresholds. (1) and (2) are checked in front, back and close-rim, (3) and (4) in the `uvs` looks, at every size. At 512 the
    measured values are the research's: 21.3 / 16.5 / 20.1 and 14.7 / 16.8 / 18.6 luma, 0.419, 0.327.
  - In every view, the clean iron and wood as M-7's relations on each set's mean colour: lit at `Pool.T` or over, and the wood warm by `Pool.M`
    where the iron is not.
  - The caps' end grain through `Pool.Rings` on the top view and on the sheet's +y and -y tiles.
- **Stage 3** (`RebakeStage.cs`) is written. All of it passes except the unwrap's D-6 check (question 1). Each step checks the `baked:` line.
  - The same render again: nothing baked, byte-identical.
  - The top hoop's eight faces moved outward 0.01, one `move` each: baked. The staves beside the moved hoop read 80.0 then 72.1 luma, in the
    close on the stave and over points seen both before and after. The bottom hoop's 1318 points are unchanged. The bounds are unchanged.
  - `undo` x8: nothing baked, byte-identical.
  - `unwrap` of the rest with margin 0.03: baked, and the `uvs` look differs.
  - `wear_amount` 0: baked. Rim minus hoop-top middle is **0.031 luma, so ruled threshold (5) holds.**
  - The graph restored: nothing baked (a cache hit), byte-identical.
- Breaks: see the table below.

**Questions for the person (not decided here):**
1. **Stage 3's unwrap fails D-6's rule.** The front differs from stage 2's in 10344 of 262144 pixels (3.9%), largest patch 8407. The limit is
   0.5% or a patch of 50. The cause: the iron graph's noise (`chips`, `breakup`, `grain`) is laid out in UV space, so a new layout moves the
   wear's breakup and the grain. No change to the UVs avoids that. The triptych is the run's `compare/rebake-unwrapped.png`. Possible answers:
   D-6 over the staves only (their UVs do not change); D-6 dropped for this step; another UV change. The check stays as written and fails, so
   `verify` cannot pass until this is ruled.
2. **"Match the lit colour of the clean outputs".** `ColourRule` predicts a dielectric at roughness 1. The iron is metallic 1 and the staves
   carry a roughness map, so it cannot predict either one. Stage 2 checks M-7's relations on set means instead (above). The person should say
   whether that stands.
3. **"Read darker" after the move** is taken as a lower mean luma, with no margin. It is checked in the close on the stave because in the front
   view the moved hoop hides the stave pixels next to it, and the mean there rose from 77.5 to 80.6. The grime texels rose 0.333 -> 0.508.

**Left:** stage 4 (errors), stage 5 (save, sample, export, `render_glb`), `tests/Carve.WeatheredLoad.Tests` with the script's load step and its
inputs, `samples/barrel-weathered.carve`, the CLAUDE.md Layout rows and README, `est-seconds` (now 120; a run takes 121 s, so expect about
150 s with stages 4 and 5; no split yet), `verify`, the frame review.

**Checked:** only `cairn test run scenario-weathered`: it fails on question 1 alone. No frame was opened. No `verify` was run.

**Breaks** (`CARVE_WEATHERED_BREAK`, each run with `cairn test run --fresh scenario-weathered`; the unwrap failure of question 1 is in every
run and is left out of the count):
| break | caught by | count |
|---|---|---|
| `swap` | (1), (2) and the clean relations in every view, e.g. `w256-front: shaded, hoop rim minus hoop top middle: 93.625 - 93.580 = 0.045, under the ruled 8` | 77 |
| `neutral` | (3) and (4) at each size, e.g. `at 256: wear, hoop rim minus hoop top middle: 0.000 - 0.000 = 0.000, under the ruled 0.2` | 6 |
| `mirror` | the caps' end grain (`Pool.Rings`, which fails through the driver, so the run reports this first failure and no count): `w256-top: top at 256: end grain sample at (250, 84) is on face 32, not the cap 33 in sight` | 1 or more |
| `stale` | stage 3's darker check: `after the move: the 54 seen staves beside the moved hoop read 80.773 luma, not darker than stage 2's 80.014` | 1 |
| `no-rebake` | stage 3's baked check: `after wear_amount 0: expected nothing baked, render said baked: [...]` | 1 |

Stage 3's bottom-hoop check is not reached by `mirror`, because the end grain fails the run first. The runs took 122 to 147 s.
