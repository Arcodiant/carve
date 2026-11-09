---
description: >-
  Export refuses an existing file without overwrite
---
# T-52 — Export refuses an existing file without overwrite

**Status:** In Progress

**Pri:** P2

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-46

**Verify:** server-regression, scenario-export

**Perf:** skip: Carve declares no heavy suites

## Goal

`export` refuses to replace an existing file unless given `overwrite: true`, as D-6 and D-1 say (the person, 2026-11-06), in every mode.

## Scope

- **`export(path, overwrite?)`:** `overwrite` optional, false by default, in the closed schema beside `allow_unweighted` and `mode`. An existing file without it is a tool error naming the path and `overwrite`, with the file unchanged. With it, the file is replaced and the result says `replaced`. Applies to `model` and `clips` modes alike. `save` is unchanged: it replaces, since the `.carve` file is the working copy.
- **Description:** `export`'s description and its `path` text say so (the path text today says "replaced if it exists").
- **Existing tests and the export scenario** that export twice to one path follow the rule: the second export passes `overwrite: true`, or asserts the refusal. Name each changed test target in As built.
- **Tests:** `unit` (`ToolPaths`): refusal with the file byte-unchanged, `replaced` with `overwrite`; `regression`: the refusal and `overwrite` through the real `carve`, in both modes, with identical bytes on replacement.

## Notes

- Found by the orchestrator on 2026-11-09 before T-36: the person ruled on 2026-11-06 that `export` refuses to overwrite, and T-16's file was edited to say so, but T-16 had been built on 2026-11-05, so the rule was never implemented. The person chose a task of its own in M-16, ahead of T-36.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame and exported file, by hash before and after; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
