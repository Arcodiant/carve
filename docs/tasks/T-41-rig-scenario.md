---
description: >-
  A scenario finds planted skinning faults through get_weights and the bones and weights looks, in its own suite
---
# T-41 — Rig scenario

**Status:** Open

**Pri:** P1

**Milestone:** M-17

**Batch:** — (standalone)

**Depends on:** T-38, T-40

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Skinning faults are proved visible before export.

## Scope

- **`scenarios/Carve.Scenario.Rig`**, suite `scenario-rig`, on the gardener with planted faults: an unweighted patch, a hat vertex on the wrong arm, a bone out of place.
- Each fault is found through `get_weights` or a frame with `bones` or `weights`, then fixed through the tools, and the frame re-checked.
- Frames are kept and looked at, with a pixel check for each fault before and after.

## Notes

- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
