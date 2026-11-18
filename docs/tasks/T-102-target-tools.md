---
description: >-
  define_target gives a mesh a named target from positions or a second file, naming the first topology difference; delete_target removes one; get_mesh reports them
---
# T-102 — Target tools

**Status:** Done

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

### As built

Built by Claude Sonnet 5.5.

- **`define_target`** (`src/Carve.Server/DefineTargetTool.cs`). Arguments: `name` (required), `mesh` (optional), and exactly
  one of `positions` and `file`; `file_mesh` only with `file`. `positions` is absolute, never deltas: an array of
  `[x,y,z]` one per vertex slot (`null` at a deleted slot; a non-null there is ignored; `null` at a live vertex is
  refused) or an object `{"<id>": [x,y,z]}`. `file` resolves as `open` does (`.carve`, relative to the tool folder);
  `file_mesh` defaults to the target mesh's own name, else the file's only mesh, else the refusal lists the file's meshes.
  The file's own targets are ignored. Result JSON: `{mesh, name, vertices (count moved), largest_move (metres),
  replaced (bool), targets:[names in order]}`. A same name replaces in place (`replaced:true`). One undo step (tool name
  `define_target`). Refusals use T-99's `MeshError` texts: `name: ...`, `positions[7]: ...`, `positions: N positions given;
  the mesh has M vertex slots ...`, `vertices: 8 here, 5 in the other mesh` (first topology difference). Both or neither
  of `positions` and `file` is a tool error.
- **`delete_target`**: `{name, mesh?}` -> `{mesh, deleted, targets:[names left]}`. An unknown name says
  `target: this mesh has no target 'X'; its targets are 'A', 'B'` (or `it has no targets`). One undo step.
- **`get_mesh`**: new optional `target`. Adds `targets:[{name, vertices}]` only when the mesh has any (a mesh with none
  reports exactly what it did), and with `target:"<name>"` also `target:{name, positions:{"<id>":[x,y,z]}}`, base plus
  delta summed in double and rounded once, ids ascending, sparse. An unknown name is a refusal listing the targets.
- **`define_mesh`**: new optional `drop_targets` (bool). Replacing a mesh that has targets without it is refused:
  `name: the mesh 'm' has the morph targets 'A', 'B', which replacing it would drop; give drop_targets: true ...`.
  A new name, or a mesh with no targets, is unaffected. The result shape is unchanged. (`open`'s matching refusal is T-101.)
- Tests: `tests/Carve.Server.Tests/TargetToolTests.cs` (unit, `ToolHarness`); `ToolHarnessTests` tool count 36 -> 38;
  `DefineMeshDefinitionTests` schema string gains `drop_targets` after `faces`.
- Store: no new `ModelStore` method; both tools use `ModelStore.Rework`, which holds the write lock and pushes one step.
- Not checked: no frames and no real-process run (unit tier only); T-101's file reader is not yet under `file`, so the
  rebase may need `CarveFile.Read`'s signature checked.
- Model: Claude Sonnet 5.5.
