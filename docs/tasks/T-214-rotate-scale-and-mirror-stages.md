---
description: >-
  scenario-shapes gains a stage for rotate and scale on an L-block about a tilted axis and one for mirror with its new faces highlighted, every pixel worked from the geometry
---
# T-214 — Rotate, scale and mirror stages

**Status:** Open

**Pri:** P1

**Milestone:** M-10

**Batch:** — (standalone)

**Depends on:** T-210, T-211, T-213

**Verify:** core-unit, server-unit

## Goal

`rotate`, `scale` and `mirror` seen on the product: an asymmetric block turned about a tilted axis and scaled about a
corner, and a half mirrored into a whole, each from more than one side, each pixel worked from the geometry.

## Scope

- **`TurnStage.cs`** (one line added to `ShapesStages.cs`):
  - The L-block is a `primitive box`, with one end face extruded and one face coloured, so nothing in it is
    symmetric.
  - Steps: `rotate` its arm about `[1, 1, 0]` by 30° about a pivot at the elbow, then `scale` the whole block by
    `[1, 0.5, 2]` about a corner, then `undo` twice. The undone frame is byte-equal to the first frame.
  - Frames are taken before, after each step, from the front and the back-right three-quarter, and in the wireframe
    with the moved vertices highlighted.
  - Refusals are called and their text checked: a zero axis, a factor ≤ 0 naming mirror, and a fold.
- **`MirrorStage.cs`**:
  - The subject is half of an asymmetric shape: a box primitive at x 0..1, with its x = 0 face deleted, a face
    extruded on the +z side and one face coloured. It is mirrored with `axis x`.
  - Frames are taken before and after, from the front, top and three-quarter. The wireframe has the created faces
    highlighted and the welded seam on show, labelled.
  - The checks: `check` closed, with the flipped-UV warning present as T-211 said, then gone after `unwrap`. The
    refusals are also called: a face in the plane, and a weighted mesh.
- **Every frame has a positive pixel check and guard points** a mirrored camera fails. Use T-213's shared rules and
  linked files only; a rule T-213 did not name goes back to the person.
- **Breaks on the scenario side:** `CARVE_SHAPES_BREAK` gains `turn-sign` (predicted at −30°), `pivot` (predicted
  about the origin), `mirror-order` (the created ids predicted in descending order) and `weld` (predicted unwelded).
  Each fails at its own check, and the table is reported.
- Work the probe points first in `ShapesGeometry` (T-213's Core test).
- `README.md`: nothing expected. Say so in `### As built`.

## Notes

- Add `scenario-shapes` (created by T-213) to this file's `**Verify:**` line in the first commit, once T-213 has landed: lint refuses a suite that does not exist yet.

- Look work: plan a separate frame-review agent. Keep the stages within the suite's time guard, and split the suite if
  the two stages push it past 300 s (ask first).
- Do not touch `PrimitivesStage.cs` or T-213's baselines. Ask before re-taking any frame.
- Not design-heavy: it checks what T-210 and T-211 decided.
