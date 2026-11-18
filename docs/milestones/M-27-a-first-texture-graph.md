---
description: >-
  An agent builds a small node graph in a new texture server, sees it as swatches, exports PNGs and textures the barrel with them
---
# M-27 — A first texture graph

**Status:** Active

**Depends on:** M-20

**One-liner:** *An agent builds a small node graph in a new texture server, sees it as swatches, exports PNGs and textures the barrel with them*

## What this milestone delivers

The thin slice of [D-7](../design/D-7-texture-graphs-and-layered-surfaces.md), end to end: an agent defines a texture graph on a new MCP server, looks at it, exports its outputs as PNGs, and names them in a Carve material with M-7's tools.

- The shared, engine-free texture-graph package: the graph format, deterministic evaluation, and errors that name the node and port at fault.
- Six to eight first nodes, enough for one material: a noise, a gradient, levels, blend, colour, height to normal, and outputs for base colour, normal and roughness.
- A new MCP server with tools to define, edit, read back, save and open a graph.
- Swatch renders: flat, tiled 3 x 3 to show seams, and on a sphere through `Material.Pbr` with Carve's fixed sky.
- PNG export of each output at a given resolution.

Content: a planks graph, committed with its PNGs, that gives M-7's barrel its staves in place of T-77's generated ones.

## Estimated size

About **6 tasks**:

1. Graph format and evaluator in the shared package, with determinism tests.
2. The first nodes.
3. The server project and its graph tools (define, edit, get, save, open).
4. Swatch renders and PNG export.
5. The planks graph, the barrel textured from it, and a scenario.
6. Acceptance: fresh agents build a material from the tool descriptions alone.

## Why

Raised in a discussion with the project owner on 2026-11-17; see D-7's *Intent*. In short: M-7 can only take supplied PNGs, and T-77's barrel textures needed a purpose-written generator.

## Open at planning

- The server's name, the graph file's format and extension (D-7, *Open questions*).
- Which six to eight nodes come first.
- Whether the server keeps an undo history like Carve's.

**Decided at planning (2026-11-17).**

By the person:

- **The server is `patina`** (exe `patina`, project `src/Patina.Server`).
- **A graph is versioned JSON, `.texgraph`**, written and read back bit-exact. Nodes are keyed by an id the agent
  chooses, an input names `"<id>"` or `"<id>.<port>"`, and outputs are `output` nodes with a role.
- **Seven first node types:** `noise`, `gradient`, `levels`, `blend`, `colour`, `height_to_normal` and `output`
  (roles `base_colour`, `normal`, `roughness` and `occlusion`). The parameters are in T-112.
- **Size:** the graph states a default `size`, and every parameter is relative to the image. `export` takes any power
  of two from 32 to 8192, evaluated in bounded memory. `swatch` stays at 2048 or below.
