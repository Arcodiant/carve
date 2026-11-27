---
description: >-
  A Patina node that reads a PNG file into a graph, a normal map decoded to -1..1, so a bake's maps (a bevel normal) can be combined with graph nodes
---
# T-240 — image: read a PNG into a Patina graph

**Status:** In Progress

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

A Patina graph can read a PNG, so a map baked by carve's `bake` (a bevel normal, a mask, a curvature) or any other image is a node's input beside the generated ones. T-224's `normal_blend` needs it to lay a noise bump over a baked bevel normal.

## Scope

- A new Patina node `image` in `src/Patina.Graphs/Nodes/`, format version 2 (as `input` and `graph` are), registered in `NodeRegistry`.
  - Parameter `path`: a PNG, relative to the graph's file (`TextureGraph.Folder`, as a `graph` node's path is; no extension added).
  - Parameter `kind` `colour` (default), `grey` or `normal`: `colour` is R, G, B / 255; `grey` is R / 255 (carve's bakes write grey as R = G = B); `normal` is each of R, G, B decoded as 2c / 255 - 1 and renormalised to unit length, the form `height_to_normal` outputs, so it passes `NormalNotUnitRule`. Alpha is not read.
  - The image is laid over the graph's 0..1 square whatever its size: a PNG of the graph's size is read texel for texel exactly; any other size is sampled bilinear at each texel's centre, wrapping, so the node tiles and is the same at every tile size. A non-square image is stretched to the square.
  - A missing file, a PNG it cannot decode and a format it does not read are `GraphFault`s on `params['path']` naming the file, found at validation, not at evaluation.
- An engine-free PNG decoder in `src/Patina.Graphs/Imaging/` on .NET's zlib (no package): 8-bit greyscale, grey + alpha, RGB and RGBA, non-interlaced, all five row filters; 16-bit, palette and interlaced images are refused with a message saying so. Tests decode every byte of what `PngWriter` writes and hand-made PNGs of each colour type and filter.
- Each image file a graph reads (nested graphs' too) is among the files `GraphValidator.Uses`/`GraphSlotBake.Read` list, so carve's `GraphBakes` key changes when the PNG's bytes do, and patina's `open` and `get_graph` name it as they name nested graph files.
- `NodeCatalogue`: the type's line, its gist and a worked example reading a committed PNG (run as written by `NodeToolTests`, the file copied beside it as `NestedGraphToolTests` does), every description still at 3,686 or less. The node-library README lists it.
- Tests in `tests/Patina.Graphs.Tests`: texel-exact at the graph's size for each kind, normal decode unit length, a 2x and a 0.5x image against bilinear by hand, the wrap at the seam, tile-size and cap invariance, every fault, and a table of broken copies (no wrap, no renormalisation, rows flipped, a channel dropped) each caught. In `tests/Carve.Graphs.Tests`: the slot's hash changes when only the PNG changes.

## Notes

- Raised on 2026-11-27 while starting T-224: its Scope assumed a graph could read a PNG through an `input` node, and none can (`input` passes only what an outer `graph` node wires to it, or a flat default). The person chose a task of its own, landed before T-224 resumes.
- Bilinear rather than nearest so a 1024 bake read into a 256 graph does not alias; at the graph's own size the two agree exactly.
