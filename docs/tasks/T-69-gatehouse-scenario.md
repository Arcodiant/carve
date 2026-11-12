---
description: >-
  The gatehouse wall built by cuts alone: cornice, gable, door and a see-through window, shown at each stage and kept as samples/gatehouse.carve
---
# T-69 — Gatehouse scenario

**Status:** In Progress

**Pri:** P1

**Milestone:** M-5

**Batch:** — (standalone)

**Depends on:** T-66, T-67, T-68

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

M-5's sample: a gatehouse wall that only exists because it was cut. It is built through the tools from a plain slab, seen at every stage, saved, reopened and exported.

## Scope

**The build**, the milestone's agreed sketch, through the real tools in `scenarios/Carve.Scenario.Gatehouse`, with suite `scenario-gatehouse`. Each stage reads the ids it needs from the previous results, never typed constants.

1. `define_mesh`: the slab, x -1.5..1.5, y 0..4, z -0.15..0.15.
2. `loop_cut` on a vertical edge at fractions 0.5 and 0.55 (y = 2.0 and 2.2). Then `extrude` the front band face out by 0.08: the cornice.
3. `merge_faces` three times: the back's three pieces, and each end's three pieces.
4. `plane_cut` through (1.5, 2.6, 0) and (0, 3.4, 0), normal (0.8, 1.5, 0) normalised, keeping `below` with fill. Then its mirror, normal (-0.8, 1.5, 0). The top face is cut away whole and shows in `deleted`.
5. `knife`, open, on the front (z = 0.15): (-1.1, 0) → (-1.1, 1.6) → (-0.4, 1.6) → (-0.4, 0). Then `set_colour` the door face brown, `#6b4423`.
6. `knife`, closed, on the front (z = 0.15) and then the back (z = -0.15): (0.3, 0.8), (1.1, 0.8), (1.1, 1.5), (0.7, 1.75), (0.3, 1.5).
7. `delete` both window faces, then `bridge` the front and back hole loops (five reveal quads). Then `check`: closed, with no warnings.

**Work every count and check from the geometry first.** The first commit is a Core `unit` test, `GatehouseGeometry`, that runs the same steps through `Carve.Core` and asserts:

- the counts, the tombstones and the closed result at each stage;
- the expected ids;
- a ray test for every pixel the scenario will check.

It is written before any frame. A planned check that cannot pass on the agreed geometry is a stop, not a change.

**Frames.**

- After every stage: from `[0.6, 0.35, 1]` and from `[-0.6, 0.35, -1]`, shaded.
- After stages 2, 4 and 6: in the wireframe with that stage's new edges highlighted.
- After stage 7: orthographic `[0, 0, 1]` (the background seen through the window), a labelled frame of the window's reveal faces, and a six-view sheet.

Every frame has a positive pixel check from the ray test, at corners as well as middles. Each check is shown failing on a break (the table in As built). Asymmetric checks are included that a mirrored camera would fail. Two clean runs give byte-identical frames.

**Kept and shipped.**

- The result is saved as `samples/gatehouse.carve` through `save`, kept equal by content to the build by a `unit` test, as `gardener.carve` is.
- It is reopened with `open`, and compared to the build in ids and positions, tombstones included.
- It is exported to a content folder in the run folder and read back through the engine's loader in the suite's checks, as `scenario-export`'s are: live triangles only, the door's material present.
- `CLAUDE.md`'s Layout gains the scenario and the sample.

## Notes

- **Decided by the person at planning (2026-11-09):** the gatehouse as sketched, its coordinates, and its three views.
- The sketch could not be worked against the geometry at planning, because the tools do not exist yet. `GatehouseGeometry` is that check, and it comes first. M-4 lost eleven stops to spec checks that were a hand calculation away.
- **Open rules: stop and ask.**
  - Any change to a coordinate, a stage or a view.
  - A check that cannot pass on the agreed geometry.
  - A tool behaviour the build needs that the tool's task did not give.
- Every existing frame stays byte-identical by hash, listed.
- The person approves the frames (`mcp__waystone__review`, kind `approve`) before `finish`. Nothing has sound or motion, so there is no listening or real-time pass.
- This task is frames and checks, kept apart from the tools' code on purpose. Sonnet, with the stop list above. A defect found in a tool is a bug filed under M-5, not a fix here.
- Evidence goes to the milestone worktree's `artifacts/review/T-69/` by absolute path.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built by four Sonnet agents (claude-sonnet-5-5), the last one finishing it. The earlier hand-offs are folded in here.

