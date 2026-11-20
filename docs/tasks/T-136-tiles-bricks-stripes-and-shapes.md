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
