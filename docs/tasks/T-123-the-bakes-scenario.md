---
description: >-
  Suite scenario-bakes opens the wrench, bakes its masks and base colour, and sees them in the UV layout look and on the textured wrench from every side and up close
---
# T-123 — The bakes scenario

**Status:** In Progress

**Pri:** P1

**Milestone:** M-24

**Batch:** — (standalone)

**Depends on:** T-121, T-122

**Verify:** core-unit, server-unit, scenario-bakes

## Goal

M-24's content, seen. A new scenario opens the wrench and bakes it through the tools. It shows the bakes in the `uvs`
look and on the wrench under a material named from them, from every side and up close. It is built in stages, so
T-124 to T-126 each add theirs.

## Scope

- **`scenarios/Carve.Scenario.Bakes`**, written as `CLAUDE.md`'s *Writing a scenario* says. Stages follow the Cuts
  scenario's pattern: one class per stage in its own file, run in the order of `BakesStages.cs`, so a later task adds
  one file and one line. `samples/wrench.carve` is copied beside the scenario, as scenario-uvs copies the barrel.
- **Stage 1, `SurfaceStage.cs`: masks and base colour.**
  - `open` the wrench, then `bake` at 1024 with padding 8 into the run folder. The maps are `masks` and `base_colour`,
    and `surface` holds fps-arms' group colours: `all` first as `dark`, then `rubber` #282A2E, `steel` #80858A,
    `dark` #36393E, `red` #BE3C32, `black` #1E1F22 (fps-arms `pipeline/tools.py`, `GROUP_RGB`).
  - `define_material` `wrench` with `base_colour_texture` set to the baked path, and `set_material` on every face.
  - **Frames** (the walk; each new thing seen from more than the default view):
    - the `uvs` look with the base colour under it;
    - the `uvs` look with the `steel` mask under it, and a crop of one island's padding at 4x (cropped by the scenario
      from a 1024 frame, not a new look);
    - `render` of the textured wrench from front, back, left, right, top and a three-quarter direction;
    - close frames at zoom 4 on the jaw and on the handle's rubber grip;
    - a close frame on face 80, the smallest face (390 texels at 1024);
    - a six-view sheet.
  - **Checks**, each with a break shown failing:
    - each `uvs` frame against the PNG by T-121's block rule. Break: the base colour baked with `steel` and `red`
      swapped;
    - each render frame: at the centre of named faces of each group, worked from `get_mesh` and the camera as the
      Materials scenario works its samples, the pixel's class matches its group. `red` is red-dominant. `steel` is
      lighter than `rubber` and `dark` by a margin, with the margin probed on the wrench's own frames and brought to
      the person before landing. Break: the same swap. Sample near face corners as well as centres (M-3);
    - a check a mirrored or wrong camera would fail, since the wrench is near-symmetric in x (M-2);
    - **the export:** `export` the textured wrench, and compare `render_glb` of it with `render` in the front and
      three-quarter views under D-6's frame rule (`ComparisonPng.cs`, linked from the Orcward scenario).
  - Writes `bakes.tsv` into the run folder: one row per baked file with its stats from the result.
- **Suite `scenario-bakes`** in `tests.json` (regression, its inputs including every linked file and
  `samples/wrench.carve`, a gap M-5, M-19 and M-20 each found). The project joins `Carve.slnx`. `CLAUDE.md`'s layout
  table gains its row: what each stage does, as the Cuts row reads.

## Notes

- Decided at planning (2026-11-18), by the person: the wrench as content; one material from bakes alone; the `uvs`
  look's image as the way a bake is seen.
