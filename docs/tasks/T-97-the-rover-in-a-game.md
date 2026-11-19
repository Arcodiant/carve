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

### Hand-off

Sonnet 5.5, 2026-11-19, about 33 tool calls. Stopped on a question for the person (below); not finished, `verify` is red on one check only.

- **Done, committed:**
  - `PartsScenario.cs` exports the saved rover after the reopen to `<run>/game/Content/models/rover.glb` (fails on a warning or anything but a `6 nodes` report) and writes `export-result.txt`: 576 triangles, 6 materials, bounds (-1.1, 0, -0.87) to (1.1, 1.7, 0.87), nodes `rover` and `Mast`, `Wheel_FL`..`Wheel_RR`.
  - `samples/rover.carve`, the version 7 file `parts/rover.carve` of the run (byte copy), equal by content to the saved file and to `RoverSpec.Build()` (checked by the new tests).
  - `scripts/run-parts-scenario.ps1` (scenario with `-ScenarioTimeoutSeconds 900`, viewer on `models/rover.glb` with `--sky`, views `viewer-front=1.2,0.5,2.2` and `viewer-angle=1.6,1.2,2.2`, then the checks; same skipped/none-passed guards as the materials script). `tests.json`'s `scenario-parts` runs it, with the viewer, the new tests project, `samples/rover.carve` and the linked files in its inputs.
  - `tests/Carve.PartsLoad.Tests` (in `Carve.slnx`), 9 checks, all pass on run `artifacts/scenarios/parts/1`: split load (names and pre-order, parents, composed transforms against `Model.WorldTransform`, sketch positions, one wheel mesh, 40/40/124 triangles, 3 meshes), merged and split merged-mesh bounds and 576 triangles, the five agreed colours as materials, the sample by content, `view.json`, and the viewer frames by hue and order (olive, sand, dark grey, yellow; cab centre at least 60 px right of the lens centre, which a mirrored view fails).
  - Break table, 9 breaks made on copies of the run folder, each fails at least one check: `G:\My Drive\artifacts\carve\M-9\T-97\break-table.tsv`.
  - `CLAUDE.md` rows for the scenario, the tests project, the script and the sample.
- **Verify state:** `scenario-parts` passes (130 s) through the new script, `viewer-cube` passes. `core-unit` fails on one check: `PartTreeFileTests.Every_committed_file_reads_below_version_7_and_writes_byte_for_byte("samples/rover.carve")` asserts that every `samples/*.carve` is below version 7 (T-92's guard, written when no sample had a part tree). The rover is the first committed version 7 file. I did not touch it: changing it loosens a check.
- **Question for the person:** recommended: have `CommittedFiles()` skip `samples/rover.carve`, and add a test of its own that the sample reads as version 7 with `HasPartTree`, and writes byte for byte; the check for every other sample stays as is. Alternatives: a second list of "version 7 samples" in the same theory with the opposite asserts, or keeping the rover out of `samples/` (against the spec).
- **Left:** that change, `cairn task T-97 verify` to green, the viewer frame review (two frames copied to `G:\My Drive\artifacts\carve\M-9\T-97\round-1\`, `viewer-front.png` and `viewer-angle.png`, both already opened once by the first agent: the rover reads as olive chassis, pale sand cab, dark blue-grey wheels with light-grey bosses, a small yellow lens on the mast's lamp, the cab to the right of the mast; the mast and lamp body are white-grey, since the spec paints only the lens; nothing clipped; the viewer's sky tints the olive and sand paler than the swatches, but the hues are plainly there, so the "colours cannot be shown" rule does not trigger), As built, `finish`.
- **Surprising:** `view.json` reports `materials` as 12 entries for this file (6 materials; the split load makes one per glTF mesh and the viewer's merged load lists per submesh); the tests do not rely on that count. The viewer front view is `1.2,0.5,2.2` and not the materials barrel's `0,0.5,2.5`, because the lens faces +X and is edge-on from straight ahead (a choice, not in the spec).
