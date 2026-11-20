---
description: >-
  blur and gradient_map nodes, and samples/graphs/painted-metal.texgraph with its PNGs
---
# T-137 — Blur and gradient map

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-134, T-135, T-136

**Verify:** patina-unit

## Goal

Two filters: a blur to soften masks and heights, and a gradient map that colours a greyscale image through a list of
colour stops. They come with a painted metal plate whose paint has worn through to bare metal.

## Scope

- **Two node types**, one file each, one line each in `NodeRegistry.BuiltIn`, on T-133's node API:
  - `blur`: input `in` (greyscale or colour), `radius` 0..0.1 of the image width, `quality` `box` (one box pass) or
    `smooth` (three box passes, close to a Gaussian). Separable, with a running sum, so its cost per texel does not grow
    with the radius; wrapping, so it tiles. Its input rectangle is the output's widened by the radius (times three for
    `smooth`), through T-133's API. Deterministic: the running sum in a fixed order, in `float` or `double` as the
    research in this task settles and records.
  - `gradient_map`: input `in` (greyscale), `positions` (2 to 8 numbers, 0..1, ascending) and `colours` (the same count
    of `"#rrggbb"`): T-134's `Floats` and `Colours` parameter kinds. Linear between stops in sRGB, as `#rrggbb` is held;
    clamped beyond the ends. A count mismatch or positions out of order is a fault naming the node and both parameters.
- **Tests (unit), worked by hand**: a blur of a one-texel impulse (its sum kept, its width as the radius says), of a
  step across the wrap (tiling), `radius` 0 equal to its input, size-invariance at 256 and 1024; a gradient map at
  each stop and between two; the faults; a table of broken copies (the wrap dropped, an off-by-one window, stops read
  from the wrong end) and what caught each.
- **Speed**: measure `blur` `smooth` at radius 0.05 on a colour input at 2048 and 8192, and write the times here. More
  than 10 s at 8192 is a stop.
- **The painted metal plate**, `samples/graphs/painted-metal.texgraph`, 256: paint (a `gradient_map` of an `fbm`, for
  a mottled green-grey) over steel, worn through where a blurred `fbm` mask with some `worley` chips falls below a level.
  `metallic` 1 on bare steel and 0 on paint, sharp at the edge so check's metallic rule holds. Roughness lower on
  steel. A soft height step at the paint's edge through `height_to_normal`. A named output `wear` (the worn mask,
  T-134). Its PNGs from `tools/Patina.SampleGraphs`, with a test keeping them equal.
  - Work its numbers against check's rules in a test before the PNGs are shown. Metal texels (metallic 0.5 or more)
    need a brightest channel of 120 or more. Metallic between 0.1 and 0.9 must be in 5% of texels or fewer, so the blur
    may soften the height but not the metallic edge.
  - The person looks at the PNGs and a swatch sheet with the sphere (showing metallic) before it lands.
- `define_graph`'s node list names the two types. One worked example in `define_graph`'s description uses
  `gradient_map`; the description tests run it as written.

## Notes

- **Decided at planning (2026-11-19, by the person):** painted metal is this task's content. check's rules (T-141 builds
  them): base colour (non-metal) by brightest channel in 20..245 sRGB for 99% or more of texels, metal texels 120 or
  more; metallic between 0.1 and 0.9 in 5% of texels or fewer; normals of unit length within 0.02 in 99.9% of texels;
  no greyscale output of one value; no seam across the wrap.
- Sonnet: the design is settled, apart from blur's arithmetic, which is recorded.
- **Stop and ask:** a parameter beyond those above, a blur slower than 10 s at 8192, or a sample that cannot meet check's
  rules.
- Runs beside T-138, T-139: its own node files, registry lines, sample and tests. A conflict that only adds is settled by
  keeping both sides.