- **Stop and ask** before landing: any threshold. Probe it on the wrench's own frames and bring the numbers; do not
  pick one (M-7's dark iron fell under a threshold probed on bright metal). Also stop before a check that cannot fail,
  or a change to another scenario.
- A look member: plan for two agents (M-6, M-7, M-16). Opus. Hand the frame review to its own agent. That agent looks
  at the frames itself, cropped or scaled (M-7).
- **Evidence:** the orchestrator names an evidence folder outside the worktree at dispatch, since `finish` empties
  `artifacts/` (every past milestone). Fetch queued frames by job id, not by suite (M-16).
- Owns the scenario project, `BakesStages.cs`, the `scenario-bakes` suite and its `CLAUDE.md` row. T-124 to T-126 each
  add a stage file and one line in `BakesStages.cs`, extend the row by a clause and add any new inputs to the suite.
  Those conflicts only add.
- The person looks at the frames before this lands (a Waystone review). Treat "Commented" as unanswered until the
  person says approved.
- **Verify line:** at planning, `cairn lint` accepts only suites already in `tests.json`, so this file names none it
  will add. Once scenario-bakes is in `tests.json`, add it to this file's `**Verify:**` line, with the task's other
  suites.

### Hand-off

By Claude Opus 5.5 (2026-11-19), agent 1 of 2. Status: built, run and probed; a threshold waits on the person; the frame
review (Waystone, round 1) is the next agent's.

**Done.** `scenarios/Carve.Scenario.Bakes` (`BakesScenario.cs`, `BakesStages.cs`, `SurfaceStage.cs`, `BakesChecks.cs`),
in `Carve.slnx`, suite `scenario-bakes` in `tests.json` (every linked file and `samples/wrench.carve` as inputs), its
`CLAUDE.md` row, and the suite on the `**Verify:**` line. A new stage is one file and one line in `BakesStages.cs`.
Stage 1 bakes all six masks and the base colour (coverage 0.4856), checks the file list, no skipped faces and no
warnings, writes `bakes.tsv`, and captures 14 frames plus the 4x crop `wrench-uvs-steel-padding-4x.png`.

**Checks and their breaks** (all run on this machine; the clean run passes, run 2):

- `uvs` frames by T-121's block rule (nearest texel, tolerance 2, a pixel taken a quarter to three quarters into its
  texel and 4 px from every face edge and the square's border), the island pixels also held to the colour worked from
  the groups. `CARVE_BAKES_BREAK=swap` (steel and red swapped in the bake): 13778 island pixels fail.
- Render frames: every face turned to the eye (cosine ≥ 0.5, `BakesChecks.Facing`), at its centre and 0.7 of the way
  to each corner, kept only when the rays through the pixel and 1.5 px around it all meet that face, in sight. `red`
  must be red-dominant (R > max(G, B)) and no other group may be. Swap: 441 failures in all nine views.
- M-2: the x-mirror of each red-box centre (front, back, top, three-quarter, the two zoom-4 closes; not the views
  along x) must not be red. `CARVE_BAKES_BREAK=mirror`: 51 failures, the mirror check among them.
- Export: `render_glb` against `render` under D-6's rule: front 48 px differ, patch 15; three-quarter 10 px, patch 3;
  the control (glb three-quarter against render front) fails it, as it must.

**The question: the steel split (not landed).** `SurfaceStage.SteelSplit` is `null`, so no steel check runs; the
numbers go to `probe.tsv`. Rec. 709 luma of the sRGB pixel, per view, over the sampled points (392 steel):

| view | steel min | rubber max | dark max | black max | margin (steel min − darkest max) | red lead min | others' lead max |
|---|---|---|---|---|---|---|---|
| front (+z) | 119.1 | 47.7 | 59.4 | – | 59.6 | 112 | −11 |
| back (−z) | 118.1 | 47.7 | – | 39.9 | 70.4 | – | −10 |
| left (−x) | 108.5 | 47.7 | 63.1 | – | 45.4 | 110 | −6 |
| right (+x) | 109.4 | 47.7 | 63.1 | – | 46.3 | 107 | −6 |
| top (+y) | 127.5 | – | 61.2 | – | 66.3 | 105 | −18 |
| three-quarter | 117.3 | 46.7 | 64.2 | – | 53.1 | 105 | −8 |
| jaw close ×4 | 120.0 | – | 62.4 | – | 57.6 | 106 | −10 |
| grip close ×4 | 114.1 | 46.7 | – | – | 67.4 | – | −11 |
| face 80 close ×8 | 111.6 | – | 62.1 | – | 49.5 | 117 | −6 |
| all | 108.5 | 47.7 | 64.2 | 39.9 | 44.3 | 105 | −6 |

Under the swap break the steel faces read luma 73.6 to 89.2 (margin 10.5 to 33.1 by view), so a luma rule alone
does not catch the swap; red-dominance does.

Options I would offer:

1. **One split at luma 86** (midway between 64.2 and 108.5): steel above, rubber, dark and black below; 22 of headroom
   each side.
2. **A per-view margin**: in each view, the least steel luma exceeds the most of `rubber`, `dark` and `black` by at
   least 30 (the least seen is 45.4, left).
3. **Red-dominance with a lead**: red's R exceeds max(G, B) by at least 50 and every other group's lead stays below 0
   (seen: red ≥ 105, others ≤ −6). Today it is a lead above 0, with no number chosen; say if that stands.

Also the person's to rule: the sampling placement (`Facing` 0.5, corners at 0.7, the 1.5 px solid test).

**Left.** The person's ruling, landed with its break shown failing; the Waystone frame review (round 1) of the 14
frames and the crop; then `### As built`, `cairn lint` and `finish`. Not checked: the frames by eye beyond the sheet
and the crop (both look right: the ratchet wrench, red box at +x, a white steel island with its grey padding); the
`samples/wrench.carve` stats against fps-arms. Evidence: `G:\My Drive\artifacts\carve\M-24\T-123\probe` (run 2's
frames, `compare/`, the TSVs, and `probe.tsv` of both breaks). The queued run, job `J-20261120-004722-5c8b13` (22a6fb7a7), is byte-identical frame for frame to run 2; its frames, `compare/` and TSVs are staged in `G:\My Drive\artifacts\carve\M-24\T-123
ound-1` for the review.

**Surprising.** The first run failed on the `uvs` square's border line (white over the image) and on mirror checks in
the views along x (a point and its mirror share a line of sight); both are now excluded by rule, not by tolerance. Two
dark corners next to the red box read red-dominant until the solid-pixel ray test was added.
