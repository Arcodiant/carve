---
description: >-
  An agent builds a small node graph in a new texture server, sees it as swatches, exports PNGs and textures the barrel with them
---
# M-27 — A first texture graph

**Status:** Planned

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

## Required tasks

Not planned yet.

## Retrospective
