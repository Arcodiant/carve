---
description: >-
  A graph node uses another .texgraph by path, its exposed parameters set by name and its input nodes as ports; grime and old-wall sample graphs
---
# T-140 — Graphs as nodes

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-139, T-135, T-153

**Verify:** patina-unit, scenario-patina

## Goal

A graph uses another graph as one node: a generator (the bricks) or a filter (grime applied to any base colour). Its
exposed parameters are set on the node by name, its `input` nodes are the node's input ports, and its outputs are the
node's output ports.

## Scope

- **`input` node** (format version 2): parameters `name` (`[a-z][a-z0-9_]*`), `kind` `grey` or `colour`, and an
  optional `default` (a grey value or `"#rrggbb"`). One output port. A graph whose inputs all have defaults evaluates
  on its own (swatch, export, check use the defaults). One with an input lacking a default cannot be evaluated on its
  own: the fault names the input, and it can still be saved and used as a node.
- **`graph` node** (format version 2): `path` to a `.texgraph`, relative to the using graph's file. A graph that has
  never been saved resolves against the tool folder, and `get_graph` says which.
  - Its other parameters are the nested graph's exposed parameters, by name, faulted against the nested graph's ranges
    ("nodes['wall'].params['mortar']: is 0.5, but must be a number from 0 to 0.2 (bricks.texgraph, parameter
    'mortar')").
  - Its inputs are the nested graph's `input` nodes, by name and kind.
  - Its outputs are the nested graph's outputs, by role or name (`wall.base_colour`, `wall.height`, `wall.wear`).
- **Evaluation by expansion**: at validation, each `graph` node is expanded inline, its nodes renamed `<node>/<id>`
  (`wall/layout`), with its parameters substituted through T-139's resolution. The evaluator, tiles and memory are
  therefore unchanged. A fault inside a nested graph names the path from the top (`nodes['wall/layout'].params['gap']`)
  and the file it is in.
