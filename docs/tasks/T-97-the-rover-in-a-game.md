---
description: >-
  The rover is saved as samples/rover.carve, exported, shown by the viewer and read back as a split part tree through Empyrean's loader
---
# T-97 — The rover in a game

**Status:** Done

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

### As built

Sonnet 5.5, two agents (the first stopped on a question; this one finished), 2026-11-19.

- `PartsScenario.cs` exports the saved rover after the reopen to `<run>/game/Content/models/rover.glb` (fails on a warning or anything but a `6 nodes` report) and writes `export-result.txt`: 576 triangles, 6 materials, bounds (-1.1, 0, -0.87) to (1.1, 1.7, 0.87), nodes `rover`, `Mast`, `Wheel_FL`..`Wheel_RR`.
- `samples/rover.carve`: the version 7 file `parts/rover.carve` of the run (byte copy), equal by content to the saved file and to `RoverSpec.Build()`.
- `scripts/run-parts-scenario.ps1` runs the scenario, then the viewer on `models/rover.glb` with `--sky` and views `viewer-front=1.2,0.5,2.2` and `viewer-angle=1.6,1.2,2.2`, then the checks. `tests.json`'s `scenario-parts` runs it.
- `tests/Carve.PartsLoad.Tests` (in `Carve.slnx`), 10 checks: split load (names, pre-order, parents, composed transforms, sketch positions, one wheel mesh, triangle counts), merged and split bounds and 576 triangles, the five agreed colours, the sample by content, `view.json`, the viewer frames by hue and order (a mirrored view fails), and the material count below.
- Break table, 9 breaks on copies of the run folder, each failing at least one check: `G:\My Drive\artifacts\carve\M-9\T-97\break-table.tsv`.
- `CLAUDE.md` rows for the scenario, tests project, script and sample.

Rulings by the person, 2026-11-19:

1. Sample-version guard. The person said: "Why are we testing file version? Just remove the test." Removed `Every_committed_file_reads_below_version_7_and_writes_byte_for_byte` from `PartTreeFileTests.cs` with `RepoRoot` and `CommittedFiles`, which only it used. The byte-for-byte rewrite of every committed sample (and the fixtures, `samples/rover.carve` included) is still held by `MorphTargetFileTests.Every_committed_file_rereads_and_rewrites_equal` (T-101), which globs `samples/*.carve`. `CombinedIdentityGuardTests` (T-92) hashes a fixed list of eight samples and does not cover the rover, and it is not a rewrite check.
2. Viewer views stand as chosen, the person's ruling: viewer-front `1.2,0.5,2.2` (the lens faces +X, edge-on from straight ahead) and viewer-angle `1.6,1.2,2.2`.
3. `view.json` lists 12 materials for a `.glb` of 6. Cause: the viewer's `materials` is one entry per submesh of the merged load (`ViewerSystem.Describe` over `SubmeshMaterials`), and the load flattens node instances. The rover has 6 mesh instances (`rover`, `Mast`, four wheels sharing one mesh) of 2 primitives each: 12 submeshes, 6 distinct colours. This is the report's documented per-submesh meaning, not a miscount, and listing distinct materials would change every existing multi-submesh `view.json`, so I did not change it and filed no bug. `The_viewer_lists_a_material_per_submesh_so_the_four_wheel_instances_repeat_the_wheel_pair` pins it.

Frame review: the person approved both viewer frames (`round-1/viewer-front.png`, `viewer-angle.png`). The rover reads as olive chassis, sand cab, dark blue-grey wheels with light-grey bosses, yellow lens; mast and lamp body are white-grey since the spec paints only the lens.

