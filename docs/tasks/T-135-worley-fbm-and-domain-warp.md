---
description: >-
  worley, fbm and warp nodes, and samples/graphs/cobblestones.texgraph with its PNGs
---
# T-135 — Worley, fBm and domain warp

**Status:** In Progress

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

### As built

Built by Claude Sonnet 5.5 (`claude-sonnet-5-5`), one agent.

**Nodes** (`src/Patina.Graphs/Nodes/WorleyNode.cs`, `FbmNode.cs`, `WarpNode.cs`, one line each in `NodeRegistry.BuiltIn`; each exposes its pure
sample function as a `public static` so a test makes broken copies):
- `worley`: no inputs; outputs `f1`, `edge`, `random` (greyscale); params `cells` [x, y] 1..1024 (default 6, 6), `jitter` 0..1 (1), `metric`
  `euclidean|manhattan`, `seed` 0..1000000 (0, added to the graph's seed). Distances in cell widths (each axis in its own cells); the nearest two
  points searched in the 3 x 3 cells around the texel's; `random` is a second hash (`seed ^ 0x5BD1E995`) of the nearest point's cell.
- `fbm`: no inputs; output `out` (grey, 0..1); params `cells` [x, y] 1..1024 (4, 4), `octaves` 1..8 (4), `gain` 0..1 (0.5), `basis`
  `value|gradient` (value), `style` `plain|ridged|billow` (plain), `seed`. Octave o has cells << o and seed `graph + seed + o * 7919` (as `noise`);
  weight is gain^o (by repeated multiplication), the sum divided by the weights' sum; the style is applied to each octave. Gradient: eight unit
  gradients by hash, quintic fade, stretched from +-0.7071 to 0..1.
- `warp`: inputs `in` (any) and `by` (any); output `out`, as colour as `in`; params `amount` 0..0.25 (0.05), `angle` 0..360 degrees (0). `angle` is a
  parameter beyond the spec's list: it is the spec's "along `angle`" for a greyscale `by`. `by` colour: the offset in texels is
  `(R - 0.5) * 2 * amount * size` in u and `(G - 0.5) * ...` in v, so a channel at 0 or 1 moves `amount` of the image; greyscale: `(v - 0.5) * 2 *
  amount * size` along (cos, sin) of `angle`, 0 along u, 90 down. Bilinear, wrapping. `InputRect("in")` is the output widened by
  `ceil(amount * size) + 2`; `by` is read pointwise. Uses `Math.Cos`/`Math.Sin` once per tile on the angle (no `Pow`/`Exp`).

**Tests:** `WorleyFbmWarpTests.cs` (12: values worked by hand, tiling across both wraps, tile-size invariance, 256 vs 1024, amount 0 equals input,
a constant `by` equals a shift, a colour `by`, the input rectangle, and `EveryNodeIsCaughtOnABrokenCopy`'s table of 13 breaks: worley no wrap / jitter
ignored / edge sign / manhattan as euclidean; fbm not normalised / weights reversed / ridged-billow swapped / gradient lattice not wrapped; warp
offset sign / u-v swapped / cos-sin swapped / bilinear weights swapped / reads clamped), `CobblestonesGraphTests.cs` (18), and one worked example
test in `WorkedExampleTests` for `define_graph`'s new worley example (`DefineGraphTool.WorkedStones`); the node list names the three through
`NodeCatalogue.Says`.

**Cobblestones** (`samples/graphs/cobblestones.texgraph`, 256, seed 135, four outputs; PNGs in `samples/graphs/cobblestones/`): `worley` 6 x 6
jitter 0.85; `by` is a colour from two gradient `fbm`s (4 x 4 cells, 4 octaves) built with `blend` multiply of red and green colours and add;
`edge` and `random` are each warped by it at amount 0.05; height is `levels` of the warped edge (in_high 0.65, gamma 1.6) into
`height_to_normal` strength 0.08; the gaps are a `levels` mask of the edge that mixes a dirt colour (an fbm-toned) over the stone (tone per stone from
`random` 0.7..1.15, gradient-fbm grain); roughness is the stone grain 0.6..0.85 with `max` of the gap mask at 0.95; occlusion is the height from 0.3.
check's rules measured on the committed PNGs (and held by `CobblestonesGraphTests`): base colour brightest channel 48..164, 0 of 65536 outside
20..245; worst normal length error 0.0066, none over 0.02; every output has at least 8 distinct bytes; seam mean step against the limit (twice the
interior plus 2): base_colour u 6.97 / 10.37 and v 5.54 / 10.40, normal 7.77 / 19.56 and 9.65 / 19.61, roughness 2.39 / 4.84 and 1.82 / 4.84,
occlusion 8.55 / 14.19 and 8.54 / 14.30. Metallic is 0. The person approved the look in Waystone (round-1) on 2026-11-19; patina's `swatch` was not
reachable from the agent, so the tiled 3 x 3 views were made by tiling the PNGs.

**Time at 8192** (Release build, AMD Ryzen 5800X3D, Windows 10 Pro, one run each, the 64 MiB cap and tiles of 128; the planks' figures in
T-133 are Debug, so not comparable): cobblestones base_colour 134.4 s, 123,456 tiles evaluated; normal 202.2 s, 161,672; roughness 156.4 s,
90,688; occlusion 142.6 s, 78,400; peak held 64 MiB in each. (A run with no recomputation makes one tile per node per position, 4096 for each node
the output depends on.) A warp (`fbm` 8 x 8 gradient 4 octaves, warped by an `fbm`, grey out): amount 0.05, 27.3 s, 12,800 tiles (the minimum);
amount 0.25 at 4096, 8.4 s, 3,648 tiles (minimum 3,072); amount 0.25 at 8192 did not finish: stopped by the measurement after 301 s with 379 of
4096 output tiles done (the first row of 64 at 56 s), so about 0.8 s a tile and 55 minutes in all, about 120 times the 0.05 case: each output tile
asks for about 34 x 34 = 1,150 input tiles and the cache holds 1,024 grey ones. Memory stays at the cap. This is the ruled-on cost: the person was
offered a data-dependent request, a larger cap, or a limit on the amount, and approved the sample without choosing one, so nothing was changed and
T-137 and T-140 should treat a warp amount above about 0.1 at 8192 as slow.
