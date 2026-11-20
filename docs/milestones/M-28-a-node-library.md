---
description: >-
  The texture server gains the noises, patterns and filters most game materials need, a check tool, and graphs reusable as nodes
---
# M-28 — A node library

**Status:** Active

**Depends on:** M-27

**One-liner:** *The texture server gains the noises, patterns and filters most game materials need, a check tool, and graphs reusable as nodes*

## What this milestone delivers

An agent can make most stylised game materials (stone, brick, metal, fabric, wood) from [M-27](M-27-a-first-texture-graph.md)'s server, and can measure what it made.

- More noises: Worley (cells), fBm, and domain warp.
- Patterns: tiles and bricks with gaps and jitter, stripes, and simple shapes.
- Filters: blur, transform and tile, gradient map, and distance from an edge.
- A `check` tool, as Carve's lists mesh faults: an output that does not tile, base colour outside a plausible sRGB range, metallic far from 0 or 1, normals not of unit length, and a mask of one value.
- Exposed parameters, so one graph gives variants, and a graph used as a node in another.

Content: four or five sample materials, each a committed graph with its swatch renders, built on the new nodes.

## Estimated size

About **7 tasks**:

1. Noises: Worley, fBm, domain warp.
2. Patterns: tiles, bricks, stripes, shapes.
3. Filters: blur, transform and tile, gradient map, distance.
4. The `check` tool.
5. Exposed parameters and graphs as nodes.
6. The sample materials and their scenario.
7. Acceptance: fresh agents make a named material and fix what `check` reports.

## Why

[D-7](../design/D-7-texture-graphs-and-layered-surfaces.md): M-27 is a thin slice with only enough nodes for one material. Value checks are D-7's answer to an agent judging textures mostly through renders.

## Open at planning

- ~~Whether the new nodes outgrow one task each and should be split.~~ Answered below: the filters are two tasks, and
  exposed parameters and graphs as nodes are two.
- ~~How a nested graph's parameters are shown to the graph that uses it.~~ Answered below: by name, as the `graph`
  node's own parameters.

**Decided at planning (2026-11-19).** Every decision below is the person's, put to them with a recommendation:

- **Tiled evaluation first** (T-133). The person proposed evaluating in small square tiles that travel back through the
  graph, so a rotated tile needs at worst four source tiles. That replaced this plan's offer of offset, tile and mirror
  only. Each node names the input rectangle it needs; a capped tile cache keeps memory bounded; every existing output
  stays byte-identical. Reducing the graph to a per-texel function was weighed and left as a later optimisation.
  T-130 (faster 8192 evaluation) is held until T-133 lands.
- **`transform`** has offset, tile, mirror, free rotation and free scale (T-138). A result that does not tile is the
  agent's choice, and check reports it. The patterns carry their own direction too.
- **Output roles**: `metallic` (packed into `roughness.png`'s blue channel, the glTF metallic-roughness image, so the
  planks' bytes and the barrel's binding are unchanged), `height`, and named outputs (`<name>.png`), which are a nested
  graph's extra ports and the masks M-30 will layer with (T-134).
- **Exposed parameters** are declared in a graph's `parameters`, each driving node parameters by `"node.param"` and
  taking the narrowest range of what it drives. `swatch`, `export` and `check` take overrides, so one graph gives
  variants (T-139). **A nested graph's parameters** are set on the `graph` node by name, faulted against the nested
  graph's ranges. Declined: `"$name"` references in node parameters, and one `values` object.
- **Graphs as nodes** take image inputs through `input` nodes, so a reusable graph can be a filter (T-140). The plan
  expands nested graphs inline at validation, so evaluation is unchanged; the outer graph's seed and size rule.
- **Format**: version 2 only for `parameters`, `graph` and `input`; every other file is written byte for byte as before.
  List parameters (`Floats`, `Colours`) and the new roles stay version 1.
- **`check`** looks at the open graph (with overrides, up to 2048) or at PNG files given with their roles. It reports
  and never refuses. Its thresholds were probed on the planks' committed PNGs: in 12.5% of texels the brightest
  channel is below 30, and none is below 20; the normals are unit within 0.003; there is no metallic. That set the
  base-colour floor at 20, not the common 30:
  - `base-colour-range`: a non-metal texel's brightest channel below 20 or above 245 sRGB, or a metal texel's (metallic
    0.5 or more) below 120. A fault over 1% of texels.
  - `metallic-not-binary`: between 0.1 and 0.9 in over 5% of texels.
  - `normal-not-unit`: |length - 1| > 0.02 in over 0.1% of texels (0.03 for a PNG).
  - `one-value`: a greyscale output whose texels all pack to one byte.
  - `does-not-tile`: across the wrap, a mean step greater than twice the interior mean step plus 2 levels.
