---
description: >-
  define_target gives a mesh a named target from positions or a second file, naming the first topology difference; delete_target removes one; get_mesh reports them
---
# T-102 — Target tools

**Status:** In Progress

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-99

**Verify:** server-unit

## Goal

An agent defines, reads back and removes targets through MCP, from its own positions or from a second `.carve`
file of the same topology, as fps-arms makes them.

## Scope

- **`define_target`** (new `DefineTargetTool.cs`): `mesh` (optional, as other tools), `name`, and exactly one of:
  - `positions`: an array of `[x, y, z]`, one per vertex slot (`null` at a tombstone), or an object of
    `"<id>": [x, y, z]` for a sparse set. These are absolute positions, not deltas;
  - `file` (a `.carve` path, resolved as `open` resolves it) with optional `file_mesh`: the mesh in that file
    (default the same name, else the file's only mesh).

  An existing name is replaced (say so in the result). The result gives the target's name, how many vertices it
  moves and the largest move. Topology mismatch, unknown ids and bad names come back with T-99's texts. One undo
  step.
- **`delete_target`**: `mesh`, `name`; an unknown name lists the mesh's targets.
- **`get_mesh`** lists `targets: [{name, vertices}]`. With `target: "<name>"` it also returns that target's
  absolute positions for the vertices it moves, sparse.
- **`define_mesh`** replacing a mesh that has targets is refused, naming them, unless `drop_targets: true`.
- **Descriptions and worked examples** for each tool are part of this task (content). The `define_target`
  example is a sparse bulge on a small mesh, and a second example uses `file`.
- **Tests** on M-20's in-process harness (server-unit): every argument path and refusal, the replace-refusal,
  undo, and the description and schema checks M-20 moved down a tier.

## Notes

- Owns `Program.cs`'s tool list in this wave, plus `GetMeshTool.cs` and `DefineMeshTool.cs`.
- **Goes back to the person, stop and ask:** a new argument beyond these; taking deltas instead of absolute
  positions; any change to an existing tool's result shape other than `get_mesh`'s new `targets`.
- Sonnet.