- **Rulings since the hand-off (2026-11-20, by the person):** (1) blur's speed is a separable two-pass evaluation, a new
  member T-155 (depends on T-137, makes blur opt in, owns `Evaluation/`); T-137 lands without meeting 10 s at 8192 and
  the speed check moves to T-155. (2) `gradient_map` keeps equal neighbouring positions as a hard step. (3) The person
  asked to see the complete painted metal on a sphere and a plane: round 2 showed the engine's swatch sheet and sphere.
  `swatch` has sheet, flat, tiled and sphere views and no lit plane view; the plane is the flat and tiled 3 x 3 rows of
  the sheet. Round 2 was approved with a note: "quite low res for the object, let's tile 3x3" (the sphere at 256 px;
  `swatch`'s sphere has no tiling parameter, so this is left as a note for a swatch change, not done here).

### As built

Built by Claude Sonnet 5.5 (`claude-sonnet-5-5`), one agent, which stopped at the speed rule below; the rest (rebase, rulings, round 2, finish) was done by a second Claude Sonnet 5.5 agent.

**Nodes** (`src/Patina.Graphs/Nodes/BlurNode.cs`, `GradientMapNode.cs`, one line each in `NodeRegistry.BuiltIn`):
- `blur`: input `in` (grey or colour, the same out); `radius` 0..0.1 (0.01), made whole texels `r = floor(radius * size + 0.5)`; `quality` `box` (one pass of the `2r + 1` texels centred on each) | `smooth` (three). Horizontal passes then vertical, running sums, wrapping; `InputRect` is the output inflated by `r` (`3r` for smooth). **Arithmetic, as the research asked:** each running sum is an integer, the texels in 2^32 fixed point, divided once in `double` and rounded once to `float`, so a texel is the same in every tile and the sum has no drift (a float or double running sum starts from a different texel in each tile). Tests show the tile size changes not one bit.
- `gradient_map`: input `in` (grey), output colour; `positions` 2..8 numbers 0..1 (0, 1), `colours` the same count (`#000000`, `#ffffff`). Linear in sRGB, clamped beyond the ends. A count mismatch or positions out of order is a fault on the node's `in` (through `CheckInputKinds`, as `GraphValidator` is T-139's), naming the node, `positions` and `colours`. **Equal neighbouring positions are allowed** (a hard step, the later colour at the value itself): the spec said "ascending" without settling this; say if it should be strict.
- `define_graph` names both and has a worked example (`DefineGraphTool.WorkedMap`, run by `WorkedExampleTests`); `NodeCatalogue` has their lines.
- The test node `blur` (5 x 5 box) in `TestNodes.cs` is renamed `box5`, since the real `blur` took the name; `GraphFaultTests`' type list has `blur`, `box5`, `gradient_map`.

**Tests:** `BlurGradientMapTests` (23: impulse sum and width for box and smooth, the step across the wrap, radius 0, 256 vs 1024, tile sizes 8/32/128 equal to the bit, colour, the input rectangle, the running sums equal a slow blur written beside them; a table of broken copies, each caught: wrap dropped by the wrap step, window off by one by the box impulse, one pass for three by the smooth impulse, a rectangle one texel short by a throwing read; gradient map at each stop, between, beyond, in a graph, both faults, and broken copies: stops from the wrong end, not clamped, stops held not mixed) and `PaintedMetalTests`.

**Painted metal** (`samples/graphs/painted-metal.texgraph`, 256, seed 137; PNGs in `painted-metal/`, kept equal by the test): paint is a `gradient_map` (3 stops, green-grey) of a gradient `fbm`; steel a `gradient_map` of a value `fbm`; `wear` is a step (`levels` with in_low = in_high 0.42, hard) of a smooth `blur` (0.012) of a gradient `fbm` minus 0.4 of `worley` chips (9 x 9, only cells whose `random` is above 0.65). Metallic is `wear` itself, so exactly 0 or 1. Roughness paint 0.6..0.85, steel 0.22..0.42. Height is a smooth `blur` (0.006) of the paint mask into `height_to_normal` (0.02); occlusion from the same. Named output `wear`. Check's rules, measured on the committed files: metal texels 12632 of 65536 (19.3%), their brightest channel from 158 (rule 120); non-metal brightest channel 74..120, none outside 20..245; metallic between 0.1 and 0.9 in 0 texels (rule 5%); normals worst length error 0.0060; every output has more than one value; seam steps at the wrap at most the interior's.

**Speed (not met; moved to T-155 by the person's ruling).** Release build, one run each, `smooth` radius 0.05 on a colour input (an `fbm` through a `gradient_map`), the 64 MiB cap and tiles of 128: **2048: 10.5 s** (960 tiles evaluated). **8192: not finished**: stopped at 120 s with 131 of 4096 output tiles done, about 0.9 s a tile, so about an hour. Cause: each 128 output tile asks for 128 + 2 x 1229 = 2586 texels a side (6.7 M texels, 400 input tiles against a cache of about 340 colour tiles), 4096 times. No code change inside `blur` can reach 10 s with this API; it needs the node to see wider tiles or to be split in two passes (an **engine need**: a node that can ask the evaluator for whole-row or whole-column bands, or a larger tile size per node).

**Not checked:** blur at 8192 against 10 s (T-155's). Round 2 (G:\My Drivertifacts\carve\M-28\T-137ound-2): the engine's swatch sheet and sphere of the sample, drawn through patina's `swatch` over MCP, approved by the person; there is no lit plane view, so the plane is the sheet's flat and tiled 3 x 3 rows. Rebased onto `origin/milestone/M-28` (T-134, T-135, T-136, T-153): one conflict, CLAUDE.md's Layout lines, both sides kept; `verify` passes unchanged.
