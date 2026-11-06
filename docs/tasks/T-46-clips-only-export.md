---
description: >-
  export in clips mode writes an armature, skin and animations with no mesh, which LoadAnimationSet reads with the same bones and clips
---
# T-46 — Clips-only export

**Status:** Open

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-34

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

One file of clips serves every character in the game.

## Scope

- **`export` with `mode: clips`:** armature, `skins[0]` and animations with no mesh, as D-6's *Export modes* says. It refuses a model with no skeleton or no clips.
- **Tests, `unit`:** `ParseAnimationSet` gives the same bones in the same order and the same clips as the model file, including a breadth-first skeleton; `Parse` throws `glTF contains no meshes.`; byte identity. `ClipRetargeter` takes the clips onto a second Synty-named skeleton of other proportions without throwing, so the file is proved as a retargeting source.
- **Description:** says what the file is for, and that role maps and events are sidecars the game writes.

## Notes

- Moved into M-16 on Orcward's answer: M-16 is meant to prove retargeting in the game, so the clip file cannot wait for M-19.
- The skin stays even with no mesh: the probe in D-6 shows why. A fixer may not drop it.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
