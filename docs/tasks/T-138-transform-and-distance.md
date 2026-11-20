---
description: >-
  transform (offset, tile, mirror, rotation, scale) and distance-from-an-edge nodes on the tiled evaluator, and samples/graphs/floor-tiles.texgraph with its PNGs
---
# T-138 — Transform and distance

**Status:** Done

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
- **Rulings on the stop (2026-11-20, by the person):**
  - *Distance's speed and pinned window:* a separable two-pass evaluation, in M-28, as a new member, T-155
    (separable passes and rotated-read locality). T-155 depends on T-138, makes `distance` opt in, and brings its
    pinned window under the cap. T-138 lands without meeting the 10 s.
  - *Transform's speed:* fix the source recomputed under a free rotation, rather than accept it or bound the overhead.
    This is also T-155's, in the evaluator. T-138 lands without meeting the 10 s.
  - Both 10 s checks move to T-155. T-138 does not change `Evaluation/`, which T-155 owns. The regression test's peak
    working set of 2 GB or less still holds here. A time over 10 s is recorded, not asserted; T-155 asserts it.
  - *Scale* stays as built. Scale is the image's size, so 1/n gives n repeats and tiles, and a whole number above 1
    zooms in and does not tile. The description says so.
- **The floor tiles approved (2026-11-20, by the person, Waystone round-1):** the five PNGs, the swatch sheet and the
  sphere. The person's note: "Let's show the swatch on a sphere and a cube, with 3x3 tiling". That is a change to
  `swatch`, not T-138's. T-156 (landed) adds a sphere tiling option; a cube is in no member. T-138 builds neither.
