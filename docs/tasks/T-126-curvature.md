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

### Hand-off

By Claude Sonnet 5.5 (claude-sonnet-5-5), agent 1. Stopped on the thresholds: nothing below is landed as ruled.

**Done and committed.** `Curvature.cs` (`Bake.Curvature(layout, width)`, the formula as written, unpadded; the tool pads);
`bake` has map `curvature` (`<mesh>.curvature.png`), block `curvature: {width}` (required when the map is asked, above 0,
each error named) and its paragraph in the description; a call with no `maps` still bakes `masks` and `base_colour`
(`BakeTool.DefaultMaps`). `CurvatureStage.cs` bakes at 1024, width 0.003, takes the `uvs` frame with the map under it
(`wrench-uvs-curvature`) and the 4x crop on the jaw's islands (`wrench-uvs-curvature-jaw-4x`), and checks the convex jaw
edges (all 120 of them) and the handle's flat middle. Tests: `BakeCurvatureTests` (box, L-block, 45 degrees, corner clamp,
determinism, each width error) and two in `BakeToolTests`. `scenario-bakes` is on the Verify line and the CLAUDE.md row has
its clause. `tests.json` needed no new input (the suite's dotnet closures hold the new files). Break `CARVE_BAKES_BREAK=curvature`
(scenario side: reads each value as 255 minus it, which is `s` inverted) fails with 121 checks (120 convex, the flat middle 127).
Clean run passes. Evidence, outside the worktree: `G:\My Drive\artifacts\carve\M-24\T-126\probe` (`curvature.tsv`, the frame, the
crop, the PNG, `core-probe.txt`). No image has been looked at by the person.

**The wrench has no concave break edge at all.** Probed in Core at 1024, width 0.003, and from the geometry: of 212 hard
edges and every edge bending over 1 degree, all are convex (hard: 65 to 115 degrees; unhard roundings 22 to 30). Over every
texel the least value is exactly 128 (no group has one below). So the spec's check "a texel in the jaw's inside corner is
below 128" cannot hold on this model; I did not invent a corner.

**Probed numbers (PNG values, 0 to 255).**

| What | Probe |
|---|---|
| Texel at the middle of a convex jaw edge's UV edge, 90 degree edges (72) | 254 to 255 |
| the same, 109 degree edges (24) | 254 to 255 |
| the same, 71 degree edges (24) | 227 to 228 |
| Whole jaw (`dark`) | 128 to 255; 39842 of 105559 texels above 128 |
| Concave texels on the whole wrench | none (min 128) |
| Handle (`rubber`) largest face middle, 3 faces | exactly 128, no edge within 3 mm |
| Seam UV edges (not a break; texel 0.5 mm or more from an edge) | 128 to 130 |

The stage's convex check is a provisional margin of 90 (reads 218 or more), not ruled.

**For the person (each option names the one check it applies to).**

1. *Convex jaw edge, above 128 by:* (a) 90, reads at least 218, about the margin the probe's least 99 leaves (provisional now);
   (b) 60, reads at least 188, looser than the 71 degree edges need; (c) 120, reads at least 248, fails the 71 degree edges
   (227), so only with those edges left out.
2. *Inside corner, below 128 (no such corner on the wrench):* (a) drop this check on the wrench and rely on the unit tests
   (the L-block's inner edge reads 0, an inverted `s` fails them); (b) the stage `define_mesh`es a small L-block, bakes it and
   checks a texel in its inner corner is below 128 by a margin (to be probed; reads 0 now), with the same break; (c) both (b)
   and (a) off the wrench.
3. *Flat middle of the handle's face:* exactly 128, as the spec says, no margin to rule.

**Left.** The person's rulings on 1 and 2; landing them (the margin constant, the concave check if (b), its break shown
failing); the Waystone review of the frames (`wrench-uvs-curvature`, the crop; `mcp__waystone__review`, files under
`G:\My Drive\artifacts\carve\M-24\T-126\round-1`); `cairn task T-126 verify`; `### As built`; `cairn lint`; `finish`.

**Surprising.** (1) `CheckUvImage` (T-123's, additive change, existing calls unaffected: `worked` is optional and a new
`blend` argument) held the curvature frame to exact nearest texels and 74 pixels on steep ramps failed: at 1024 a texel is
under a frame pixel (0.875), and on a ramp the pixel shows a value between its neighbours. The curvature frame uses
`blend: 2` (the value must lie within 2 of the least and most of the texels within 2 of the sampled one). That is looser than
T-121's rule on ramps only. (2) A file written by a Bash heredoc with an embedded apostrophe failed to parse; the files were
written with Write.
