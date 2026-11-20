---
description: >-
  transform (offset, tile, mirror, rotation, scale) and distance-from-an-edge nodes on the tiled evaluator, and samples/graphs/floor-tiles.texgraph with its PNGs
---
# T-138 — Transform and distance

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-133, T-134, T-136

**Verify:** patina-unit, patina-regression

## Goal

Two filters that read far from the texel they write: `transform`, which moves, repeats, mirrors, turns and scales an
image, and `distance`, which says how far each texel is from the edge of a mask. They come with a floor of bevelled
tiles.

## Scope

- **`transform`**: input `in` (greyscale or colour); `offset` `[u, v]` (fractions of the image, wrapping), `tile`
  `[x, y]` (whole repeats, 1 to 16), `mirror` `none`, `u`, `v` or `both`, `rotation` (degrees) and `scale` (0.25 to 4),
  about the image centre, applied in that order: tile, scale, rotate, mirror, offset. Sampling: bilinear, wrapping, with
  `filter` `bilinear` or `nearest`. Its input rectangle is the bounding box of the output's, mapped back through the
  transform, through T-133's API, so a turned 64-square tile reads at most the few source tiles under it.
  - A rotation that is not a quarter turn, or a scale that is not a whole number or its reciprocal, generally does not
    tile. That is the agent's choice; check's seam rule (T-141) reports it. The node's description says so, with the
    angles that keep tiling (quarter turns).
- **`distance`**: input `in` (greyscale, a mask: inside at 0.5 or more), `range` 0..0.25 of the image width; output, per
  texel, its distance to the mask's edge as a fraction of `range`, clamped to 1, measured `inside`, `outside` or
  `both` (`side`). Exact Euclidean (a separable two-pass transform, for example Felzenszwalb's) within the window that
  `range` widens, wrapping, so it tiles and its cost does not grow with the square of the range.
- **Tests (unit), worked by hand**: `transform` with the identity equal to its input bit for bit; an offset of a whole
  texel equal to a shift; `tile` 2 equal to the input at half size repeated (within an interpolation step); a quarter
  turn equal to a transposed and flipped image; a 30-degree turn compared point by point against the mapping; mirror;
  `distance` of a single square measured at chosen texels against the Euclidean formula; both across the wrap; a table of
  broken copies (the angle's sign, the order of operations, distance in the wrong pass, no wrap) and what caught each.
- **Memory and speed**: a 30-degree turn of a colour `fbm` at 8192 evaluated in a `regression` test (beside T-115's in
  `ExportBoundedMemoryTests`, or its own class) with the peak working set at 2 GB or less; `distance` at range 0.1 at
  8192 timed. Write the times here. More than 10 s for either is a stop.
- **The floor tiles**, `samples/graphs/floor-tiles.texgraph`, 256: square ceramic tiles (`tiles`, 4 x 4, no stagger)
  with a `distance`-shaped bevel and grout, a `shape` motif in each tile turned by `transform` a quarter turn on
  alternating tiles, glaze roughness low and grout rough, and outputs `base_colour`, `normal`, `roughness`, `occlusion`
  and `height`. Its PNGs from `tools/Patina.SampleGraphs`, with a test keeping them equal. Work its numbers against
  check's rules in a test before the PNGs are shown. The person looks at the PNGs and a swatch sheet before it lands.
- `define_graph`'s node list names both types; one worked example uses `transform` with a rotation and says it does not
  tile; the description tests run it as written.

## Notes

- **Decided at planning (2026-11-19, by the person):** after the explanation of the band evaluator, the person chose
  tiled evaluation (T-133) and a free `transform` on it: offset, tile, mirror, rotation and scale. Floor tiles are this
  task's content.
- check's rules (T-141 builds them; the sample must pass): base colour by brightest channel in 20..245 for 99% or more of
  texels; normals unit within 0.02; no greyscale output of one value; no seam across the wrap.
- Opus: the first nodes that read far from a texel on T-133's API, whose far-reading cases this task proves out. If the
  API cannot say what `transform` needs, change it here and record the change for M-29; that is a stop if a committed
  output's bytes would move.
- **Stop and ask:** a parameter beyond those above, a time over 10 s, a peak over 2 GB, or a sample that cannot meet
  check's rules.
- Runs beside T-137 and T-139: its own node files, registry lines, sample and tests. A conflict that only adds is settled
  by keeping both sides.
