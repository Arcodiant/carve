---
description: >-
  Patina.Graphs: the engine-free .texgraph format read back bit-exact, deterministic banded evaluation, and faults that name the node and port
---
# T-110 — Texture graph format and evaluator

**Status:** Open

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** — (none)

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
