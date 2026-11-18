---
description: >-
  bake writes a curvature map for wear, grey where flat, lighter toward convex edges and darker toward concave ones over a given width
---
# T-126 — Curvature

**Status:** Open

**Pri:** P1

**Milestone:** M-24

**Batch:** — (standalone)

**Depends on:** T-123

**Verify:** core-unit, server-unit

## Goal

`bake` writes a curvature map that marks where a hard-surface model wears: light on its convex edges and dark in its
creases. M-29 layers wear and grime under it. In M-24 it is seen on the wrench's layout.

## Scope

- **Core, `src/Carve.Core/Baking/Curvature.cs`:**
  - the value starts at 0.5;
  - for each break edge within `width` from T-120's `NearEdges`, it adds `0.5 · s · min(θ / (π/2), 1) · w`:
    - `s` is +1 for a convex edge and −1 for a concave one;
    - `θ` is the dihedral bend angle;
    - `w = smoothstep(width, 0, d)`;
  - the edges are taken in a fixed order and the sum is clamped to 0..1;
  - a right-angle convex edge reads 1 on the edge, and a right-angle crease reads 0;
  - flat texels away from edges read exactly 0.5, which is 128;
  - the image is one channel, grey, padded by T-120's `Pad`.
- **`bake`:** a new map `curvature` (`<mesh>.curvature.png`) and a new block `curvature: {width}`. `width` is in
  metres, greater than 0, and required when `curvature` is asked for. The description gains a paragraph: what the map
  shows, and that a material does not take it directly. It is for wear masks in a texture graph (M-29) or a tool of
  the agent's own.
- **Stage `CurvatureStage.cs`** in the Bakes scenario:
  - bake `curvature` with width 0.003 at 1024;
  - **frames:** the `uvs` look with the curvature under it, whole and cropped at 4x on the jaw's islands;
  - **checks**, each with a break:
    - in the PNG, a texel on a convex jaw edge is above 128 and a texel in the jaw's inside corner is below 128, by
      probed margins;
    - the middle of the handle's flat face is exactly 128;
    - break: `s` inverted;
  - the numbers are probed on the wrench and brought to the person before landing.
- **Tests** (`unit`):
  - a flat box's edge reads 1 at the edge and 0.5 at `width`;
  - an L-block's inner edge reads 0;
  - a 45° edge reads 0.75;
  - a corner near two edges is clamped;
  - two bakes byte-identical;
  - each error.

## Notes

- Decided at planning (2026-11-18), by the person: curvature is edge-based, grey where flat, lighter toward convex and
  darker toward concave edges, falling off over `width` and scaled by the dihedral angle.
- Chosen by the orchestrator, each a stop if it proves wrong: the scale `θ / (π/2)`, under which a right angle is full
  scale; summing several edges and clamping.
- **Stop and ask** before landing: a definition other than the above, or any threshold.
- Sonnet should do: T-120's `NearEdges` gives everything the formula needs. The stage is small, two frames.
- Runs beside T-124 and T-125. All three add a map and a block to `bake`, a paragraph to its description, a stage file
  and a line in `BakesStages.cs`; keep both sides. It reads T-120's `NearEdges` as it stands. A change to it is a stop,
  since T-125 relies on it.
- The person looks at the curvature frames before this lands (a Waystone review). Treat "Commented" as unanswered.
- **Verify line:** once scenario-bakes (T-123) is in `tests.json`, add it to this file's `**Verify:**` line, with the
  task's other suites.