- **Faults**: a missing file (listing the `.texgraph` files in its folder, as carve's missing texture does, T-81); a
  cycle through files (naming the chain); an unknown parameter, input or output port; a kind mismatch on an input; and
  nesting deeper than 8.
- **The tools**:
  - `get_graph` shows a `graph` node's resolved file, its parameters with ranges, and its ports.
  - `define_graph`'s node list describes `graph` and `input`.
  - `open` reports the nested files it read.
  - `save` writes only the open graph, never a nested file.
  - `swatch`, `export` and (T-141) `check` evaluate through the expansion.
  - A worked example in `define_graph`'s description, run by its tests, uses `bricks.texgraph` as a node. The test copies
    the sample beside its temporary graph.
- `tools/Patina.SampleGraphs` resolves nested paths relative to each graph. A graph with an input lacking a default is
  skipped, with a line saying so.
- **Tests (unit)**: expansion equal, byte for byte, to the same nodes written out by hand in one graph; a parameter set on
  the node equal to the same override through T-139; every fault; relative paths from two folders; a three-file cycle;
  a table of broken copies (a renamed node clashing with a parent's id, a parameter not passed down, an input wired to
  the wrong port) and what caught each.
- **Content**:
  - `samples/graphs/grime.texgraph`: a filter with input `in` (colour, default `#808080`) and a `height` input (grey,
    default 0.5). It darkens and desaturates toward low height with `fbm` streaks running down v, and exposes `amount`.
  - `samples/graphs/old-wall.texgraph` uses `bricks.texgraph` (mortar 0.06) and `grime.texgraph` (amount 0.6) as nodes,
    giving all of the bricks' outputs with grime on its base colour.
  - Both with PNGs from the sample tool (grime at its defaults), kept equal by tests, and each run against check's rules
    in a test. The person looks at the PNGs and sheets before it lands.
- D-7 gains the rule: nested graphs are expanded inline at validation, their faults named by path, and a graph's
  `input` nodes are a nested graph's ports.

## Notes

- **Decided at planning (2026-11-19, by the person):** graphs used as nodes take image inputs through `input` nodes, so a
  reusable graph can be a filter; the nested graph's exposed parameters are set on the `graph` node by name (T-139). The
  expansion inline is this plan's recommendation, which keeps T-133's evaluator and memory bound unchanged. A design
  that evaluates a nested graph separately is a stop.
- Design-heavy, and it decides a format: Opus, and plan for two agents.
- **Write down for T-141 and M-29** (As built): the expansion's naming, how a path resolves, and where in the code a
  Carve material naming a graph (M-29) would resolve its nested files.
- Owns `TexGraphFile.cs`, `GraphValidator.cs`, `FileTools.cs` and `tools/Patina.SampleGraphs` in wave 4. T-141 runs
  beside it and adds `CheckTool.cs`, `Patina.Graphs/Checks/` and one line in `Program.Tools`.
- **Stop and ask:** a rule the spec does not settle (a nested graph's seed or size: the plan's default is that the
  outer graph's seed and size rule, and the nested graph's own are ignored, which the description says), or a committed
  PNG that changes.
- **Ruled (2026-11-20, by the person)**, on the first hand-off's open points:
  - a fault inside a nested graph takes the spec's form, the graph node's own over-range form:
    `nodes['wall/layout'].params['gap']: ... (bricks.texgraph)`, not `node 'wall/layout' parameter 'gap' ... (in bricks.texgraph)`;
  - pass-through parameters are in T-140: an exposed parameter of the outer graph may drive a value on a `graph` node by
    `"node.param"` (`"wall.mortar"`), taking the narrowest range of what it drives, the nested graph's parameter range
    included, faulted as T-139's drives are, tested with a worked case;
  - the other two stand as built: grime's `height` is wired from `wall.occlusion`, and a cycle back to the top file is
    found one level down.

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`) over three agents: the first two built and tested it, the third rebased onto
T-156, checked the frames and took the review.

**First agent:** the `input` and `graph` nodes (`Nodes/InputNode.cs`, `NestedGraphNode.cs`);
`TextureGraph.Folder`, set by `TexGraphFile.ReadFile` and by `GraphStore` (the file's folder once saved or opened, else
the tool folder), `GraphValidator.ResolvePath`; the expansion in `GraphValidator.Analyse`/`Expand` (nodes renamed
`<node>/<id>`, a source `wall.base_colour` resolving to the nested output's own source, no cascade from a use at fault);
every fault the spec lists (missing file listing the folder's `.texgraph` files, cycle naming the chain, unknown
parameter, input or output, kind mismatch, an input lacking a default left unconnected, depth 8, `OnItsOwn` for an
input with no default at the top); get_graph's graph-node lines (`GraphValidator.Uses`), open's nested-file line, save
ignoring `OnItsOwn`; `grime.texgraph` and `old-wall.texgraph` with PNGs; `NestedGraphTests`, `NestedSamplesTests`
(PNGs equal; check's rules measured: grime base colour brightest channel 97..128, old-wall 79..196, none outside
20..245, normals worst |len-1| 0.0050, old-wall mean level 94.1 vs 107.5 clean, chroma 70.9 vs 83.0). Every other
committed PNG unchanged (sha256 compared by the first agent).

**Second agent:**
- *Ruling 1*: `GraphValidator.NestedMessage` rewrites a nested fault as `nodes['wall/layout'].params['gap']: ...`,
  `nodes['wall/x'].inputs['in']: ...` or `nodes['wall/x']: ...`, prefixing again at each level up, with ` (<file>)`
  for the innermost file. Tests updated: the nested fault, and the three-file cycle, now
  `nodes['u/u/u/u'].params['path']: uses b.texgraph, a cycle through files: ... (a.texgraph)`.
- *Ruling 2*: `GraphParameters.Analyse` takes the chain of files; a drive naming a `graph` node's parameter other than
  `path` reads the nested graph's exposed parameter through `GraphValidator.NestedParameters` (null when unreadable, in
  the chain or too deep, so a cycle of drives ends) and uses its `Range`. `Expand` leaves a driven value out of the
  node's own overrides and sets the driver's value. Bricks' `mortar` range is **0 to 0.45** (its layout's gap), not the
  spec's illustrative 0.2. Tests: the worked case (`An_outer_parameter_drives_a_graph_nodes_parameter_with_the_nested_range`:
  equal to bricks with the override, an outer override reaching bricks, written without the node's own value), the
  narrowest range over two graph nodes and `max`, a fault table (out of range, unknown nested parameter, `path`, two
  kinds, an empty range), and drives through a three-file cycle ending with the cycle named.
- *define_graph*: `WorkedNested` and `WorkedPassThrough` in its description, run by `NestedGraphToolTests` with
  `bricks.texgraph` copied beside them. **A bug fixed**: define_graph validated the graph before the store placed it,
  so a graph node in a never-saved graph resolved against the working directory; it now validates `store.Graph`.
  `NodeCatalogue`'s `graph` line names the pass-through.
- *Server tests* (`NestedGraphToolTests`): get_graph's graph-node lines, open's nested files, save writing only the open
  graph (nested files unchanged by bytes), save of a graph whose input lacks a default, export of old-wall equal to its
  sample PNGs, an export override out of bricks' range refused. Swatch needs the engine, so it is in the scenario.
- *Scenario*: `NestedGraphStage.cs` (one line in `PatinaStages.cs`): old-wall opened from samples (both nested files
  named), exported and compared byte for byte, get_graph's graph-node line, and frames `nested-old-wall-sheet` and
  `nested-grime-sheet` (view sheet, which holds the sphere) captured. It passed in verify.
- *Docs*: D-7's rule under "A graph is its own file"; CLAUDE.md's Layout lines for `Patina.Graphs`, `Patina.Server`,
  `tools/Patina.SampleGraphs`, both test projects and `samples/`.

**Review and frames (third agent):** rebased onto `milestone/M-28` after T-156 (one conflict, CLAUDE.md's Layout lines
for `Patina.Graphs` and `Patina.Server`; both kept, T-156's `tiling` clause with T-140's additions). This branch's
`scenario-patina` job J-20261120-085227-39ade3: all 43 frames and exports it shares with T-139's job
J-20261120-070631-363baf equal by SHA-256; 13 new (T-156's 7, this member's 6); the old-wall exports equal the committed
samples byte for byte. The only committed PNGs changed are the five added samples. The person approved grime and the old
wall (round 1: the sample PNGs, both scenario sheets and both sheets at `tiling` 3, drawn with `t140-swatch.py` in Temp).

**For T-141, T-142, T-143 and M-29:**
- *Naming*: a nested graph's nodes become `<graph node>/<id>`, again at each level (`wall/layout`, `u/u/u/u`); a source
  `wall.base_colour` resolves to the nested output's own source, so no node `wall` survives expansion. A fault inside is
  `nodes['wall/layout'].params['gap']: ... (bricks.texgraph)`, the file the innermost one.
- *Paths*: a `graph` node's `path` resolves through `GraphValidator.ResolvePath` (`src/Patina.Graphs/GraphValidator.cs`)
  against `TextureGraph.Folder`, which `TexGraphFile.ReadFile` sets to the file's folder and `GraphStore` sets to the
  saved or opened file's folder, else the tool folder. A Carve material naming a graph (M-29) should read it with
  `TexGraphFile.ReadFile` so `Folder` is set, and nested files then resolve there with no other change.
- *Checks (T-141)*: validation hands on the expanded graph, so a check that evaluates sees no `graph` node; check's
  rules on the two samples are measured in `NestedSamplesTests`.
- *Pass-through*: `GraphParameters.Analyse` takes the chain of files and reads a nested range through
  `GraphValidator.NestedParameters`; bricks' `mortar` is 0 to 0.45.

**Surprising:** backslash escapes written through the Bash tool were consumed several times (a C# `\n` became a
newline); use `chr(92)` or the Edit tool. The verify checkout gives samples CRLF, so a text comparison with a sample
must ignore line endings. In the swatch sheet the sphere's label does not name the tiling (`sphere, Pbr, carve lights,
angle 0` at tiling 3), though the title does; T-156's, not this member's.
