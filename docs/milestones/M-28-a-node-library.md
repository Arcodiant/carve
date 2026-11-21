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
| 7 | T-170 tool descriptions read whole (added 2026-11-20) | Opus | T-143 | `DefineGraphTool.cs`, `NodeCatalogue`, any new tool |
| 7 | T-171 edit_graph takes list parameters (added 2026-11-20) | Sonnet | T-143 | `EditGraphTool.cs` |
| 8 | T-172 scenario-patina under its time limit (added 2026-11-20) | Sonnet | T-170, T-171 | `scenarios/Carve.Scenario.Patina/`, `tests.json`'s patina suites |
| 9 | T-173 RV-76 answers (added 2026-11-21) | Opus | — | see its Notes |
| 9 | T-174 RV-76 answers (added 2026-11-21) | Opus | — | see its Notes |
| 9 | T-175 RV-76 answers (added 2026-11-21) | Opus | — | see its Notes |
| 9 | T-176 RV-76 answers (added 2026-11-21) | Opus | — | see its Notes |
| 9 | T-177 RV-76 answers (added 2026-11-21) | Opus | — | see its Notes |

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
- [ ] [T-155](../tasks/T-155-separable-passes-and-rotated-read-locality.md) — A wide neighbourhood node runs as a cached row pass and a column pass, so blur at 8192 takes about 2 minutes, not an hour, and distance 18 s, not 474, within the memory cap (the 10 s target moved to T-159), and a free rotation stops recomputing its source
- [ ] [T-156](../tasks/T-156-a-sphere-tiling-option-on-swatch.md) — swatch takes a sphere tiling, default 1, so a material can be judged on the sphere at a texel density nearer a game's; the samples' sheets use 3
- [ ] [T-157](../tasks/T-157-a-cube-view-on-swatch.md) — swatch gains a cube view, with the same tiling option as the sphere, so a material can be judged on flat faces and edges as well as a curve
- [ ] [T-170](../tasks/T-170-tool-descriptions-an-agent-can-read-whole.md) — patina's tool descriptions reach an agent whole, with the node catalogue no longer crammed into define_graph's
- [ ] [T-171](../tasks/T-171-edit-graph-takes-list-parameters.md) — edit_graph's set_params and set_node take list parameters as define_graph does
- [ ] [T-172](../tasks/T-172-scenario-patina-back-under-its-time-limit.md) — scenario-patina runs well inside run-scenario's 300 s guard again: the library stage is split into two halves, suites `scenario-patina-library-1` and `scenario-patina-library-2`, beside `scenario-patina` for the other stages, with no check and no approved frame lost
- [ ] [T-173](../tasks/T-173-rv-76-answers-the-evaluation-engine.md) — Every finding of RV-76's `## Findings: the evaluation engine` is answered, fixed where it is a defect, with outputs byte-identical
- [ ] [T-174](../tasks/T-174-rv-76-answers-the-nodes-and-check-rules.md) — Every finding of RV-76's `## Findings: the nodes, check rules and samples`, and the check-tool findings of the server's, is answered
- [ ] [T-175](../tasks/T-175-rv-76-answers-the-file-format-and-graph-model.md) — Every finding of RV-76's `## Findings: the file format and graph model` is answered, fixed where it is a defect
- [ ] [T-176](../tasks/T-176-rv-76-answers-the-patina-server.md) — The patina server's RV-76 findings on save, the swatch sheet, export and override wording are answered, fixed where they are defects
- [ ] [T-177](../tasks/T-177-rv-76-answers-the-scenarios-and-docs.md) — Every finding of RV-76's `## Findings: the scenarios and test configuration` and `## Findings: the whole diff` is answered

## Retrospective

**As planned.**

- **The library is there and reachable from a plain `patina` launch.**
  - **New nodes:** worley, fbm and warp (T-135); tiles, stripes and shape (T-136); blur and gradient_map (T-137);
    transform and distance (T-138).
  - **New outputs:** metallic, height and named outputs (T-134).
  - **Variants:** exposed parameters with overrides on swatch, export and check (T-139).
  - **Graphs as nodes,** with `input` nodes and parameters that pass through (T-140).
  - **The check tool** (T-141).
  - **Samples:** seven, plus a grey variant of the bricks, each look approved by the person before its task landed.
    They are the cobblestones, bricks, fabric, painted metal, floor tiles, grime and the old wall.
- **Tiled evaluation first (T-133) held.** Every committed PNG and frame stayed byte-identical through it, and the
  planks at 8192 got 4% faster (187.6 s against 196.0 s, Debug, medians of five).
- **Acceptance (T-143).** Fresh agents made all three materials from the tool descriptions alone, each with an exposed
  parameter and a variant, and `check` clean:
  - the woven rug in 25 calls;
  - the rusted corrugated iron in 18;
  - the mossy path, with a moss filter graph used as a node, in 19.

  A fourth agent found all three planted faults from one `check`, kept all 14 node ids and saved it clean, in 11 calls.
  The person approved every sheet.