- **Content, each with the task whose nodes it shows**: cobblestones (T-135), bricks and fabric (T-136), painted metal
  (T-137), floor tiles (T-138), the bricks' grey variant (T-139), grime and the old wall (T-140), and planted fault graphs
  (T-141). Every sample is worked against check's rules before its PNGs are shown, and the person approves each look
  before its task lands.
- **Acceptance** (T-143): fresh agents make three materials (a woven rug, rusted corrugated iron, and a mossy flagstone
  path with a moss filter graph used as a node), and fix a planted draft from check's report.
- **Eleven tasks in one milestone** (the estimate was 7), kept by the person over splitting the milestone or merging
  tasks.

**The check report, as agreed:**

```
check {}          (open graph, its own size; optional size<=2048,
                   parameters, files:[{path, role}])
{"ok": false,
 "problems": [
  {"check":"base-colour-range", "output":"base_colour",
   "message":"3.2% of texels are outside 20..245 (brightest channel), worst #0a0806 at (12, 40); over 1% is a fault"},
  {"check":"metallic-not-binary", "output":"metallic",
   "message":"41% of texels are between 0.1 and 0.9; over 5% is a fault"},
  {"check":"normal-not-unit", "output":"normal", ...},
  {"check":"one-value", "output":"wear",
   "message":"every texel is 1.000 (255); a mask of one value masks nothing"},
  {"check":"does-not-tile", "output":"base_colour", ...}],
 "notes":["metal texels (metallic>=0.5): 12%"],
 "stats":{"size":256, "outputs":{"base_colour":{"min":[..],"max":[..],"mean":[..]}, ...}}}
```

**The swatch sheet, as agreed:**

```
+--------------------------------------------------------------------------+
| painted-metal, 256 px, seed 5                                            |
+--------+--------+--------+--------+--------+--------+--------+-----------+
|base_col|normal  |roughnes|metallic|occlusio|height  |wear    |           |
|  flat  |  flat  |  flat  |  flat  |  flat  |  flat  |  flat  |  sphere   |
|        |        |        | (grey) |        | (grey) | (grey) |  wears    |
+--------+--------+--------+--------+--------+--------+--------+  base,    |
| tiled  | tiled  | tiled  | tiled  | tiled  | tiled  | tiled  |  normal,  |
| 3 x 3  | 3 x 3  | 3 x 3  | 3 x 3  | 3 x 3  | 3 x 3  | 3 x 3  |  rough+   |
|        |        |        |        |        |        |        |  metal,   |
+--------+--------+--------+--------+--------+--------+--------+  occl.    |
  roles in order: base_colour normal roughness metallic occlusion height,
  then named outputs a..z; a missing role has no column (as now)
  cells 256 px; 7 columns + sphere 512 = 2310 px wide
```

**Order:**

| Wave | Task | Model | Depends on | Owns (no sibling in its wave touches) |
|---|---|---|---|---|
| 1 | T-133 tiled evaluation | Opus (research, then build) | — | `Evaluation/`, `NodeType.cs`, the seven nodes |
| 2 | T-134 roles, list params, version 2 | Opus (+ frame review) | T-133 | `TexGraphFile.cs`, `TextureGraph.cs`, `OutputPacking.cs`, `OutputNode.cs`, the swatch sheet |
| 2 | T-135 worley, fbm, warp + cobblestones | Sonnet | T-133 | its node files and sample |
| 2 | T-136 tiles, stripes, shape + bricks, fabric | Sonnet | T-133 | its node files and samples |
| 3 | T-137 blur, gradient_map + painted metal | Sonnet | T-134, T-135, T-136 | its node files and sample |
| 3 | T-138 transform, distance + floor tiles | Opus | T-133, T-134, T-136 | its node files and sample |
| 3 | T-139 exposed parameters + bricks variant | Opus | T-134, T-136 | `TexGraphFile.cs`, `GraphValidator.cs`, `EditGraphTool.cs`, `FileTools.cs`, swatch and export overrides |
| 2b | T-153 data-dependent input requests (added 2026-11-20) | Opus | T-135 | `Evaluation/`, `NodeType.cs`, `WarpNode` |
| 3b | T-155 separable passes, rotated-read locality (added 2026-11-20) | Opus | T-137, T-138 | `Evaluation/`, `NodeType.cs`, `BlurNode`, `DistanceNode` |
| 3c | T-156 sphere tiling option on swatch (added 2026-11-20) | Sonnet | T-139 | `SwatchTool.cs`, `SwatchSphere.cs` |
| 3d | T-157 cube view on swatch (added 2026-11-20) | Sonnet | T-156 | `SwatchTool.cs`, a cube beside `SwatchSphere.cs` |
| 4 | T-140 graphs as nodes + grime, old wall | Opus (two agents) | T-139, T-135, T-153 | `TexGraphFile.cs`, `GraphValidator.cs`, `FileTools.cs`, `tools/Patina.SampleGraphs` |
| 4 | T-141 check | Sonnet | T-134, T-137, T-138, T-139 | `CheckTool.cs`, `Patina.Graphs/Checks/` |
| 5 | T-142 library stage in scenario-patina | Sonnet (+ frame review) | T-140, T-141, T-155, T-156, T-157 | `LibraryStage.cs`, `samples/graphs/*/swatch.png` |
| 6 | T-143 acceptance | Sonnet (inner sessions on the default, Opus) | T-142 | its round folders and checks |

