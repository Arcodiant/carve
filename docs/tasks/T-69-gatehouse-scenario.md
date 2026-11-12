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

First agent (Sonnet, claude-sonnet-5-5), stopped at a checkpoint and on one STOP (below). Nothing is finished: no `verify`, no `finish`, no review, no breaks.

**Done and committed**

- `tests/Carve.Core.Tests/GatehouseGeometry.cs` (the first commit, `b34f287`): the seven stages through Core, with counts, tombstones, ids and the closed result, plus the ray test of every probe (the scenario's own `Camera`, `Solid` and `GatehousePlan`, linked into the test project; `tests.json` `core-unit` lists the linked files). 28 tests pass. Ids as built: slab faces 0..5; loop_cut splits 0->[0,6,7] 1->[1,8,9] 4->[4,10,11] 5->[5,12,13]; cornice cap 8, walls 14..17; merges delete [6,7] [10,11] [12,13]; plane cuts create faces 22 then 26, 27 and delete 3 (the top, in the second cut: the first leaves a sliver of it); door face 28; window inner faces 29 (front), 30 (back); reveal faces 31..35; deleted vertices 2, 3, 6, 7, 20, 22. Final: 36 live vertices, 19 live faces, closed, `check` clean.
- `scenarios/Carve.Scenario.Gatehouse/` (project in `Carve.slnx`): `GatehouseScenario.cs` (the build through the tools, ids read from results and `get_mesh`), `GatehouseFrames.cs` (all frames and checks), `GatehousePlan.cs` (shared probes, the expected sure counts and faces per stage and view). Frames: 14 shaded (two directions, seven stages), 3 wireframes (stages 2, 4, 6), the orthographic window frame, the reveal-label frame, the six-view sheet. Checks reuse only the existing rules (Cuts ray test, `SeenNarrow`, the Crate colour rule at `Exact` for the door, `Checks.Line`/`NotLine`, T-68's label and sheet checks). The plane cuts use the point (0, 3.4, 0) for both (the same planes as the sketch's two points give).
- Local run `scripts/run-scenario.ps1 gatehouse` (artifacts in the worktree's `artifacts/scenarios/gatehouse/2`, not yet copied to the review folder): every check passes but one.

**STOP: a planned check that cannot pass on the agreed geometry.** `gate-2-cornice-wire` checks that each of the stage's new edges "in sight" (on a face turned toward the camera, T-67's rule, `GatehousePlan.InSight`) is drawn orange at 0.2..0.8 of its length. Edge 12-14 (the front at y = 2.0, the line where the cornice's underside meets the front) is on the front face, which faces the camera, but the cornice's body (y 2.0..2.2, out to z = 0.23) hides it from `[0.6, 0.35, 1]`, which looks down on it. The wireframe is depth-tested, so it is not drawn: the pixel is the fill (85, 87, 89). So the planned count of 11 new edges in sight at stage 2 (`GatehouseGeometry`, `TheWireframesHighlightTheStagesNewEdges`, and `Wires` in `GatehouseFrames.cs`) includes one edge nothing can show. Stages 4 and 6 pass (5 and 5). Options for the person: (a) in-sight means "the ray test shows the edge's middle as one of its two faces" (10 at stage 2; a new rule, since T-67's only looks at facing); (b) leave the underside edge out by declaring the count 10 with a stated reason; (c) another direction for the stage-2 wireframe. I did not change the check or the count.

**Left to do**

1. The person's ruling on the STOP; then fix `Wires`, the Core test and the check.
2. `tests.json`: suite `scenario-gatehouse` (GPU, like `scenario-cuts`, with `GatehouseGeometry`'s linked files as inputs), and run `cairn lint`.
3. Kept and shipped: `save` to `samples/gatehouse.carve`, a `unit` test keeping it equal by content to the build (as `gardener.carve`, see `SubdivideToolTests`), `open` and compare ids, positions and tombstones; `export` to a content folder in the run folder and read back through the engine's loader as `scenario-export` does (`scripts/run-export-scenario.ps1`, `ExportScenario.cs`): live triangles only, the door's material present. None of this is started.
4. `CLAUDE.md` Layout: the scenario and the sample.
5. Breaks: each new check shown failing on a broken product (table in As built). Candidates: mirror the camera (the `Camera` or the render's), a plane_cut that keeps both sides, `delete` that does not tombstone, `bridge` that is not run, a door not coloured, the label overlay off, the sheet tile order swapped. None run yet.
6. Evidence and hashes: GPU run on a near-final commit, copy frames and `all.sha256` to `D:\Repos\carve\branches\milestone\M-5\artifacts\review\T-69\`, compare against `...\T-67\frames-8481a22\all.sha256` (every existing frame byte-identical, listed), `cairn task T-69 verify` until clean, review of the gatehouse frames through `mcp__waystone__review`, `### As built`, `finish`.

**Checked / surprising**

- The merge of an end's three pieces first picked four faces: the cornice's end wall lies in the end's plane too. Selection now excludes faces with z above 0.15. (Not a tool fault.)
- The top face is not cut away whole by the first plane cut: it leaves a sliver; the second cut deletes it, so `deleted.faces` of the second cut holds face 3. The scenario asserts it in either cut's `deleted`.
- The top face (and the cornice's thin ends) are too thin on screen for the ray test to be sure of; they are asked about only where `SeenNarrow` is sure.
- The Core test copies no camera maths: it links the scenario's own `ShedGeometry.cs`, `CutsGeometry.cs` and `GatehousePlan.cs`, and references `Carve.Debug`.
- Images opened so far: 0. Tool calls so far: about 52.

**Second agent (Sonnet, claude-sonnet-5-5): STOP on the ruling, nothing else started.** The first agent's local frames are copied to `D:epos\carveranches\milestone\M-5rtifactseview\T-69\local-run-1rames`. The in-sight helper (`GatehousePlan.InSight`) is local to the Gatehouse scenario; Cuts has its own inline facing test, so nothing is shared and Cuts is untouched. I tried the ruling literally (the pixel of the edge's middle, `Camera.Pixel`, first hit by `Solid.FirstHit`, must be one of the edge's two faces), and it does not give 10 at stage 2 from the front: it gives 8 (from the back, 6; not ruled). Beside 12-14 (hidden under the cornice, as ruled), two more edges fail on pixel rounding at a silhouette or a coplanar neighbour: 14-16 (bottom of the cornice's +x end wall; the middle pixel hits end face 1, coplanar below) and 18-19 (the cap's -x edge; the pixel is background). Both are drawn in the wireframe (they are on the outline), so the Line check would pass on them. Taking any hit in the 3x3 pixels round the middle gives exactly 10 from the front (and 7 from the back), but that is a new tolerance of my own design, so I made no change (worktree reverted to the committed state `5e21535`). The person to choose: (a) the 3x3 neighbourhood rule (front 10, back 7 at stage 2; stages 4 and 6 not yet counted under it); (b) declare the count 8 front / 6 back with 12-14, 14-16, 18-19 named; (c) another rule. Tool calls so far for this agent: 13; images opened: 0.