- **`cairn milestone M-28 verify`** (2026-11-21, after T-172): every check passes but `review`, which comes after the
  person's go.
  - 18 of 18 members done and merged.
  - 25 suites passed, 19 ran and 6 carried, `server-regression`, `patina-regression` and every scenario among them.
- No engine need was recorded.

**Not as planned.**

- **Eighteen members, not eleven.** Seven were added mid-milestone, each by the person's ruling on a stop:
  - **T-153, data-dependent input requests.** A warp at amount 0.25 at 8192 was on course for about 55 minutes, because
    a static input rectangle had to cover the largest offset. It now takes 26.9 s, and the cobblestones became 36 to
    63% faster.
  - **T-155, separable passes and rotated-read locality.**
    - **The misses it fixed:** blur at 8192 was about an hour and distance 474 s, against the 10 s the plan set.
    - **The other fixes:** a 30-degree turn made its source about 2.4 times over, and the strong minify needed 4.3 GB.
    - **The figures now:** blur 90.9 s, distance 15.1 s, the turn 12.1 s with its source made 1.09 times, and the
      minify 1.5 GB in Debug.
    - It took five Opus agents.
  - **T-156 and T-157, the sphere's tiling and a cube view on swatch.** Both were asked for by the person in reviews;
    the samples' sheets are drawn at tiling 3.
  - **T-170, descriptions an agent can read whole.** Claude Code cuts each MCP tool description at 4,096 characters.
    `define_graph` was 17,908, so T-143's agents saw 6 of 19 node types and learned the rest by sending wrong graphs.
    - **The fix:** a `nodes` tool, and every description held at 3,686 or under by a test.
    - **The re-run:** the rug took 10 calls, none probing, against 25.
  - **T-171, edit_graph takes list parameters.** T-143's fix session had to redefine a whole graph to change one
    gradient stop.
  - **T-172, scenario-patina under its time limit.** The milestone's first verify was stopped by run-scenario's 300 s
    guard after T-142's library stage.
    - **Why a split:** export and check cost about 20 s of 220; the rest is the stage's own per-pixel checking.
    - **The suites now:** `scenario-patina`, `scenario-patina-library-1` and `scenario-patina-library-2`, at 52, 80 and
      82 s alone.
- **The 10 s targets for blur and distance at 8192 were not met.** Blur's own share is 120 s and distance's 17.6 s
  (Release). The person landed T-155 without them and filed **T-159** (strip passes, off-milestone) to own them.
- **Rules changed from planning, each by the person:**
  - **`does-not-tile`** compared one wrap step with the mean interior step, and so flagged the committed planks, whose
    stave edge falls on u = 0. It now judges the wrap against the 99th percentile of the interior boundaries.
  - **T-134's metallic check.** Both spheres peak at 255 under carve's lights, so the planned peak measure could not
    work. The check uses the median inside the silhouette and the count of pixels at 255 instead.
  - **Transform's scale.** 1/n tiles; a whole number above 1 zooms in and does not tile. No meaning of scale lets both
    tile.
  - **The Debug regression tests at 8192 assert only the 2 GB peak.** The 10 s checks are measured in Release.
- **Smaller rulings:**
  - gradient_map allows equal stops;
  - a graph parameter may drive an `IntPair` but not a list;
  - ranges round inward;
  - nested-graph faults use the `nodes['a/b'].params['c']` form;
  - the sphere's correlation bars are 0.6, and 0.62 for metal;
  - T-138's floor-tiles test bound was loosened, with approval.
- **A gap between two members.** T-141 added `check` to the tool list, and scenario-patina's graph stage, which
  checks that list exactly, then failed. T-141's `**Verify:**` named only `patina-unit`, so nothing caught it until
  T-142 fixed it.
- **The default model moved.** With no `--model`, `claude -p` now starts Fable 5.2, not Opus 5.5. The person had T-143's
  sessions pinned to `claude-opus-5-5`.
- **Interruptions:**
  - Waystone closed twice with an agent under way (T-155 and T-143); both were resumed from what was on disk.
  - T-172's Sonnet agent hit the weekly usage limit, and its last steps ran on Opus.
