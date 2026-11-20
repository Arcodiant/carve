---
description: >-
  tiles (bricks with gaps and jitter), stripes and shape nodes, and the bricks and fabric sample graphs with their PNGs
---
# T-136 — Tiles, bricks, stripes and shapes

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-133

**Verify:** patina-unit

## Goal

The patterns that walls, floors and cloth are laid out from: tiles and bricks with gaps and jitter, stripes, and simple
shapes repeated across the image. They come with two materials, bricks and fabric.

## Scope

- **Three node types**, one file each, one line each in `NodeRegistry.BuiltIn`, on T-133's node API, every parameter
  relative to the image, every output tiling, deterministic:
  - `tiles`: `count` `[x, y]` tiles across the image (whole numbers), `stagger` 0..1 (a row's shift in tiles: 0.5 is
    running bond), `gap` 0..0.45 of a tile's width, `bevel` 0..0.5 of a tile's width (the ramp inside each tile's edge),
    `jitter` 0..1 (each tile's size and placement moved by the hash, staying inside its slot), `orientation` `rows` or
    `columns` (bricks laid along u or along v). Outputs: `mask` (1 on a tile, 0 in the gap), `height` (0 in the gap,
    rising over the bevel to 1), `random` (one value per tile) and `uv` (colour: each texel's place within its tile,
    0..1 in R and G, for a pattern laid inside each tile).
  - `stripes`: `count` (whole stripes across the image), `angle` in whole steps that tile (`axis` `u`, `v`, or
    `diagonal` with `count` stripes along each axis), `width` 0..1 of a period, `softness` 0..0.5. Greyscale.
  - `shape`: one shape per cell of `count` `[x, y]`: `kind` `circle`, `square`, `ring`, `diamond` or `polygon` (with
    `sides` 3 to 12), `size` 0..1 of the cell, `softness`, `rotation` in degrees (applied inside the cell, so tiling
    holds). Greyscale, 1 inside.
- **Tests (unit), worked by hand**: values at chosen texels (a gap texel, a bevel texel, a tile centre, a stagger
  row); tiling across both wraps; size-invariance at 256 and 1024; gap and bevel widths measured in texels against the
  parameters; jitter keeping every tile inside its slot (no tile overlaps a neighbour's gap); a table of broken copies
  and what caught each.
- **Two sample graphs**, each with its PNGs from `tools/Patina.SampleGraphs` and a test keeping them equal:
  - `samples/graphs/bricks.texgraph`, 256: running-bond red bricks (`tiles`, 4 x 8, stagger 0.5), a tone per brick from
    `random` with a little `noise`, light mortar in the gap, a height from `tiles.height` through `height_to_normal`,
    and roughness and occlusion. T-139 later exposes its mortar, tint and jitter, and T-140 uses it as a node, so keep
    node ids readable (`layout`, `brick`, `mortar`, `ao`, ...).
  - `samples/graphs/fabric.texgraph`, 256: a woven cloth from two `stripes` sets (warp and weft) crossing with
    alternating over-under, a fine `noise` for the fibre, a soft height and a high roughness.
  - Work each one's numbers against check's rules (below) in a test before the PNGs are shown. The person looks at the
    PNGs and a swatch sheet of each (from patina's `swatch`, in a Waystone review) before it lands.
- `define_graph`'s node list names the three types. One worked example in `define_graph`'s description lays out
  `tiles`; the description tests run it as written.

## Notes

- **Decided at planning (2026-11-19, by the person):** bricks and fabric are this task's content. Patterns carry their
  own direction (`orientation`, `angle`, `rotation`), since `transform` (T-138) is a filter on a finished image. check's
  rules, decided at planning (T-141 builds them; every sample must pass): base colour (non-metal) by brightest channel in
  20..245 sRGB for 99% or more of texels; metallic between 0.1 and 0.9 in 5% of texels or fewer; normals with
  |length - 1| > 0.02 in 0.1% of texels or fewer; no greyscale output of one value; no seam across the wrap. Mortar and
  gaps must stay above 20 (the planks' gaps reach 9 in a channel but not 20 in their brightest).
- The design is settled; the ranges are this task's to set and record: Sonnet. Plan for the PNG look as a stop.
- **Write down for T-139 and T-140** (As built): the bricks' node ids and which parameters are worth exposing.
- **Stop and ask:** a parameter beyond those above, or a sample that cannot meet check's rules.
- Runs beside T-134 and T-135: node files, registry lines, its two samples and its tests only. A conflict that only adds
  (`NodeRegistry.cs`, `NodeCatalogue` text, CLAUDE.md's Layout) is settled by keeping both sides.

### As built

Built by Claude Sonnet 5.5 (`claude-sonnet-5-5`), one agent. The person approved the bricks' and the fabric's looks in a
Waystone review (round-1: base colour, normal, roughness, occlusion and a 3 x 3 tiling of each).

**Nodes** (`src/Patina.Graphs/Nodes/TilesNode.cs`, `StripesNode.cs`, `ShapeNode.cs`; all worked from the texel alone at its
centre, in `double`, so any tile size gives the same bytes):
- `tiles`: `count` [x, y] 1..256 (4, 4); `stagger` 0..1 (0); `gap` 0..0.45 (0.1) and `bevel` 0..0.5 (0.1), both parts of a tile's
  width as the image's width over `count` x, in either orientation; `jitter` 0..1 (0); `orientation` `rows` | `columns`.
  Outputs `mask`, `height`, `random` (grey), `uv` (colour, R and G the place in the tile, clamped in the gap). Odd rows (odd
  columns for `columns`) shift by `stagger` tiles. A gap is held to 90% of a tile's extent on either axis. Jitter shrinks a tile
  by up to 60% on each axis and places it anywhere inside its inset slot, so a tile never reaches a neighbour's gap. An odd count
  of rows lets the first and last row meet unshifted at the wrap (still tiles).
- `stripes`: `count` 1..4096 (8); `axis` `u` | `v` | `diagonal` (the spec said "angle in whole steps (`axis` ...)"; I named the
  parameter `axis`, as `gradient` does); `width` 0..1 (0.5); `softness` 0..0.5 (0), the smoothstep centred on each edge, as a part
  of a period. A stripe is centred on each multiple of 1/count (the first at the image edge), so a set of half the count at width
  0.5 covers every second stripe of the full set (the fabric's over-under uses this). A diagonal's texel centres land on a stripe
  edge, so its hard coverage is 17/32 rather than 16/32.
- `shape`: `count` [x, y] 1..256 (4, 4); `kind` circle | square | ring | diamond | polygon (circle); `sides` 3..12 (6); `size` 0..1
  (0.6) of the cell's shorter side; `softness` 0..1 (0), the ramp inward over softness of the shape's depth; `rotation` -360..360
  degrees (0). Rotation uses a Taylor-series sine and cosine (no `Math.Sin`).
- `define_graph` names the three, and has a second worked example laying out `tiles` (`DefineGraphTool.WorkedTiles`, run by
  `WorkedExampleTests`).

**Bricks** (`samples/graphs/bricks.texgraph`, 256, seed 21), ids: `layout` (tiles 4 x 8, stagger 0.5, gap 0.1, bevel 0.1,
jitter 0.25); `tone` (levels of `layout.random`, 0.7..1.1) and `clay` (#a8452f) multiply into `toned`; `fleck_raw`/`fleck` (noise
cells 48, 3 octaves, 0.88..1.08) multiply into `brick`; `mortar_raw` (#b8b0a0) times `fleck` is `mortar`; `base` = blend mix
`mortar` to `brick` by `layout.mask`; `normal` = `height_to_normal` of `layout.height` (strength 0.015); `rough_base` (levels of
the mask, 0.95 in the gap, 0.72 on a brick) times `fleck` is `roughness`; `ao` (levels of `layout.height`, in_high 0.6, out_low
0.45). Outputs `base_out`, `normal_out`, `roughness_out`, `ao_out`. **For T-139 to expose:** `layout.gap` (mortar width),
`layout.jitter`, `mortar_raw.colour` (mortar), `clay.colour` and `tone`'s `out_low`/`out_high` (tint and variation per brick).
T-140 uses the graph as a node through those same ids.

**Fabric** (`fabric.texgraph`, 256, seed 33): `warp`/`weft` (stripes count 16, width 0.9, softness 0.35), `warp_even`/`weft_even`
(count 8, width 0.5, softness 0.06), `warp_odd` (inverted `warp_even`), `over` (XOR by a mix with `weft_even` as the mask), `height`
(mix `weft` to `warp` by `over`), `thread` (mix `weft_colour` #cdbf9c to `warp_colour` #2c4f86 by `over`), `shade`, `fibre`
(noise cells 128), `base`; normal strength 0.012; roughness 0.78..0.95 from the fibre noise; occlusion from the height.

**Check's rules, measured** (`PatternSamplesTests`, figures printed): base colour brightest channel 107..196 (bricks) and 90..226
(fabric), none outside 20..245; normals worst |length - 1| 0.0052 and 0.0047 (PNG, limit 0.03); no output of one value; metallic
none; the seam across the wrap at most 1.2 against interior steps of 3 to 10 in every output. The committed PNGs equal the graphs.

**Tests**: `PatternNodesTests` (hand-worked texels, tile-size invariance, wrap, 256 vs 1024, gap and bevel in texels, jitter in its
slot, columns as rows transposed, and tables of broken copies for the three nodes) and `PatternSamplesTests`. `GraphFaultTests`'s
type list now includes shape, stripes and tiles.

**Not checked:** scenario-patina frames (no scenario stage added; the swatch sheet is T-134's, so the review used my own
PNGs and 3 x 3 tilings), 8192 timing.
