---
description: >-
  The rover is saved as samples/rover.carve, exported, shown by the viewer and read back as a split part tree through Empyrean's loader
---
# T-97 — The rover in a game

**Status:** In Progress

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** T-96

**Verify:** viewer-cube, scenario-parts

**Perf:** skip: Carve declares no heavy suites

## Goal

The rover that `scenario-parts` builds reaches a game's content folder as a `.glb`. M-3's viewer shows it, and Empyrean's own loader reads it back as the agreed part tree.

## Scope

- **`samples/rover.carve`**, the version 7 file that `scenario-parts` saves. It is committed and kept equal by content to the one the scenario builds, and to `RoverSpec`'s model.
- **The scenario exports** `rover.glb` into a content folder in its run folder.
- **`scripts/run-parts-scenario.ps1`**, built like `run-materials-scenario.ps1`: the scenario, then the viewer on the content folder (`viewer-front`, `viewer-angle`, merged load), then the checks of a new `tests/Carve.PartsLoad.Tests`. Suite `scenario-parts` runs this script from now on.
- **`tests/Carve.PartsLoad.Tests`** reads the run folder in `CARVE_PARTS_RUN` (skipped when unset). It:
  - reads the `.glb` through `IModelGeometryLoader` with `SplitParts`, and repeats T-95's part checks on the file the tools made, not the one Core made;
  - reads the viewer's `view.json`, and checks the viewer frames by hue (olive chassis, sand cab, dark grey wheels, yellow lens) and by order left and right, with one check a mirrored view fails;
  - compares `samples/rover.carve` by content.
  
  It links `RunContent.cs` and `FramePixels.cs` as the materials checks do.
- **Layout rows** in `CLAUDE.md` for the script, the test project and the sample.

## Notes

- **Sonnet.** The viewer frames go to the person with the scenario's frames, if T-96's review has not covered them.
- **Not in scope:** turning a part in the viewer. M-13 shows pivots after import. The split load's numbers prove the pivots here.
- **Open rules: stop and ask.** A viewer frame that cannot show the agreed colours; any change to the viewer itself.
- **Carried:** `viewer-cube` unchanged; each new check shown failing on a broken product, as a table; evidence at the absolute path the dispatch names; `finish` run inside the worktree, its output read whole and its `Merge task/T-97` line given; report a `verify` that waits more than a few minutes.
