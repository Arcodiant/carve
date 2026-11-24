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

**Verify:** server-unit, scenario-materials, scenario-planks, scenario-weathered, scenario-weathered-sizes

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

### Rulings on agent 2's three questions (2026-11-24, by the person, each asked with the measured figures)

1. **Stage 3's unwrap.** D-6's frame rule is applied to the front's pixels that show staves and caps (whose UVs do not change), worked
   from `get_mesh`'s geometry and clear of the hoops' silhouette by a stated margin; and the hoops are held to threshold (1) again (rim at
   least 8 luma brighter than the hoop-top middle), beside what already passes (it bakes again, the `uvs` look differs). If D-6 does not
   hold on those pixels, stop and report the numbers. Declined: dropping D-6 for the step; a UV change that keeps the texels.
2. **The clean colours of stage 2.** M-7's relations on each set's mean colour (lit at `Pool.T` or over; the wood warm by `Pool.M`, the iron
   not) stand in place of the Scope's `ColourRule` match. Declined: widening `ColourRule` first. No exact lit colour is predicted for a
   metal: `ColourRule` predicts a dielectric at roughness 1 only, and the iron is metallic 1 and the staves carry a roughness map.
3. **"Read darker" after the move.** The close on the stave's mean luma over the staves beside the moved hoop must fall by **at least 4**
   (measured 7.9: 80.0 to 72.1). The front view is not held to it: the raised hoop hides those stave pixels there (its mean rose 77.5 to
   80.6 over a different set of points). Declined: any decrease with no margin; a texel rule in place of the shaded check.

### As built (2026-11-24; five agents, every one on Opus 5.5 `claude-opus-5-5`: research and build; stage 2 and 3; the rulings in code, stages 4 and 5 and the split; the breaks, the rebase and the docs; the frame review)

**The scenario.** `scenarios/Carve.Scenario.Weathered`, stages in the order of `WeatheredStages.cs`, one class a file:
`BuildStage` (stage 1), `SeenStage` (2), `RebakeStage` (3), `ErrorsStage` (4), `SavedStage` (5), with the texel sets and the
checks' camera in `WeatheredSets.cs`. Two suites, split by `--Weathered:Stages=`:
- `scenario-weathered` (`=main`, by `scripts/run-weathered-scenario.ps1`, which then runs `tests/Carve.WeatheredLoad.Tests`):
  stage 1, stage 2 at 512, stages 3 to 5. 97 s in the final verify (70 s alone; 160 s when verify ran eight suites at once).
- `scenario-weathered-sizes` (`=sizes`, by `run-scenario.ps1`): stage 1 and stage 2 at 256 and 1024. 93 s in the final verify
  (96 s alone). `est-seconds` 90 and 110.

**For T-168: the barrel, as tool calls.** `open samples/barrel.carve`; `extrude` faces 0 to 7 and 24 to 31 by 0.015 (98 faces,
64 new walls, ids 34 to 97, 32 of them thin wedge walls at the octagon's corners); `project_uvs {method: cylindrical, faces:
8..23, into: [0, 0, 1, 0.34]}` (every stave exactly 0.125 wide in u, at k/8, so each facet is one plank); `unwrap {faces: every
face but 8..23, into: [0, 0.34, 1, 1]}` (3 islands, `check` silent); `define_group hoops` (every non-stave, non-cap face, walls
included) and `staves`; `define_material weathered-iron` and `weathered-staves` from `samples/graphs/weathered-iron.texgraph`
and `weathered-staves.texgraph` at 512 (each graph slot warns "the graph reads the mesh, so it is baked onto each mesh",
expected), `end-grain` from M-7's PNGs; `set_material` on the groups and the caps. Seen with `render` (shaded), and a map with
`render {mode: uvs, image: {graph, output: "wear" | "grime", size}}`. The sample is `samples/barrel-weathered.carve` (version
10, graph paths `graphs/…`).

**The research numbers** (at 512, the `uvs` look at frame 1024): see `### Research` above. Wear: rim 0.459, hoop-top middle 0.040.
Grime: stave beside hoop 0.327, stave middle 0, the hoops 0; 6.3% of stave texels above 0.1, all within about 0.02 m of a hoop.

