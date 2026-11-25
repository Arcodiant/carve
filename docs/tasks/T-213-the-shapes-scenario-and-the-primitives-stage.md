---
description: >-
  scenario-shapes: a new scenario whose first stage shows the four primitives from several sides and in the wireframe and UV looks, every pixel worked from the geometry
---
# T-213 — The shapes scenario and the primitives stage

**Status:** Open

**Pri:** P1

**Milestone:** M-10

**Batch:** — (standalone)

**Depends on:** T-209

**Verify:** core-unit, server-unit

## Goal

The primitives, seen: a box, a cylinder, a cone and a sphere built through `primitive`, rendered from several sides and
in the wireframe, checker and UV-layout looks. Every frame is checked pixel by pixel against the geometry, in a new
scenario that later stages join.

## Scope

- **A new scenario project, `scenarios/Carve.Scenario.Shapes`**, in `Carve.slnx`, with suite `scenario-shapes`
  (`regression`, run by `scripts/run-scenario.ps1 shapes -NoBuild`) in `tests.json`.
  - Its stages go one class to a file, in the order of `ShapesStages.cs`. A later task adds its file and one line
    there, as `CutsStages.cs` does.
  - It links rather than copies: the Cuts scenario's ray test (`CutsGeometry.cs`, `CutsChecks.cs`), the shed's camera
    and checks, the Crate's `ColourRule.cs`, `CameraPixels.cs`, `CrateChecker.cs`, `CheckerColours.cs`,
    `CrateUvLayout.cs` and `UvLayoutPixels.cs`, and the Uvs scenario's `UvsChecker.cs`.
  - Add `scenario-shapes` to this file's `**Verify:**` line in the commit that creates it.
- **`PrimitivesStage.cs`**:
  - The four kinds in a row along +X, 1.5 m apart, each with its own `centre` and name: Box size `[1, 0.6, 0.8]`,
    Cylinder segments 12 rings 2, Cone segments 10, Sphere segments 16 rings 8. Each has **one face coloured** (with
    `set_colour`) at a known +X+Z place, so a mirrored or upside-down camera fails.
  - The frames are front, back-left three-quarter, top and the six-view sheet, the wireframe from the front with
    labels on the cylinder, the checker look zoomed on the cylinder and on the sphere, and the `uvs` layout look of
    the cylinder and the box.
  - Each frame has a positive pixel check and guard points worked from the framing's bounds. The shaded pixels are
    worked by the ray test and `ColourRule`, and the checker and layout pixels by the linked rules.
  - The stage also checks `get_mesh` counts and the id order T-209 wrote down, `check` clean, and the islands per kind
    (6, 3, 2, 1).
- **Breaks on the scenario side only** (no switch in product code): `CARVE_SHAPES_BREAK` with `mirror` (the expected
  camera flipped), `segments` (the cone predicted at 9), `uv` (the cylinder's islands predicted turned) and `colour`
  (the coloured face moved one k). Each must fail at its own check. Report the table of breaks and the check that
  caught each.
- **Work the numbers first**, in a Core test (`tests/Carve.Core.Tests/ShapesGeometry.cs`, linking the scenario's plan
  file as `GatehouseGeometry` does): counts, every probe point and every guard. Bring any miss back to the person in
  one question before writing the frame checks.
- `CLAUDE.md` layout table: a `scenarios/Carve.Scenario.Shapes` row (its stages, linked files and suite).
- Size the stage against run-scenario's 300 s guard. If it nears that, split the suite.
- `README.md`: the new scenario and its suite in the run conditions, if the README lists suites; otherwise say in
  `### As built` that nothing needed changing.

## Notes

- **The project's owner:** this task creates the project, `ShapesStages.cs` and the suite. T-214 and T-215 each add a
  stage file and one line. A clash that only adds is settled by keeping both sides.
- Look work fills context fastest. Plan two agents, the second reading the frames (it opens every frame itself), as a
  separate review from the start.
- Name the shared check rules you use (`ColourRule`, `SeenNarrow`, anti-aliasing at edges) in `### As built`. T-214 and
  T-215 use the same ones and invent none. `ColourRule` reads dark albedo too dark (B-8), so pick mid-tone colours.
- Keep evidence at an absolute path outside the worktree's `artifacts/`. Fetch a queued run's frames by job id.
- Thresholds and margins are the person's: bring measured figures with a recommendation; do not set them.
