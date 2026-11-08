---
description: >-
  The kernel and the store hold a model of named meshes, and every existing tool works on a named mesh with every frame unchanged
---
# T-29 — A model of named meshes

**Status:** In Progress

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-20

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Carve holds a model, not one mesh. An agent can name meshes and aim any existing tool at one, and a session that names nothing works exactly as it does today.

## Scope

- **Core:** an immutable `Model` (`src/Carve.Core/Model.cs`), with named meshes in definition order. Names are non-empty and unique. Skeleton and clips arrive in T-31 and T-33.
- **Store:** `ModelStore` holds a `Model`; undo and version step over models, and the 100-step depth stays.
- **`define_mesh`** takes an optional `name` (default `mesh`). Defining an existing name replaces that mesh, and a new name adds one. Each is one undo step.
- **`move`, `extrude`, `get_mesh`, `save`, `open`** take `mesh`, which may be left out when the model has one. Left out with several, the tool error lists the names.
- **`render`** draws every mesh together, bounds taken over all of them.
- **A sample of the mechanic:** a two-mesh worked example in `define_mesh`'s description.
- **Tests:** `unit` for naming and refusals; `regression` for two meshes through the real tools, undo across them, and a two-mesh frame.

## Notes

- This task decides the `mesh` argument every later tool uses, so run it on the stronger model.
- `export` stays single-mesh until T-34: with several meshes it is a tool error saying so. `save` writes a version 1 file for one mesh and refuses several until T-35.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
