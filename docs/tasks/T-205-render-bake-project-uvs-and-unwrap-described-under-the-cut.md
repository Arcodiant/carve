---
description: >-
  render, bake, project_uvs and unwrap fit under the description cut, their reference text served by help
---
# T-205 — render, bake, project_uvs and unwrap described under the cut

**Status:** Open

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-199, T-201, T-202

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

`render`, `bake`, `project_uvs` and `unwrap` are read whole by an agent in Claude Code, their reference text in `help`.

## Scope

- `render` (18,551 characters at planning), `bake` (8,410), `project_uvs` (3,809) and `unwrap` (3,720, as T-202 leaves it) become 3,686 or less each, keeping what T-203 keeps.
- The rest moves, sentence for sentence where it can, into topics by T-199's pattern: `render` (camera, framing, sizes, sheets, parts, targets and clips), `render-modes` (each look and what it is for), `bake` (each map, its parameters and its file) and `uvs` (islands, seams, the axes, `project_uvs`' methods). Split a topic that passes about 6,000 characters. Say in `### As built` where each paragraph went.
- **Tests**: remove the four from `DescriptionLengthTests`' table. `RenderDefinitionTests`, `ProjectUvsToolDefinitionTests`, `UnwrapToolDefinitionTests`, the bake tests and any other that finds a phrase in these descriptions (search) assert it where it now is; `PartToolsListTests`' companion follows `RenderPartTexts` and `PartTexts.PartSpace`. Worked examples moved to a topic are run as written.
- **`README.md`**: the new topics on the `help` line.

## Notes

- Runs beside T-203 and T-204, sharing what T-203's Notes list. T-206 deletes the table once all three have emptied it.
- `render` is the largest move of the milestone: nothing but text and tests changes, no frame. If a frame differs, something else was changed; stop.
- Depends on T-201 (`BakeTool.cs`, `ProjectUvsTool.cs`) and T-202 (`UnwrapTool.cs`).
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.