**The rulings and their measured outcomes** (thresholds as ruled in `### Research`, the three questions below):
- (1) shaded rim minus hoop-top middle at least 8: 21.3 / 16.5 / 20.1 (front / back / close) at 512, held at 256 and 1024.
- (2) shaded stave middle minus stave beside hoop at least 7: 14.7 / 16.8 / 18.6.
- (3) wear texels at least 0.2: 0.419. (4) grime texels at least 0.15: 0.327; grime max on stave middle and hoops 0.000 (at most 0.02).
- (5) after `wear_amount` 0, rim minus hoop-top middle at most 2: 0.031.
- Question 1 (stage 3's unwrap and D-6): D-6 over the front's stave and cap pixels with no hoop pixel within 3 px: 51,443 px, 0
  differ; threshold (1) on the new layout 20.4. The whole frame: 3,598 differ, patch 468 (logged).
- Question 2: M-7's relations on set means stand in for `ColourRule` (lit at `Pool.T`; wood warm by `Pool.M`, iron not).
- Question 3: the close on the stave falls 80.0 to 72.1 after the move (at least 4), over 54 points seen before and after.

**Stage 3's unwrap step is now** `unwrap {faces: the "hoops" group (hoops and walls), into: [0, 0.646, 1, 1], margin: 0.03}`,
then `render`, then `undo`: the widest strip of the rest band clear of the caps' UVs (u 0 to 0.602, v 0.340 to 0.646). Unwrapping
the hoops over the caps' texels made the bake refuse with T-166's overlap message. It no longer exercises a change to the caps' UVs
(the end grain is never re-baked into a new layout) nor a margin-only re-unwrap (the hoops also change band and texel density), so
D-6 on stave and cap pixels holds by construction. The person was told this, with the wedge walls, in the frame review.

**Stage 3, the rest:** the same render again (nothing baked, byte-identical); the top hoop's eight faces `move`d out 0.01 (every
slot of both materials bakes again; the bottom hoop's 1318 points unchanged); `undo` x8 (cache hit, byte-identical); the graph
restored (cache hit, byte-identical). **Stage 4:** the run's iron graph renamed; `render` and `export` refuse and `check` warns with
T-166's message listing the folder's `.texgraph` files; renamed back, all succeed. **Stage 5:** saved, reopened with every 512 frame
of stage 2 byte-identical, exported to `game/Content/models/barrel-weathered.glb`; `render_glb` against `render`: 0 of 262,144
differ, front and three-quarter; the control fails (59,662 differ). **The load tests** (`tests/Carve.WeatheredLoad.Tests`, 2):
the saved file equals the sample by content; the `.glb` loads 3 materials with no warning, each texture byte-equal to the bake.

**The breaks** (`CARVE_WEATHERED_BREAK=<b> cairn test run --fresh scenario-weathered`, after the rebase onto T-207; the count is
`failures.txt`'s first line):

| break | caught first by | count |
|---|---|---|
| `swap` | (1) and (2) in front, back and close-rim (e.g. `w512-front: ... 92.981 - 93.137 = -0.156, under the ruled 8`), the clean relations, stage 3 | 28 |
| `neutral` | (3) and (4) in the `uvs` looks alone | 2 |
| `mirror` | the caps' end grain (`Pool.Rings`) on the top view and the sheet's ±y tiles; also (1), (2), the relations, stage 3 | 19 |
| `stale` | stage 3's ruled darker check alone (`80.014 - 80.773 = -0.760, under the ruled 4`) | 1 |
| `no-rebake` | stage 3's baked check alone | 1 |

**The frame review** (agent 5): the frames of J-20261124-192551-565eeb and -192556-65c2cb, compared by hash first (every
`-reopened` frame and `rebake-again`, `-undone`, `-restored` byte-identical to stage 2's), then 12 images as montages and crops. It
found one fault in the scenario, fixed with this commit set: the rim close camera chose its rim texel from both hoops, so the
tie fell to the bottom hoop at 256 and 1024 and the three sizes showed different hoops; it now takes the top hoop's. The grime
`uvs` crop was centred on the hoop rim, where the staves material never reads grime; it is now on the stave beside the top hoop.
Re-verified (J-20261124-193539-b43147, J-20261124-193542-965283): only those six frames changed. What was seen: raised blue-grey
hoops; the wear a bright, fairly even border round each hoop plate (rim, stave side and both ends), reading as worn edges, not
noise, but regular; with `wear_amount` 0 the hoops go flat dark iron. Grime barely shows from the front and is a soft dark line
under the hoop close up; after the move the band under the moved hoop is plainly darker in the stave close and hard to see in the
front. 256, 512 and 1024 read as one material, 256 a little softer. The wedge walls show in the rim closes as a thin bright
vertical sliver and a small step where plates meet, reading as a joint. The re-unwrapped frame differs only on the hoop rims;
the `.glb` frames match. The stave gaps are wide black bands (the planks graph's) and the top cap's rings are off-centre (M-7's).
**The person approved the set (round 1, 27 files in `G:\My Drive\artifacts\carve\M-29\T-167\round-1`) with no notes**; no
`levels` retune was asked for, so `samples/graphs/` is unchanged.

**Not checked:** the 256 and 1024 frames other than the front and rim closes were compared by hash and test only, not opened;
the exact rim-close framing still differs a little between sizes (the camera follows the chosen texel); no frame on another GPU.

**Docs:** `CLAUDE.md` gains a Layout row for the scenario (stages, the two suites, `--Weathered:Stages=`, the load tests, the
script, the breaks) and `barrel-weathered.carve` in the `samples/` row. `README.md` gains `-weathered` and `-weathered-sizes` in
Build and test, and the sample under Materials and textures.
