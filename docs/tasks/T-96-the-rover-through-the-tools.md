---
description: >-
  scenario-parts builds the rover through the tools at two levels, turns its mast, edits an instance and shows each in frames worked from the geometry
---
# T-96 — The rover through the tools

**Status:** Open

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** T-92, T-93, T-94, T-95

**Verify:** core-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

The agreed rover is built through the product's own tools, blockout first and then refined, and is seen in the frames agreed at planning. Each frame has pixel checks worked from the geometry.

## Scope

- **`scenarios/Carve.Scenario.Parts`**, a new scenario. It links `RoverSpec` from `tests/Carve.Core.Tests` (T-91), so no number is typed twice, and builds the rover through MCP calls only:
  - `define_part` for the tree, the empties and the instances;
  - `define_mesh` for the level 0 boxes and the 8-sided wheel, then `set_colour`, then `keep_level {level: 0}`;
  - the refinement: the wheel's 16-sided `define_mesh` and its hub boss with `extrude`, and the cab's windscreen and lamp's lens with `extrude` (inset, then recess), each coloured;
  - `keep_level {level: 1, parts: [Cab, Lamp, Wheel_FL]}`.
- **Stages and frames (512 px unless noted), named as agreed:**
  1. `blockout-front`: level 0, view front;
  2. `blockout-sheet`: level 0, views +x, +z, +y;
  3. `front`: live;
  4. `back-left`: direction [−1, 0.6, −1];
  5. `top`: +y, orthographic;
  6. `mast-turned`: after `define_part {part: Mast, rotation: [0, 0.7071068, 0, 0.7071068]}`, front, with the lamp now at −Z;
  7. `part-axle-front`: `part: Axle_F`, direction [1, 0.3, 0], with the boss outward on both wheels;
  8. `part-wheel-labels`: `part: Wheel_FL`, `labels: faces`, 1024;
  9. `part-wheel-L0`: `part: Wheel_FL`, level 0;
  10. `reopened-front`: after `save` and `open`, byte-equal to frame 3;
  11. `instance-edit`: after one `move` on `Wheel_FL`'s hub boss, all four wheels changed, then `undo`.
- **`check` and `get_parts` stages:** the rover is silent. `Axle_F` lowered 0.3 m gives exactly T-91's envelope warning, and the scenario puts it back. An edit aimed at `Wheel_FR` is refused with the instance text.
- **Every frame's checks** are worked from `get_mesh` and `get_parts`, and from `RoverGeometry`'s counts and bounds, with the shared rules T-94 names (`ColourRule`, the Cuts ray test on the world-placed geometry, `SeenNarrow`, `Drawn`, in-sight). Each frame has one check that a mirrored or wrong-up camera fails, worked from the framing's bounds. Write a `CARVE_PARTS_BREAK` switch (`mirror`, `no-instance-rotation`, `levels-ignored`, `part-not-isolated`) that makes the scenario wrong on purpose, and show it failing on each, as the materials scenario does.
- **Suite `scenario-parts`** (`regression`) in `tests.json`, the project in `Carve.slnx`, and a Layout row in `CLAUDE.md`. Once the suite exists, add it to the `**Verify:**` lines of T-96, T-97 and T-98 (left out at planning, since `cairn lint` refuses a suite `tests.json` does not have yet).

## Notes

- **Frames fill an agent's context fastest:** Sonnet for the build and checks, with the frame review as a separate agent. That agent opens every frame itself, cropped or scaled, before it asks the person (M-7 lesson). Waystone's "Commented" is not an approval until the person says approved.
- **Bound by the agreed sketch** in the milestone file. If a frame cannot show what its line says (for example a boss hidden at the agreed direction), stop and ask; do not move the camera or the model.
- **Open rules: stop and ask.** A new shared check rule; any loosened threshold; any frame that differs from the sketch.
- **Write down for dependents (As built):** the run folder layout and the save path, for T-97.
- **Carried:** every existing frame byte-identical by hash; each new check shown failing on a broken product, as a table; evidence at the absolute path the dispatch names, outside `artifacts/`; `finish` run inside the worktree, its output read whole and its `Merge task/T-96` line given; report a `verify` that waits more than a few minutes.
