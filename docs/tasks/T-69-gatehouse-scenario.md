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

### Hand-off

Third agent (Sonnet, claude-sonnet-5-5), at about 60 tool calls, 0 images opened. The first two agents' hand-offs are folded in below; the STOPs are ruled.

**Ruling applied (the person, 2026-11-12), in sight.** `GatehousePlan.InSight(solid, camera, toEye, orthographic)` is the geometric rule: a ray from the eye to the edge's middle nudged 1e-3 of the bounds diagonal toward a face's centroid (in its plane), first hit is that face, for either of the edge's two faces. Worked in `GatehouseGeometry.TheWireframesHighlightTheStagesNewEdges`: new edges in sight from the front 10 / 5 / 5 at stages 2 / 4 / 6 (planned 10 at stage 2: it matches; 12-14 is not in sight, test `TheEdgeUnderTheCorniceIsNotInSightFromTheFront`; the others hidden at stage 2: 8-10, 8-12, 9-11, 9-13, 12-19); from the back 7 / 5 / 5 (worked, no plan; stage 2 hidden: 10-14, 11-15, 12-14, 13-15, 14-16, 15-17, 16-17, 16-19, 17-18). Stage 4 front hidden 24-25, 24-26 (back 21-23, 23-27); stage 6 front hidden 37-38, 37-41, 38-39, 39-40, 40-41 (back 32-33, 32-36, 33-34, 34-35, 35-36). `Wires` in `GatehouseFrames.cs` is now `[2] = (16, 10)`.

**Done and committed** (commits after `114ae4e`): the ruling above; suite `scenario-gatehouse` in `tests.json` (and `samples/gatehouse.carve` in `core-unit`'s inputs); `scripts/run-gatehouse-scenario.ps1`; new project `tests/Carve.GatehouseLoad.Tests` (in `Carve.slnx`; sample equal by content to the run's saved file, live triangles only read back through `IModelGeometryLoader.Load`, the door's material present, export clean); the scenario's `SaveOpenExportAsync` (save new, something else opened over it, open, `get_mesh` text equal to the build's incl. tombstones, closed, save again replaced with equal bytes, export to `game/Content/models/gatehouse.glb`); `samples/gatehouse.carve` (copied from local run 3's `work/`); Core unit test `TheSampleIsTheGatehouseBuiltInCoreByContent` (the plane_cut normals in `GatehouseGeometry` now use the scenario's doubles `0.8/1.7`, `1.5/1.7`, since a pre-normalised float differs by 1e-8 in the cut's x); `CLAUDE.md` Layout rows; `cairn lint` clean. A local `scripts/run-gatehouse-scenario.ps1` run passes, with all checks and the 4 load checks (artifacts `artifacts/scenarios/gatehouse/3`, not yet the review folder).

**Breaks done** (`C:/Users/andre/AppData/Local/Temp/t69/breaks.py`, each alone, build with warnings as errors, scenario run, restored; log in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-69/breaks.log`). All caught:

| Check | Break | Result |
|---|---|---|
| shaded frames' ray-test pixels (mirrored camera) | B1 eye and target x negated in `ViewFraming` | caught, 404 failures (gate-1-slab-front corner 1 of face 2 is background, expected the mesh) |
| orthographic window frame and sheet captions | B2 orthographic drawn in perspective | caught, 18 failures (caption lacks "orthographic") |
| six-view sheet tile order | B3 tile columns reversed | caught, 4 failures (tile +x at (121, 20) is background, expected the +x view's pixel) |
| reveal labels | B4 labels not drawn (when no tile caption) | caught, 4 failures (no white pixel in reveal face 31's label box) |
| wireframe highlight (in-sight Line) | B5 highlight drops edges from vertex 18 | caught, 1 failure (the new edge 18-19, closest pixel to orange is the fill) |
| door colour (Crate colour rule at Exact) | B6 colour not applied in the draw | caught, 35 failures |
| plane_cut counts | B7 `keep=below` keeps both | caught, 34 failures (vertices expected 22, got 24) |
| door material read back through the loader | B8 export ignores face colours | caught by `The_doors_material_is_present_...` |
| in-sight rule (Core) | plain facing test in place of the ray | `TheWireframesHighlightTheStagesNewEdges` fails (11, not 10) and `TheEdgeUnderTheCorniceIsNotInSightFromTheFront` |

Not broken: delete not tombstoning, bridge not run (their counts and the closed `check` are asserted by `Summary` / `Equal` and by `GatehouseGeometry`; B7 shows the counts bite), the NotLine on old edges, and the save/open comparison. Do one or two of these if time allows (a scenario edit that skips `bridge`; `save` dropping the colours, for the sample/open check), or state them as not broken.

**Left to do**

1. Commit, then submit the GPU suites once on the near-final commit: `cairn task T-69 verify` (it queues `scenario-gatehouse`, `core-unit`, etc.). Follow with `cairn test status`. Take `cairn test artifacts scenario-gatehouse`, check `output.log` names the commit, copy `frames` and a `all.sha256` to `D:epos\carveranches\milestone\M-5rtifactseview\T-69rames-<commit>\`, and compare to the T-67 baseline `...\T-67rames-8481a22ll.sha256`: every existing frame byte-identical (list). Run twice to show byte-identical frames (the verify run plus one more, or a local run 3 vs 4 hash).
2. Review of the gatehouse frames through `mcp__waystone__review` (kind `approve`; at most 32 files; real frames only, not a broken one). Local frames of run 1 are in `...\T-69\local-run-1rames`; use the GPU run's.
3. `### As built` (Sonnet, claude-sonnet-5-5; the person's ruling recorded; every count from the above and the stage 4/6 hidden edges; the break table above; the folded hand-offs: the end-merge picks four faces unless cornice walls (z over 0.15) are excluded, the top face leaves a sliver after the first cut and is deleted by the second, the top face and the cornice's thin ends are asked about only where `SeenNarrow` is sure), remove this Hand-off section, `verify` until `VERIFY PASS`, `cairn lint`, commit, `finish` from the worktree.

**Surprising:** `export` and `check` work as in the character scenario; the sample is a version 4 file (holes and tombstones). The in-sight rule needed the scenario's camera `Eye`, which `Camera` already exposes (nothing in Shed changed).