**The rulings (the person, 2026-11-12).** `in sight` for the wireframe highlight is geometric: `GatehousePlan.InSight(solid, camera, toEye, orthographic)` casts a ray from the eye to the edge's middle, nudged 1e-3 of the bounds diagonal toward a face's centroid (in its plane); the first hit must be one of the edge's two faces. New edges in sight from the front 10 / 5 / 5 at stages 2 / 4 / 6 (the planned 10 at stage 2 matches; 12-14 under the cornice is not in sight); from the back 7 / 5 / 5. Hidden at stage 2 (front): 8-10, 8-12, 9-11, 9-13, 12-19; (back): 10-14, 11-15, 12-14, 13-15, 14-16, 15-17, 16-17, 16-19, 17-18. Stage 4 front hidden 24-25, 24-26 (back 21-23, 23-27); stage 6 front hidden 37-38, 37-41, 38-39, 39-40, 40-41 (back 32-33, 32-36, 33-34, 34-35, 35-36).

**What was built.** Core `unit` test `GatehouseGeometry` (counts, tombstones, closed, ids and a ray test for every checked pixel, written before any frame); project `scenarios/Carve.Scenario.Gatehouse` with suite `scenario-gatehouse` (seven stages through the real tools; 2 frames per stage, 3 wireframes at 2/4/6, and at stage 7 the orthographic window, the labelled reveals, the six-view sheet: 26 frames); `scripts/run-gatehouse-scenario.ps1`; `tests/Carve.GatehouseLoad.Tests` (the export read back through `IModelGeometryLoader`: live triangles only, the door's material present, export clean); save, an unrelated open, reopen, `get_mesh` text equal to the build's including tombstones, save again byte-equal, export; `samples/gatehouse.carve` (version 4, kept equal by content to the Core build by `TheSampleIsTheGatehouseBuiltInCoreByContent`; the plane_cut normals use the scenario's doubles 0.8/1.7 and 1.5/1.7); `CLAUDE.md` Layout rows. Folded findings: the end-merge picks four faces unless the cornice walls (z over 0.15) are excluded; the top face leaves a sliver after the first plane cut and is deleted by the second; the top face and the cornice's thin ends are asked about only where `SeenNarrow` is sure. No tool code changed; no bug reserved.

**Breaks** (each alone, build with warnings as errors, scenario run, restored; script `C:/Users/andre/AppData/Local/Temp/t69/breaks.py`, log `artifacts/review/T-69/breaks.log` in the milestone worktree). All caught.

| Check | Break | Result |
|---|---|---|
| shaded frames' ray-test pixels (mirrored camera) | B1 eye and target x negated in `ViewFraming` | caught, 404 failures |
| orthographic window frame and sheet captions | B2 orthographic drawn in perspective | caught, 18 failures |
| six-view sheet tile order | B3 tile columns reversed | caught, 4 failures |
| reveal labels | B4 labels not drawn | caught, 4 failures |
| wireframe highlight (in-sight Line) | B5 highlight drops edges from vertex 18 | caught, 1 failure |
| door colour | B6 colour not applied in the draw | caught, 35 failures |
| plane_cut counts | B7 `keep=below` keeps both | caught, 34 failures |
| door material through the loader | B8 export ignores face colours | caught by `The_doors_material_is_present_...` |
| in-sight rule (Core) | plain facing test in place of the ray | `TheWireframesHighlightTheStagesNewEdges` (11, not 10) and `TheEdgeUnderTheCorniceIsNotInSightFromTheFront` fail |
| delete tombstones | B9 `MeshDelete` deletes nothing | caught, 9 failures (bridge refused, 10 problems) |
| bridge is run | B10 the bridge call replaced by `check` | caught, 12 failures (the scenario throws KeyNotFound reading the bridge summary, so by exception not by a count) |
| save/open keeps tombstones | B11 save writes deleted vertices as live (0,0,0) | caught, 2 failures (reopened `get_mesh` differs; export warns) |
| save/open keeps colours | B12 save drops face colours | caught, 1 failure (reopened `get_mesh` differs) |

Not broken separately: the NotLine check on old edges (the highlight counts and B5 bite on the same drawing).

**GPU run.** `cairn task T-69 verify` on commit 4764a90: `VERIFY PASS`, ran 12, carried 0, failed 0. `scenario-cube` and `scenario-crate` are not queued by verify, so they were submitted separately on 4764a90 (both PASS); every suite's `output.log` names 4764a904b. Evidence: `artifacts/review/T-69/frames-4764a90/` (frames per suite, `all.sha256`, 163 frames), local run in `local-run-3/`.

**Existing frames:** all 137 in the T-67 baseline `frames-8481a22/all.sha256` (character 17, crate 44, cube 32, cuts 16, export 7, shed 10, soft 11) are byte-identical in the GPU run (diff of the sorted hash lists empty). The 26 gatehouse frames are byte-identical between the local run 3 and the GPU run (two runs); so are `gatehouse.carve` (and `samples/gatehouse.carve`) and `gatehouse.glb`.

**Review:** the person approved the 20 gatehouse frames (stages 1-7, front/back, three wireframes, ortho window, reveal labels, sheet) through `mcp__waystone__review` (kind approve), on the GPU run's frames. The six per-axis 256 px tiles (`gate-7-axis-*`) are the sheet's tiles and were not sent separately.
