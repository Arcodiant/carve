---
description: >-
  Patina.Graphs: the engine-free .texgraph format read back bit-exact, deterministic banded evaluation, and faults that name the node and port
---
# T-110 — Texture graph format and evaluator

**Status:** Done

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit

## Goal

The engine-free package every texture graph goes through: `src/Patina.Graphs`, holding the `.texgraph` format, its
validation, deterministic evaluation in bounded memory, the `colour` and `output` nodes, and PNG encoding. The patina
server (T-113) and, from M-29, Carve both evaluate graphs through it, so what Carve draws is what patina shows (D-7).

## Scope

- **Projects.** `src/Patina.Graphs` (class library) and `tests/Patina.Graphs.Tests` (xunit v3, `Tier` traits as
  CLAUDE.md says), both in `Carve.slnx`. The package references no `Arcodiant.*` package, no `System.Random` and no
  wall clock; it may reference `StbImageWriteSharp` (`$(StbImageWriteSharpVersion)`) for PNGs. Add a guard test in the
  manner of `tests/Carve.Core.Tests/CoreIsEngineFreeTests.cs`, and add the rule to CLAUDE.md's *Architecture rules*.
- **The file format, `.texgraph`, version 1**: versioned JSON, written and read back bit-exact (write, read, write gives
  the same bytes), as the Carve file is.

  ```json
  {
    "version": 1,
    "size": 256,
    "seed": 7,
    "nodes": {
      "tone":  { "type": "noise", "params": { "cells": [8, 1], "interpolation": "step" } },
      "wood":  { "type": "colour", "params": { "colour": "#aa7040" } },
      "base":  { "type": "blend", "inputs": { "a": "wood", "b": "tone" }, "params": { "mode": "multiply" } },
      "out":   { "type": "output", "inputs": { "in": "base" }, "params": { "role": "base_colour" } }
    }
  }
  ```

  Nodes are keyed by an id the agent chooses (`[a-z][a-z0-9_]*`). An input names `"<id>"`, or `"<id>.<port>"` for a
  node with more than one output port (none in M-27, but the grammar is there). Written in a fixed key order with
  nodes in id order, so a saved file diffs well. `size` is a power of two from 32 to 8192, and `seed` is an unsigned
  32-bit integer.
- **Validation, all at once, never only the first fault.** Each fault names the node id and the port or parameter:
  an unknown type (listing the types), an unknown parameter or one out of range (giving the range), an input naming a
  node that does not exist (listing the ids), a missing required input, a port that does not exist, a cycle (naming
  the nodes on it), a greyscale/colour mismatch the node cannot take, two outputs with one role, and no output at all.
  The fault list is a value (`GraphFault`: node, port or parameter, message) that the server returns as it stands.
  A graph with reference faults still loads, so that `open` can hold it and report them (T-113). JSON that does not
  parse, or a `version` it does not know, is a load error with its position.
- **Node types are registered, not switched on.** A `NodeType` describes the node's ports (name, greyscale or colour,
  required or optional), its parameters (name, kind, range, default), its neighbourhood radius in texels (0 for
  pointwise), and its evaluation. A registry holds the built-in types. M-29's mesh nodes plug in through the same
  registration from Carve, so the package knows nothing of meshes (D-7). T-112 adds five types, one file each, plus
  one line each in the registry.
- **Two node types here:** `colour` (a constant `"#rrggbb"`, sRGB, giving a colour image) and `output` (input `in`,
  parameter `role`: `base_colour`, `normal`, `roughness` or `occlusion`).
- **Evaluation.** It is deterministic: the same graph, size and seed give identical bytes, whatever the machine and
  thread count. Use integer hashing and `float` arithmetic with no `Math.Pow`/`Math.Exp` whose results may differ by
  platform. Prefer exact multiplication, as M-17 did. Every parameter is relative to the image (cells across, repeats
  across, slope per image width), so the same graph at 256 and at 2048 gives the same picture at two resolutions.
  Evaluation runs in **row bands**: a band of an output is pulled through the graph, and each node asks its inputs
  for its own rows plus its radius, with wrap-around, since every texture tiles. No node's full image is held unless
  the size is small enough (choose the threshold, and record it). At 8192 the evaluator holds at most a few bands
  per node: write the bound into the type's doc comment. T-115 tests it.
