---
description: >-
  A Patina node that reads a PNG file into a graph, a normal map decoded to -1..1, so a bake's maps (a bevel normal) can be combined with graph nodes
---
# T-240 — image: read a PNG into a Patina graph

**Status:** Done

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

### As built

- **Built.** `PngReader` (`src/Patina.Graphs/Imaging/PngReader.cs`, engine-free on .NET's `ZLibStream`): 8-bit greyscale, grey + alpha, RGB and RGBA, not interlaced, all five row filters, every chunk's CRC checked, ancillary chunks skipped; 16-bit, palette, interlaced and 1/2/4-bit images are refused saying so, and a damaged file (no signature, cut short, a wrong CRC, data that does not inflate or ends early, an unknown filter or critical chunk) saying what is wrong. It gives `PngImage(Width, Height, Rgba)`, RGBA8, grey as R = G = B. The `image` node (`Nodes/ImageNode.cs`, format version 2, in `NodeRegistry`): no inputs, one output `out`.
- **What later tasks call.** `{"type":"image","params":{"path":"<png relative to this graph's file>","kind":"colour|grey|normal"}}`; `kind` defaults to `colour`. Output kind: grey for `grey`, colour otherwise. `colour` gives R, G, B / 255; `grey` R / 255 (G and B ignored); `normal` gives each of R, G, B as 2c / 255 - 1, then the vector divided by its length: a unit tangent-space vector in -1..1, the form `height_to_normal` outputs, so it feeds `normal_blend` (T-224) or an output of role `normal` directly. Alpha is never read. `ImageNode.Texel(image, kind, x, y, size)` is the per-texel read, public for tests.
- **Sampling.** Texel x of a size-n graph reads image x (2x + 1) W / 2n - 0.5, worked in integers, so at W = n it is exactly texel x (no float drift); otherwise bilinear between that texel and the next, both wrapped, in x and y separately (a non-square image is stretched).
- **Files.** `GraphValidator.Files(graph)` (new) lists every file validation reads, all levels down: nested graphs' files and image PNGs. `GraphSlotBake.Read` hashes from it (so carve's `GraphBakes` key follows the PNG's bytes), and patina's `open` names them (`read N nested file(s) for its graph and image nodes: ...`, reworded from `for its graph nodes`; the two scenario stages and `NestedGraphToolTests` updated). `GraphValidator.Images(graph)` (new) gives each own image node's file and size; `get_graph` prints `image nodes (N), paths resolved against ...:` with `- id: <file>; WxH` after the graph-node lines. A nested graph's PNGs are in its `NestedGraphUse.Files`.
- **Decisions the Scope left open.** (1) The decoded image travels to evaluation on `NodeParameters.Image` (internal), set by the validator, so the evaluator is unchanged and the file is read once per analysis, not per tile; a nested graph's image nodes carry theirs through the expansion (`GraphAnalysis.Images`, keyed `<node>/<id>`), resolved against the nested file's folder. (2) A PNG larger than 8192 (the largest graph) on a side is refused, bounding a decode at 256 MiB. (3) A bilinear blend of opposite normals with no length reads as (0, 0, 1). (4) `path` is required: an image node without one is a fault. A missing file's fault lists the `.png` files in its folder, as a graph node's lists the `.texgraph` ones. (5) `save` into another folder now also refuses when an image node's path would there name a PNG with other bytes (`image node 'x' would read ..., which differs from ...`), and a nested graph's PNG is compared by bytes rather than parsed as a graph. (6) `ParamSpec.FilePath` takes what file it names, so `nodes` says "a path to a .png file". (7) RV-94: one PNG is decoded once per validation however many nodes or nested uses read it (`NodeBudget.Images`), and `GraphValidator.Files` lists PNGs without decoding them, so carve's cache-key path (`GraphSlotBake.Read`, on every render) decodes nothing. (8) Bilinear as the Scope says: below half the graph's size a texel reads only the 2x2 texels around its centre, so a 1-texel line in a 1024 bake read at 256 can drop out (RV-94). Averaging the footprint would be a Scope change, left to the person.
- **README.md.** The Nodes list names `image` among the sources; the graphs-as-nodes section says an `image` node makes a file version 2 and gives a paragraph on how it is read (size, bilinear wrap, `kind: normal`, formats, faults, `open`/`get_graph`, carve re-baking on a PNG change).
- **Not verified.** No carve material was baked end to end through MCP with an image graph (`GraphSlotBakeTests` holds the hash, not a render); no swatch was looked at; the two edited scenario stages (`LibraryStage`, `NestedGraphStage`) are regression scenarios not run by this task's verify, edited only in an expected string. Validation decodes the PNG on every analysis (define_graph, edit_graph, Uses, Images, the evaluator each run one; `Files` does not), which is fine at 1024 and has not been timed at 8192; carve's render path no longer decodes (`Files` lists only), but a bake that misses the cache decodes once to evaluate.
