---
description: >-
  check's and export's descriptions fit under Claude Code's 4,096-character cut, their reference text served by help
---
# T-203 — check and export described under the cut

**Status:** Done

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-199, T-200

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent in Claude Code reads all of `check`'s and `export`'s descriptions: each fits under the cut, with its reference text in `help`.

## Scope

- `check` (7,565 characters at planning) and `export` (13,480) become 3,686 or less each. A description keeps: what the tool does and when to call it, each argument, the shape of the result, the refusals in one sentence, one worked example run as written, and the `help` topics that explain the rest by name.
- Everything else moves, sentence for sentence where it can, into topics added by T-199's pattern: `check` (its problems, notes and stats; warnings are already in `warnings`), `export` (its modes, pieces and clips), and `export-limits` (the game limits) if `export` passes about 6,000 characters. Nothing an agent could read before is dropped: say in `### As built` where each paragraph went.
- **Tests**: remove `check` and `export` from `DescriptionLengthTests`' table. `CheckToolDefinitionTests`, `ExportToolDefinitionTests` and any test that finds a phrase in these descriptions (search) assert the same phrase where it now is, description or topic. `PartToolsListTests`' companion test follows `PartTexts.Check` and the other agreed sentences to where they now are. Every worked example moved to a topic is still run as written.
- **`README.md`**: nothing a user reaches changes but where the text is; say so in `### As built`, and add the new topics to the `help` line.

## Notes

### As built

Delivered by M-29's T-207; what was left of it (check's warnings into help, and the pointer) moved into T-199.

- Runs beside T-204 and T-205. Shared with them: `HelpTopics.All` (adds one line each), `DescriptionLengthTests`' table (removes rows), `PartToolsListTests`' companion; keep both sides of each conflict.
- Rewording is not the task: move text, cut repetition. A sentence that states a rule keeps its words.
- Depends on T-200 because both edit `CheckTool.cs` and `ExportTool.cs`.
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.
