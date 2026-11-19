---
description: >-
  worley, fbm and warp nodes, and samples/graphs/cobblestones.texgraph with its PNGs
---
# T-135 — Worley, fBm and domain warp

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-133

**Verify:** patina-unit

## Goal

Three noises most stone, rust and organic materials start from: Worley cells, fractal noise with more control than
`noise`'s octaves, and a domain warp that bends any input. They come with a cobblestone material built on them.

## Scope

- **Three node types**, one file each in `src/Patina.Graphs/Nodes`, one line each in `NodeRegistry.BuiltIn`, written
  against T-133's node API. All parameters are relative to the image, every output tiles, and evaluation is
  deterministic (T-77's integer hash, `float`, no `Math.Pow` or `Math.Exp`):
  - `worley`: one feature point per cell of a `cells` `[x, y]` lattice, placed by the hash, with `jitter` 0..1 (0 is the
    cell centre), and the cells wrapping so it tiles. Distances are in cell widths. Outputs: `f1` (distance to the
    nearest point), `edge` (f2 - f1: 0 on the boundary between two cells), and `random` (one value per cell, 0..1, for
    a tone per stone). `metric` `euclidean` or `manhattan`. `seed` is an offset, as on `noise`.
  - `fbm`: the sum of octaves of a base noise, `basis` `value` (as `noise`) or `gradient` (Perlin-style, hashed
    gradients), with `cells` `[x, y]`, `octaves` 1 to 8, `gain` 0..1 (the weight of each octave against the one
    before), the cells doubling per octave (whole numbers, so it tiles), and `style` `plain`, `ridged` (1 - |2n - 1|) or
    `billow` (|2n - 1|). Normalised to 0..1.
  - `warp`: input `in` (greyscale or colour) and `by` (colour: R and G are the u and v offsets about 0.5, or greyscale:
    the offset along `angle`), `amount` 0..0.25 of the image. The input is read at the displaced texel with bilinear
    interpolation, wrapping. The input rectangle is the output's widened by `amount`, through T-133's API.
- **Tests (unit), with numbers worked by hand**: each node's value at chosen texels; tiling across both wraps; size-
  invariance between 256 and 1024 at the same uv (within one interpolation step); `warp` with `amount` 0 equal to its
  input, and with a constant `by` equal to a shift of it; each node shown failing on a broken copy (no wrap, a sign
  flipped, the jitter ignored): a table of breaks and what caught each.
- **The cobblestones**, `samples/graphs/cobblestones.texgraph`, size 256, with its PNGs from `tools/Patina.SampleGraphs`
  and a test keeping them equal to the graph, as `PlanksGraphTests` does:
  - rounded stones from `worley` `edge` (about 6 by 6 cells), each stone's tone from `random`, the edges bent by `warp`
    of an `fbm`, dirt in the gaps from `fbm`, and a height from `edge`, through `height_to_normal`;
  - outputs `base_colour`, `normal`, `roughness` and `occlusion` (M-27's roles: `height` is T-134's, in the same wave,
    so this sample does not wait on it);
  - work its numbers against check's rules, decided at planning (below), in a test before the PNGs are shown: base
    colour, by brightest channel, in 20..245 for 99% or more of texels, every output varying, normals of unit length.
  - The person looks at the PNGs and a swatch sheet (from patina's `swatch`, in a Waystone review) before it lands.
- `define_graph`'s node list (from `NodeCatalogue`) names the three types with their parameters. One worked example in
  `define_graph`'s description uses `worley`. The description tests run it as written.

## Notes

- **Decided at planning (2026-11-19, by the person):** the cobblestones are this task's content. check's rules, decided at
  planning and built by T-141: base colour (non-metal) by brightest channel below 20 or above 245 sRGB is out, and a fault
  over 1% of texels; metallic between 0.1 and 0.9 in over 5% of texels; normals with |length - 1| > 0.02 in over 0.1% of
  texels; a greyscale output whose texels all pack to one byte; and a seam across the wrap (mean step greater than twice
  the interior mean step plus 2 levels). The sample must pass all of them.
- The design is settled: Sonnet. A frame and PNG look by the person is part of landing; treat Waystone's "Commented" as
  unanswered until the person says approved.
- **Stop and ask:** a parameter beyond those above, a change to a node's meaning, or a sample that cannot meet check's
  rules.
- Runs beside T-134 and T-136: it adds node files, registry lines, its sample and its tests only. A conflict in
  `NodeRegistry.cs`, `NodeCatalogue` text or CLAUDE.md's Layout that only adds is settled by keeping both sides.
- Breaks are made in the test, never by a switch in the product.
