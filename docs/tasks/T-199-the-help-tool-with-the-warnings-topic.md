---
description: >-
  help lists reference topics and returns one; the warnings topic explains every warning kind, its grouping and the verbosity levels, and a test holds description lengths
---
# T-199 — The help tool, with the warnings topic

**Status:** Open

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-198

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

The reference text that does not fit a tool description is served by one tool, `help`, starting with the topic every M-23 task writes to: `warnings`.

## Scope

- **`help`** (`src/Carve.Server/HelpTool.cs`, registered in `CarveServer.AddTools`): `help {}` lists the topics, one line each; `help {"topic": "warnings"}` returns that topic's text; an unknown topic or argument is a tool error listing the topics. It changes nothing and takes no `verbosity`. Agreed sketch:
  ```
  help {}
    topics: warnings, check, export, render, render-modes, bake, files, select, ...
  help {"topic": "warnings"}
    Every warning by kind, what it means for a game, the fix, how a kind is
    grouped past 3, the select query that names each, and the verbosity levels.
  ```
- **Topics** are one file each under `src/Carve.Server/Help/`, listed in `HelpTopics.All` (a later task adds its file and one line there; never a switch). A topic over about 6,000 characters is split in two (a guideline of the planner's, not a ruling).
- **The `warnings` topic**, written here: every `MeshWarningKind` with what it means for a game and the fix (the text `check`'s description carries today; `CheckTool.cs` is not edited here, T-203 trims it), T-197's grouping rule and its exact lines, T-198's smooth rule, and the three verbosity levels as decided: `silent` answers only `{"ok": true}`, `summary` is the default, `full` lists every warning and every list whole; a tool error is always whole.
- **Worked examples in a topic are run as written** by a test, as a description's are (`ExampleMarker`).
- **`DescriptionLengthTests`** (`unit`): every carve tool's description is 3,686 characters or less (Claude Code cuts at 4,096; a 10% margin, as patina's `NodeToolTests`), except an explicit table of the tools over it, each with its length when this task lands as a ceiling it may not pass. At planning: render 18,551, export 13,480, bake 8,410, check 7,565, save 5,997, select 5,571, open 5,334, get_mesh 4,068, define_mesh 4,032, project_uvs 3,809, unwrap 3,720. T-203, T-204 and T-205 each remove their rows.
- **Tests** (`unit`): `help` with no topic, each topic, an unknown topic; every topic named in a description exists. `regression`: `help` through the real process, and every test that counts or lists the tools (search `ListToolsAsync`) updated for the new tool.
- **`README.md`**: `help` in the list of tools under `## Use`, and a line in `## Dense meshes`.

## Notes

- **Decides a public API**: the tool's name, argument and topic names. Write the topic list and the registration pattern in `### As built`; T-203, T-204 and T-205 add topics by it.
- Runs beside T-200. It edits no existing tool file but `CarveServer.cs` (one line); T-200 keeps out of that file.
- Depends on T-198 so the `warnings` topic states rules that have landed.
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.
