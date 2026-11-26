---
description: >-
  The parts, style, UV, skeleton, weight, clip, target, save and bake tools take verbosity, silent answering only ok
---
# T-201 — verbosity on the other tools that change the model or write a file

**Status:** In Progress

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-200

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

Every other tool that changes the model or writes a file takes `verbosity`, so a whole script can run with one-word answers.

## Scope

- **Tools**: `define_part`, `keep_level`, `define_group`, `set_colour`, `define_material`, `set_material`, `set_shading`, `mark_edges`, `set_uvs`, `project_uvs`, `define_skeleton`, `set_weights`, `weigh`, `define_clip`, `delete_clip`, `define_target`, `delete_target`, `save`, `bake`, and `displace` (added by M-29). Not `unwrap` (T-202 owns `UnwrapTool.cs`).
- Each adopts T-200's schema fragment and reader as T-200's `### As built` says: `silent` is exactly `{"ok":true}`; `summary`, the default, is byte for byte today's result; `full` is `summary` unless the tool shortens a list today (then the whole list). Errors whole at every level. No tool description grows.
- Remove the twenty from T-200's guard table.
- **Tests** (`unit`): one theory over the twenty, each called on a model that makes it succeed: `silent` exactly `{"ok":true}` with the change made (read back by the matching `get_*`, or the file on disk for `save` and `bake`); the default unchanged; an error at `silent` whole; a bad value.
- **`README.md`**: the list of tools that take `verbosity`, in `## Dense meshes`.

## Notes

- Mechanical: twenty tools on one helper. If a tool's result cannot go through the helper as it stands, widen the helper; do not write a second one.
- Runs beside T-202; the two share only T-200's guard table (each removes its own rows; keep both sides of that conflict).
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.