- **Rulings on the second hand-off (2026-11-20, by the person):**
  - *The loosened floor-tiles test is accepted:* the alternate-tile check asks for more than a third of the motif's
    ink to move, with the exact per-texel checks unchanged.
  - *Transform's strong minify* (tile 16, scale 0.25, at 8192, a working set of 4,348 MB) goes to T-155, which brings
    it under 2 GB with a regression test. T-138 lands with the figure recorded in As built.
  - *The distance memory test runs at 2048, not 8192, until T-155,* with a deadline that suits 2048 in place of the
    120-minute guard. T-155 raises it to 8192 and asserts the 10 s. The 8192 figures stay in As built.
  - *The cube view* asked for on the floor tiles' approval is a new member, T-157. T-138 builds nothing for it.

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`) in three agents, all on that model: the first wrote the nodes, the sample
and the tests up to its call checkpoint; the second rebased onto T-153, took the person's rulings and the review, ran
the regression class and `verify`, and at `finish` rebased by hand onto T-137, T-139 and T-156, keeping both sides of
the registry, catalogue, `DefineGraphTool`, `GraphFaultTests` and CLAUDE.md Layout lines; the third rebased onto the
milestone again (T-157 planned), made the person's second rulings, moved the distance memory test to 2048, ran
`verify` and finished. Both hand-offs are folded in here. **Status: done, without the spec's 10 s for either node,
which the person moved to T-155 (see the Notes).**

**Nodes:**
- `Nodes/TransformNode.cs`: `in` (any); `offset` [u, v] -1..1 (0, 0), `tile` [x, y] 1..16 (1, 1), `mirror`
  none|u|v|both, `rotation` -360..360 degrees clockwise as seen (0), `scale` 0.25..4 (1), `filter` bilinear|nearest.
  The ranges for offset and rotation are mine (the spec gives none). It is the inverse map in `double`: offset, mirror,
  turn, scale about the centre, then times tile, with wrapping reads. Quarter turns use an exact sine and cosine; other
  angles use `ShapeNode.SinCos`. `InputRect` is the bounding box of the four corners mapped back, margin 2. Scale is the
  image's size, as the person ruled: 1/n gives n repeats and tiles, and a whole number above 1 zooms in and does not.
  `NodeCatalogue`'s line and the class comment say so.
- `Nodes/DistanceNode.cs`: `in` (grey mask, inside at 0.5 or more); `range` 0..0.25 (0.05); `side`
  inside|outside|both. The distance runs from a texel's centre to the nearest texel centre on the other side, divided
  by the range in texels and clamped to 1. The window is the output widened by ceil(range x size) + 1. Pass 1 runs
  along the window's rows, reading outward from the tile only until a target is met. Pass 2 is the Felzenszwalb
  envelope down each tile column. The values are exact integers until the square root, so every tile size gives the
  same bytes (tested at 16, 32 and whole against a brute-force copy). Both passes are in the one node; T-155 splits
  them so the evaluator caches the first.
- The registry lines; `NodeCatalogue` lines for both; `GraphFaultTests`' type list; the define_graph worked example
  `WorkedTurn` (stripes turned 30 degrees, which the example says do not tile) with a `WorkedExampleTests` test; and
  CLAUDE.md's Layout lines. `Evaluation/` is unchanged; T-133's API said all both nodes need, so there is no API change
  to record for M-29.

**Tests:**
- `TransformDistanceTests` (16) cover these for transform: the identity bit for bit (grey and colour), a whole-texel
  offset as a shift, tile 2 as a 2x2 mean, scale 0.5 as tile 2 moved a quarter, the quarter turns, mirror, the step
  order, 30 degrees against the mapping at every texel, nearest, tile-size invariance, and the input rectangle. A
  turned 64-tile covers 9 or fewer tiles, and tile 16 at scale 0.25 asks for the whole image. For distance they cover
  hand-worked texels of a square, the wrap, and equality with a brute-force copy. The table of 9 broken copies and what
  caught each: angle sign, step order, clamped reads, bilinear about corners, mirror axes, sums not squares, no second
  pass, no wrap, sides swapped.
- `FloorTilesGraphTests` keeps the PNGs equal to the graph and checks the turn on alternating tiles exactly. *Changed
  in the second agent:* after the rebase, its last line failed. That line says the turn is visible, meaning more than
  a twentieth of the tile differs between a tile and its turned neighbour. The motif has 348 ink texels, and 186 of
  them move, which is under a twentieth of the tile (204). It now asks for more than a third of the motif's ink to move,
  a measure of the ink rather than the tile. The exact checks above it (every texel turned, the diagonal the same) are
  unchanged. The person accepted this (see the Notes).
- `TransformDistanceBoundedMemoryTests` (regression, the real patina, debug build) asserts the peak working set at
  2 GB or less and records the time; the 10 s is T-155's to assert. The turn exports at 8192. Distance exports at 2048,
  as the person ruled: at 8192 it took about an hour in a debug patina, past the 10-minute deadline (cancelled at
  10 m 4 s), and a 120-minute guard made `patina-regression` take half an hour. The deadline is 10 minutes again;
  T-155 raises distance to 8192.

**The floor tiles,** `samples/graphs/floor-tiles.texgraph` (256, seed 138), with its five PNGs. It is `tiles` 4x4,
gap 0.06, bevel 0. The bevel is `distance` inside at range 0.035, put through `levels` gamma 0.7 into `height`. A
`shape` triangle in each cell is turned 90 by `transform` and mixed in by a checker made from two `gradient`s. The
colours are a glaze #e2d9c6 and ink #2d4c78 on grout #8c857a, with a noise grain. Roughness is 0.12 on the glaze and 0.9
in the grout. The check figures: base colour's brightest channel is 115..231, with 0% outside 20..245. The worst normal
error is 0.0034. Distinct values: base 392, roughness 47, occlusion 7, height 10. The seam step across the wrap, u and
v, is at most 0.67, against interior steps of 1.58..6.31. The person approved the five PNGs, the swatch sheet and the
sphere in Waystone round-1 (`G:\My Drive\artifacts\carve\M-28\T-138\round-1`, made by driving patina's real
`swatch` with `%TEMP%\t138-swatch.py`; the PNGs hashed equal to the committed ones after the rebase). The note to show
the swatch on a sphere and a cube, tiled 3x3, is in the Notes and is not built here.

**8192 figures** (AMD 5800X3D, tiles of 128, 64 MiB cap):
- *In process, Release* (`GraphEvaluator`; harness `%TEMP%\t138-measure`, log `%TEMP%\t138-8192.log`):
  - The colour fbm source alone (8x8 cells, 4 octaves, toned): 16.8 s, 16,384 tiles evaluated, working set 372 MB.
  - Turned 30 degrees: 46.7 s, 39,478 tiles evaluated (the source recomputed about 2.9 times), peak held 64 MiB,
    working set 380 MB.
  - Scale 0.25: 278.5 s, 300,556 tiles, working set 381 MB. Tile 4 at scale 0.25 was stopped at 300 s with 336 of 4,096
    output tiles, about 61 minutes in all, working set 668 MB. Tile 16 at scale 0.25 was stopped at 305.5 s with 24 of
    4,096, about 869 minutes in all, working set 4,348 MB. That is over the 2 GB, from the whole-image input
    rectangle a strong minify asks for every tile. No test covers that case; the person gave it to T-155, which
    brings it under 2 GB with a regression test.
  - Distance of a 4x4 tiles mask at range 0.1: inside 473.8 s, both 378.6 s, each 288,138 tiles evaluated, peak held
    84 MiB, which is over the 64 MiB cap (the 196 pinned window tiles of each output tile), working set 331-332 MB.
- *The regression class, real patina, debug build, one output exported:*
  - The 30-degree turn: 261.6 s, peak working set 1,721 MB (bound 2,048 MB): passes, with little headroom.
  - Distance at range 0.1, inside: 3,566.7 s (59 m 27 s for the test), peak working set 904 MB: passes. With that
    test at 8192, `cairn task T-138 verify` ran `patina-regression` in 1,799 s. The test now runs at 2048:
    `verify` ran the whole of `patina-regression`, the 8192 turn included, in 318 s (on the runner Mantorok), so
    the distance figure at 2048 is not recorded separately.

**Surprising:** `tools/Patina.SampleGraphs -- --canonical` rewrites every committed `.texgraph` with LF endings,
though the bytes are otherwise the same. Restore the other samples with `git checkout --` after a run. (The second
agent did not run it: the floor's PNGs and graph were current.)

**Not checked:** the strong minify's working set is not under any test; T-155 adds one. The pinned window's 84 MiB
over the cap is the evaluator's pinning, not a leak, and T-155 brings it under. Distance's peak at 8192 is not under
test until T-155 raises the test again. The cube view (T-157) is not built here.

