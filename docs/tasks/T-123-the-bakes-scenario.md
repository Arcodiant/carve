---
description: >-
  Suite scenario-bakes opens the wrench, bakes its masks and base colour, and sees them in the UV layout look and on the textured wrench from every side and up close
---
# T-123 — The bakes scenario

**Status:** Done

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

### As built

By Claude Opus 5.5 in two agents: agent 1 (Opus 5.5) built, ran and probed the scenario; agent 2 (Opus 5.5) landed the
person's rulings, showed each failing and ran the frame review.

- **`scenarios/Carve.Scenario.Bakes`** (`BakesScenario.cs`, `BakesStages.cs`, `SurfaceStage.cs`, `BakesChecks.cs`), in
  `Carve.slnx`; suite `scenario-bakes` in `tests.json` (every linked file and `samples/wrench.carve` as inputs), its
  `CLAUDE.md` row, and the suite on the `**Verify:**` line. A new stage is one file and one line in `BakesStages.cs`.
- **Stage 1** bakes all six masks and the base colour at 1024, padding 8 (coverage 0.4856), checks the file list, no
  skipped faces and no warnings, writes `bakes.tsv`, `samples.tsv`, `mirrors.tsv`, `probe.tsv` and `differences.tsv`,
  and captures 14 frames plus the 4x crop `wrench-uvs-steel-padding-4x.png`.
- **Checks.** `uvs` frames by T-121's block rule (nearest texel, tolerance 2, a pixel a quarter to three quarters into
  its texel and 4 px from every face edge and the square's border), the islands also held to the colour worked from the
  groups. Render frames sampled on faces turned to the eye (cosine ≥ 0.5, `BakesChecks.Facing`), at the centre and 0.7
  of the way to each corner, kept only when the rays through the pixel and 1.5 px around it all meet that face. M-2: the
  x-mirror of each red-box centre (views not along x) must not lead red. Export: `render_glb` against `render` under
  D-6's rule, front 48 px differ (patch 15), three-quarter 10 px (patch 3); the control (glb three-quarter against render
  front) fails it, as it must.
- **The person's rulings (2026-11-20)**, landed as written:
  - *Steel:* in each view, the darkest `steel` sample is at least 30 luma (Rec. 709) above the lightest `rubber`,
    `dark` or `black` sample of that view (`SurfaceStage.SteelMargin`). No fixed split. Least margin seen: 45.4 (left).
  - *Red:* `red` samples lead max(G, B) by 50 or more (`RedLeadMin`); every other group's lead is at most 0. Seen: red
    ≥ 105, the rest ≤ −6.
  - *Sample placement:* accepted as built (cos 0.5, centre plus 0.7 toward each corner, the 1.5 px solid test).
- **Breaks** (`CARVE_BAKES_BREAK`, scenario side only), each run on this machine after the rulings landed; the clean
  run passes:
  - `swap` (steel and red swapped in the bake): 448 failures: red lead under 50 ×38, another group leading ×392, steel
    margin ×7 (10.5 to 23.9), the mirror ×2, the `uvs` frames ×9.
  - `faint` (new: steel baked #5C6064, red #8C6460, so steel stays grey and red still leads): 53 failures: red lead
    under 50 ×38 (leads 16 to 35, which the old lead-above-0 rule passed), steel margin ×6 (16.5 to 27.9), `uvs` ×9.
  - `mirror` (camera mirrored left to right): 57 failures, the mirror check ×2 among them.
- **Frame review:** Waystone round 1, the 14 frames, the crop and both compare triptychs from job
  `J-20261120-005533-8f1553` (06f0844; byte-identical to job `J-20261120-004722-5c8b13`), in
  `G:\My Drive\artifacts\carve\M-24\T-123\round-1`. The person **approved**, no notes. Agent 2 looked at the frames
  itself, scaled, before sending. Break logs in `...\T-123\breaks`, the probe in `...\T-123\probe`.
- **Not checked:** `samples/wrench.carve`'s stats against fps-arms.
- **Surprising.** The first run failed on the `uvs` square's border line and on mirror checks in the views along x (a
  point and its mirror share a line of sight); both are excluded by rule, not by tolerance. Two dark corners next to the
  red box read red-dominant until the solid-pixel ray test was added.
