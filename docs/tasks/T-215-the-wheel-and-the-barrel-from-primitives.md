---
description: >-
  samples/wheel.carve and samples/barrel-turned.carve built from a cylinder by rotate, delete, extrude, mirror and a symmetric scale, seen in scenario-shapes and kept equal by content to what it builds
---
# T-215 — The wheel and the barrel from primitives

**Status:** Open

**Pri:** P1

**Milestone:** M-10

**Batch:** — (standalone)

**Depends on:** T-209, T-210, T-211, T-212, T-213

**Verify:** core-unit, server-unit

## Goal

The milestone's content: a wheel and a barrel built almost entirely from a primitive and the new edits, through the
tools, seen, saved and kept as samples.

## Scope

- **The agreed sketch (2026-11-25), as built through the tools in `SamplesStage.cs`** (one line in `ShapesStages.cs`):
  - **Wheel** (mesh `Wheel`, axis X, radius 0.35, width 0.12, 16 sides):
    1. `primitive cylinder` radius 0.35, height 0.06, segments 16: 32 v, 18 f.
    2. `rotate` 90° about z, pivot the origin (exact, T-210).
    3. `move` every vertex +0.03 along x, so the half spans x 0..0.06.
    4. `delete` the cap at x = 0: 32 v, 17 f.
    5. `extrude` the outer cap with inset 0.22 and distance 0.04, the hub boss: 48 v, 33 f.
    6. `mirror` axis x at 0, welding the 16 vertices on the plane: 80 v, 66 f, closed.
    7. `unwrap`.
  - **Barrel** (mesh `Barrel`, radius 0.36, height 1, 16 sides, 4 bands):
    1. `primitive cylinder` rings 4: 80 v, 66 f.
    2. `scale` ring 2 by `[1.17, 1, 1.17]`, the bulge.
    3. `scale` ring 1 by `[1.06, 1, 1.06]` with `symmetric: "y"`, so ring 3 follows.
    4. `unwrap`.
  - Both end with `check` silent and closed.
- **Work it first in Core:** a Core test (`tests/Carve.Core.Tests/ShapeSamplesTests.cs`) builds both through the Core
  APIs and holds the counts above. If a count is off, bring it to the person before going on.
- **Samples:** `samples/wheel.carve` and `samples/barrel-turned.carve`, written by `CarveFile.Write`.
  - The Core test keeps them equal by content to what it builds.
  - The scenario saves both into its run folder and compares them with the samples copied beside it, as the Uvs
    scenario does. It reopens them and checks that the frames are byte-identical.
- **Frames:**
  - The wheel from front (+Z), side (+X) and three-quarter, the wireframe with the mirrored faces highlighted, and the
    checker look after the unwrap.
  - The barrel from front, top and three-quarter, the wireframe with rings 1 and 3 highlighted, and the checker look.
  - A six-view sheet of both.
  - Every frame has a positive pixel check and guards from T-213's shared rules. The coloured faces (one per sample at
    a known +X+Z place) make a mirrored camera fail.
- **A symmetric frame pair:** the barrel before and after step 3, with the bulge measured in pixels on both sides.
- **Breaks on the scenario side:** `CARVE_SHAPES_BREAK` gains `asym` (the barrel predicted with ring 3 unscaled) and
  `hub` (the wheel predicted unmirrored). Each fails at its own check.
- **`README.md`**: both samples in the samples list, and the wheel's seven calls as a worked sequence run as written,
  since the scenario runs them.
- **`CLAUDE.md`** layout table: the `Carve.Scenario.Shapes` row (T-213 adds it; this task finishes the sample part)
  and the two samples in the `samples/` row.

## Notes

- Add `scenario-shapes` (created by T-213) to this file's `**Verify:**` line in the first commit, once T-213 has landed: lint refuses a suite that does not exist yet.

- **The sketch and its counts were agreed by the person at planning** (2026-11-25). A change to a step or a number goes
  back to the person.
- If `check` warns on either sample after its unwrap, bring the warning back; do not change the steps to silence it.
- Placeholder content: until this task lands, each tool's worked examples (T-209 to T-212) are the content the
  mechanic arrives with. This task has to come after every mechanic it uses.
- Look work: plan a separate frame-review agent.
