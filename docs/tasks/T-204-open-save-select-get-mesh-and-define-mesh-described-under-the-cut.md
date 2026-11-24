---
description: >-
  open, save, select, get_mesh and define_mesh fit under the description cut, their reference text served by help
---
# T-204 — open, save, select, get_mesh and define_mesh described under the cut

**Status:** Open

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-199, T-201

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

`open`, `save`, `select`, `get_mesh` and `define_mesh` are read whole by an agent in Claude Code, their reference text in `help`.

## Scope

- `save` (5,997 characters at planning), `select` (5,571), `open` (5,334), `get_mesh` (4,068) and `define_mesh` (4,032) become 3,686 or less each, keeping what T-203 keeps: what it does, each argument, the result's shape, the refusals in one sentence, one worked example run as written, and its topics by name.
- The rest moves, sentence for sentence where it can, into topics by T-199's pattern: `files` (the Carve file's versions, what `open` replaces and keeps, part trees and morph targets in a file, paths, the worked sequence `save` and `open` share), `select` (the properties of each kind and the grammar) and, only if needed, one for the mesh report. Say in `### As built` where each paragraph went.
- **Tests**: remove the five from `DescriptionLengthTests`' table; `SaveOpenToolDefinitionTests`, `SelectToolDefinitionTests`, `DefineMeshDefinitionTests`, `NamedMeshDefinitionTests` and any other test that finds a phrase in these descriptions (search) assert it where it now is; `PartToolsListTests`' companion follows `PartTexts.DefineMesh` and `GetMeshTool.PlacementText`. Worked examples moved to a topic are run as written.
- **`README.md`**: the new topics on the `help` line; otherwise as T-203.

## Notes

- Runs beside T-203 and T-205, sharing what T-203's Notes list; keep both sides of each conflict.
- Move text, cut repetition; a sentence that states a rule keeps its words.
- Depends on T-201 because it edits `SaveTool.cs`, and through it on T-200 (`OpenTool.cs`, `DefineMeshTool.cs`).
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.