- **No undo history: `push_snapshot` and `pop_snapshot`** instead (the person's idea). A session-only stack, cleared
  by `open` and `define_graph`. Edits are all or nothing.
- **A shared host library**, `Carve.Hosting`, extracted from `Carve.Server` (T-111). Both servers host through it,
  and patina loads the same extensions, so `Carve.Debug` and the scenario harness serve it unchanged.
- **M-27 waits for M-20.** The extraction touches `Program.cs` and `EngineStatus.cs`, which M-20's T-87 has changed
  on its branch.
- **The barrel sits beside M-7's:** `samples/barrel-planks.carve` in a new Planks scenario. M-7's barrel, generator,
  checks and frames are unchanged.
- **The swatch sheet** below.
- **The acceptance plant:** a starter graph in which a `blend`'s `mask` names `grain_2` where the node is `grain2`,
  given to an agent with no file tools.

By the orchestrator, recorded in the tasks' Notes, each a stop if it proves wrong:

- the package is named `Patina.Graphs`;
- roughness is exported packed for glTF's metallic-roughness slot (R 255, G roughness, B metallic 0);
- `export` writes `<role>.png` into the folder given;
- a graph with reference faults opens and is reported, so it can be fixed through the tools, and `save` refuses it.

Checked at planning:

- The engine has a built-in UV sphere (`PrimitiveMesh.Sphere`, radius 0.5, Y-up), and its normal mapping builds the
  tangent frame from screen derivatives, so the sphere swatch needs no mesh code (Empyrean 0.119.0 docs).
- `StbImageWriteSharp` 1.16.7 writes identical PNG bytes twice over at 2048 and at 8192. At 8192 it took about 10 s
  and peaked at about 750 MB for one image, giving 41 MB on disk (a throwaway probe in the temp folder).
- T-77's staves are 32 texels wide at 256, with boundaries at u = k/8 (`TextureSet.StaveWidth`). The planks graph's
  `gradient(u, repeat 8)` falls on them, and T-112 makes its gaps at least 3 texels each side, since M-7's one-texel
  gaps could not be sampled.

Not checked: whether M-7's lit prediction (`MaterialsChecks`) holds with a roughness map. T-116's research pass
checks it before any frame.

**The swatch sheet**, as agreed:

```
 planks.texgraph   256 px   seed 7
+------------+------------+------------+------------+-------------------------+
| base_colour| normal     | roughness  | occlusion  |                         |
|            |            |            |            |        .-'''''-.        |
|   flat     |   flat     |   flat     |   flat     |      .'         '.      |
|            |            |            |            |     /   sphere    \     |
+------------+------------+------------+------------+    |  Pbr, fixed   |    |
|+--+--+--+  |+--+--+--+  |+--+--+--+  |+--+--+--+  |     \   sky      /     |
||  |  |  |  ||  |  |  |  ||  |  |  |  ||  |  |  |  |      '.         .'      |
|+--+--+--+  | tiled 3x3  | tiled 3x3  | tiled 3x3  |        '-.....-'        |
+------------+------------+------------+------------+-------------------------+
  cells 256 x 256; sphere cell 512 x 512; a 24 px title strip on top
  columns are the graph's outputs, in role order; a missing role has no column
```

`swatch view=flat|tiled|sphere` gives one view alone: flat at `size`; tiled at 3 x `size`, at most 1536 px; the
sphere at 512 px, turned by `angle` about y.

**Order.**

| Wave | Tasks | Why |
|---|---|---|
| 1 | T-110 beside T-111 | The package and the host library share no file but `Carve.slnx` and CLAUDE.md (conflicts there only add) |
| 2 | T-112 beside T-113 | The nodes and planks need T-110; the server needs T-110 and T-111 |
| 3 | T-114, T-115 and T-116 side by side | Swatch and export need the server and the planks; the barrel needs only the planks' PNGs |
| 4 | T-117 | Acceptance, once everything is reachable |

Shared files with one owner: `run-scenario.ps1`, `src/Patina.Server/Program.cs` and the README's patina section
belong to T-113 (T-114 and T-115 add one line or section each); `samples/graphs/` belongs to T-112; T-110 owns the
node registry (T-112 adds a line per node). `tests.json`, `Carve.slnx` and CLAUDE.md's Layout take additions from
every task, so keep both sides.

Frames: T-114 and T-116 draw new frames. Each plans its frame review as its own agent, and the person approves each
set before it lands. There is no sound or motion, so there is no real-time pass.

## Required tasks

- [ ] [T-110](../tasks/T-110-texture-graph-format-and-evaluator.md) — Patina.Graphs: the engine-free .texgraph format read back bit-exact, deterministic banded evaluation, and faults that name the node and port
- [ ] [T-111](../tasks/T-111-shared-engine-host-library.md) — Carve.Hosting: the engine thread, extension loading, host set-up, fixed sky and texture upload rules move out of Carve.Server, every frame unchanged
- [ ] [T-112](../tasks/T-112-the-first-texture-nodes-and-the-planks-graph.md) — noise, gradient, levels, blend and height_to_normal, and samples/graphs/planks.texgraph with its PNGs kept equal to the graph
- [ ] [T-113](../tasks/T-113-the-patina-server-and-its-graph-tools.md) — patina, a second MCP server: define_graph, edit_graph, get_graph, save, open, push_snapshot and pop_snapshot
- [ ] [T-114](../tasks/T-114-swatch-renders.md) — patina's swatch: the sheet, flat, tiled 3 x 3 and a sphere through Material.Pbr under Carve's fixed sky
- [ ] [T-115](../tasks/T-115-png-export-at-a-given-size.md) — patina's export writes each output as a PNG at any power of two to 8192, in bounded memory
- [ ] [T-116](../tasks/T-116-the-barrel-with-planks-staves.md) — samples/barrel-planks.carve: M-7's barrel with its staves from the planks graph, in a scenario of its own
- [ ] [T-117](../tasks/T-117-texture-graph-acceptance-run.md) — Fresh agents build a wood material in patina from the tool descriptions alone, recover from a broken starter graph and put it on the barrel

## Retrospective
