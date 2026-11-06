---
description: >-
  An agent overrides weights on a face group, reads weights back, and is warned about unweighted vertices
---
# T-38 — Weight overrides and read-back

**Status:** Open

**Pri:** P1

**Milestone:** M-17

**Batch:** — (standalone)

**Depends on:** T-37

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent corrects a rig by group and reads what it has, with gaps reported rather than found in the game.

## Scope

- **`set_weights`** takes `group` as well as `vertices`, so a hat is all `Head` in one call.
- **`get_weights(mesh, vertices? | bone?)`:** per-vertex influences by bone name, or every vertex a bone moves, plus a summary and warnings.
- **Warnings:** unweighted vertices, listed by id with a count; a bone no vertex follows, as a note.
- **Tests:** `unit` for group override after `weigh`, read-back equal to what was set, and warnings; `regression` for the worked examples as written.

## Notes

- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
