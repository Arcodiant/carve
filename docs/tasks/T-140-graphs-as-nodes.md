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

### Hand-off

Written by Claude Opus 5.5 (`claude-opus-5-5`), the first agent, at its call checkpoint (about 55 calls). Code and unit
tests committed; the server side's tests, the worked example, the scenario stage, the docs and the review are left.

**Done (committed):**
- *Nodes* (`src/Patina.Graphs/Nodes/InputNode.cs`, `NestedGraphNode.cs`, both in `NodeRegistry.BuiltIn`, version 2 through
  `TexGraphFormat.Current.WithNodeType`): `input` (`name`, `kind` grey/colour, optional `default` as the new
  `ParamKind.GreyOrColour`); `graph` (`path`, the new `ParamKind.FilePath`). `input` has one optional input port, `value`,
  which only expansion wires (a file wiring it is a fault): when wired the node passes its input through, else it fills
  its default. That keeps a kind mismatch on the outer graph's input named on the graph node's input, with no cascade.
- *Where a path resolves*: `TextureGraph.Folder` (an init property, never written; `TexGraphFile.ReadFile` and
  `Read(..., folder)` set it, null is the working directory). `GraphStore` (now built with `ToolFolder`) sets it on every
  graph it takes: the file's folder once saved or opened, else the tool folder. `GraphValidator.ResolvePath(graph, path)`
  adds `.texgraph` when missing. **M-29**: a Carve material naming a graph should read it with `TexGraphFile.ReadFile`
  (or set `Folder` to the material file's folder); nothing else is needed, the validator resolves nested files itself.
- *Expansion* (`GraphValidator.Analyse`, recursive with the chain of files and the depth): each `graph` node is read with
  `TexGraphFile.Parse` (new, internal: no validation), its other params checked one by one against the nested graph's
  parameters (the spec's wording: `nodes['wall'].params['mortar']: is 0.5, but must be ... (bricks.texgraph, parameter
  'mortar')`), set through `GraphParameters.WithOverrides`, and the nested graph analysed on its own. Its faults come
  back renamed `wall/layout` with ` (in bricks.texgraph)` and `GraphFault.File` (new) set; a use with faults is left out
  and sources naming it are not checked further (no cascade). Otherwise its nodes (`GraphAnalysis.Flat()`: sources
  resolved to `"id.port"`, driven parameters set, outputs dropped) are added as `<node>/<id>`, and a source naming
  `wall.base_colour` resolves to the nested output's own source. Expanded ids pass `GraphNames.IsValidPath` and their
  sources `TryParsePathSource`; a source a file writes is parsed strictly, so `g/mix` is "not a source". The evaluator
  is unchanged and uses the outer graph's seed and size (it only ever read `Graph.Seed` and the size it is given).
- *Faults*: missing file (the `.texgraph` files in its folder, as open lists them), cycle through files (the top file's
  own name is not known to the graph, so a cycle back to the top is found one level down: `u/u/u/u`), unknown parameter,
  input or output, kind mismatch, an input with no default left unconnected, nesting deeper than 8
  (`GraphValidator.MaxDepth`), a bad path, two inputs of one name. An `input` with no default on the graph's own level is
  a fault with `GraphFault.OnItsOwn = true` (new): the evaluator refuses it, `save` writes it.
- *Tools*: `get_graph` lists graph nodes (resolved file and what it resolved against, parameters with ranges, inputs,
  outputs) through `GraphValidator.Uses` (new public `NestedGraphUse`, `NestedGraphInput`); `open` reads with the
  file's folder and names the nested files it read; `save` ignores `OnItsOwn` faults; `NodeCatalogue` lines for `graph`
  and `input`. `swatch` and `export` evaluate through the expansion with no change to them.
- *Samples*: `samples/graphs/grime.texgraph` (input `in` colour `#808080`, `height` grey 0.5; `fbm` cells [24, 2]
  streaks down v, mixed toward `#2e2a24` more at low height, `amount` 0.5 drives the mix's opacity) and
  `old-wall.texgraph` (bricks at mortar 0.06, grime at amount 0.6 on its base colour, **grime's height wired from
  `wall.occlusion`**, since bricks has no height output). PNGs in `samples/graphs/grime/` and `old-wall/` from the
  sample tool, which skips a graph whose only faults are `OnItsOwn`. Every other committed PNG is unchanged
  (sha256 of all `samples/graphs/*/*.png` and variants compared before and after).
- *Tests* (`tests/Patina.Graphs.Tests`): `NestedGraphTests` (expansion equal to the nodes by hand, a warp among them, at
  three tile sizes; three broken copies; the node's parameter equal to the T-139 override on bricks; paths from two
  folders; every fault; a three-file cycle; depth 8 passes and 9 fails; `Uses`), `NestedSamplesTests` (PNGs equal the
  graphs; old-wall's normal, roughness and occlusion equal bricks at mortar 0.06; check's rules measured: grime base
  colour brightest channel 97..128, old-wall 79..196, none outside 20..245; old-wall normals worst |len-1| 0.0050;
  wrap steps below the interior's; old-wall mean level 94.1 vs 107.5 clean, chroma 70.9 vs 83.0).
  `GraphFaultTests`' type list gains `graph` and `input`. Patina.Graphs.Tests 328 pass, Patina.Server.Tests unit 114 pass.

**Left:**
- `define_graph`'s description: a worked example using `bricks.texgraph` as a node, run by a server test that copies
  the sample beside its temporary graph; server tests for get_graph's lines, open's nested-file line, save writing a
  graph with an input lacking a default, and swatch/export of old-wall.
- A scenario stage (a file and one line in `PatinaStages.cs`): old-wall opened, swatched and exported through patina.
- D-7's rule (nested graphs expanded inline at validation, faults named by path, `input` nodes as ports); CLAUDE.md's
  Layout lines (`Patina.Graphs`, `Patina.Server`, `tools/Patina.SampleGraphs`, the two test classes).
- The review: the grime and old-wall PNGs and a swatch sheet with its sphere through patina's `swatch`, to the person
  (`G:\My Drive\artifacts\carve\M-28\T-140\round-1`). No image has been opened by this agent.
- `### As built`.

**For the person (not settled by the spec):** the wording of a fault inside a nested graph (`node 'wall/layout' parameter
'gap' ... (in bricks.texgraph)`, the node faults' own style with the path, where the spec writes
`nodes['wall/layout'].params['gap']`); grime's height taken from bricks' occlusion; an outer exposed parameter cannot
yet drive a graph node's parameter (`wall.mortar` is "no such parameter"), which the spec does not ask for.
