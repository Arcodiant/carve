---
description: >-
  render draws the skeleton over the model, with bone names available as labels
---
# T-39 — Bones in renders

**Status:** Open

**Pri:** P1

**Milestone:** M-17

**Batch:** — (standalone)

**Depends on:** T-36

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees the skeleton in place, so a bone in the wrong spot is obvious.

## Scope

- **`render` argument `bones`:** each bone a line from head to child, drawn over the model and not hidden by it, in the current pose, clip and time. `PositionVertex` line lists draw by the engine's docs.
- **`bone_labels`:** names beside the bones, if the engine's text path allows it without M-4's work. If not, stop and ask.
- **Tests:** a frame of the gardener with bones, and pixel checks that a bone's line is at its expected place in rest and in a pose.

## Notes

- M-4's T-24 and T-25 later grow `render` too. Keep new arguments independent, so those tasks rebase cleanly.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
