---
description: >-
  bake writes a curvature map for wear, grey where flat, lighter toward convex edges and darker toward concave ones over a given width
---
# T-126 — Curvature

**Status:** In Progress

**Pri:** P1

**Milestone:** M-24

**Batch:** — (standalone)

**Depends on:** T-123

**Verify:** core-unit, server-unit, scenario-bakes

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

### As built

By Claude Sonnet 5.5 (claude-sonnet-5-5): agent 1 Sonnet 5.5, agent 2 (this one) Sonnet 5.5.

`Curvature.cs` (`Bake.Curvature`), the `bake` map `curvature` and block `curvature: {width}` with its errors and description paragraph,
`CurvatureStage.cs` (wrench at 1024, width 0.003; the `uvs` frame with the map under it and its 4x jaw crop), unit tests in
`BakeCurvatureTests` and `BakeToolTests`, and `scenario-bakes` on the Verify line.

**The person's rulings (2026-11-20), landed:**
- Convex jaw edge: a texel reads 218 or more (above 128 by at least 90), on all 120 convex jaw edges, the 71 degree ones included
  (probed least 227, 254 on the rest).
- Concave: the stage builds a small L-block (0.1 m arms, extruded 0.1 m) through `define_mesh` and `project_uvs` box (an `unwrap`
  needs seams on a closed block), bakes `curvature`, takes its own `uvs` frame and a 4x crop at the inner edge, and checks a texel
  just inside each of the two inner faces (4 and 5) reads below 128. The block is undone afterwards (two `undo`s, asserted gone), so
  later stages see the model as before. The wrench has no concave edge (its least value is exactly 128).
- Concave margin, the person's answer in the Waystone review (picked from 90, 64, 110): 64, so the texel reads at most 64. Probed
  reads 4 and 14 (49 at the third texel).
- Blend: only the curvature `uvs` frames (the wrench's and the L-block's) pass `blend: 2` to `CheckUvImage`; every other frame's check is
  unchanged.
- Handle's flat middle: exactly 128.

**Breaks** (`CARVE_BAKES_BREAK=curvature`, each value read as 255 minus it, which is `s` inverted): fails 123 checks (120 convex, the
flat middle 127, both L-block texels 251 and 241 against at most 64). Clean run passes. Probe note: box islands abut with no gap, so the
concave texel is half a texel inside the face, not the one on the UV edge (which landed in a neighbouring island's convex rim).

**Frames:** Waystone round 1 (wrench `uvs`, jaw 4x, L-block `uvs`, L-block inner 4x): approved, no notes. The L-block changed no wrench
frame or bake (hash compared with a run without it). Evidence: `G:\My Drivertifacts\carve\M-24\T-126ound-1`.

**Not checked:** the margin's slack on other block sizes or widths; the L-block frame by eye beyond a scaled look.
