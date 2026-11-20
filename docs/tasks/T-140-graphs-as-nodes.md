---
description: >-
  A graph node uses another .texgraph by path, its exposed parameters set by name and its input nodes as ports; grime and old-wall sample graphs
---
# T-140 — Graphs as nodes

**Status:** Open

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
