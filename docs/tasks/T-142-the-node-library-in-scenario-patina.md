---
description: >-
  scenario-patina's library stage opens, swatches, checks and exports every sample material and variant, with every pixel worked from its texels, and commits each sample's swatch sheet
---
# T-142 — The node library in scenario-patina

**Status:** Done

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-140, T-141, T-155, T-156, T-157

**Verify:** scenario-patina, patina-unit

## Goal

The real `patina`, driven through its tools alone, shows every M-28 material: each sample opened, seen from more than
one view, checked clean, exported, and its variants seen. Each sample's sheet is committed, so a person can see the
library without running anything.

## Scope

- **`LibraryStage.cs`** in `scenarios/Carve.Scenario.Patina`, one line in `PatinaStages.cs`. For each of
  `cobblestones`, `bricks` (and its grey variant by `parameters` override), `fabric`, `painted-metal`, `floor-tiles`,
  `old-wall`, and `grime` at its defaults:
  - `open` it (the nested files of `old-wall` listed);
  - `check` it: `ok` true and no problems;
  - `swatch` its sheet, `tiled` at 3 x 3, and the sphere at angles 0 and 90 (a turned view, so a texture mirrored or
    laid the wrong way round shows);
  - `export` at 256, compared byte for byte with `samples/graphs/<name>/*.png`;
  - every pixel of flat and tiled worked from the exported texels, and the sphere's from `SwatchSphere.cs`'s geometry and
    those texels, under the rules `SwatchStage.cs` uses (T-114, T-128), with no reference to a product library (RV-68).
- Plus one `check` call on `samples/graphs/faults/chrome-in-paint.texgraph`, which must report `metallic-not-binary`, and
  a `swatch` of the floor tiles with a 30-degree `transform` override built through `edit_graph`, which check must
  report as `does-not-tile`.
- **Breaks, on the scenario side** (`library-breaks.txt`): a texel prediction from a u-mirrored export, one shifted by
  a tile, the bricks' variant drawn from the base graph's texels, and a sphere predicted without metallic. Each must
  fail.
- **Committed sheets**: `samples/graphs/<name>/swatch.png` for each sample, copied from the run. They are for a person
  to read and are not compared by the scenario. CLAUDE.md's `samples/` row says so.
- `tests.json`: scenario-patina's `est-seconds` is measured and updated.

## Notes

- **Decided at planning (2026-11-19, by the person):** the five materials (cobblestones, bricks with fabric, painted metal,
  floor tiles, the old wall with grime), each committed with its PNGs and sheet, seen through the real server.
- Draws many new frames, so this is a task of its own. Sonnet builds the stage. A second agent opens every frame and
  puts them to the person in one Waystone review before it lands. Treat "Commented" as unanswered until the person
  says approved.
- Fetch a queued run's frames by job id, not by suite: `cairn test artifacts <suite>` returns the newest job from any
  branch (M-27).
- **Stop and ask:** a sample that check flags, a threshold to loosen, or a frame rule that cannot hold. Each sample
  task's look was approved before it landed, so a look that changes here is a stop too.

### As built

Built by Claude Sonnet 5.5 (`claude-sonnet-5-5`) in one agent.

- `scenarios/Carve.Scenario.Patina/LibraryStage.cs` (one line in `PatinaStages.cs`, after the nested stage) and `LibrarySphere.cs`.
  For cobblestones, bricks, bricks-grey (the `parameters` override `mortar 0.08, tint "#7a7a70"`), fabric, painted-metal,
  floor-tiles, old-wall (its two nested files named by `open`) and grime: `open`, `check` (ok, no problems), `export` at 256
  compared byte for byte with the committed PNGs (and no file more or fewer), the sheet at tiling 3 and the sphere at
  angles 0 and 90, also at tiling 3.
- Sheet columns come from the graph file's output nodes (roles in role order, then named a to z); each flat cell is 1:1 and each
  tiled cell 3 x 3 scaled into 256, exact against the exported texels (roughness alone is `roughness.png` with B 0, metallic
  alone its B spread grey); the sheet's sphere cell equals the sphere view at angle 0.
- The sphere: silhouette as the planks' rule (outside is the slate, at most 0.1 % of inside is), and a new rule, since a lit shade
  cannot be predicted without a lighting model. A pixel has the same light at every angle, so between 0 and 90 the pixel's luma
  difference follows the difference of the tone under it (mean base-colour luma over the texels in its footprint at tiling 3,
  times `1 - 0.34 m` for the metallic texel m: T-134's measured drop). The Pearson correlation over about 105,000 pixels must be at
  least `CorrelationMin` 0.6 (measured 0.896 to 0.994; u-mirrored texels give -0.31 to 0.05) and, for painted-metal, `MetalMin`
  0.62 (0.661 with metallic in the prediction, 0.571 without). The painted-metal margin is the thin one (0.04 above its line).
  These are new rules, not loosened ones; `library-checks.tsv` in the run folder has the numbers.
- Faults: `faults/chrome-in-paint.texgraph` reports `metallic-not-binary`; floor tiles with a 30-degree `transform` on `base`, made
  through `edit_graph`, report `does-not-tile` on base_colour, and its tiled view is exact against its export.
- Breaks (`library-breaks.txt`): a u-mirrored export (every sample's sheet and sphere), a half-tile shift of the tiled cells (a
  whole-tile shift gives the same picture, so half a tile is the shift that can fail), the grey bricks drawn from the plain bricks'
  texels, and painted-metal's sphere predicted without metallic. Each fails the check named for it.
- Committed sheets: `samples/graphs/<name>/swatch.png` (bricks' grey at `bricks/variants/grey/swatch.png`), copied from run
  `artifacts/scenarios/patina/3`; not compared by the scenario. CLAUDE.md's `samples/` row and the scenario row say so.
- Also fixed: `GraphStage`'s expected tool list lacked `check` (T-141 added it; the stage failed on this branch before my change).
  `SwatchStage`'s `CheckRegion`, `Under`, `Bright`, `IsSlate`, `Luma`, `Tampered`, `Drawn` and `RepoRoot` are now `internal` for reuse.
- Looked at: all eight sheets and sixteen spheres (as three montages). Cobblestones, bricks, grey bricks, fabric, painted metal
  (green paint with bright worn steel patches, which read glossy on the sphere), floor tiles (blue triangles on cream tiles, grout
  lines, five columns with height), old wall (bricks with grime streaks) and grime (one column, vertical streaks) all read as
  their sheets, the turned view of a sphere differs from angle 0 as the texture, not a mirror, predicts. The person approved the
  committed sheets and frames (Waystone, round 1, `G:\My Drive\artifacts\carve\M-28\T-142\round-1`).
- **Not decided:** whether the library stage also captures each sample's cube (T-157). I put it to the person in the review; the
  answer said only "Approved", so it is not added. Adding it is one `view: cube` capture per sample judged by `SwatchCubeCheck`.
- **For T-143:** the stage writes `library-*-texels/`, `library-checks.tsv`, `library-breaks.txt` and frames `library-<name>-sheet`,
  `-sphere-0`, `-sphere-90`, `library-floor-tiles-turned-tiled` into the run folder. The stage takes about 3 minutes of the
  scenario (`est-seconds` set to 300; two verify runs took 217 s and 303 s). Frames were not hash-compared with another branch: every library frame is new, and the
  existing stages were not changed.