- **Output encoding.** One function makes each role's RGBA8 image and PNG bytes:
  - `base_colour`: sRGB RGB, alpha 255;
  - `normal`: tangent space, glTF convention (green up), `127.5 + 127.5 * n`;
  - `roughness`: written as a glTF metallic-roughness image (R 255, G roughness, B metallic 0), so a Carve material
    takes it directly as `metallic_roughness_texture`;
  - `occlusion`: grey in R, G and B.

  Rounding is `MidpointRounding.AwayFromZero`, as `TextureSet` rounds. PNGs go through `StbImageWriteSharp`'s
  `ImageWriter` (probed at planning to be deterministic at 2048 and 8192).
- **Tests (unit):**
  - round trip, byte for byte, on a graph using every field;
  - every fault above, with the node and port in the message, and several faults reported from one graph;
  - determinism: the same graph evaluated twice, and with bands of two different heights, gives identical bytes;
  - wrap-around at band edges: a test node with radius 2 gives the same image banded as whole;
  - `colour` and `output`: a `colour` to `base_colour` graph gives exactly that colour; each role's packing;
  - a node registered from outside the package (as M-29's will be) evaluates.
- `tests.json`: add suite `patina-unit` (tier `unit`, `cpu`) running `tests/Patina.Graphs.Tests`. T-113 adds its
  server unit tests to the same suite. CLAUDE.md's Layout: rows for both projects.

## Notes

- **Design-heavy: it settles a format and an API others build on** (T-112's node registration, T-113's tool
  arguments, T-115's banded export, and M-29's mesh nodes). Opus, with a research pass first.
- Decided at planning (2026-11-17, by the person): the server is named `patina`; the file is versioned JSON with the
  extension `.texgraph`; sizes are powers of two up to 8192; the seven first node types are noise, gradient, levels,
  blend, colour, height_to_normal and output (T-112 has their parameters).
- Decided at planning by the orchestrator, to be confirmed by the person if it binds differently: the package is
  named `Patina.Graphs`; the output roles and their packing above; a graph with reference faults still loads.
- **Stop and ask** before landing: any change to the file layout above beyond field order or naming within it; a
  parameter that is not image-relative; a fault that is reported without its node. D-7 keeps evaluation apart from
  the image types by a namespace boundary (*No generic graph base yet*): keep it.
- Write down in this file's As built: the registration API (for T-112 and M-29), the band size and memory bound
  (for T-115), and the fault type's shape (for T-113).
- Touch no file outside `src/Patina.Graphs`, `tests/Patina.Graphs.Tests`, `Carve.slnx`, `tests.json` and
  CLAUDE.md. T-111 runs beside this task and edits `Carve.slnx` and CLAUDE.md too: a conflict there only adds, so
  keep both sides.
- **Verify line:** at planning, `cairn lint` accepts only suites already in `tests.json`, so this file names none it will add. Once patina-unit is in `tests.json`, add it to this file's `**Verify:**` line (creating the line if there is none), with the task's other suites.

### Plan (research pass, Opus 5.5)

- **Namespaces.** `Patina.Graphs` holds the format (`TextureGraph`, `GraphNode`, `ParamValue`, `TexGraphFile`), the
  faults (`GraphFault`, `GraphLoadError`) and validation; `Patina.Graphs.Nodes` the registration API (`NodeType`,
  `InputPort`, `ParamSpec`, `NodeRegistry`) and the built-in types, one file each; `Patina.Graphs.Evaluation` the
  banded evaluator; `Patina.Graphs.Imaging` the pixel side (`Band`, `PortKind`, `OutputEncoding`, `Png`), which
  references nothing of graphs. That is D-7's boundary: imaging below, graphs above, so a split is mechanical.
- **Format layout** exactly as the Scope's example. Keys written in a fixed order: `version`, `size`, `seed`,
  `nodes`; in a node `type`, `inputs`, `params` (the last two left out when empty); nodes, inputs and params in
  ordinal key order, so the bytes do not depend on which types are registered. One node per line, `\n` endings, a
  final newline. Numbers written shortest round-trip (`"R"`, invariant). Shape errors (not JSON, an unknown or
  missing top-level key, a container of the wrong JSON type, a duplicate key, `version` other than 1, a `size` that
  is not a power of two in 32..8192, a `seed` outside uint32) are a `GraphLoadError` with line and column; every
  thing about what the nodes say is a fault and the graph still loads.
- **Values.** A parameter is a number, a string or an array of numbers (`ParamValue`); a `ParamSpec` says which and
  checks it: `Int`, `Float`, `Enum`, `IntPair`, `Colour` (`#rrggbb`). A missing parameter takes its default.
- **Registration.** `NodeType` is an abstract class: `Name`, `Inputs` (`InputPort`: name, `PortKind` Grey/Colour/Any,
  required), `Outputs` (port names, default `["out"]`), `Parameters`, `Radius(parameters, size)` (texels, default 0),
  `OutputKind(port, inputKinds, parameters)`, an optional `CheckInputKinds` for a rule a port kind cannot say (the
  output node's role), and `Evaluate(NodeEvaluation)`. `NodeRegistry` is immutable: `NodeRegistry.BuiltIn` is made
  from one list (one line per type; T-112 adds its five), and `registry.With(type)` gives a new registry with an
  outside type, so M-29's mesh nodes need no package change and there is no global mutable state.
- **Faults.** `GraphFault(string? Node, string? Port, string? Parameter, string Message)`, the message naming the
  node and port or parameter itself, so the list can be returned as it stands. Only "no output at all" has no node,
  having none to name.
- **Evaluation.** Single-threaded, `float`, integer hashing, no `Math.Pow`/`Exp`, so the bytes are the same whatever
  the machine. Colour channels hold sRGB-encoded values (as the `#rrggbb` they come from, and as T-77 multiplied
  them), so no transfer function is needed. An output is made band by band: for each band the evaluator works, in
  reverse topological order, each node's row range (the hull of every consumer's range widened by the consumer's
  radius), then evaluates the nodes forwards into one `Band` each and drops them all before the next band. Rows are
  logical and wrap modulo the size, columns wrap inside the node, so every texture tiles.
- **Band size:** `BandTexels = 2^18`, band rows `max(1, 2^18 / size)`: sizes up to 512 are evaluated whole (the
  threshold), 1024 in bands of 256 rows, 8192 in bands of 32. Bound: at any moment each node holds one band of at
  most `bandRows + 2 * (sum of radii downstream)` rows of the full width, i.e. about 1 MB (grey) or 3 MB (colour)
  per node at 8192 with no radius, plus the output's RGBA8 image the PNG encoder needs (256 MB at 8192).
- **Output encoding:** `OutputEncoding.Encode(role, size, evaluator)` gives the RGBA8 image and the PNG bytes, packed
  as the Scope says, rounding `MidpointRounding.AwayFromZero`.

### As built (Opus 5.5)

Built to the plan above; the file layout is the Scope's, unchanged. 42 `unit` tests in `tests/Patina.Graphs.Tests`,
about 2.5 s, suite `patina-unit`.

- **Registration API (for T-112 and M-29).** Subclass `Patina.Graphs.Nodes.NodeType`, one file per type:
  `Name`; `Inputs` (`new InputPort("a", PortKind.Grey | Colour | Any, required: true)`); `Outputs` (default
  `["out"]`; several make `"id.port"` sources required); `Parameters` (`ParamSpec.Int(name, min, max, default)`,
  `Float`, `Enum(name, default, choices...)`, `IntPair(name, min, max, x, y)`, `Colour(name, "#rrggbb")`; a spec
  checks values and writes the range into the fault); `Radius(parameters, size)` (texels, default 0);
  `OutputKind(port, inputKinds, parameters)`; optionally `CheckInputKinds` for a rule a port's kind cannot say; and
  `Evaluate(NodeEvaluation e)`, which fills logical rows `e.Y0 .. e.Y0 + e.Rows - 1`, every column, of
  `e.Output(port)` from `e.Input(port)` (null when an optional input is absent), reading parameters through
  `e.Parameters.Int/Float/Text/IntPair/Colour(name)` (the default where the file gives none). Image row is
  `Band.Wrap(y, e.Size)`; neighbours across the left/right edge come through `Band.Grey/Colour(x, y)`, which wrap x;
  `Band.Colour` spreads a greyscale band to grey. The graph's seed is `e.Seed`. Built-ins are the list in
  `NodeRegistry.BuiltIn` (T-112: one line per type). From outside, `NodeRegistry.BuiltIn.With(new MyNode())` gives a
  new registry; pass it to `TexGraphFile.Read`, `GraphValidator.Validate` and `new GraphEvaluator`. There is no
  global mutable registry.
- **Band size and memory bound (for T-115).** `GraphEvaluator.BandTexels = 2^18`; band rows
  `BandRowsFor(size) = clamp(2^18 / size, 1, size)`: up to 512 the image is one band (the threshold), 1024 → 256
  rows, 2048 → 128, 8192 → 32. For each band every node the output depends on holds one band of
  `bandRows + 2R` rows, full width, R the sum of radii on its way to the output, all dropped before the next band: at
  8192, 1 MB per greyscale node and 3 MB per colour node (+ 64 KB a row of radius). The only full-size buffer is the
  caller's: `OutputImage.Make` keeps the RGBA8 image (256 MB at 8192) for the PNG encoder. `GraphEvaluator.LastRun`
  (`EvaluationRun(BandRows, MostRows, PeakBytes)`) reports what an evaluation held, for T-115's test; the bound is in
  `GraphEvaluator`'s doc comment.
- **Fault shape (for T-113).** `GraphFault(string? Node, string? Port, string? Parameter, string Message)`; the
  message names the node and the port or parameter itself (`node 'base' input 'mask': names 'grain_2', which is not
  a node; the nodes are ...`), so it can be returned as it stands. `TexGraphFile.Read(text, registry)` gives
  `TexGraphReadResult(Graph, Error, Faults)`: a `GraphLoadError(Line, Column, Message)` (1-based) for a file that is
  not JSON, has not the shape, or has an unknown version, a bad size or seed; otherwise the graph with all its faults.
  `GraphValidator.Validate(graph, registry)` re-checks a graph after an edit; `new GraphEvaluator` throws
  `GraphFaultException` (with `.Faults`) on a faulty graph.
- **Encoding.** `OutputImage.Make(evaluator, role, size)` gives `Rgba` and `Png`; packing is
  `Imaging.OutputPacking` (base_colour sRGB, normal `127.5 + 127.5 n`, roughness R 255 / G / B 0, occlusion grey),
  rounding away from zero. Colour channels in the graph hold sRGB-encoded values (no transfer function, so no `Pow`).
- **Namespaces (D-7's boundary).** `Patina.Graphs.Imaging` (bands, roles, packing, PNG) references nothing of graphs;
  the format, validation, nodes and evaluator sit above it.
- **Judgement calls, for the person to overrule.** "No output at all" is the one fault with `Node` null, having no
  node to name (the Scope lists it; the stop on node-less faults is read as not covering it). A bad `size`/`seed`
  is a load error rather than a fault, so that no other graph-level fault exists. An `output`'s `role` defaults to
  `base_colour`; `colour`'s `colour` to `#808080`. `base_colour` takes greyscale too (spread to grey); `normal`
  needs colour, `roughness` and `occlusion` greyscale. Unknown keys in a node, or a parameter of a JSON kind that is
  not a number, string or array of numbers, are load errors (shape), not faults.
- **Not checked here:** evaluation speed at 8192 (T-115). Texel access goes through bounds-checked `Band` methods;
  a node that needs speed can work on `Band.Row(y)` spans directly.
