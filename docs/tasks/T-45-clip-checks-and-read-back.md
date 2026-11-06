---
description: >-
  get_clips reads clips back, and looping clips that do not close or clips that travel are warned about
---
# T-45 — Clip checks and read-back

**Status:** Open

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-36

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent finds a popping loop or a drifting walk from a result, not from the game.

## Scope

- **Warnings** on `define_clip`, `get_clips`, `save` and `export`, as D-6's *Clips* says: a looping clip whose channel ends differently from how it starts, naming the bone, channel and difference; a clip whose root bones end elsewhere on the ground plane.
- **`get_clips(name?)`:** names, durations, `loop`, channels and keys, as given.
- **Tests:** `unit` for each warning, for no warning on a clean clip, and for a rotation equal up to sign counting as closed; the stated tolerance is a constant with a comment.

## Notes

- The closing tolerance is a new rule: propose it and ask.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
