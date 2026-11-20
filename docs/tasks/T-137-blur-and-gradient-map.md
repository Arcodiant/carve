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
