---
description: >-
  scenario-patina's library stage opens, swatches, checks and exports every sample material and variant, with every pixel worked from its texels, and commits each sample's swatch sheet
---
# T-142 — The node library in scenario-patina

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-140, T-141

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