- **Backslashes eaten in transit** spoiled an `.mcp.json` (T-143's first launch did nothing), C# string escapes
  (T-157) and a path in T-157's As built, which the orchestrator repaired.
- **Hand-offs** were many:
  - T-155: four;
  - T-133, T-138 and T-140: two each;
  - T-134, T-137, T-139, T-141, T-157, T-170 and T-172: one each.

**Never verified.**

- **Frames nobody opened:**
  - `swatch-sheet-grid-tiling-3.png` differs from T-140's job. T-172 traced the difference to work after that job;
    T-177 (RV-76) found it: the 1,600 pixels that differ lie at x 536 to 615, y 27 to 46, inside the sphere cell's label
    band, where T-157 (9b207c2) added ", tiling 3" to the sphere's label. The cell below the band is now judged against
    the sphere view at tiling 3, pixel for pixel.
  - The re-run rug's sheets (T-170).
  - The library frames after T-172's split, which were held by hash only.
- **Not measured after the last evaluator change** (T-155's bands of 32):
  - the turn ratio, measured at 1.16 with bands of 16;
  - any Debug peak, with bands of 32.

  Distance's own time at 2048 was not recorded either.
- **The metal sphere bar has 0.04 of margin** (0.661 against 0.62).
- **Swatch's tiling relies on undocumented engine behaviour.** It uses a `UvRect` larger than 1, which Empyrean 0.129.0
  draws as a repeat although its docs say components lie in [0, 1]. A scenario check would catch a change, but no
  engine need was recorded.
- **Untested since T-171:** a list given as a parameter override, now read since T-171, is expected to be refused by
  kind, but no test holds it. T-143's fix session was not re-run after T-171.
- **The 90-degree sphere at tiling 3** is not distinguished on T-156's stripe grid.
- **The M-7 barrel textures fail `check`.** These are reported, not judged:
  - iron: base colour and roughness seams in u, and metallic not binary in 8.7% of texels;
  - staves: a small v step;
  - end grain: the normal off unit length in 0.62% of texels.

  No one has looked at those seams.
- `ExportToolTests.Other_tools_answer_while_an_export_runs` timed out once on one runner (T-139) and passed on re-run.

**Known and left.**

- **T-159** (strip passes and the 10 s targets) and **T-169** (cube captures in the library stage), both off-milestone.
- **T-130** (faster 8192 evaluation) was held for T-133 and now wants re-scoping against the tiled, banded evaluator.
- **T-129** (a heavy tier for 8192 tests) matters more now: patina-regression ran 1,162 s in the last verify.
- CLAUDE.md and `tools/Patina.SampleGraphs/Program.cs` are inputs of no suite (verify warns).
- **Files for the person to delete:**
  - the unregistered member worktree folders `branches/task/T-133` to `T-172`;
  - an empty `D:\Users\andre\AppData\Local\Temp` made by a mistyped path (T-133);
  - a stray `t157-review-err.log` in `G:\My Drive\artifacts\carve\M-28\T-157\`.

**Models.**

| Member | Model | Agents |
|---|---|---|
| T-133 | Opus 5.5 | 3 (research, build, finish) |
| T-134 | Opus 5.5 | 2 |
| T-135 | Sonnet 5.5 | 1 |
| T-136 | Sonnet 5.5 | 1 |
| T-137 | Sonnet 5.5 | 2 |
| T-138 | Opus 5.5 | 3 |
| T-139 | Opus 5.5 | 2 |
| T-140 | Opus 5.5 | 3 |
| T-141 | Sonnet 5.5 | 2 (a stop on the tile rule, then the build) |
| T-142 | Sonnet 5.5 | 1 |
| T-143 | Sonnet 5.5; inner sessions Opus 5.5 | 1 (resumed once) |
| T-153 | Opus 5.5 | 1 |
| T-155 | Opus 5.5 | 5 (resumed once) |
| T-156 | Sonnet 5.5 | 1 |
| T-157 | Sonnet 5.5 | 2 |
| T-170 | Opus 5.5; probe and re-run Opus 5.5 | 2 |
| T-171 | Sonnet 5.5 | 1 |
| T-172 | Sonnet 5.5, then Opus 5.5 (usage limit) | 2 |

No rework was traced to the Sonnet tier. The Sonnet members' stops (T-137's blur speed, T-141's tile rule) were real
findings, put to the person correctly. The extra agents went on performance work on Opus, mostly T-155.

**For the next plan.**

- **Probe every wide node at 8192 at planning.** Do it before setting a time target: warp at its largest amount, blur
  at its largest radius, distance, a free rotation, a strong minify. M-27's retrospective said to measure an 8192 claim
  on the real graph; the 10 s targets here were set without a probe, and three members (T-153, T-155, T-159) came from
  them.
- **Hold every MCP tool description under 4,096 characters from the start,** by a test, in carve too. Check carve's
  descriptions against the cap now.
- **A member that changes the tool list names every suite that checks it** in its `**Verify:**` line.
- **Size a scenario at planning.** A stage that adds many frames and per-pixel checks gets its own suite before the
  milestone's verify finds the guard.
- **Pin `--model` in every acceptance run's conditions,** since the default moves.
- **Brief every agent that writes configs, docs or code through the shell about backslashes** (`chr(92)`), as later
  briefs here did.
- **Expect performance members to hand off repeatedly.** Each long measurement costs a checkpoint, so plan them as a
  research agent plus a build agent with the harness written down for the next.
