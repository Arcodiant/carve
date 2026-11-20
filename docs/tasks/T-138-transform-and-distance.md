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

### Hand-off

Written by Claude Opus 5.5 (`claude-opus-5-5`), the first agent, at its call checkpoint (about 60 calls). **Status: a
stop is open (two times over 10 s); the person has not yet seen the floor tiles.**

**Done (commits `f6a4f00`, `bb67d46`):**
- `Nodes/TransformNode.cs`: `in` (any); `offset` [u, v] -1..1 (0, 0), `tile` [x, y] 1..16 (1, 1), `mirror`
  none|u|v|both, `rotation` -360..360 degrees clockwise as seen (0), `scale` 0.25..4 (1), `filter` bilinear|nearest.
  Inverse map in `double` (offset, mirror, turn, scale about the centre, then times tile; reads wrap); quarter turns
  use exact sine and cosine, other angles `ShapeNode.SinCos`. `InputRect` is the bounding box of the four corners
  mapped back, margin 2. Ranges for offset and rotation are mine (the spec gives none).
- *Scale's meaning:* scale is the image's size (0.5 halves it, so it shows twice). Under it 1/n tiles and a whole
  number above 1 does not. No meaning makes both tile, though the spec says "a whole number or its reciprocal". The
  description says 1 or 1/n. This is for the person to confirm.
- `Nodes/DistanceNode.cs`: `in` (grey mask, inside at 0.5 or more); `range` 0..0.25 (0.05); `side`
  inside|outside|both. The distance runs from a texel's centre to the nearest texel centre on the other side, divided
  by the range in texels and clamped to 1. The window is the output widened by ceil(range x size) + 1. Pass 1 runs
  along the window's rows, reading outward from the tile only until a target is met. Pass 2 is the Felzenszwalb
  envelope down each tile column. The values are exact integers until the square root, so every tile size gives the
  same bytes (tested at 16, 32 and whole against a brute-force copy).
- The registry lines; `NodeCatalogue` lines for both; `GraphFaultTests`' type list; define_graph worked example
  `WorkedTurn` (stripes turned 30 degrees, which the example says do not tile) with a `WorkedExampleTests` test; and
  CLAUDE.md's Layout lines.
- `TransformDistanceTests` (16, all pass) cover the identity bit for bit (grey and colour), a whole-texel offset as a
  shift, tile 2 as a 2x2 mean, scale 0.5 as tile 2 moved a quarter, the quarter turns, mirror, the step order, 30
  degrees against the mapping at every texel, nearest, tile-size invariance, and the input rectangle. A turned 64-tile
  covers 9 or fewer tiles, and tile 16 at scale 0.25 asks for the whole image. For distance they cover hand-worked
  texels of a square, across the wrap, and brute-force equality. The table of 9 broken copies is: angle sign, step
  order, clamped reads, bilinear about corners, mirror axes, sums not squares, no second pass, no wrap, sides swapped.
- `samples/graphs/floor-tiles.texgraph` (256, seed 138) with its five PNGs. It is `tiles` 4x4, gap 0.06, bevel 0. The
  bevel is `distance` inside at range 0.035, put through `levels` gamma 0.7 into `height`. A `shape` triangle in each
  cell is turned 90 by `transform` and mixed in by a checker made from two `gradient`s. The colours are a glaze
  #e2d9c6 and ink #2d4c78 on grout #8c857a with a noise grain. Roughness is 0.12 on the glaze and 0.9 in the grout.
  `FloorTilesGraphTests` (all pass) keeps the PNGs equal to the graph and checks the turn on alternating tiles exactly.
  The check figures: base colour's brightest channel is 115..231, with 0% outside 20..245. The worst normal error is
  0.0034. Distinct values: base 392, roughness 47, occlusion 7, height 10. The seam step across the wrap, u and v, is
  at most 0.67 against interior steps of 1.58..6.31.
- `TransformDistanceBoundedMemoryTests` (regression, real patina, peak at 2 GB or less, time written to
  `%TEMP%\patina-t138-8192-*.txt`): it builds, but has **not been run**.
- patina-unit: Patina.Graphs.Tests 253 pass and Patina.Server.Tests unit 108 pass. `cairn task T-138 verify` has
  **not** been run yet.

**8192 figures** (Release, in-process `GraphEvaluator`, tiles of 128, 64 MiB cap, AMD 5800X3D; harness in
`%TEMP%\t138-measure`, log `%TEMP%\t138-8192.log`):
- The colour fbm source alone (8x8 cells, 4 octaves, toned): 16.8 s, 16,384 tiles evaluated.
- That source turned 30 degrees: 46.7 s, 39,478 tiles evaluated (the source recomputed about 2.9 times), peak held
  64 MiB, working set 380 MB.
- Distance, inside, range 0.1, of a 4x4 tiles mask: **473.8 s**, 288,138 tiles evaluated, **peak held 84 MiB** (over
  the 64 MiB cap: the 196 pinned window tiles of each output tile), working set 331 MB.
- Still running when this was written, in the same log: distance `both`, scale 0.25, tile 4 at scale 0.25, and tile
  16 at scale 0.25 (stopped at 300 s with an estimate).

**Open stop, for the person (do not choose):** both times are over the spec's 10 s.
- *Transform:* the source alone is 16.8 s, so the 10 s cannot hold for any colour fbm. The turn's own cost is the
  source recomputed under the cap. The options:
  - read the 10 s as transform's own cost over its source (about 30 s here, mostly recomputation);
  - use a larger cap;
  - accept the cost as T-135's warp was accepted.
- *Distance:* one node's window is (128 + 2 x 820)^2 texels per output tile, so its cost per texel grows with
  (range / tile)^2. Inside a single node on T-133's API this cannot be avoided. The options:
  - split it into two passes the evaluator caches: a row-distance node, then a column node (a new type or an evaluator
    change, which may touch T-153's `Evaluation/`);
  - a lower range bound (for example 0.02, which is the floor's 0.035 at 256 scaled down only roughly);
  - a per-node tile size or a larger cap;
  - accept the cost.
  The pinned window also passes the 64 MiB cap (84 MiB). That is the evaluator's pinning, not a leak, but the person
  should hear it.
- T-153's data-dependent request would not help either node: both know their rectangles statically.

**Left:**
- Get the person's ruling on the stop.
- Run the review: the PNGs and the swatch are in `G:\My Drive\artifacts\carve\M-28\T-138\round-1` (the five PNGs,
  `swatch-sheet.png`, `swatch-sphere.png`, made by driving patina's real `swatch` with `%TEMP%\t138-swatch.py`).
- Run the regression class.
- Run `cairn task T-138 verify`.
- Write As built, then run `cairn lint` and `finish`.

**Surprising:** `tools/Patina.SampleGraphs -- --canonical` rewrites every committed `.texgraph` with LF endings. The
bytes are otherwise the same. I restored the other four with `git checkout --`; a later run must do the same.
**Checked by eye:** one half-size copy of the swatch sheet (1 image of 12). It looks right: cream tiles with blue
triangles turning between up and right, a bevel, and dark grout.
