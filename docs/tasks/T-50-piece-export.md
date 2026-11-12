---
description: >-
  export writes chosen meshes to their own .glb with every bone in the same order and no clips, so a costume can be swapped at runtime on the body's palette
---
# T-50 — Piece export

**Status:** In Progress

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-41

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

Orcward swaps costumes at runtime: orcs wear monster costumes, and crew change on the way between rooms. An agent exports a chosen piece to its own `.glb`, and it follows the body's animation in the game.

## Scope

- **`export` with `mode: piece` and `meshes`:** the named meshes in one file, as D-6's *Pieces share the body's palette* says: every bone in the same order, the same inverse binds, no animations, one glTF mesh. It refuses an unknown mesh (listing the names), no skeleton, or an empty list. Unweighted vertices in the chosen meshes are refused as in T-34.
- **Description:** says a game loads the piece and shares the body's `SkinnedPose.Palette`, with the body as driver and only the body holding an `Animator`, with a short example.
- **Sample:** the gardener's `Hat` as a piece file.
- **Tests, `unit`:** through `Parse`, no `Warnings`; bone names, order, count and inverse binds equal across the model file and each piece, on a piece using few bones and on a breadth-first skeleton; no clips; byte identity. **Show it failing** with a skeleton trimmed to the bones used.

## Notes

- A fixer may not trim the skeleton: the palette is positional, and nothing throws if it misaligns.
- The engine's guide says pieces should bind the same `Skeleton` instance. Separate files load separate instances, so the game relies on the order guaranteed here. T-48 checks the file side; proving the shared palette in a running scene is T-47's tool if it is cheap, otherwise Orcward's.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
