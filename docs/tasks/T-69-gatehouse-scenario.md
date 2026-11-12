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