Dependencies on a sibling's feature: T-137's gradient map needs T-134's list parameters, and its sample T-134's
`metallic` and named outputs. T-138's floor tiles need T-136's `tiles` and T-134's `height`. T-139's content edits
T-136's bricks. T-140's grime uses T-135's `fbm`. T-141's all-samples test covers T-140's samples once both have landed.
Shared lines that only add (`NodeRegistry.BuiltIn`, `Program.Tools`, `define_graph`'s node list, CLAUDE.md's Layout)
are settled by keeping both sides.

No engine need was found. No sound or motion is added, so no listening or real-time pass is needed. The milestone is
reached from a plain `patina` launch, through its MCP tools.

## Required tasks

- [ ] [T-133](../tasks/T-133-tiled-texture-graph-evaluation.md) — Patina.Graphs evaluates in square tiles, each node naming the input rectangle it needs, under a capped tile cache, with every existing output byte-identical
- [ ] [T-134](../tasks/T-134-metallic-height-and-named-outputs.md) — Texture graphs gain metallic (packed into the glTF metallic-roughness image), height and named outputs, list parameters and format version 2's machinery, with export and the swatch sheet showing them
- [ ] [T-135](../tasks/T-135-worley-fbm-and-domain-warp.md) — worley, fbm and warp nodes, and samples/graphs/cobblestones.texgraph with its PNGs
- [ ] [T-136](../tasks/T-136-tiles-bricks-stripes-and-shapes.md) — tiles (bricks with gaps and jitter), stripes and shape nodes, and the bricks and fabric sample graphs with their PNGs
- [ ] [T-137](../tasks/T-137-blur-and-gradient-map.md) — blur and gradient_map nodes, and samples/graphs/painted-metal.texgraph with its PNGs
- [ ] [T-138](../tasks/T-138-transform-and-distance.md) — transform (offset, tile, mirror, rotation, scale) and distance-from-an-edge nodes on the tiled evaluator, and samples/graphs/floor-tiles.texgraph with its PNGs
- [ ] [T-139](../tasks/T-139-exposed-graph-parameters.md) — A graph declares named parameters that drive its node parameters, and swatch and export take overrides, so one graph gives variants; the bricks sample exposes mortar, tint and jitter
- [ ] [T-140](../tasks/T-140-graphs-as-nodes.md) — A graph node uses another .texgraph by path, its exposed parameters set by name and its input nodes as ports; grime and old-wall sample graphs
- [ ] [T-141](../tasks/T-141-the-check-tool.md) — patina's check lists texture faults in the open graph's outputs or in PNG files: seams, base colour out of range, metallic not near 0 or 1, normals not of unit length, and an output of one value
- [ ] [T-142](../tasks/T-142-the-node-library-in-scenario-patina.md) — scenario-patina's library stage opens, swatches, checks and exports every sample material and variant, with every pixel worked from its texels, and commits each sample's swatch sheet
- [ ] [T-143](../tasks/T-143-node-library-acceptance-run.md) — Fresh agents make a woven rug, rusted corrugated iron and a mossy flagstone path on patina from the tool descriptions alone, and fix what check reports on a planted graph
- [ ] [T-153](../tasks/T-153-data-dependent-input-requests-for-warp.md) — A node can ask for an input's rectangle after reading another input, so warp at a large amount reads the few tiles its offsets reach
- [ ] [T-155](../tasks/T-155-separable-passes-and-rotated-read-locality.md) — A wide neighbourhood node runs as a cached row pass and a column pass, so blur and distance at 8192 meet 10 s within the cap, and a free rotation stops recomputing its source
- [ ] [T-156](../tasks/T-156-a-sphere-tiling-option-on-swatch.md) — swatch takes a sphere tiling, default 1, so a material can be judged on the sphere at a texel density nearer a game's; the samples' sheets use 3
- [ ] [T-157](../tasks/T-157-a-cube-view-on-swatch.md) — swatch gains a cube view, with the same tiling option as the sphere, so a material can be judged on flat faces and edges as well as a curve

## Retrospective
