---
description: >-
  define_mesh, the edits, subdivide, undo, open, check and export take verbosity: silent answers only ok, summary groups, full lists every warning
---
# T-200 — verbosity on the tools that return a mesh report

**Status:** In Progress

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-198

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The tools that answer with a mesh report take `verbosity`, so a script can run them silently and an agent can ask for the whole list.

## Scope

- **The argument**, `verbosity`: `"silent"`, `"summary"` (the default) or `"full"`; any other value is a tool error naming the three. One schema fragment and one reader in `MeshToolSupport`, used by every tool (T-201 and T-202 use them too).
  ```
  verbosity: "silent"     {"ok": true}
  verbosity: "summary"    today's fields, warnings grouped (T-197)     (default)
  verbosity: "full"       as summary, every warning one per line
  ```
- **Tools here**: `define_mesh`, `move`, `extrude`, `delete`, `merge_faces`, `bridge`, `plane_cut`, `loop_cut`, `knife`, `subdivide`, `undo`, `open`, `check`, `export`.
- **`silent`** (the person, 2026-11-24: "silent says only ok"): the result is exactly `{"ok":true}`, no counts, no ids, no warnings; the edit is made and is one undo step as ever. A tool error is returned whole at every level. Two readings of the planner's, to confirm with the person before landing: `check` at `silent` answers `{"ok": <its own ok>}`, since `true` over a broken mesh would mislead; `export` at `silent` answers the JSON `{"ok":true}` although its result is otherwise text.
- **`full`**: every `MeshWarning` one per line as before T-197, and nothing else changed. With no argument a result is byte for byte what it is without this task.
- **The explanation goes in the argument's schema description** (not cut by the client), not in tool descriptions, which must not grow (T-199's ceilings).
- **Guard test** (`unit`): every tool in tools/list either has `verbosity` or is one of `get_mesh`, `get_parts`, `get_skeleton`, `get_weights`, `get_clips`, `select`, `render`, `render_glb`, `help` (absent until T-199 lands; the guard must pass either way), or is in a table of tools still to do (T-201's nineteen and `unwrap`), which T-201 and T-202 empty.
- **Tests** (`unit`, one theory over the fourteen): `silent` is exactly `{"ok":true}` and the model changed; an error at `silent` is the same text as without it; `summary` equals no argument; `full` on the holed cage of `DenseMeshBudgetTests` lists 798 open-edge lines; a bad value. `DenseMeshBudgetTests` gains the `silent` column (every result exactly `{"ok":true}`, `check` per the reading above).
- **`README.md`**: `verbosity` in `## Dense meshes`.

## Notes

- **Decides a mechanism others build on**: write the helper names and how a tool adopts them in `### As built` for T-201 and T-202.
- Runs beside T-199: do not edit `CarveServer.cs` or add a tool.
- `ToolHarnessMatchesProcessTests` and `ToolFamiliesThroughProcessTests` (`regression`) compare results with the real process, hence `Verify: full`.
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.
