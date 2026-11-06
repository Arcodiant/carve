---
description: >-
  The export scenario saves, reopens and exports the crate into a game content folder, and the viewer shows it in its own suite
---
# T-19 — Export scenario

**Status:** Done

**Pri:** P1

**Milestone:** M-3

**Batch:** — (standalone)

**Depends on:** T-17, T-18

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The milestone's sample is proved end to end in one suite. Through the real tools, the crate is saved, reopened unchanged, and exported into a game's content folder. Then the viewer loads that `.glb` by content path through `IModelLoader` and shows it as a game would.

## Scope

**`scenarios/Carve.Scenario.Export`** (new, laid out as `CLAUDE.md`'s *Writing a scenario* says). The crate's call sequence is shared with `Carve.Scenario.Crate` by linking its source or a shared file, not by copying it. The run folder holds `work/` for the `.carve` file and `game/Content/models/` for the `.glb`. If a scenario cannot learn its run folder, widen the debug extension (for example a `RunFolder` on `ScenarioDriver`) rather than reaching into the product.

1. Build the crate, asserting counts after every call as `CrateScenario` does. Capture `front`: the native frame.
2. `save` to `work/crate.carve`. The result names the path, 72 vertices and 70 faces.
3. `define_mesh` the example cube, then `open` `work/crate.carve`. The report matches the crate's (closed, no warnings), and `get_mesh` equals the one before the save, field for field. Capture `front` again; it is **byte-identical** to step 1's.
4. `undo` returns the cube (`open` is one step). `open` again.
5. `save` to the same path again; the result says `replaced`. The file's bytes are identical to step 2's.
6. `export` to `game/Content/models/crate.glb`. The result names the path, the triangle count, 1 material `carve`, and size `1.04 x 0.8 x 1 m`. Export again to the same path: a tool error naming `overwrite`, with the file unchanged. Export with `overwrite: true`: `replaced`, and the bytes are identical.
7. **Error paths**, each a tool error after which the next `front` render is byte-identical to the last:
   - `open` `work/crates.carve`: the error names the path and lists `crate.carve`;
   - `save` to `work/crate.glb`: wrong extension;
   - `export` to `game/Content/models/crate.carve`: wrong extension.

**The suite `scenario-export`** (`regression`, `gpu`, `vulkan`) in `tests.json`. Its command runs the scenario, then T-18's `run-viewer.ps1` on the run's `game/Content` with `models/crate.glb` and the two agreed views, `front=1.6,1.2,2.2` and `minus-x=-2.2,1.2,1.6`. It fails if either fails. The viewer's output goes in the same run folder, under `viewer/`.

**Viewer checks** (a positive pixel check on every frame, as M-1's retrospective requires), in a small check step the suite runs after the viewer: in the scenario if it can read the viewer's output, otherwise in a script or test that T-19 adds.

- `view.json`: the triangle count equals the export result's; one material with base colour `(0.72, 0.72, 0.70, 1)`, metallic 0, roughness 1; bounds `(-0.02, 0, 0)` to `(1.02, 0.8, 1)`.
- `front.png`:
  - corners are background;
  - the top, +Z and +X frame faces differ by at least 10/255;
  - the +X bar's top differs from the +X panel behind it by at least 10/255;
  - a pixel on the +Z panel differs from the +Z frame by at least 10/255 (the recess shows).
- `minus-x.png`:
  - corners are background;
  - the -X bar stands out from the -X panel by at least 10/255;
  - the -X and +Z faces differ by at least 10/255.
- **A check that a wrong camera would fail.** The crate is symmetric about x = 0.5, so a pixel check in one view can pass on its mirror image. At least one check per view must fail if the view were mirrored or the up flipped. Say which, and show it failing.

Pick the sample pixels from the first good run, record them as constants with a comment saying why each was chosen, and **show each check failing** on a broken product. Breaks to use:

- the export written with metallic 1;
- with no normals;
- with the winding reversed;
- the +X bar extrude skipped;
- `open` that drops the last face;
- the viewer given the wrong view.

Give the table.

**Wiring**:

- `Carve.slnx` lists the project.
- `CLAUDE.md`'s *Layout* gains the `Export` row.

**Second run**: run the suite twice and compare all frames, `view.json` and the `.glb` byte for byte. Record it.

**Show the person** the viewer's `front.png` and `minus-x.png` (`mcp__waystone__review`, kind `approve`) beside Carve's native `front` and the M-2 crate's `(-2.2, 1.2, 1.6)` frame, and say how the game look differs from Carve's look. Their approval is the gate.

## Notes

- **Decided by the person at planning:**
  - the crate is the sample;
  - the two viewer views;
  - the viewer's look;
  - a run folder standing in for a game's content folder;
  - the wrong-file-name error.
- **Open rules: stop and ask.**
  - Any change to the crate, its sequence, the views or the viewer's light.
  - Any tolerance looser than 10/255.
  - Any golden or cross-machine comparison.
  - Changing a tool's behaviour to make a step pass.
- **A fixer may not change** the crate, the views, the light, the framing or the material to make a check pass. If a frame is not what was agreed, stop and report.
- This task is the run and its frames, split from the code on purpose. A defect found in a tool is a bug filed under M-3, not a fix here, unless it is a one-line fix in this task's own files.
- Its frames fill context. Look at them together at the end.
- Nothing has sound or motion, so there is no listening or real-time pass.
- Run `finish` from inside the worktree and leave the folder.

### As built

Run on **Claude Sonnet 5.5** (`claude-sonnet-5-5`).

**Files.** `scenarios/Carve.Scenario.Export/` (`ExportScenario.cs`, csproj). The crate's call sequence moved out of `CrateScenario.cs` into `scenarios/Carve.Scenario.Crate/CrateBuild.cs` (an internal static class: `BuildAsync`, the call and count helpers, `CheckCorners`, `CheckFront`), which `CrateScenario` uses and the Export project links, so there is one source. `ScenarioDriver.RunFolder` already existed, so the debug extension did not change. `scripts/run-export-scenario.ps1` runs the scenario, then `run-viewer.ps1` on `<run>/game/Content` with `models/crate.glb` and `front=1.6,1.2,2.2`, `minus-x=-2.2,1.2,1.6` (output in `<run>/viewer/`), then the xunit checks in `tests/Carve.ExportViewer.Tests` (`CARVE_EXPORT_RUN` names the run). Suite `scenario-export` in `tests.json`; `Carve.slnx` and `CLAUDE.md` Layout updated. The scenario writes the export result to `<run>/export-result.txt` for the check step (view.json triangles must equal it).

**One deviation from the spec.** The bullet "a pixel on the +Z panel differs from the +Z frame by 10/255" cannot hold: both are +Z faces, so under one fixed sun with no shadows they are the same shade (191). The recess shows as its wall between them (170, facing +X). The check samples that wall against the +Z frame, and a second check requires five points across the +Z panel to be one flat shade (spread at most 2). No tolerance, view, light or crate changed.

**Pixels.** Constants in `Checks` (`ExportViewerTests.cs`), each commented with why, picked from the first good run: front top (95,150) 207, +Z frame (72,300) 191, +X (306,330) 171, recess wall (91,300) 170, bar top (350,290) 206, +X panel behind (350,240) 171; minus-x -X panel (150,250) 125, +Z (350,300) 191, bar top (210,300) 206, panel above (210,250) 125, lid (250,160) 207. Background is 0.

**Mirror/up-flip.** Directional checks: front needs top > +Z > +X by 10 (left sample +Z, right +X); minus-x needs +Z (right) above -X (left) by 10, and the lid above +Z. `A_mirrored_or_up_flipped_view_would_fail...` runs the same checks on the mirror and the upside-down copy of each real frame and requires a finding in each.

**Checks and the break that failed each** (each break applied to the product or scenario and reverted; run through the real suite):

| Break | What failed |
|---|---|
| Export with metallic 1 | `View_json...`, both frame tests (+Z panel not flat) |
| No normals (`VertexPosition` only) | front frame test: +Z 126 not above +X, +Z panel not flat (200..). minus-x still passes |
| Winding reversed (B and C swapped) | both frame tests (top 125, faces not apart) |
| +X bar extrude skipped (undone after the build, expected counts and sizes adjusted to the smaller crate) | `View_json...` (bounds) and front frame test (bar top equals panel) |
| `open` drops the last face | the scenario, at `open`: expected 72 vertices, 70 faces, closed; got 69 faces, not closed |
| Viewer given `front=-1.6,1.2,2.2` (mirrored) | front frame test (+Z sample is background, order wrong) |
| Viewer given `front=1.6,-1.2,2.2` (from below) | front frame test (top not above +Z, bar, flatness) |

Zero normals (a variant I also ran) failed `View_json...` and both frame tests. The scenario also asserts, with its own checks, the save/open/export wording, byte-identical `.carve` and `.glb` on a second write, `get_mesh` equal after `open`, `undo` naming `open`, each error path followed by a byte-identical `front`, and `CheckFront` on every native front frame (corners are background, the centre is the mesh, the top, +Z and +X faces in order at their points, and the +X bar's top differing from the panel behind it).

**Second run.** The suite ran twice (`artifacts/scenarios/export/1` and `/2`): every frame (5 native, 2 viewer), `view.json`, `crate.glb` and `crate.carve` have identical SHA-256. Native `front` (bf8ecf04...), viewer `front` (fa9ca6a9...), `minus-x` (ff9ca6d9...). Copies are under `D:\Repos\carve\branches\milestone\M-3\artifacts\review\T-19\runs\`.

**Review.** The person approved, with no notes, the viewer `front.png` and `minus-x.png` beside Carve's native `front` and the M-2 crate `(-2.2,1.2,1.6)` frame (`...\review\T-19\round-1\`). They were told: the viewer's background is pure black, Carve's dark slate; the viewer's sun is fixed in the world, so shades differ (top 207, +Z 191, +X 171, -X 125); the viewer flat-shades from the file's normals. I opened `front.png` and `minus-x.png` from the viewer only; the others were shown through the review.

**Not checked.** No window run; no other machine's frames compared (no golden). `minus-x` is not hit by the no-normals break.
